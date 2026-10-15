using System;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.InputAbstraction;
using Project.Player;
using Project.Rooms;
using TaSource.LinkEffect;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.GreenHook
{
    [DisallowMultipleComponent]
    public sealed class GreenHookAbility : MonoBehaviour
    {
        private enum HookPhase
        {
            None = 0,
            Extending = 1,
            Pulling = 2,
            Retracting = 3,
            Finishing = 4
        }

        private static readonly int LinkColorId =
            Shader.PropertyToID("_LinkColor");
        private static readonly int LineThicknessId =
            Shader.PropertyToID("_LineThickness");
        private static readonly int FeasibleId =
            Shader.PropertyToID("_Feasible");
        private static readonly int OpacityId =
            Shader.PropertyToID("_Opacity");
        private static readonly int NoiseStrengthId =
            Shader.PropertyToID("_NoiseStrength");
        private static readonly int EffectSpeedId =
            Shader.PropertyToID("_FlickerSpeed");
        private static readonly int StyleId =
            Shader.PropertyToID("_Style");
        private static readonly int RevealProgressId =
            Shader.PropertyToID("_RevealProgress");
        private static readonly int DissolveProgressId =
            Shader.PropertyToID("_DissolveProgress");
        private static readonly int LineLengthId =
            Shader.PropertyToID("_LineLength");

        [SerializeField] private PlayerInteractionSensor sensor;
        [SerializeField] private PlayerColorWheel colorWheel;
        [SerializeField] private PlayerController player;
        [SerializeField] private InteractionLinkEffect linkEffect;
        [SerializeField] private Material hookMaterial;
        [SerializeField, Min(.01f)] private float idleLengthCells = 1f;
        [SerializeField, Range(0f, 1f)] private float idleOpacity = .5f;
        [SerializeField, Range(0f, 1f)] private float hookOpacity = .9f;
        [SerializeField, Min(.05f)]
        private float growthSpeedCellsPerSecond = 10.44f;
        [SerializeField, Min(0f)]
        private float hookCatchRadiusCells = .5f;
        [SerializeField, Min(.05f)] private float pullSpeed = 8f;
        [SerializeField, Min(0f)] private float contactPadding = .05f;
        [SerializeField, Min(.01f)] private float blockedSeconds = .25f;
        [SerializeField, Min(.0001f)] private float minimumProgress = .002f;
        [SerializeField, Min(0f)] private float maximumPullSeconds = 10f;
        [SerializeField, Min(.01f)] private float finishDissolveSeconds = .18f;
        [SerializeField] private Vector3 sourceOffset =
            new Vector3(0f, .75f, 0f);
        [SerializeField] private float cameraFacingOffset = -.18f;
        [SerializeField, Range(2, 32)] private int curveSegments = 12;
        [SerializeField, Min(0f)] private float arcHeight = .08f;
        [SerializeField, Min(0f)] private float waveHeight = .055f;
        [SerializeField, Min(0f)] private float noiseStrength = .75f;
        [SerializeField, Min(0f)] private float effectSpeed = 7f;

        private LineRenderer line;
        private MaterialPropertyBlock properties;
        private IDisposable controlLock;
        private HookPhase phase;
        private IGreenHookTarget target;
        private ContactCubeMotion targetMotion;
        private Transform targetTransform;
        private Collider playerCollider;
        private Vector2 pointerPosition;
        private Vector2 hookDirection;
        private Vector3 hookStart;
        private Vector3 hookEnd;
        private Vector3 sourcePoint;
        private float hookLength;
        private float extensionElapsed;
        private float extensionDuration;
        private float retractElapsed;
        private float retractDuration;
        private float pullingElapsed;
        private float blockedElapsed;
        private Vector3 lastActorPosition;
        private float finishElapsed;
        private bool ownsFallbackMaterial;
        private bool pullingPlayer;

        public bool IsHookActive => phase != HookPhase.None;
        public bool IsExtending => phase == HookPhase.Extending;
        public bool IsPulling => phase == HookPhase.Pulling;
        public bool IsRetracting => phase == HookPhase.Retracting;

        private void Awake()
        {
            ResolveReferences();
            properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            ResolveReferences();
            phase = HookPhase.None;
            HideLine();
        }

        private void OnDisable()
        {
            CancelHook();
        }

        public void Tick(
            Vector2 currentPointerPosition,
            bool greenSelected)
        {
            pointerPosition = currentPointerPosition;
            if (phase != HookPhase.None)
            {
                return;
            }

            if (!greenSelected || !CanShowIdleIndicator())
            {
                HideLine();
                return;
            }

            UpdateIdleIndicator();
        }

        public GreenHookLaunchResult TryLaunch(
            Vector2 screenPosition,
            bool greenSelected)
        {
            if (!greenSelected)
            {
                return GreenHookLaunchResult.Ignored;
            }

            if (phase != HookPhase.None)
            {
                return GreenHookLaunchResult.Busy;
            }

            if (player == null ||
                player.IsControlLocked ||
                player.IsDead ||
                sensor == null ||
                GameInput.IsPointerOverUi())
            {
                return GreenHookLaunchResult.Ignored;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return GreenHookLaunchResult.Rejected;
            }

            if (!sensor.TryGetScreenHit(
                    camera,
                    screenPosition,
                    out RaycastHit screenHit,
                    QueryTriggerInteraction.Ignore))
            {
                if (TryGetPointerWorldPoint(
                        camera,
                        screenPosition,
                        out Vector3 pointerWorldPoint) &&
                    !IsWithinRange(pointerWorldPoint))
                {
                    return GreenHookLaunchResult.OutOfRange;
                }

                return GreenHookLaunchResult.Rejected;
            }

            if (!IsWithinRange(screenHit.point))
            {
                return GreenHookLaunchResult.OutOfRange;
            }

            if (!TryResolveHookTarget(
                    screenHit,
                    out IGreenHookTarget foundTarget) ||
                foundTarget == null ||
                !foundTarget.CanBeGreenHooked(gameObject))
            {
                return GreenHookLaunchResult.Rejected;
            }

            Component targetComponent = foundTarget as Component;
            if (!sensor.IsWithinRange(targetComponent))
            {
                return GreenHookLaunchResult.OutOfRange;
            }

            Vector3 clickPoint = screenHit.point;
            Vector2 rawDirection = new Vector2(
                clickPoint.x - transform.position.x,
                clickPoint.y - transform.position.y);
            if (rawDirection.sqrMagnitude < .0001f)
            {
                return GreenHookLaunchResult.Rejected;
            }

            hookDirection = SnapToCardinal(rawDirection);
            hookLength = Mathf.Min(
                rawDirection.magnitude,
                sensor.ScanRadius);
            Vector3 correctedEnd =
                transform.position +
                new Vector3(hookDirection.x, hookDirection.y, 0f) *
                hookLength;
            Vector3 targetPoint = GetTargetCenter(
                foundTarget.GreenHookTransform);
            if (!IsPathWithinRoom(correctedEnd) ||
                !IsPathWithinRoom(targetPoint))
            {
                return GreenHookLaunchResult.Rejected;
            }

            BeginHook(foundTarget, targetPoint);
            return GreenHookLaunchResult.Success;
        }

        public void CancelHook()
        {
            if (phase == HookPhase.None)
            {
                return;
            }

            if (targetMotion != null)
            {
                targetMotion.EndGreenHookPull(false);
            }

            player?.EndGreenHookPull(this);
            controlLock?.Dispose();
            controlLock = null;
            target = null;
            targetMotion = null;
            targetTransform = null;
            phase = HookPhase.None;
            finishElapsed = 0f;
            HideLine();
        }

        private void Update()
        {
            switch (phase)
            {
                case HookPhase.Extending:
                    UpdateExtension();
                    break;
                case HookPhase.Pulling:
                    UpdatePullingVisual();
                    break;
                case HookPhase.Retracting:
                    UpdateRetracting();
                    break;
                case HookPhase.Finishing:
                    UpdateFinishing();
                    break;
            }
        }

        private void FixedUpdate()
        {
            if (phase != HookPhase.Pulling)
            {
                return;
            }
            if (player == null || targetTransform == null)
            {
                FinishHook();
                return;
            }

            float deltaTime = Time.fixedDeltaTime;
            pullingElapsed += deltaTime;
            if (maximumPullSeconds > 0f &&
                pullingElapsed >= maximumPullSeconds)
            {
                FinishHook(true, false);
                return;
            }

            Vector3 targetCenter = GetTargetCenter(targetTransform);
            Vector3 targetSurfacePoint = GetTargetPoint(
                targetTransform,
                player.transform.position);
            float distance = DistanceToPlayer(targetSurfacePoint);
            if (IsTouchingPlayer(targetSurfacePoint, distance))
            {
                FinishHook(true, true);
                return;
            }

            Vector3 actorPosition = pullingPlayer
                ? player.transform.position
                : targetMotion != null
                    ? targetMotion.GreenHookPosition
                    : targetTransform.position;
            if ((actorPosition - lastActorPosition).sqrMagnitude >
                minimumProgress * minimumProgress)
            {
                blockedElapsed = 0f;
            }
            else
            {
                blockedElapsed += deltaTime;
            }
            lastActorPosition = actorPosition;

            if (blockedElapsed >= blockedSeconds)
            {
                if (!pullingPlayer &&
                    target.GreenHookMotionMode ==
                        GreenHookMotionMode.PullTarget)
                {
                    SwitchToPlayerPull();
                }
                else
                {
                    FinishHook(true, false);
                    return;
                }
            }

            Vector3 toTarget = targetCenter - GetPlayerCenter();
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude < .0001f)
            {
                FinishHook();
                return;
            }

            toTarget.Normalize();
            if (!pullingPlayer)
            {
                targetMotion?.SetGreenHookVelocity(
                    -toTarget * pullSpeed);
            }
            else
            {
                player.SetGreenHookPullVelocity(
                    this,
                    new Vector2(toTarget.x, toTarget.y) * pullSpeed);
            }
        }

        private void SwitchToPlayerPull()
        {
            if (pullingPlayer)
            {
                return;
            }

            pullingPlayer = true;
            blockedElapsed = 0f;
            lastActorPosition = player.transform.position;
            targetMotion?.SetGreenHookVelocity(Vector3.zero);
        }

        private void BeginHook(
            IGreenHookTarget foundTarget,
            Vector3 targetPoint)
        {
            target = foundTarget;
            targetTransform = foundTarget.GreenHookTransform;
            targetMotion = GetTargetMotion(targetTransform);
            hookStart = ResolveSourcePoint();
            hookEnd = hookStart +
                new Vector3(hookDirection.x, hookDirection.y, 0f) *
                hookLength;
            extensionElapsed = 0f;
            retractElapsed = 0f;
            retractDuration = 0f;
            pullingElapsed = 0f;
            blockedElapsed = 0f;
            finishElapsed = 0f;
            phase = HookPhase.Extending;

            float cellSize = Mathf.Max(
                .0001f,
                GridCellSizeUtility.Resolve(this));
            float distanceCells = hookLength / cellSize;
            extensionDuration = distanceCells /
                Mathf.Max(.05f, growthSpeedCellsPerSecond);

            controlLock = player.AcquireControlLock(this);
            player.BeginGreenHookPull(this);
            EnsureLine();
            UpdateLine(hookStart, idleOpacity, 0f, 0f);
        }

        private void UpdateExtension()
        {
            extensionElapsed += Time.deltaTime;
            float progress = extensionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(extensionElapsed / extensionDuration);
            Vector3 end =
                hookStart +
                new Vector3(hookDirection.x, hookDirection.y, 0f) *
                (hookLength * progress);
            UpdateLine(end, hookOpacity, progress, 0f);

            float catchRadius =
                hookCatchRadiusCells *
                Mathf.Max(
                    .0001f,
                    GridCellSizeUtility.Resolve(this));
            if (HasReachedTarget(end, catchRadius))
            {
                hookEnd = end;
                BeginPulling();
                return;
            }

            if (progress < 1f)
            {
                return;
            }

            hookEnd =
                hookStart +
                new Vector3(hookDirection.x, hookDirection.y, 0f) *
                hookLength;
            BeginRetracting();
        }

        private void BeginPulling()
        {
            phase = HookPhase.Pulling;
            pullingElapsed = 0f;
            blockedElapsed = 0f;
            retractElapsed = 0f;
            pullingPlayer =
                target.GreenHookMotionMode ==
                GreenHookMotionMode.PullPlayer;
            if (!pullingPlayer)
            {
                targetMotion?.BeginGreenHookPull();
            }
            blockedElapsed = 0f;
            lastActorPosition = pullingPlayer
                ? player.transform.position
                : targetMotion != null
                    ? targetMotion.GreenHookPosition
                    : targetTransform.position;
        }

        private bool TryResolveHookTarget(
            RaycastHit hit,
            out IGreenHookTarget target)
        {
            target = null;
            if (hit.collider == null)
            {
                return false;
            }

            MonoBehaviour[] behaviours =
                hit.collider.GetComponentsInParent<MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (!(behaviours[index] is IGreenHookTarget candidate) ||
                    !(behaviours[index] is Behaviour behaviour) ||
                    !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                target = candidate;
                return true;
            }

            return false;
        }

        private void BeginRetracting()
        {
            phase = HookPhase.Retracting;
            retractElapsed = 0f;
            retractDuration = hookLength /
                Mathf.Max(
                    .0001f,
                    GridCellSizeUtility.Resolve(this)) /
                Mathf.Max(.05f, growthSpeedCellsPerSecond);
        }

        private void UpdateRetracting()
        {
            retractElapsed += Time.deltaTime;
            float progress = retractDuration <= 0f
                ? 1f
                : Mathf.Clamp01(
                    retractElapsed /
                    Mathf.Max(.01f, retractDuration));
            Vector3 end = Vector3.Lerp(
                hookEnd,
                hookStart,
                progress);
            UpdateLine(
                end,
                hookOpacity,
                Mathf.Lerp(1f, 0f, progress),
                0f);
            if (progress >= 1f)
            {
                hookEnd = hookStart;
                FinishHook(false);
                targetTransform = null;
            }
        }

        private bool HasReachedTarget(
            Vector3 hookHead,
            float catchRadius)
        {
            if (targetTransform == null)
            {
                return false;
            }

            Vector3 targetPoint = GetTargetPoint(
                targetTransform,
                hookHead);
            Vector3 targetCenter = GetTargetCenter(targetTransform);
            float centerDistance =
                (targetCenter - hookHead).sqrMagnitude;
            float surfaceDistance =
                (targetPoint - hookHead).sqrMagnitude;
            return centerDistance <=
                   catchRadius * catchRadius ||
                   surfaceDistance <=
                   catchRadius * catchRadius;
        }

        private void UpdatePullingVisual()
        {
            if (targetTransform == null)
            {
                FinishHook();
                return;
            }

            Vector3 targetPoint = GetTargetCenter(
                targetTransform);
            UpdateLine(
                targetPoint,
                hookOpacity,
                1f,
                0f);
        }

        private void UpdateFinishing()
        {
            finishElapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(
                finishElapsed /
                Mathf.Max(.01f, finishDissolveSeconds));
            UpdateLine(
                targetTransform != null
                    ? GetTargetCenter(
                        targetTransform)
                    : hookEnd,
                hookOpacity,
                1f,
                progress);
            if (progress >= 1f)
            {
                phase = HookPhase.None;
                target = null;
                targetMotion = null;
                targetTransform = null;
                HideLine();
            }
        }

        private void FinishHook(
            bool snapTargetToGrid = true,
            bool separateFromPlayer = false)
        {
            if (phase == HookPhase.None)
            {
                return;
            }

            if (targetMotion != null)
            {
                Vector3 separationDirection = Vector3.zero;
                if (separateFromPlayer &&
                    targetTransform != null &&
                    player != null)
                {
                    separationDirection =
                        targetTransform.position -
                        player.transform.position;
                    if (separationDirection.sqrMagnitude < .0001f)
                    {
                        separationDirection =
                            new Vector3(
                                hookDirection.x,
                                hookDirection.y,
                                0f);
                    }
                }

                targetMotion.EndGreenHookPull(
                    snapTargetToGrid && !pullingPlayer,
                    separationDirection,
                    separateFromPlayer
                        ? Mathf.Max(.05f, contactPadding * 1.5f)
                        : 0f);
            }
            player?.EndGreenHookPull(this);
            controlLock?.Dispose();
            controlLock = null;
            phase = HookPhase.Finishing;
            finishElapsed = 0f;
        }

        private void UpdateIdleIndicator()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                HideLine();
                return;
            }

            if (!TryGetPointerWorldPoint(
                    camera,
                    pointerPosition,
                    out Vector3 pointerWorldPoint))
            {
                HideLine();
                return;
            }

            Vector2 rawDirection = new Vector2(
                pointerWorldPoint.x - transform.position.x,
                pointerWorldPoint.y - transform.position.y);
            if (rawDirection.sqrMagnitude < .0001f)
            {
                HideLine();
                return;
            }

            Vector2 direction = SnapToCardinal(rawDirection);
            float cellSize = Mathf.Max(
                .0001f,
                GridCellSizeUtility.Resolve(this));
            Vector3 end =
                ResolveSourcePoint() +
                new Vector3(direction.x, direction.y, 0f) *
                (cellSize * Mathf.Max(.01f, idleLengthCells));
            UpdateLine(end, idleOpacity, 1f, 0f);
        }

        private bool CanShowIdleIndicator()
        {
            return player != null &&
                   !player.IsControlLocked &&
                   !player.IsDead &&
                   Time.timeScale > 0f &&
                   !GameInput.IsPointerOverUi();
        }

        private void UpdateLine(
            Vector3 destination,
            float opacity,
            float revealProgress,
            float dissolveProgress)
        {
            EnsureLine();
            if (line == null)
            {
                return;
            }

            sourcePoint = ResolveSourcePoint();
            sourcePoint.z += cameraFacingOffset;
            Vector3 targetPoint = destination;
            targetPoint.z += cameraFacingOffset;
            Vector3 delta = targetPoint - sourcePoint;
            Vector3 side = Vector3.Cross(Vector3.forward, delta.normalized);
            int segments = Mathf.Clamp(curveSegments, 2, 32);
            line.positionCount = segments + 1;
            line.widthMultiplier = Mathf.Max(
                .01f,
                GetMaterialFloat(LineThicknessId, .2f) * 3f);

            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float envelope = Mathf.Sin(Mathf.PI * t);
                float seed =
                    sourcePoint.x * 13.1f +
                    targetPoint.y * 5.7f;
                float noise = Mathf.PerlinNoise(
                    t * 3.5f +
                    Time.time * Mathf.Max(.1f, effectSpeed) * .18f,
                    seed);
                float motion =
                    ((noise - .5f) * 2f * noiseStrength +
                     Mathf.Sin(t * 11f -
                         Time.time * effectSpeed) * .25f) *
                    waveHeight;
                Vector3 point = Vector3.Lerp(
                    sourcePoint,
                    targetPoint,
                    t) +
                    side * envelope * (arcHeight + motion);
                line.SetPosition(index, point);
            }

            float length = 0f;
            for (int index = 1; index <= segments; index++)
            {
                length += Vector3.Distance(
                    line.GetPosition(index - 1),
                    line.GetPosition(index));
            }

            properties.Clear();
            properties.SetColor(LinkColorId, new Color(.08f, 1f, .08f));
            properties.SetFloat(FeasibleId, 1f);
            properties.SetFloat(OpacityId, opacity);
            properties.SetFloat(StyleId, 3f);
            properties.SetFloat(RevealProgressId, revealProgress);
            properties.SetFloat(DissolveProgressId, dissolveProgress);
            properties.SetFloat(LineLengthId, Mathf.Max(.01f, length));
            line.SetPropertyBlock(properties);
            line.enabled = true;
        }

        private void EnsureLine()
        {
            if (line != null)
            {
                return;
            }

            if (hookMaterial == null && linkEffect != null)
            {
                hookMaterial = linkEffect.LinkMaterial;
            }
            if (hookMaterial == null)
            {
                Shader shader = Shader.Find("2026TapTap/Interaction Link");
                if (shader == null)
                {
                    return;
                }

                hookMaterial = new Material(shader)
                {
                    name = "Green Hook Runtime Material"
                };
                ownsFallbackMaterial = true;
            }

            GameObject child = new GameObject("__GreenHookLine");
            child.hideFlags = HideFlags.DontSave;
            child.transform.SetParent(transform, false);
            line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Stretch;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = 1000;
            line.sharedMaterial = hookMaterial;
            line.enabled = false;
        }

        private void HideLine()
        {
            if (line != null)
            {
                line.enabled = false;
            }
        }

        private Vector3 ResolveSourcePoint()
        {
            Vector3 source = transform.position +
                transform.TransformVector(
                    new Vector3(sourceOffset.x, 0f, sourceOffset.z));
            source += transform.up * sourceOffset.y;
            return source;
        }

        private Vector3 GetPlayerCenter()
        {
            if (playerCollider == null && player != null)
            {
                playerCollider = player.GetComponent<Collider>();
            }
            return playerCollider != null
                ? playerCollider.bounds.center
                : transform.position;
        }

        private float DistanceToPlayer(Vector3 targetPoint)
        {
            if (playerCollider == null && player != null)
            {
                playerCollider = player.GetComponent<Collider>();
            }
            if (playerCollider == null)
            {
                return Vector3.Distance(transform.position, targetPoint);
            }

            Vector3 playerPoint = playerCollider.ClosestPoint(targetPoint);
            return Vector3.Distance(playerPoint, targetPoint);
        }

        private bool IsTouchingPlayer(
            Vector3 targetSurfacePoint,
            float surfaceDistance)
        {
            if (surfaceDistance <= contactPadding)
            {
                return true;
            }

            if (targetTransform == null || playerCollider == null)
            {
                return false;
            }

            Bounds playerBounds = playerCollider.bounds;
            Collider[] colliders =
                targetTransform.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (collider.bounds.Intersects(playerBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector3 GetTargetPoint(
            Transform target,
            Vector3 from)
        {
            if (target == null)
            {
                return from;
            }

            Vector3 best = target.position;
            float bestDistance = float.MaxValue;
            Collider[] colliders =
                target.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 point = collider.ClosestPoint(from);
                float distance = (point - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }

            return best;
        }

        private static Vector3 GetTargetCenter(Transform target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            bool found = false;
            Bounds bounds = default;
            Collider[] colliders =
                target.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!found)
                {
                    bounds = collider.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return found ? bounds.center : target.position;
        }

        private static ContactCubeMotion GetTargetMotion(
            Transform target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<ContactCubeMotion>() ??
                   target.GetComponentInParent<ContactCubeMotion>();
        }

        private bool IsWithinRange(Vector3 point)
        {
            return sensor != null &&
                   (point - transform.position).sqrMagnitude <=
                   sensor.ScanRadius * sensor.ScanRadius;
        }

        private bool IsPathWithinRoom(Vector3 end)
        {
            RoomLifecycleManager manager =
                RoomLifecycleManager.ForScene(gameObject.scene);
            if (manager == null)
            {
                return true;
            }

            string startRegion =
                manager.FindRegion(transform.position);
            if (string.IsNullOrEmpty(startRegion) ||
                manager.FindRegion(end) != startRegion)
            {
                return false;
            }

            float distance = Vector3.Distance(
                transform.position,
                end);
            int steps = Mathf.Max(
                1,
                Mathf.CeilToInt(
                    distance /
                    Mathf.Max(.01f, manager.CellWorldSize)));
            for (int index = 1; index <= steps; index++)
            {
                float t = (float)index / steps;
                Vector3 point = Vector3.Lerp(
                    transform.position,
                    end,
                    t);
                if (manager.FindRegion(point) != startRegion)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector2 SnapToCardinal(Vector2 direction)
        {
            if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.y))
            {
                return new Vector2(
                    direction.x >= 0f ? 1f : -1f,
                    0f);
            }

            return new Vector2(
                0f,
                direction.y >= 0f ? 1f : -1f);
        }

        private bool TryGetPointerWorldPoint(
            Camera camera,
            Vector2 screenPosition,
            out Vector3 worldPoint)
        {
            worldPoint = default;
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            Plane gameplayPlane =
                new Plane(Vector3.forward, transform.position);
            if (!gameplayPlane.Raycast(ray, out float enter))
            {
                return false;
            }

            worldPoint = ray.GetPoint(enter);
            return true;
        }

        private float GetMaterialFloat(
            int propertyId,
            float fallback)
        {
            return hookMaterial != null &&
                   hookMaterial.HasProperty(propertyId)
                ? hookMaterial.GetFloat(propertyId)
                : fallback;
        }

        private void ResolveReferences()
        {
            if (sensor == null)
            {
                sensor = GetComponent<PlayerInteractionSensor>();
            }
            if (colorWheel == null)
            {
                colorWheel = GetComponent<PlayerColorWheel>();
            }
            if (player == null)
            {
                player = GetComponent<PlayerController>();
            }
            if (linkEffect == null)
            {
                linkEffect = GetComponent<InteractionLinkEffect>();
            }
            if (hookMaterial == null && linkEffect != null)
            {
                hookMaterial = linkEffect.LinkMaterial;
            }
        }

        private void OnDestroy()
        {
            if (ownsFallbackMaterial && hookMaterial != null)
            {
                Destroy(hookMaterial);
            }
        }

        private void OnValidate()
        {
            idleLengthCells = Mathf.Max(.01f, idleLengthCells);
            growthSpeedCellsPerSecond =
                Mathf.Max(.05f, growthSpeedCellsPerSecond);
            hookCatchRadiusCells =
                Mathf.Max(0f, hookCatchRadiusCells);
            pullSpeed = Mathf.Max(.05f, pullSpeed);
            contactPadding = Mathf.Max(0f, contactPadding);
            blockedSeconds = Mathf.Max(.01f, blockedSeconds);
            minimumProgress = Mathf.Max(.0001f, minimumProgress);
            maximumPullSeconds = Mathf.Max(0f, maximumPullSeconds);
            finishDissolveSeconds =
                Mathf.Max(.01f, finishDissolveSeconds);
            curveSegments = Mathf.Clamp(curveSegments, 2, 32);
        }
    }
}
