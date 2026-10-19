using Project.BlockFeatures;
using Project.CameraModes;
using Project.LevelEditor;
using Project.Player;
using UnityEngine;

namespace Project.Levels.ILorR
{
    /// <summary>R1-only approach cue, held until landing on the designated spring.</summary>
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    [RequireComponent(typeof(CameraControlManager), typeof(CameraModeController))]
    [AddComponentMenu("2026TapTap/关卡/R1 弹性植物镜头提示")]
    public sealed class R1SpringCameraGuide : MonoBehaviour
    {
        [SerializeField] private Transform buttonAnchor;
        [SerializeField] private Transform landingPlantAnchor;
        [SerializeField] private bool autoFramePlant;
        [SerializeField, Min(1f)] private float expandedSize = 20f;
        [SerializeField, Min(0f)] private float enterSeconds = .45f;
        [SerializeField, Min(0f)] private float restoreSeconds = .6f;
        [SerializeField, Min(.1f)] private float approachWidthCells = 10f;
        [SerializeField, Min(.1f)] private float approachHeightCells = 6f;
        [SerializeField] private float approachVerticalOffsetCells = .5f;
        [SerializeField, Min(0f)] private float framingMarginCells = 1f;

        private CameraControlManager manager;
        private CameraModeController mode;
        private CameraFollowController follow;
        private PlayerController player;
        private Collider playerBody;
        private LevelEditorPlacedBlock buttonMarker;
        private BouncePlantFeature plant;
        private Collider[] plantColliders;
        private CameraLensRequestHandle lens;
        private bool guiding, wasInside;
        private float heldSize, requestedMultiplier, nextLookupTime;

        public bool GuideActive => guiding && lens.IsValid;
        public bool AutomaticFraming => autoFramePlant;
        public float RequestedSize => guiding ? heldSize : 0f;
        public Transform ButtonAnchor => buttonAnchor;
        public Transform LandingPlantAnchor => landingPlantAnchor;
        private float CellSize => buttonMarker != null ? buttonMarker.CellWorldSize : 1f;
        public Bounds ApproachBounds => new Bounds(
            (buttonAnchor != null ? buttonAnchor.position : transform.position) +
            Vector3.up * (approachVerticalOffsetCells * CellSize),
            new Vector3(Mathf.Max(.1f, approachWidthCells), Mathf.Max(.1f, approachHeightCells), 4f) * CellSize);

        private void OnEnable()
        {
            manager = GetComponent<CameraControlManager>();
            mode = GetComponent<CameraModeController>();
            follow = GetComponent<CameraFollowController>();
            wasInside = false;
            nextLookupTime = 0f;
        }

        private void OnDisable()
        {
            ClearGuide(true);
            BindPlayer(null);
            UnbindPlant();
        }

        private void LateUpdate() => TickGuide();

        private void TickGuide()
        {
            ResolveAnchors();
            Transform target = follow != null ? follow.Target : manager != null ? manager.FocusTarget : null;
            BindPlayer(target != null ? target.GetComponentInParent<PlayerController>() : null);
            if (!isActiveAndEnabled || GameplayPresentationGate.IsSuppressed ||
                manager == null || !manager.isActiveAndEnabled || mode == null || !mode.isActiveAndEnabled ||
                player == null || !player.isActiveAndEnabled || player.IsDead || player.IsRespawning ||
                player.gameObject.scene != gameObject.scene ||
                playerBody == null || !playerBody.enabled || playerBody.isTrigger ||
                buttonAnchor == null || !buttonAnchor.gameObject.activeInHierarchy ||
                plant == null || !plant.IsBounceSurfaceAvailable || !plant.isActiveAndEnabled ||
                !mode.TryGetProjectionState(out CameraState projection) ||
                projection.projection != CameraProjectionMode.Orthographic)
            {
                ClearGuide(true);
                return;
            }

            bool inside = ApproachBounds.Contains(playerBody.bounds.center);
            if (!guiding && inside && !wasInside)
            {
                guiding = true;
                heldSize = Mathf.Max(expandedSize, projection.orthographicSize);
            }
            wasInside = inside;
            if (!guiding) return;

            // Carrying / holding down may suppress the spring's bounce; genuine top
            // contact still completes the cue. A side brush or passing overhead does not.
            if (IsOnPlantTop(.12f * CellSize) && player.VerticalVelocity <= .1f)
            {
                ClearGuide(false);
                return;
            }

            float manualSize = Mathf.Max(expandedSize, projection.orthographicSize);
            if (autoFramePlant)
            {
                // Automatic framing holds its maximum until landing. Use an unscaled
                // mode pose, never the already-expanded camera output.
                var context = new CameraControlContext(manager.FocusPoint, 0f);
                if (mode.TryGetCameraState(in context, out CameraState basePose))
                    heldSize = Mathf.Max(heldSize, RequiredSize(basePose));
                heldSize = Mathf.Max(heldSize, manualSize);
            }
            else
            {
                // Manual edits can increase AND decrease the active request, including
                // immediately after turning automatic framing off. Leaving the button
                // does not end the cue; only its size is no longer locked to a maximum.
                heldSize = manualSize;
            }
            float multiplier = Mathf.Max(1f, heldSize / Mathf.Max(.01f, projection.orthographicSize));
            if (!lens.IsValid)
            {
                lens = manager.RequestLensMultiplier(this, multiplier, CameraTransition.Ease(enterSeconds));
                requestedMultiplier = multiplier;
            }
            else if (autoFramePlant ? Mathf.Abs(multiplier - requestedMultiplier) > .002f
                         : !Mathf.Approximately(multiplier, requestedMultiplier))
            {
                lens.Set(multiplier, CameraTransition.Ease(enterSeconds));
                requestedMultiplier = multiplier;
            }
        }

        private float RequiredSize(CameraState pose)
        {
            float aspect = manager.OutputCamera != null ? Mathf.Max(.1f, manager.OutputCamera.aspect) : 1f;
            Quaternion inverse = Quaternion.Inverse(pose.rotation);
            float required = 0f;
            foreach (Collider surface in plantColliders)
            {
                if (surface == null || !surface.enabled || surface.isTrigger || !surface.gameObject.activeInHierarchy) continue;
                Bounds bounds = surface.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 local = inverse * (point - pose.position);
                    float margin = framingMarginCells * CellSize;
                    required = Mathf.Max(required, Mathf.Max(Mathf.Abs(local.y) + margin, (Mathf.Abs(local.x) + margin) / aspect));
                }
            }
            return required;
        }

        private bool IsOnPlantTop(float tolerance)
        {
            if (playerBody == null || plantColliders == null) return false;
            Bounds body = playerBody.bounds;
            foreach (Collider surface in plantColliders)
            {
                if (surface == null || !surface.enabled || surface.isTrigger || !surface.gameObject.activeInHierarchy) continue;
                Bounds bounds = surface.bounds;
                if (Mathf.Abs(body.min.y - bounds.max.y) <= tolerance &&
                    body.center.x >= bounds.min.x && body.center.x <= bounds.max.x &&
                    body.center.z >= bounds.min.z && body.center.z <= bounds.max.z) return true;
            }
            return false;
        }

        private void OnPlantBounce()
        {
            // The event has no actor argument; restrict it to this camera's actual player.
            if (guiding && player != null && !player.IsDead && !player.IsRespawning && IsOnPlantTop(.3f * CellSize))
                ClearGuide(false);
        }

        private void OnPlantReset() => ClearGuide(true);
        private void OnPlayerRelocated() => ClearGuide(true);

        private void BindPlayer(PlayerController next)
        {
            if (player == next) return;
            ClearGuide(true);
            if (player != null) player.CameraFollowRelocated -= OnPlayerRelocated;
            player = next;
            playerBody = player != null ? player.GetComponent<Collider>() : null;
            if (player != null) player.CameraFollowRelocated += OnPlayerRelocated;
        }

        private void ResolveAnchors()
        {
            // The level editor can replace a prop during an explicit incremental edit.
            // Recover by stable planning ID only when missing; never search the scene per frame.
            if ((buttonAnchor == null || landingPlantAnchor == null) && Time.unscaledTime >= nextLookupTime)
            {
                nextLookupTime = Time.unscaledTime + .5f;
                foreach (LevelEditorPlacedBlock candidate in LevelEditorPlacedBlock.ActiveInstances)
                {
                    if (candidate == null || candidate.gameObject.scene != gameObject.scene) continue;
                    if (buttonAnchor == null && candidate.PlanningBoxId == "r1_button_middle") buttonAnchor = candidate.transform;
                    if (landingPlantAnchor == null && candidate.PlanningBoxId == "r1_spring_lower") landingPlantAnchor = candidate.transform;
                }
            }
            if (buttonAnchor != null && (buttonMarker == null || buttonMarker.transform != buttonAnchor))
                buttonMarker = buttonAnchor.GetComponent<LevelEditorPlacedBlock>();
            if (plant != null && plant.transform == landingPlantAnchor) return;
            UnbindPlant();
            if (landingPlantAnchor == null) return;
            plant = landingPlantAnchor.GetComponent<BouncePlantFeature>();
            if (plant == null) return;
            plantColliders = plant.GetComponentsInChildren<Collider>(true);
            plant.BounceTriggered += OnPlantBounce;
            plant.AppearanceReset += OnPlantReset;
        }

        private void UnbindPlant()
        {
            if (plant != null)
            {
                plant.BounceTriggered -= OnPlantBounce;
                plant.AppearanceReset -= OnPlantReset;
            }
            plant = null;
            plantColliders = null;
        }

        private void ClearGuide(bool immediate)
        {
            if (lens.IsValid) lens.Release(immediate ? CameraTransition.Immediate : CameraTransition.Ease(restoreSeconds));
            lens = default;
            guiding = false;
            heldSize = requestedMultiplier = 0f;
            wasInside = false;
        }

        public void ResetGuideForTesting() => ClearGuide(false);

        private void OnDrawGizmosSelected()
        {
            if (buttonAnchor == null) return;
            Color previous = Gizmos.color;
            Gizmos.color = new Color(1f, .75f, .15f, 1f);
            Gizmos.DrawWireCube(ApproachBounds.center, ApproachBounds.size);
            if (landingPlantAnchor != null) Gizmos.DrawLine(buttonAnchor.position, landingPlantAnchor.position);
            Gizmos.color = previous;
        }
    }
}
