using Project.CameraModes;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.BossLogic
{
    public enum BossCameraPlayableMode
    {
        BossExecutionApproach,
        DualTargetFrame,
        PlayerFocus
    }

    public sealed class BossCameraPlayable :
        PlayableAsset
    {
        [SerializeField] private BossCameraPlayableMode mode;
        [SerializeField] private string bossTag = "Boss";
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private Vector3 bossCameraOffset =
            new Vector3(0f, 2f, -10f);
        [SerializeField] private Vector3 playerFocusOffset =
            new Vector3(0f, 0f, -10f);
        [SerializeField, Min(0f)]
        private float approachDuration = 1.2f;
        [SerializeField, Min(0f)]
        private float verticalCorrectionDuration = .48f;
        [SerializeField, Min(0f)]
        private float dualTargetTransitionDuration = 1f;
        [SerializeField, Min(0f)]
        private float framePadding = 1.2f;
        [SerializeField]
        private bool keepBaseOrthographicMinimum = true;

        public override Playable CreatePlayable(
            PlayableGraph graph,
            GameObject owner)
        {
            BossCameraBehaviour behaviour =
                new BossCameraBehaviour
                {
                    mode = mode,
                    bossTag = bossTag,
                    playerTag = playerTag,
                    bossCameraOffset = bossCameraOffset,
                    playerFocusOffset = playerFocusOffset,
                    approachDuration = approachDuration,
                    verticalCorrectionDuration =
                        verticalCorrectionDuration,
                    dualTargetTransitionDuration =
                        dualTargetTransitionDuration,
                    framePadding = framePadding,
                    keepBaseOrthographicMinimum =
                        keepBaseOrthographicMinimum
                };
            return ScriptPlayable<BossCameraBehaviour>
                .Create(graph, behaviour);
        }
    }

    public sealed class BossCameraBehaviour :
        PlayableBehaviour
    {
        public BossCameraPlayableMode mode;
        public string bossTag = "Boss";
        public string playerTag = "Player";
        public Vector3 bossCameraOffset =
            new Vector3(0f, 2f, -10f);
        public Vector3 playerFocusOffset =
            new Vector3(0f, 0f, -10f);
        public float approachDuration = 1.2f;
        public float verticalCorrectionDuration = .48f;
        public float dualTargetTransitionDuration = 1f;
        public float framePadding = 1.2f;
        public bool keepBaseOrthographicMinimum = true;

        private TimelineCamRig rig;
        private CameraViewModeRequestHandle viewModeHandle;
        private Transform boss;
        private Transform player;
        private Vector3 originPosition;
        private Quaternion originRotation;
        private float baseOrthographicSize;
        private float baseFieldOfView;
        private bool started;
        private bool failed;
        private Collider[] playerColliders =
            System.Array.Empty<Collider>();
        private Renderer[] playerRenderers =
            System.Array.Empty<Renderer>();
        private Collider[] bossColliders =
            System.Array.Empty<Collider>();
        private Renderer[] bossRenderers =
            System.Array.Empty<Renderer>();

        public override void OnBehaviourPlay(
            Playable playable,
            FrameData info)
        {
            Cleanup();
            started = false;
            failed = false;
        }

        public override void ProcessFrame(
            Playable playable,
            FrameData info,
            object playerData)
        {
            if (failed)
            {
                return;
            }

            double duration = playable.GetDuration();
            if (duration > 0d &&
                playable.GetTime() >=
                duration - .0001d)
            {
                Cleanup();
                return;
            }

            if (!started && !Initialize())
            {
                failed = true;
                return;
            }

            float time = (float)playable.GetTime();
            if (mode ==
                BossCameraPlayableMode.DualTargetFrame)
            {
                ApplyDualTargetFrame(time);
            }
            else if (mode ==
                     BossCameraPlayableMode.PlayerFocus)
            {
                ApplyPlayerFocus();
            }
            else
            {
                ApplyBossApproach(time);
            }
        }

        public override void OnBehaviourPause(
            Playable playable,
            FrameData info)
        {
            Cleanup();
        }

        public override void OnGraphStop(Playable playable)
        {
            Cleanup();
        }

        public override void OnPlayableDestroy(
            Playable playable)
        {
            Cleanup();
        }

        private bool Initialize()
        {
            GameObject bossObject =
                FindTagged(bossTag);
            GameObject playerObject =
                FindTagged(playerTag);
            if (bossObject == null || playerObject == null)
            {
                return false;
            }

            boss = bossObject.transform;
            player = playerObject.transform;
            CameraControlManager manager =
                ProjectDiscovery.FindFirst<
                    CameraControlManager>();
            if (manager == null &&
                Camera.main != null)
            {
                manager = Camera.main.GetComponent<
                    CameraControlManager>();
            }
            if (manager == null)
            {
                return false;
            }

            rig = manager.GetComponent<TimelineCamRig>() ??
                  manager.gameObject.AddComponent<
                      TimelineCamRig>();
            rig.Acquire(0f);
            originPosition = rig.CurrentPosition;
            originRotation = rig.CurrentRotation;
            CameraState state = manager.CurrentState;
            baseOrthographicSize = Mathf.Max(
                .01f,
                state.orthographicSize);
            baseFieldOfView = state.fieldOfView;
            viewModeHandle = rig.RequestViewMode(
                CameraViewMode.Side2D,
                CameraTransition.Immediate);

            CacheBounds(
                player,
                ref playerColliders,
                ref playerRenderers);
            CacheBounds(
                boss,
                ref bossColliders,
                ref bossRenderers);
            started = true;
            return true;
        }

        private void ApplyBossApproach(float time)
        {
            Vector3 target =
                boss.position + bossCameraOffset;
            Vector3 position;
            if (time < approachDuration)
            {
                float progress = SmoothProgress(
                    0f,
                    approachDuration,
                    time);
                Vector3 approachTarget =
                    new Vector3(
                        target.x,
                        originPosition.y,
                        target.z);
                position = Vector3.Lerp(
                    originPosition,
                    approachTarget,
                    progress);
            }
            else if (time <
                     approachDuration +
                     verticalCorrectionDuration)
            {
                float progress = SmoothProgress(
                    approachDuration,
                    approachDuration +
                    verticalCorrectionDuration,
                    time);
                position = new Vector3(
                    target.x,
                    Mathf.Lerp(
                        originPosition.y,
                        target.y,
                        progress),
                    target.z);
            }
            else
            {
                position = target;
            }

            rig.SetShotLens(
                baseOrthographicSize,
                baseFieldOfView);
            rig.SetShotTransform(
                position,
                Quaternion.Euler(
                    0f,
                    rig.PlanarYaw,
                    0f));
        }

        private void ApplyDualTargetFrame(float time)
        {
            TryGetBounds(
                playerColliders,
                playerRenderers,
                player,
                out Bounds playerBounds);
            TryGetBounds(
                bossColliders,
                bossRenderers,
                boss,
                out Bounds bossBounds);
            Bounds combined = playerBounds;
            combined.Encapsulate(bossBounds);

            Camera camera = rig.Manager != null
                ? rig.Manager.OutputCamera
                : Camera.main;
            float aspect = camera != null &&
                           camera.aspect > .01f
                ? camera.aspect
                : 16f / 9f;
            float targetSize = Mathf.Max(
                combined.extents.y + framePadding,
                (combined.extents.x +
                 framePadding) / aspect);
            if (keepBaseOrthographicMinimum)
            {
                targetSize = Mathf.Max(
                    targetSize,
                    baseOrthographicSize);
            }

            float progress = SmoothProgress(
                0f,
                dualTargetTransitionDuration,
                time);
            Vector3 targetPosition =
                new Vector3(
                    combined.center.x,
                    combined.center.y,
                    originPosition.z);
            Vector3 position = Vector3.Lerp(
                originPosition,
                targetPosition,
                progress);
            float size = Mathf.Lerp(
                baseOrthographicSize,
                targetSize,
                progress);
            rig.SetShotLens(
                size,
                baseFieldOfView);
            rig.SetShotTransform(
                position,
                Quaternion.Euler(
                    0f,
                    rig.PlanarYaw,
                    0f));
        }

        private void ApplyPlayerFocus()
        {
            rig.SetShotLens(
                baseOrthographicSize,
                baseFieldOfView);
            rig.SetShotTransform(
                player.position + playerFocusOffset,
                Quaternion.Euler(
                    0f,
                    rig.PlanarYaw,
                    0f));
        }

        private static void CacheBounds(
            Transform anchor,
            ref Collider[] colliders,
            ref Renderer[] renderers)
        {
            colliders =
                anchor.GetComponentsInChildren<Collider>(
                    true);
            renderers =
                anchor.GetComponentsInChildren<Renderer>(
                    true);
        }

        private static void TryGetBounds(
            Collider[] colliders,
            Renderer[] renderers,
            Transform fallback,
            out Bounds bounds)
        {
            bool hasBounds = false;
            bounds = new Bounds(
                fallback.position,
                new Vector3(1f, 1f, 1f));
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
            for (int index = 0;
                 index < renderers.Length;
                 index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
        }

        private static float SmoothProgress(
            float start,
            float end,
            float time)
        {
            if (end <= start)
            {
                return 1f;
            }
            float progress = Mathf.Clamp01(
                (time - start) / (end - start));
            return Mathf.SmoothStep(
                0f,
                1f,
                progress);
        }

        private static GameObject FindTagged(
            string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return null;
            }
            try
            {
                return GameObject.FindGameObjectWithTag(
                    tag);
            }
            catch (UnityException)
            {
                return null;
            }
        }

        private void Cleanup()
        {
            if (viewModeHandle.IsValid)
            {
                if (rig != null)
                {
                    rig.ReleaseViewMode(
                        viewModeHandle);
                }
                else
                {
                    viewModeHandle.Release(true);
                }
            }
            viewModeHandle = default;
            if (rig != null &&
                rig.IsTimelineControlling)
            {
                rig.ReleaseTimelineControl(true);
            }

            rig = null;
            boss = null;
            player = null;
            playerColliders =
                System.Array.Empty<Collider>();
            playerRenderers =
                System.Array.Empty<Renderer>();
            bossColliders =
                System.Array.Empty<Collider>();
            bossRenderers =
                System.Array.Empty<Renderer>();
            started = false;
        }
    }
}
