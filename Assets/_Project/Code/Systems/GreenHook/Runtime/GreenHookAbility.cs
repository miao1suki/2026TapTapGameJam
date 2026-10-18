using System;
using System.Collections.Generic;
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
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class GreenHookAbility : MonoBehaviour
    {
        private enum HookPhase
        {
            None = 0,
            Extending = 1,
            Pulling = 2,
            Retracting = 3,
            Finishing = 4,
            Swinging = 5
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
        [SerializeField, Min(.01f)] private float finishDissolveSeconds = .18f;
        [SerializeField] private Vector3 sourceOffset =
            new Vector3(0f, .3f, 0f);
        [SerializeField] private float cameraFacingOffset = -.18f;
        [SerializeField, Range(2, 32)] private int curveSegments = 12;
        [SerializeField, Min(0f)] private float arcHeight = .08f;
        [SerializeField, Min(0f)] private float waveHeight = .055f;
        [SerializeField, Min(0f)] private float noiseStrength = .75f;
        [SerializeField, Min(0f)] private float effectSpeed = 7f;
        [SerializeField, Min(0f)] private float aimToleranceCells = .65f;
        [SerializeField, Min(0f)] private float launchRangeSlackCells = .5f;
        [SerializeField, Min(0f)] private float idleFollowSeconds = .045f;

        private LineRenderer line;
        private Vector3[] linePoints;
        private MaterialPropertyBlock properties;
        private LineRenderer aimRing;
        private Material aimRingMaterial;
        private readonly Vector3[] ringPoints = new Vector3[48];
        private Vector3 lastRingCenter = Vector3.positiveInfinity;
        private float lastRingRadius = -1f;
        private bool? lastRingReachable;
        private readonly List<MonoBehaviour> targetBehaviours = new List<MonoBehaviour>(16);
        private Collider[] aimHits = new Collider[64];
        private Vector3 idleTipOffset;
        private bool idleTipInitialized;
        private float AimCellWorldSize => player != null ? player.GridCellWorldSize : hookCellWorldSize;
        public float AimToleranceWorld => aimToleranceCells * AimCellWorldSize;
        public float LaunchRangeWorld => (sensor != null ? sensor.ScanRadius : 0f) + launchRangeSlackCells * AimCellWorldSize;
        private IDisposable controlLock;
        private HookPhase phase;
        private IGreenHookTarget target;
        private IGreenHookMotion targetMotion;
        private Collider[] targetColliders = Array.Empty<Collider>();
        private Collider[] playerColliders = Array.Empty<Collider>();
        private Rigidbody playerBody;
        private Vector2 swingInput;
        private Vector2 swingVelocity;
        private Vector3 localAnchor;
        private float ropeLength;
        private string launchRegion;
        private Vector2 pullAxis;
        private bool pausedForPowerContact;
        private MechanismBaseFeature pulledBase;
        private IEnergySource pulledEnergy;
        private Collider[] powerContactHits = new Collider[32];
        private HashSet<MechanismBaseFeature> previousPowerContacts = new HashSet<MechanismBaseFeature>();
        private HashSet<MechanismBaseFeature> currentPowerContacts = new HashSet<MechanismBaseFeature>();
        private readonly Dictionary<MechanismBaseFeature, Vector3> pendingPowerSeats = new Dictionary<MechanismBaseFeature, Vector3>();
        private readonly List<MechanismBaseFeature> stalePowerSeats = new List<MechanismBaseFeature>();
        private Vector3 retargetVisualStart;
        private float retargetVisualElapsed;
        [SerializeField, Min(0f)] private float swingAcceleration = 18f;
        [SerializeField, Min(0.1f)] private float swingSpeedLimit = 14f;
        [SerializeField, Min(0f)] private float swingDamping = .08f;
        [SerializeField, Min(0f)] private float releaseBoostRatio = .08f;
        [SerializeField, Min(0f)] private float releaseBoostLimit = 1f;
        [SerializeField, Min(0f)] private float retargetVisualSeconds = .1f;
        [SerializeField, Min(0f)] private float attachedRangeSlackCells = 3f;
        [SerializeField, Min(0f)] private float attachedOutOfRangeGraceSeconds = .4f;
        private float hookCellWorldSize = 1f;
        private float outOfRangeSince = -1f;
        public bool IsSwinging => phase == HookPhase.Swinging;
        public bool IsPullingPlayer => pullingPlayer;
        public bool IsPullPausedForPower => pausedForPowerContact && IsPulling;

        public void DriveInput(Vector2 move, bool release)
        {
            swingInput = Vector2.ClampMagnitude(move, 1f);
            if (!release || phase == HookPhase.None) return;
            ReleaseHook(true);
        }
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
        private float finishElapsed;
        private bool ownsFallbackMaterial;
        private bool pullingPlayer;
        private bool showAimRequested;
        private float visualNoiseSeed;

        public bool IsHookActive => phase != HookPhase.None;
        public bool IsExtending => phase == HookPhase.Extending;
        public bool IsPulling => phase == HookPhase.Pulling;
        public bool IsRetracting => phase == HookPhase.Retracting;

        private void Awake()
        {
            ResolveReferences();
            properties = new MaterialPropertyBlock();
            visualNoiseSeed = GetInstanceID() * .0137f;
            if (GetComponent<GreenHookVisualDriver>() == null)
                gameObject.AddComponent<GreenHookVisualDriver>();
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
            HideLine();
        }

        public void Tick(
            Vector2 currentPointerPosition,
            bool greenSelected)
        {
            pointerPosition = currentPointerPosition;
            showAimRequested = greenSelected;
            if (phase == HookPhase.Extending && !greenSelected) { CancelHook(); return; }
            if (!greenSelected) { HideAimRing(); if (phase == HookPhase.None) HideLine(); }
        }

        // Render after the gameplay camera's LateUpdate (order 1000). Input and
        // physics retain their early execution order; no 50 Hz visual sampling.
        internal void RenderFrame()
        {
            bool showAim = showAimRequested && CanShowAimIndicator();
            if (showAim) UpdateAimRing();
            else HideAimRing();
            if (phase != HookPhase.None)
            {
                RenderAttachedFrame();
                return;
            }

            if (!showAim)
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

            bool retarget = phase == HookPhase.Swinging || phase == HookPhase.Pulling;
            if (phase != HookPhase.None && !retarget)
            {
                return GreenHookLaunchResult.Busy;
            }

            if (player == null ||
                (retarget ? !player.CanDriveGreenHook : player.IsControlLocked) ||
                player.IsDead ||
                Time.timeScale <= 0f ||
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

            if (!TryGetPointerWorldPoint(camera, screenPosition, out Vector3 pointerWorld))
                return GreenHookLaunchResult.Rejected;
            float reach = LaunchRangeWorld + AimToleranceWorld;
            if (((Vector2)(pointerWorld - transform.position)).sqrMagnitude > reach * reach)
                return GreenHookLaunchResult.OutOfRange;
            if (!TryFindAimTarget(camera, screenPosition, pointerWorld, out IGreenHookTarget foundTarget,
                    out Vector3 targetPoint, out bool outsideRange))
                return outsideRange ? GreenHookLaunchResult.OutOfRange : GreenHookLaunchResult.Rejected;
            if (foundTarget.GreenHookMotionMode == GreenHookMotionMode.PullTarget &&
                GetTargetMotion(foundTarget.GreenHookTransform) == null)
                return GreenHookLaunchResult.Rejected;

            Vector2 rawDirection = (Vector2)(targetPoint - ResolveSourcePoint());
            if (rawDirection.sqrMagnitude < .0001f)
            {
                return GreenHookLaunchResult.Rejected;
            }

            if (!IsPathWithinRoom(targetPoint))
            {
                return GreenHookLaunchResult.Rejected;
            }

            if (retarget) Retarget(foundTarget, targetPoint);
            else
            {
                BeginHook(foundTarget, targetPoint);
                // A valid click confirms attachment. Growing the visible rope must
                // never delay swinging or make a moving player miss the anchor.
                BeginPulling();
            }
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
                targetMotion.EndGreenHookPull(false, Vector3.zero, 0f);
            }

            player?.EndGreenHookPull(this);
            controlLock?.Dispose();
            controlLock = null;
            target = null;
            targetMotion = null;
            targetTransform = null;
            pausedForPowerContact = false;
            pulledBase = null;
            pulledEnergy = null;
            previousPowerContacts.Clear();
            currentPowerContacts.Clear();
            pendingPowerSeats.Clear();
            stalePowerSeats.Clear();
            phase = HookPhase.None;
            finishElapsed = 0f;
            pullingPlayer = false;
            outOfRangeSince = -1f;
            pullAxis = swingInput = swingVelocity = Vector2.zero;
            HideLine();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (phase == HookPhase.Extending || phase == HookPhase.Pulling || phase == HookPhase.Swinging)
            {
                if (!IsTargetAvailable() || player == null || player.IsDead || !IsActivePathWithinRoom())
                {
                    CancelHook();
                    return;
                }
                if (ShouldReleaseForRange()) { ReleaseHook(); return; }
            }
        }

        private void RenderAttachedFrame()
        {
            if (Time.timeScale <= 0f || player != null && player.IsControlLocked && !player.CanDriveGreenHook) return;
            switch (phase)
            {
                case HookPhase.Extending:
                    UpdateExtension();
                    break;
                case HookPhase.Pulling:
                case HookPhase.Swinging:
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
            if (phase != HookPhase.Pulling && phase != HookPhase.Swinging)
            {
                return;
            }
            if (player == null || player.IsDead || !IsTargetAvailable() ||
                !IsActivePathWithinRoom())
            {
                CancelHook();
                return;
            }
            if (ShouldReleaseForRange()) { ReleaseHook(); return; }
            if (!player.CanDriveGreenHook) { StopPullMotion(); return; }
            float deltaTime = Time.fixedDeltaTime;
            if (phase == HookPhase.Swinging)
            {
                player.SetGreenHookLocomotion(this, false);
                TickSwing(deltaTime);
                return;
            }
            if (!pausedForPowerContact) RefreshPowerContacts(false);
            if (pausedForPowerContact)
            {
                // A successful socket connection is a deliberate resting point,
                // not a reason to detach or pull the player into the mechanism.
                StopPullMotion();
                return;
            }
            Vector3 targetCenter = GetTargetCenter(targetTransform);
            Vector3 targetSurfacePoint = GetTargetPoint(
                targetTransform,
                player.transform.position);
            float distance = DistanceToPlayer(targetSurfacePoint);
            if (IsTouchingPlayer(targetSurfacePoint, distance))
            {
                StopPullMotion();
                return;
            }
            Vector3 toTarget = targetCenter - GetPlayerCenter();
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude < .0001f)
            {
                StopPullMotion();
                return;
            }
            // Visual aiming is free; the physical pull follows a stable single grid axis.
            if (pullAxis == Vector2.zero || Vector2.Dot(toTarget, pullAxis) <= .05f)
                pullAxis = SnapToCardinal(toTarget);
            toTarget = pullAxis;
            float step = Mathf.Min(pullSpeed * deltaTime, Mathf.Max(0f, distance - contactPadding));
            // The base's neighbour-volume signal may turn on while only the
            // corner of a red box has entered the neighbouring cell. Finish
            // the already-directed approach before parking, otherwise the
            // new-power pause strands a box on the lip instead of on the base.
            step = LimitPowerSeatingStep(targetCenter, -toTarget, step);
            if (pausedForPowerContact) { StopPullMotion(); return; }
            // Assess the next physical step every time. Do not infer blockage from render-frame jitter.
            float targetStep = targetMotion != null && target.GreenHookMotionMode == GreenHookMotionMode.PullTarget
                ? GreenHookPhysics.FreeDistance(targetColliders, targetTransform, transform,
                    -toTarget, step, .015f, true) : 0f;
            if (pendingPowerSeats.Count != 0 && targetStep < Mathf.Min(step, .002f))
            {
                // A real face/wall wins over the ideal cell centre. Never push
                // through the socket or switch to pulling the player on entry.
                pausedForPowerContact = true;
                StopPullMotion();
                return;
            }
            pullingPlayer = targetStep < Mathf.Min(step, .002f);
            if (!pullingPlayer && targetMotion != null)
            {
                player.SetGreenHookLocomotion(this, true);
                player.SetGreenHookPullVelocity(this, Vector2.zero);
                targetMotion.SetGreenHookVelocity(-toTarget * (targetStep / deltaTime));
            }
            else
            {
                targetMotion?.SetGreenHookVelocity(Vector3.zero);
                float playerStep = GreenHookPhysics.FreeDistance(playerColliders, transform,
                    targetTransform, toTarget, step, .015f);
                player.SetGreenHookLocomotion(this, playerStep <= .002f);
                player.SetGreenHookPullVelocity(this, (Vector2)toTarget * (playerStep / deltaTime));
            }
        }

        private void StopPullMotion()
        {
            pullingPlayer = false;
            targetMotion?.SetGreenHookVelocity(Vector3.zero);
            player?.SetGreenHookPullVelocity(this, Vector2.zero);
            if (player != null) player.SetGreenHookLocomotion(this, player.CanDriveGreenHook);
            swingVelocity = Vector2.zero;
        }

        private void RefreshPowerContacts(bool snapshot)
        {
            currentPowerContacts.Clear();
            if (pulledBase != null && pulledBase.IsSignalActive) currentPowerContacts.Add(pulledBase);
            if (pulledEnergy != null && pulledEnergy.IsEnergyAvailable && targetTransform != null)
            {
                // Local, non-allocating discovery only while pulling an energy
                // source. No scene scan, registry polling, or idle-mouse query.
                Bounds area = new Bounds(targetTransform.position, Vector3.zero);
                foreach (Collider shape in targetColliders)
                    if (shape != null && shape.enabled && !shape.isTrigger) area.Encapsulate(shape.bounds);
                int count;
                do
                {
                    count = Physics.OverlapBoxNonAlloc(area.center, area.extents + Vector3.one * hookCellWorldSize,
                        powerContactHits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    if (count < powerContactHits.Length || powerContactHits.Length >= 4096) break;
                    Array.Resize(ref powerContactHits, powerContactHits.Length * 2);
                } while (true);
                if (count == powerContactHits.Length) return;
                for (int index = 0; index < count; index++)
                {
                    MechanismBaseFeature foundation = powerContactHits[index].GetComponentInParent<MechanismBaseFeature>();
                    if (foundation != null && !currentPowerContacts.Contains(foundation) && foundation.IsPoweredBy(pulledEnergy))
                        currentPowerContacts.Add(foundation);
                }
            }
            stalePowerSeats.Clear();
            foreach (var pending in pendingPowerSeats)
                if (!currentPowerContacts.Contains(pending.Key)) stalePowerSeats.Add(pending.Key);
            foreach (MechanismBaseFeature stale in stalePowerSeats) pendingPowerSeats.Remove(stale);
            if (snapshot) pendingPowerSeats.Clear();
            else
                foreach (MechanismBaseFeature foundation in currentPowerContacts)
                    if (!previousPowerContacts.Contains(foundation))
                    {
                        if (pulledEnergy == null || foundation == pulledBase)
                        {
                            pausedForPowerContact = true;
                            break;
                        }
                        Vector2 side = SnapToCardinal(targetTransform.position - foundation.transform.position);
                        pendingPowerSeats[foundation] = (Vector3)side * GridCellSizeUtility.Resolve(foundation);
                    }
            var previous = previousPowerContacts;
            previousPowerContacts = currentPowerContacts;
            currentPowerContacts = previous;
        }

        private float LimitPowerSeatingStep(Vector3 targetCenter, Vector3 direction, float step)
        {
            float tolerance = .02f * hookCellWorldSize;
            foreach (var pending in pendingPowerSeats)
            {
                if (pending.Key == null) continue;
                Vector3 remaining = pending.Key.transform.position + pending.Value - targetCenter;
                remaining.z = 0f;
                float forward = Vector3.Dot(remaining, direction);
                Vector3 across = remaining - direction * forward;
                // Only complete an approach that is already heading into the
                // neighbouring cell. Moving away, changing axes or a socket
                // moving out from under the box must park immediately instead
                // of inventing a new movement/snap/autopilot path.
                if (forward <= tolerance || across.sqrMagnitude > tolerance * tolerance * 4f)
                {
                    pausedForPowerContact = true;
                    return 0f;
                }
                step = Mathf.Min(step, forward);
            }
            return step;
        }

        private void ReleaseHook(bool manual = false)
        {
            // Normal release (manual or range) keeps resolved swing momentum, but must
            // not inject velocity while another system owns a higher-priority lock.
            bool preserveMomentum = IsSwinging && player != null && player.CanDriveGreenHook;
            Vector2 velocity = playerBody != null ? (Vector2)playerBody.linearVelocity : swingVelocity;
            if (preserveMomentum && manual && velocity.sqrMagnitude > .0001f)
                velocity += velocity.normalized * Mathf.Min(releaseBoostLimit, velocity.magnitude * releaseBoostRatio);
            CancelHook();
            if (preserveMomentum) player.RestoreGreenHookReleaseVelocity(velocity);
        }

        private bool ShouldReleaseForRange()
        {
            if (sensor == null || targetTransform == null) return true;
            // Use the actual bound point, not the mouse or the blended visual tip.
            float radius = sensor.ScanRadius + .001f;
            bool attached = phase == HookPhase.Pulling || phase == HookPhase.Swinging;
            if (attached) radius += attachedRangeSlackCells * hookCellWorldSize;
            if ((targetTransform.TransformPoint(localAnchor) - transform.position).sqrMagnitude <= radius * radius)
            {
                outOfRangeSince = -1f;
                return false;
            }
            if (!attached) return true;
            if (outOfRangeSince < 0f) outOfRangeSince = Time.time;
            return Time.time - outOfRangeSince >= attachedOutOfRangeGraceSeconds;
        }

        private bool IsActivePathWithinRoom()
        {
            var manager = RoomLifecycleManager.ForScene(gameObject.scene);
            return manager == null || (manager.FindRegion(GetPlayerCenter()) == launchRegion &&
                IsPathWithinRoom(targetTransform.TransformPoint(localAnchor)));
        }

        private bool IsTargetAvailable() => targetTransform != null && target != null &&
            targetTransform.gameObject.activeInHierarchy &&
            (!(target is Behaviour behaviour) || behaviour.isActiveAndEnabled) &&
            (phase == HookPhase.Pulling
                ? target.GreenHookMotionMode != GreenHookMotionMode.PullTarget ||
                    targetMotion != null && targetMotion.IsGreenHookPullActive
                : target.CanBeGreenHooked(gameObject));

        private void TickSwing(float dt)
        {
            Vector2 center = GetPlayerCenter();
            Vector2 anchor = targetTransform.TransformPoint(localAnchor);
            Vector2 radial = center - anchor;
            float length = radial.magnitude;
            Vector2 normal = length > .0001f ? radial / length : Vector2.down;
            // Input pumps along the tangent instead of fighting the radial rope constraint.
            Vector2 tangent = new Vector2(-normal.y, normal.x);
            if (tangent.x < 0f) tangent = -tangent;
            Vector2 acceleration = tangent * (swingInput.x * swingAcceleration) + Vector2.up * Physics.gravity.y;
            // Read resolved physics velocity so collision contacts cannot accumulate invisible momentum.
            swingVelocity = playerBody != null ? (Vector2)playerBody.linearVelocity : swingVelocity;
            swingVelocity *= Mathf.Exp(-swingDamping * dt);
            swingVelocity += acceleration * dt;
            if (length >= ropeLength - .02f)
                swingVelocity -= normal * Mathf.Max(0f, Vector2.Dot(swingVelocity, normal));
            swingVelocity = Vector2.ClampMagnitude(swingVelocity, swingSpeedLimit);
            Vector2 next = center + swingVelocity * dt;
            Vector2 offset = next - anchor;
            if (offset.magnitude > ropeLength) next = anchor + offset.normalized * ropeLength;
            Vector2 movement = next - center;
            // A floor hit must not erase horizontal swing momentum (nor a wall
            // erase downward sliding). Preserve movement tangent to the contact.
            Vector3 allowed = GreenHookPhysics.SlideDisplacement(playerColliders, transform, movement, .01f);
            swingVelocity = (Vector2)allowed / dt;
            player.SetGreenHookPullVelocity(this, swingVelocity);
        }

        private void BeginHook(
            IGreenHookTarget foundTarget,
            Vector3 targetPoint)
        {
            target = foundTarget;
            targetTransform = foundTarget.GreenHookTransform;
            targetMotion = GetTargetMotion(targetTransform);
            localAnchor = targetTransform.InverseTransformPoint(targetPoint);
            targetColliders = targetTransform.GetComponentsInChildren<Collider>(true);
            playerColliders = player.GetComponentsInChildren<Collider>(true);
            playerBody = player.GetComponent<Rigidbody>();
            launchRegion = RoomLifecycleManager.ForScene(gameObject.scene)?.FindRegion(GetPlayerCenter());
            swingInput = Vector2.zero;
            outOfRangeSince = -1f;
            pullAxis = Vector2.zero;
            hookStart = ResolveSourcePoint();
            hookEnd = targetPoint;
            hookDirection = ((Vector2)(targetPoint - hookStart)).normalized;
            hookLength = Vector2.Distance(hookStart, targetPoint);
            extensionElapsed = 0f;
            retractElapsed = 0f;
            retractDuration = 0f;
            finishElapsed = 0f;
            phase = HookPhase.Extending;

            float cellSize = Mathf.Max(
                .0001f,
                GridCellSizeUtility.Resolve(this));
            hookCellWorldSize = cellSize;
            float distanceCells = hookLength / cellSize;
            extensionDuration = distanceCells /
                Mathf.Max(.05f, growthSpeedCellsPerSecond);

            EnsureLine();
            UpdateLine(hookStart, idleOpacity, 0f, 0f);
        }

        private void UpdateExtension()
        {
            if (!IsTargetAvailable()) { CancelHook(); return; }
            hookStart = ResolveSourcePoint();
            hookEnd = targetTransform.TransformPoint(localAnchor);
            Vector3 previousHead = Vector3.Lerp(hookStart, hookEnd,
                Mathf.Clamp01(extensionElapsed / Mathf.Max(.0001f, extensionDuration)));
            extensionElapsed += Time.deltaTime;
            float progress = extensionDuration <= 0f
                ? 1f
                : Mathf.Clamp01(extensionElapsed / extensionDuration);
            Vector3 end = Vector3.Lerp(hookStart, hookEnd, progress);
            UpdateLine(end, hookOpacity, progress, 0f);

            float catchRadius =
                hookCatchRadiusCells *
                Mathf.Max(
                    .0001f,
                    GridCellSizeUtility.Resolve(this));
            if (HasReachedTarget(end, catchRadius) || HasCrossedTarget(previousHead, end, catchRadius))
            {
                hookEnd = end;
                BeginPulling();
                return;
            }

            if (progress < 1f)
            {
                return;
            }

            BeginRetracting();
        }

        private bool HasCrossedTarget(Vector3 start, Vector3 end, float radius)
        {
            if (targetTransform == null) return false;
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < .000001f) return false;
            foreach (Collider shape in targetColliders)
            {
                if (shape == null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy) continue;
                if (shape.Raycast(new Ray(start, segment.normalized), out _, Mathf.Sqrt(lengthSquared))) return true;
                Vector3 nearest = start + segment * Mathf.Clamp01(Vector3.Dot(shape.bounds.center-start, segment)/lengthSquared);
                if ((shape.ClosestPoint(nearest)-nearest).sqrMagnitude <= radius*radius) return true;
            }
            return false;
        }

        private void BeginPulling()
        {
            if (!IsTargetAvailable()) { CancelHook(); return; }
            if (target.GreenHookMotionMode == GreenHookMotionMode.PullTarget && targetMotion == null)
            { CancelHook(); return; }
            controlLock = player.AcquireControlLock(this);
            player.BeginGreenHookPull(this, target.GreenHookMotionMode == GreenHookMotionMode.Swing);
            retargetVisualStart = ResolveSourcePoint();
            retargetVisualElapsed = 0f;
            StartAttachedMotion();
        }

        private void StartAttachedMotion()
        {
            pausedForPowerContact = false;
            previousPowerContacts.Clear();
            currentPowerContacts.Clear();
            pendingPowerSeats.Clear();
            stalePowerSeats.Clear();
            pulledBase = targetTransform.GetComponent<MechanismBaseFeature>();
            pulledEnergy = targetTransform.GetComponent<IEnergySource>();
            phase = HookPhase.Pulling;
            if (target.GreenHookMotionMode == GreenHookMotionMode.Swing)
            {
                player.SetGreenHookLocomotion(this, false);
                phase = HookPhase.Swinging;
                pullingPlayer = false;
                ropeLength = Mathf.Max(.1f, Vector2.Distance(GetPlayerCenter(), targetTransform.TransformPoint(localAnchor)));
                swingVelocity = playerBody != null ? (Vector2)playerBody.linearVelocity : Vector2.zero;
                Vector2 outward = ((Vector2)targetTransform.TransformPoint(localAnchor) - (Vector2)GetPlayerCenter()).normalized;
                // Preserve tangential/inward momentum, removing only rope-stretching velocity.
                swingVelocity -= outward * Mathf.Min(0f, Vector2.Dot(swingVelocity, outward));
                player.SetGreenHookPullVelocity(this, swingVelocity);
                return;
            }
            retractElapsed = 0f;
            pullingPlayer =
                target.GreenHookMotionMode ==
                GreenHookMotionMode.PullPlayer;
            if (!pullingPlayer)
            {
                targetMotion?.BeginGreenHookPull();
            }
            player.SetGreenHookLocomotion(this, !pullingPlayer);
            // A deliberate click on an already-connected object means continue
            // pulling it out. Only a subsequent new connection pauses again.
            RefreshPowerContacts(true);
        }

        private void Retarget(IGreenHookTarget next, Vector3 point)
        {
            Vector3 previousPoint = line != null && line.enabled && line.positionCount > 0
                ? ProjectVisualDepth(line.GetPosition(line.positionCount - 1), -cameraFacingOffset)
                : targetTransform.TransformPoint(localAnchor);
            if (!ReferenceEquals(next, target)) targetMotion?.EndGreenHookPull(false, Vector3.zero, 0f);
            target = next;
            targetTransform = next.GreenHookTransform;
            targetMotion = GetTargetMotion(targetTransform);
            targetColliders = targetTransform.GetComponentsInChildren<Collider>(true);
            localAnchor = targetTransform.InverseTransformPoint(point);
            pullAxis = Vector2.zero;
            outOfRangeSince = -1f;
            retargetVisualStart = previousPoint;
            retargetVisualElapsed = 0f;
            // Retain the existing control handle and actual physics momentum across anchor changes.
            StartAttachedMotion();
        }

        private bool TryResolveHookTarget(
            Collider collider,
            out IGreenHookTarget target)
        {
            target = null;
            if (collider == null)
            {
                return false;
            }

            targetBehaviours.Clear();
            collider.GetComponentsInParent(false, targetBehaviours);
            for (int index = 0; index < targetBehaviours.Count; index++)
            {
                if (!(targetBehaviours[index] is IGreenHookTarget candidate) ||
                    !(targetBehaviours[index] is Behaviour behaviour) ||
                    !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                target = candidate;
                return true;
            }

            return false;
        }

        private bool TryFindAimTarget(Camera camera, Vector2 screen, Vector3 pointerWorld,
            out IGreenHookTarget bestTarget, out Vector3 bestPoint, out bool outsideRange)
        {
            bestTarget = null; bestPoint = default; outsideRange = false;
            float tolerance = AimToleranceWorld;
            float bestDistance = float.PositiveInfinity;
            int bestId = int.MaxValue;
            // Click-only broad phase; idle rotation never scans the room. Z is
            // the game's one-cell-deep plane, not a screen ray blocked by walls.
            Vector3 half = new Vector3(Mathf.Max(.001f, tolerance), Mathf.Max(.001f, tolerance),
                Mathf.Max(1f, AimCellWorldSize) * 2f);
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(pointerWorld, half, aimHits, Quaternion.identity,
                    sensor.InteractionMask, QueryTriggerInteraction.Ignore);
                if (count < aimHits.Length || aimHits.Length >= 4096) break;
                Array.Resize(ref aimHits, aimHits.Length * 2);
            } while (true);
            // A saturated click buffer cannot safely select an arbitrary subset.
            if (count == aimHits.Length) return false;
            Ray ray = camera.ScreenPointToRay(screen);
            RoomLifecycleManager manager = RoomLifecycleManager.ForScene(gameObject.scene);
            string playerRegion = manager != null ? manager.FindRegion(GetPlayerCenter()) : null;
            for (int i = 0; i < count; i++)
            {
                Collider shape = aimHits[i];
                if (shape == null || shape.transform.IsChildOf(transform) ||
                    !TryResolveHookTarget(shape, out IGreenHookTarget candidate) ||
                    (!ReferenceEquals(candidate, target) && !candidate.CanBeGreenHooked(gameObject))) continue;
                Transform anchor = candidate.GreenHookTransform;
                if (anchor == null) continue;
                var member = anchor.GetComponentInParent<RoomResetObject>();
                if (manager != null && member != null && !string.IsNullOrEmpty(member.RegionId) &&
                    member.RegionId != playerRegion) continue;
                Plane plane = new Plane(Vector3.forward, anchor.position);
                if (!plane.Raycast(ray, out float enter)) continue;
                Vector3 pointerOnTargetPlane = ray.GetPoint(enter);
                Vector3 point = shape.ClosestPoint(pointerOnTargetPlane);
                point.z = anchor.position.z;
                float distance = ((Vector2)(point - pointerOnTargetPlane)).sqrMagnitude;
                if (distance > tolerance * tolerance + .000001f) continue;
                if (!IsWithinRange(point)) { outsideRange = true; continue; }
                if (!IsPathWithinRoom(point)) continue;
                int id = (candidate as Component).GetInstanceID();
                if (distance > bestDistance + .000001f ||
                    Mathf.Abs(distance - bestDistance) <= .000001f && id >= bestId) continue;
                bestDistance = distance; bestId = id; bestTarget = candidate; bestPoint = point;
            }
            return bestTarget != null;
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

            Vector3 targetPoint = targetTransform.TransformPoint(localAnchor);
            if (retargetVisualElapsed < retargetVisualSeconds)
            {
                retargetVisualElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(retargetVisualElapsed / Mathf.Max(.001f, retargetVisualSeconds));
                targetPoint = Vector3.Lerp(retargetVisualStart, targetPoint, t * t * (3f - 2f * t));
            }
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
            pausedForPowerContact = false;
            pulledBase = null;
            pulledEnergy = null;
            previousPowerContacts.Clear();
            currentPowerContacts.Clear();
            pendingPowerSeats.Clear();
            stalePowerSeats.Clear();
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

            Vector2 rawDirection = (Vector2)(pointerWorldPoint - ResolveSourcePoint());
            if (rawDirection.sqrMagnitude < .0001f)
            {
                idleTipInitialized = false;
                if (line != null) line.enabled = false; // keep the cursor ring visible
                return;
            }

            // Follow the actual pointer position, not a fixed one-cell direction
            // indicator. Preview remains within launch range; attached rope still
            // follows its real local target anchor, independent of the pointer.
            Vector3 end = pointerWorldPoint;
            Vector3 rangeCenter = sensor != null ? sensor.transform.position : transform.position;
            float radius = sensor != null ? sensor.ScanRadius : AimCellWorldSize * idleLengthCells;
            radius += launchRangeSlackCells * AimCellWorldSize;
            Vector3 desiredOffset = Vector3.ClampMagnitude(end - rangeCenter, radius);
            if (!idleTipInitialized || idleFollowSeconds <= 0f) idleTipOffset = desiredOffset;
            else idleTipOffset = Vector3.Lerp(idleTipOffset, desiredOffset,
                1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(.001f, idleFollowSeconds)));
            idleTipInitialized = true;
            // Aiming must never trail the selection circle: smoothing the tip
            // adds a phase delay on fast turns, even when rendering is fast.
            // Keep the exact pointer endpoint and ease only the interior bend.
            UpdateLine(rangeCenter + desiredOffset, idleOpacity, 1f, 0f);
        }

        private bool CanShowAimIndicator()
        {
            return player != null &&
                   (!player.IsControlLocked || player.CanDriveGreenHook) &&
                   !player.IsCarrying && !player.IsCarryBusy &&
                   !player.IsDead &&
                   Time.timeScale > 0f &&
                   !GameInput.IsPointerOverUi();
        }

        private void UpdateAimRing()
        {
            if (!TryGetPointerWorldPoint(Camera.main, pointerPosition, out Vector3 center)) { HideAimRing(); return; }
            if (aimRing == null)
            {
                Shader shader = Resources.Load<Shader>("GreenHookAimRing");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) return;
                aimRingMaterial = new Material(shader) { name = "Green Aim Tolerance", hideFlags = HideFlags.DontSave };
                var child = new GameObject("__GreenAimTolerance") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(transform, false);
                aimRing = child.AddComponent<LineRenderer>();
                aimRing.useWorldSpace = true; aimRing.loop = true;
                aimRing.positionCount = ringPoints.Length;
                aimRing.sharedMaterial = aimRingMaterial;
                aimRing.shadowCastingMode = ShadowCastingMode.Off;
                aimRing.receiveShadows = false; aimRing.sortingOrder = 1001;
            }
            float radius = AimToleranceWorld;
            // The overlay shader already ignores depth. Moving this point on
            // world Z would introduce pointer parallax with a perspective camera.
            if (center != lastRingCenter || radius != lastRingRadius)
            {
                for (int i = 0; i < ringPoints.Length; i++)
                {
                    float angle = i * (Mathf.PI * 2f / ringPoints.Length);
                    ringPoints[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
                }
                aimRing.SetPositions(ringPoints);
                aimRing.widthMultiplier = .025f * AimCellWorldSize;
                lastRingCenter = center;
                lastRingRadius = radius;
            }
            float reach = LaunchRangeWorld + radius;
            bool reachable = ((Vector2)(center - transform.position)).sqrMagnitude <= reach * reach;
            if (lastRingReachable != reachable)
            {
                Color tint = reachable ? new Color(.3f, 1f, .25f, .7f) : new Color(.55f, .65f, .55f, .3f);
                aimRing.startColor = aimRing.endColor = tint;
                lastRingReachable = reachable;
            }
            aimRing.enabled = radius > 0f;
        }

        private void HideAimRing()
        {
            if (aimRing != null) aimRing.enabled = false;
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

            sourcePoint = ProjectVisualDepth(ResolveSourcePoint());
            Vector3 targetPoint = ProjectVisualDepth(destination);
            Vector3 delta = targetPoint - sourcePoint;
            Vector3 side = Vector3.Cross(Vector3.forward, delta.normalized);
            int segments = Mathf.Clamp(curveSegments, 2, 32);
            if (linePoints == null || linePoints.Length != segments + 1)
                linePoints = new Vector3[segments + 1];
            line.positionCount = segments + 1;
            line.widthMultiplier = Mathf.Max(
                .01f,
                GetMaterialFloat(LineThicknessId, .2f) * 3f);

            float length = 0f;
            Vector3 idleBend = Vector3.zero;
            if (phase == HookPhase.None && idleTipInitialized)
            {
                Vector3 rangeCenter = sensor != null ? sensor.transform.position : transform.position;
                idleBend = rangeCenter + idleTipOffset - destination;
                idleBend.z = 0f;
                idleBend = Vector3.ClampMagnitude(idleBend, .12f * AimCellWorldSize);
            }
            for (int index = 0; index <= segments; index++)
            {
                float t = (float)index / segments;
                float envelope = Mathf.Sin(Mathf.PI * t);
                // Rotating the pointer must not re-seed the entire silhouette.
                // Only time evolves the shape, otherwise fast turns shimmer.
                float seed = visualNoiseSeed;
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
                point += idleBend * envelope;
                linePoints[index] = point;
                if (index > 0) length += Vector3.Distance(linePoints[index - 1], point);
            }
            line.SetPositions(linePoints);

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

        private Vector3 ProjectVisualDepth(Vector3 point) => ProjectVisualDepth(point, cameraFacingOffset);

        private static Vector3 ProjectVisualDepth(Vector3 point, float depthOffset)
        {
            if (Mathf.Abs(depthOffset) < .00001f) return point;
            Camera camera = Camera.main;
            // A plain Z offset causes parallax in perspective. Move along the
            // same screen ray, preserving the exact cursor/anchor projection.
            if (camera != null)
            {
                Ray ray = camera.ViewportPointToRay(camera.WorldToViewportPoint(point));
                Plane plane = new Plane(Vector3.forward, point + Vector3.forward * depthOffset);
                if (plane.Raycast(ray, out float distance)) return ray.GetPoint(distance);
            }
            point.z += depthOffset;
            return point;
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
            idleTipInitialized = false;
            HideAimRing();
            if (line != null)
            {
                line.enabled = false;
            }
        }

        private Vector3 ResolveSourcePoint()
        {
            if (linkEffect != null) return linkEffect.SourceWorldPosition;
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

            Collider[] colliders =
                targetColliders;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled || collider.isTrigger ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (Physics.ComputePenetration(playerCollider, playerCollider.transform.position,
                    playerCollider.transform.rotation, collider, collider.transform.position,
                    collider.transform.rotation, out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        private Vector3 GetTargetPoint(
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
                target == targetTransform ? targetColliders : target.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled || collider.isTrigger ||
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

        private Vector3 GetTargetCenter(Transform target)
        {
            if (target == null)
            {
                return Vector3.zero;
            }

            bool found = false;
            Bounds bounds = default;
            Collider[] colliders =
                target == targetTransform ? targetColliders : target.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null ||
                    !collider.enabled || collider.isTrigger ||
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

        private static IGreenHookMotion GetTargetMotion(
            Transform target)
        {
            if (target == null)
            {
                return null;
            }

            return target.GetComponent<IGreenHookMotion>() ??
                   target.GetComponentInParent<IGreenHookMotion>();
        }

        private bool IsWithinRange(Vector3 point)
        {
            return sensor != null &&
                   ((Vector2)(point - transform.position)).sqrMagnitude <=
                   LaunchRangeWorld * LaunchRangeWorld + .000001f;
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
                    Mathf.Max(.01f, manager.CellWorldSize * .25f)));
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
            if (aimRingMaterial != null) Destroy(aimRingMaterial);
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
            finishDissolveSeconds =
                Mathf.Max(.01f, finishDissolveSeconds);
            curveSegments = Mathf.Clamp(curveSegments, 2, 32);
            swingDamping = Mathf.Max(0f, swingDamping);
            retargetVisualSeconds = Mathf.Max(0f, retargetVisualSeconds);
            aimToleranceCells = Mathf.Max(0f, aimToleranceCells);
            launchRangeSlackCells = Mathf.Max(0f, launchRangeSlackCells);
            idleFollowSeconds = Mathf.Max(0f, idleFollowSeconds);
            releaseBoostRatio = Mathf.Max(0f, releaseBoostRatio);
            releaseBoostLimit = Mathf.Max(0f, releaseBoostLimit);
        }
    }
}
