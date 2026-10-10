using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.Playables;

namespace Project.Player
{
    public enum PlayerStateId
    {
        Normal = 0,
        Swimming = 1,
        Action = 2,
        Locked = 3,
        Climbing = 4,
        Death = 5
    }

    /// <summary>Basic physical motor. No projected platforms, ladders or rope dependencies.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerActionRunner))]
    [DisallowMultipleComponent]
    public sealed class PlayerController :
        MonoBehaviour,
        IAchievementSignalProvider,
        IPlayerControlLockTarget
    {
        [SerializeField, Min(0)] private float moveSpeed = 5;
        [SerializeField, Min(0)] private float swimSpeed = 3.5f;
        [SerializeField, Min(0)] private float swimAcceleration = 12f;
        [SerializeField, Min(0)] private float swimRiseSpeed = 1.5f;
        [SerializeField, Min(0)] private float swimSinkSpeed = .8f;
        [SerializeField, Min(0)]
        private float swimVerticalAcceleration = 4f;
        [SerializeField, Min(0)] private float climbSpeed = 3f;
        [SerializeField, Min(0)] private float climbSlideSpeed = 1.2f;
        [SerializeField, Min(0)] private float climbDownSpeed = 4.5f;
        [SerializeField, Min(0)] private float climbHorizontalSpeed = 2f;
        [SerializeField, Min(0)] private float climbAcceleration = 12f;
        [SerializeField, Min(0)] private float climbKickHorizontalSpeed = 5f;
        [SerializeField, Min(0)] private float climbKickVerticalSpeed = 10f;
        [SerializeField, Min(0)] private float climbKickDetachSeconds = .3f;
        [SerializeField, Min(0f)] private float climbKickFallProtectionSeconds = 1.2f;
        [SerializeField, Min(0f)] private float climbKickMaxFallSpeed = 3.5f;
        [SerializeField, Min(1f)] private float climbTopKickHorizontalMultiplier = 1.5f;
        [SerializeField, Min(1f)] private float climbTopKickVerticalMultiplier = 1.12f;
        [SerializeField, Min(0f)] private float environmentLaunchExtraSpeed = 2.5f;
        [SerializeField, Min(0f)] private float environmentLaunchProtectionSeconds = .2f;
        [SerializeField, Min(0)] private float moveAcceleration = 20;
        [SerializeField, Min(0)] private float moveDeceleration = 30;
        [SerializeField]
        private AnimationCurve accelerationResponseCurve =
            AnimationCurve.Linear(0f, 1f, 1f, 1f);
        [SerializeField, Min(0)] private float jumpSpeed = 9.899f;
        [SerializeField, Min(0)] private float jumpGravityMultiplier = 1.7f;
        [SerializeField, Min(0)]
        private float jumpReleaseGravityMultiplier = 2.8f;
        [SerializeField, Min(0)] private float fallGravityMultiplier = 2.5f;
        [SerializeField, Min(0)] private float maxFallSpeed = 25f;
        [SerializeField, Min(0)] private float sprintMultiplier = 1.5f;
        [SerializeField, Min(0)] private float coyoteTime = .12f;
        [SerializeField, Min(0)] private float jumpBufferTime = .12f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0f)]
        private float lethalFallHeightBlocks = 8f;
        [SerializeField, Min(0f)]
        private float respawnInvulnerabilitySeconds = 2f;
        [SerializeField, Min(0f)]
        private float pickupSenseRadiusBlocks = 2f;
        [SerializeField] private ActSO deathAction;
        [SerializeField] private Transform respawnPoint;
        [SerializeField, Min(1)] private int respawnRandomRadiusBlocks = 4;
        [SerializeField, Range(1, 128)] private int respawnRandomAttempts = 32;
        [SerializeField, Min(.05f)] private float respawnSafetyRadius = .45f;
        private Rigidbody motor;
        private CapsuleCollider capsule;
        private PlayerActionRunner runner;
        private PlayerInteractionSensor interactionSensor;
        private PlayerColorWheel colorWheel;
        private IPlayerCarryTarget carryTarget;
        private IPlayerPointerDrag pointerDrag;
        private IInteractionHoldTarget heldInteractionTarget;
        private Vector2 movement;
        private bool sprint;
        private float jumpUntil = -1, groundedUntil = -1;
        private float airborneSince = -1f;
        private float lastAirTime;
        private float lastLandingTime = float.NegativeInfinity;
        private float lastLandingFallDistance;
        private float lastLandingDownwardSpeed;
        private float highestAirPosition;
        private float currentFallDistance;
        private float lastBounceTime = float.NegativeInfinity;
        private float bounceDecayMultiplier = 1f;
        private bool landedThisStep;
        private bool jumpHeld;
        private bool dead;
        private bool enteredWaterDuringFall;
        private bool enteredClimbDuringFall;
        private float invulnerableUntil;
        private int lockDepth;
        private PlayerHealth health;
        private Vector3 initialRespawnPosition;
        private Vector3 deathPosition;
        private float gridCellWorldSize = 1f;
        private readonly Collider[] respawnOverlapBuffer =
            new Collider[32];
        private float requestedBounceSpeed = -1f;
        private float requestedJumpBoost = -1f;
        private Vector3 pendingPlatformDelta;
        private readonly HashSet<int> waterSources = new HashSet<int>();
        private readonly Dictionary<int, float> waterSpeedMultipliers =
            new Dictionary<int, float>();
        private readonly Dictionary<int, Vector2> waterVelocities =
            new Dictionary<int, Vector2>();
        private readonly Dictionary<int, float> waterBuoyancies =
            new Dictionary<int, float>();
        private readonly Dictionary<int, float> climbSpeedMultipliers =
            new Dictionary<int, float>();
        private readonly Dictionary<int, float> climbSlideSpeeds =
            new Dictionary<int, float>();
        private readonly Dictionary<int, float> climbFastSlideSpeeds =
            new Dictionary<int, float>();
        private readonly HashSet<int> climbSources = new HashSet<int>();
        private readonly Dictionary<int, float> climbInwardDirections =
            new Dictionary<int, float>();
        private float climbReattachUntil;
        private float climbKickHorizontalControlUntil;
        private float climbKickFallProtectionUntil;
        private float environmentLaunchUntil;
        private readonly Dictionary<int, float> climbTopHeights = new Dictionary<int, float>();
        private readonly HashSet<int> bounceSurfaces =
            new HashSet<int>();
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private static PhysicsMaterial zeroFrictionMaterial;
        public Rigidbody Motor => motor;
        public bool IsGrounded { get; private set; }
        public bool IsDead => dead;
        public bool IsControlLocked => dead || lockDepth > 0;
        public bool IsSwimming => waterSources.Count > 0;
        public bool IsClimbing =>
            climbSources.Count > 0 &&
            Time.time >= climbReattachUntil &&
            (!IsGrounded || movement.y > .01f ||
             movement.x * GetClimbInwardDirection() > .1f);
        public PlayerStateId CurrentStateId => dead ? PlayerStateId.Death :
            IsControlLocked ? PlayerStateId.Locked :
            runner != null && runner.IsPlaying ? PlayerStateId.Action :
            IsClimbing ? PlayerStateId.Climbing :
            IsSwimming ? PlayerStateId.Swimming : PlayerStateId.Normal;
        public Vector2 MoveInput => movement;
        public bool SprintRequested => sprint;
        public float HorizontalVelocity => motor != null
            ? motor.linearVelocity.x
            : 0f;
        public float VerticalVelocity => motor != null
            ? motor.linearVelocity.y
            : 0f;
        public bool JumpHeld => jumpHeld;
        public float AirTime => IsGrounded
            ? 0f
            : Mathf.Max(0f, Time.time - airborneSince);
        public float LastAirTime => lastAirTime;
        public float TimeSinceLastLanding =>
            float.IsNegativeInfinity(lastLandingTime)
                ? float.PositiveInfinity
                : Time.time - lastLandingTime;
        public float LastLandingFallDistance =>
            lastLandingFallDistance;
        public float LastLandingDownwardSpeed =>
            lastLandingDownwardSpeed;
        public float CurrentFallDistance => currentFallDistance;
        public float LethalFallHeightBlocks =>
            lethalFallHeightBlocks;
        public float LethalFallHeightWorld =>
            lethalFallHeightBlocks * gridCellWorldSize;
        public bool IsOnBounceSurface => bounceSurfaces.Count > 0;
        public bool LandedThisStep => landedThisStep;
        public bool IsInvulnerable =>
            !dead && Time.time < invulnerableUntil;
        public float InvulnerabilityRemaining =>
            Mathf.Max(0f, invulnerableUntil - Time.time);
        public string GravityMode
        {
            get
            {
                if (dead) return "Death";
                if (IsClimbing) return "Climbing";
                if (IsSwimming) return "Swimming";
                if (IsGrounded)
                {
                    return "Grounded";
                }

                return motor != null && motor.linearVelocity.y > 0f
                    ? jumpHeld ? "Jump" : "JumpRelease"
                    : "Fall";
            }
        }
        public float JumpBufferRemaining => Mathf.Max(
            0f,
            jumpUntil - Time.time);
        public float CoyoteRemaining => Mathf.Max(
            0f,
            groundedUntil - Time.time);
        public ActSO CurrentAction => runner != null
            ? runner.CurrentAction
            : null;
        public bool IsActionPlaying =>
            runner != null && runner.IsPlaying;
        public bool IsColorWheelOpen =>
            colorWheel != null && colorWheel.IsWheelOpen;
        public bool IsPointerDragActive => pointerDrag != null;
        public float InteractionScanRadius =>
            interactionSensor != null
                ? interactionSensor.ScanRadius
                : 0f;
        public float PickupSenseRadiusBlocks =>
            pickupSenseRadiusBlocks;
        public float PickupSenseRadiusWorld =>
            pickupSenseRadiusBlocks * gridCellWorldSize;
        public int ControlLockDepth => lockDepth;
        public event Action<ActSO> ActionStarted;
        public event Action<ActSO> ActionCompleted;
        public event Action<bool> ControlLockChanged;
        public event Action TimelineSignalReceived;
        private static readonly string[] Signals = { AchievementSignalIds.PlayerJumped,
            AchievementSignalIds.PlayerLanded, AchievementSignalIds.PlayerActionStarted,
            AchievementSignalIds.PlayerActionCompleted };
        public IReadOnlyList<string> GetAchievementSignalIds() => Signals;

        private void Awake()
        {
            motor = GetComponent<Rigidbody>(); capsule = GetComponent<CapsuleCollider>();
            runner = GetComponent<PlayerActionRunner>();
            motor.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            motor.interpolation = RigidbodyInterpolation.Interpolate;
            motor.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            motor.useGravity = false;
            capsule.sharedMaterial = GetZeroFrictionMaterial();
            highestAirPosition = transform.position.y;
            initialRespawnPosition = transform.position;
            gridCellWorldSize = GridCellSizeUtility.Resolve(this);
            health = GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = gameObject.AddComponent<PlayerHealth>();
            }

            interactionSensor = GetComponent<PlayerInteractionSensor>();
            if (interactionSensor == null)
            {
                interactionSensor =
                    gameObject.AddComponent<PlayerInteractionSensor>();
            }

            colorWheel = GetComponent<PlayerColorWheel>();
            if (colorWheel == null)
            {
                colorWheel = gameObject.AddComponent<PlayerColorWheel>();
            }
        }
        private static PhysicsMaterial GetZeroFrictionMaterial()
        {
            if (zeroFrictionMaterial == null)
            {
                zeroFrictionMaterial = new PhysicsMaterial("PlayerZeroFriction")
                {
                    dynamicFriction = 0f,
                    staticFriction = 0f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum
                };
            }

            return zeroFrictionMaterial;
        }

        public bool IsWithinPickupSenseRange(Vector3 worldPosition)
        {
            Vector3 offset = worldPosition - transform.position;
            offset.z = 0f;
            float radius = Mathf.Max(0f, PickupSenseRadiusWorld);
            return offset.sqrMagnitude <= radius * radius;
        }

        private void OnDrawGizmosSelected()
        {
            float cellSize = Application.isPlaying
                ? gridCellWorldSize
                : GridCellSizeUtility.Resolve(this);
            float radius =
                Mathf.Max(0f, pickupSenseRadiusBlocks * cellSize);
            Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }

        private void OnEnable()
        {
            runner = GetComponent<PlayerActionRunner>();
            runner.Completed -= Completed;
            runner.Completed += Completed;
            health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Died -= OnDied;
                health.Died += OnDied;
                health.Revived -= OnRevived;
                health.Revived += OnRevived;
            }
        }
        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= OnDied;
                health.Revived -= OnRevived;
            }

            if (runner != null) { runner.Completed -= Completed; runner.Stop(); }
            CancelCarry();
            CancelPointerDrag();
            ReleaseHeldInteraction();
            lockDepth = 0; movement = Vector2.zero; jumpUntil = -1;
            jumpHeld = false;
            requestedJumpBoost = -1f;
            pendingPlatformDelta = Vector3.zero;
            waterSources.Clear();
            waterSpeedMultipliers.Clear();
            waterVelocities.Clear();
            waterBuoyancies.Clear();
            bounceSurfaces.Clear();
            currentFallDistance = 0f;
            landedThisStep = false;
            dead = false;
            enteredWaterDuringFall = false;
            enteredClimbDuringFall = false;
            invulnerableUntil = 0f;
            climbSpeedMultipliers.Clear();
            climbSlideSpeeds.Clear();
            climbFastSlideSpeeds.Clear();
            climbSources.Clear();
            climbInwardDirections.Clear();
            climbReattachUntil = 0f;
            climbKickHorizontalControlUntil = 0f;
            climbKickFallProtectionUntil = environmentLaunchUntil = 0f;
            climbTopHeights.Clear();
        }
        public void EnterWater(Component source)
        {
            if (dead)
            {
                return;
            }

            if (!IsGrounded)
            {
                enteredWaterDuringFall = true;
            }

            if (source != null) waterSources.Add(source.GetInstanceID());
        }

        public void SetSwimSpeedMultiplier(Component source, float multiplier)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (multiplier <= 0f)
            {
                waterSpeedMultipliers.Remove(id);
            }
            else
            {
                waterSpeedMultipliers[id] = multiplier;
            }
        }

        public void SetWaterVelocity(Component source, Vector2 velocity)
        {
            if (dead || source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (velocity.sqrMagnitude <= .0001f)
            {
                waterVelocities.Remove(id);
            }
            else
            {
                waterVelocities[id] = velocity;
            }
        }

        public void SetBuoyancy(Component source, float targetRiseSpeed)
        {
            if (dead || source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (targetRiseSpeed <= 0f)
            {
                waterBuoyancies.Remove(id);
            }
            else
            {
                waterBuoyancies[id] = targetRiseSpeed;
            }
        }

        public void SetClimbSpeedMultiplier(
            Component source,
            float multiplier)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (multiplier <= 0f)
            {
                climbSpeedMultipliers.Remove(id);
            }
            else
            {
                climbSpeedMultipliers[id] = multiplier;
            }
        }

        public void SetClimbSlideSpeed(
            Component source,
            float speed)
        {
            SetClimbSpeedValue(climbSlideSpeeds, source, speed);
        }

        public void SetClimbFastSlideSpeed(
            Component source,
            float speed)
        {
            SetClimbSpeedValue(
                climbFastSlideSpeeds,
                source,
                speed);
        }

        public void ClearClimbSlideSpeed(Component source)
        {
            if (source != null)
            {
                climbSlideSpeeds.Remove(source.GetInstanceID());
            }
        }

        public void ClearClimbFastSlideSpeed(Component source)
        {
            if (source != null)
            {
                climbFastSlideSpeeds.Remove(source.GetInstanceID());
            }
        }

        public void ApplyVerticalBounce(float verticalSpeed)
        {
            if (dead)
            {
                return;
            }

            requestedBounceSpeed = Mathf.Max(
                requestedBounceSpeed,
                verticalSpeed);
        }

        public void ApplyVerticalBounceImmediate(float verticalSpeed)
        {
            if (dead)
            {
                return;
            }

            requestedBounceSpeed = -1f;
            if (motor == null)
            {
                return;
            }

            Vector3 velocity = motor.linearVelocity;
            ApplyEnvironmentLaunch(ref velocity, verticalSpeed);
            motor.linearVelocity = velocity;
            IsGrounded = false;
            airborneSince = Time.time;
        }

        public void EnterBounceSurface(Component source)
        {
            if (source != null)
            {
                bounceSurfaces.Add(source.GetInstanceID());
            }
        }

        public void ExitBounceSurface(Component source)
        {
            if (source != null)
            {
                bounceSurfaces.Remove(source.GetInstanceID());
            }
        }

        public bool CanBounce(float minimumInterval)
        {
            return Time.time - lastBounceTime >=
                   Mathf.Max(0f, minimumInterval);
        }

        public float GetBounceDecay(
            float decayRate,
            float resetSeconds)
        {
            if (float.IsNegativeInfinity(lastBounceTime) ||
                Time.time - lastBounceTime >
                Mathf.Max(.05f, resetSeconds))
            {
                return 1f;
            }

            return bounceDecayMultiplier;
        }

        public void RegisterBounce(
            float decayRate,
            float resetSeconds)
        {
            float current = GetBounceDecay(
                decayRate,
                resetSeconds);
            bounceDecayMultiplier = Mathf.Clamp01(
                current * Mathf.Clamp01(decayRate));
            lastBounceTime = Time.time;
        }

        public void ClampUpwardVelocity(float maxUpwardSpeed)
        {
            if (motor == null)
            {
                return;
            }

            Vector3 velocity = motor.linearVelocity;
            if (velocity.y <= maxUpwardSpeed)
            {
                return;
            }

            velocity.y = maxUpwardSpeed;
            motor.linearVelocity = velocity;
        }

        public float CalculateVerticalRiseHeight(
            float initialSpeed,
            bool jumpHeld)
        {
            float gravity = GetVerticalGravity(jumpHeld);
            return initialSpeed * initialSpeed / (2f * gravity);
        }

        public float CalculateVerticalRiseSpeed(
            float height,
            bool jumpHeld)
        {
            float gravity = GetVerticalGravity(jumpHeld);
            return Mathf.Sqrt(
                Mathf.Max(0f, height) * 2f * gravity);
        }

        private float GetVerticalGravity(bool jumpHeld)
        {
            float gravityMultiplier = jumpHeld
                ? jumpGravityMultiplier
                : jumpReleaseGravityMultiplier;
            return Mathf.Max(
                .01f,
                Mathf.Abs(Physics.gravity.y) * gravityMultiplier);
        }

        public void QueuePlatformDelta(Vector3 delta)
        {
            if (dead)
            {
                return;
            }

            pendingPlatformDelta += delta;
        }

        public void ApplyJumpBoost(float verticalSpeed)
        {
            if (dead)
            {
                return;
            }

            requestedJumpBoost = Mathf.Max(
                requestedJumpBoost,
                verticalSpeed);
            requestedBounceSpeed = Mathf.Max(
                requestedBounceSpeed,
                verticalSpeed);
        }
        public void ExitWater(Component source)
        {
            if (source != null) waterSources.Remove(source.GetInstanceID());
        }
        public void EnterClimb(Component source)
        {
            if (dead)
            {
                return;
            }

            if (!IsGrounded)
            {
                enteredClimbDuringFall = true;
            }

            if (source != null)
            {
                int id = source.GetInstanceID();
                climbSources.Add(id);
                float delta = source.transform.position.x - transform.position.x;
                climbInwardDirections[id] = Mathf.Abs(delta) > .01f
                    ? Mathf.Sign(delta) : 0f;
            }
        }
        public void ExitClimb(Component source)
        {
            if (source == null) return;
            int id = source.GetInstanceID();
            climbSources.Remove(id);
            climbInwardDirections.Remove(id);
            climbTopHeights.Remove(id);
        }

        public void SetClimbTopHeight(Component source, float worldHeight)
        {
            if (source != null && climbSources.Contains(source.GetInstanceID()))
                climbTopHeights[source.GetInstanceID()] = worldHeight;
        }

        private bool IsNearClimbTop()
        {
            float top = float.NegativeInfinity;
            foreach (float height in climbTopHeights.Values) top = Mathf.Max(top, height);
            return !float.IsNegativeInfinity(top) && transform.position.y >= top - gridCellWorldSize;
        }

        private void ApplyEnvironmentLaunch(ref Vector3 velocity, float verticalSpeed)
        {
            ApplyEnvironmentLaunchVelocity(ref velocity, verticalSpeed);
        }

        private Vector2 GetClimbKickVelocity(float inward, bool nearTop) => new Vector2(
            (nearTop ? inward * climbTopKickHorizontalMultiplier : -inward) * climbKickHorizontalSpeed,
            climbKickVerticalSpeed * (nearTop ? climbTopKickVerticalMultiplier : 1f));

        private void ApplyEnvironmentLaunchVelocity(ref Vector3 velocity, float verticalSpeed)
        {
            if (verticalSpeed <= 0f) return;
            // A speed floor is idempotent under sustained steam; do not add velocity each tick.
            velocity.y = Mathf.Max(velocity.y, verticalSpeed + environmentLaunchExtraSpeed);
            environmentLaunchUntil = Time.time + environmentLaunchProtectionSeconds;
            climbReattachUntil = Mathf.Max(climbReattachUntil, environmentLaunchUntil);
        }
        public void SetMoveInput(Vector2 value)
        {
            if (dead)
            {
                return;
            }

            movement = value;
        }
        public void SetSprintInput(bool value)
        {
            if (dead)
            {
                return;
            }

            sprint = value;
        }
        public void RequestJump()
        {
            if (dead)
            {
                return;
            }

            jumpUntil = Time.time + jumpBufferTime;
            jumpHeld = true;
        }
        public void SetJumpHeld(bool value)
        {
            if (dead)
            {
                jumpHeld = false;
                return;
            }

            jumpHeld = value;
        }
        public void ClearBufferedInput()
        {
            movement = Vector2.zero;
            jumpUntil = -1;
            jumpHeld = false;
            requestedJumpBoost = -1f;
        }

        public void RefreshInteractionTarget()
        {
            interactionSensor?.RefreshTarget();
        }

        public void UpdateInteraction(
            bool interactHeld,
            bool interactTriggered,
            bool interactionBlocked)
        {
            if (dead)
            {
                ReleaseHeldInteraction();
                return;
            }

            if (heldInteractionTarget != null)
            {
                if (interactHeld)
                {
                    heldInteractionTarget.HoldInteract(gameObject);
                }
                else
                {
                    ReleaseHeldInteraction();
                }
            }

            if (!interactTriggered ||
                interactionBlocked ||
                interactionSensor == null)
            {
                return;
            }

            if (CurrentStateId != PlayerStateId.Normal &&
                CurrentStateId != PlayerStateId.Swimming)
            {
                return;
            }

            if (interactionSensor.TryInteract() &&
                interactionSensor.CurrentTarget is
                    IInteractionHoldTarget holdTarget)
            {
                heldInteractionTarget = holdTarget;
                holdTarget.HoldInteract(gameObject);
            }
        }

        private void ReleaseHeldInteraction()
        {
            if (heldInteractionTarget == null)
            {
                return;
            }

            heldInteractionTarget.EndInteract(gameObject);
            heldInteractionTarget = null;
        }

        public void TickColorWheel(Vector2 pointerPosition)
        {
            if (dead)
            {
                return;
            }

            colorWheel?.Tick(pointerPosition);
        }

        public void UpdateColorWheelInput(
            bool holdMode,
            bool pressedThisFrame,
            bool releasedThisFrame,
            bool triggeredThisFrame,
            Vector2 pointerPosition)
        {
            if (dead)
            {
                return;
            }

            colorWheel?.UpdateInput(
                holdMode,
                pressedThisFrame,
                releasedThisFrame,
                triggeredThisFrame,
                pointerPosition);
        }

        public void ToggleColorWheel()
        {
            if (dead)
            {
                return;
            }

            colorWheel?.ToggleWheel();
        }

        public void CloseColorWheel()
        {
            colorWheel?.CloseWheel();
        }

        public void CommitColorWheelSelection()
        {
            if (dead)
            {
                return;
            }

            colorWheel?.CommitWheelSelection();
        }

        public void TryUseSelectedColorAbility()
        {
            if (dead)
            {
                return;
            }

            colorWheel?.TryUseSelectedAbility();
        }

        public void BeginPointerDrag(IPlayerPointerDrag drag)
        {
            if (dead || drag == null || pointerDrag != null)
            {
                return;
            }

            pointerDrag = drag;
            ClearBufferedInput();
            colorWheel?.CloseWheel();
            SetControlLocked(true);
        }

        public void EndPointerDrag(IPlayerPointerDrag drag)
        {
            if (drag == null ||
                !ReferenceEquals(pointerDrag, drag))
            {
                return;
            }

            pointerDrag = null;
            SetControlLocked(false);
        }

        public void UpdatePointerDrag(
            Vector2 screenPosition,
            Camera camera)
        {
            if (dead || pointerDrag == null || camera == null)
            {
                return;
            }

            pointerDrag.UpdatePointer(
                camera.ScreenPointToRay(screenPosition));
        }

        public void UpdateCarry(
            bool carryHeld,
            bool carryTriggered,
            Vector2 screenPosition,
            Camera camera)
        {
            if (dead)
            {
                return;
            }

            if (carryTarget != null)
            {
                if (!carryHeld)
                {
                    IPlayerCarryTarget releasedTarget = carryTarget;
                    carryTarget = null;
                    releasedTarget.EndCarry(this);
                    ForceEndPointerDrag();
                    return;
                }

                if (camera != null)
                {
                    carryTarget.UpdateCarry(
                        this,
                        camera.ScreenPointToRay(
                            screenPosition));
                }

                return;
            }

            if (!carryTriggered ||
                camera == null ||
                interactionSensor == null)
            {
                return;
            }

            if (interactionSensor.TryGetScreenComponent(
                    camera,
                    screenPosition,
                    out IPlayerCarryTarget target) &&
                target is Component component &&
                interactionSensor.IsWithinRange(component) &&
                target.CanCarry(this))
            {
                carryTarget = target;
                target.BeginCarry(
                    this,
                    camera.ScreenPointToRay(screenPosition));
            }
        }

        private void CancelPointerDrag()
        {
            if (pointerDrag == null)
            {
                return;
            }

            IPlayerPointerDrag drag = pointerDrag;
            pointerDrag = null;
            drag.CancelPointerDrag();
            SetControlLocked(false);
        }

        private void ForceEndPointerDrag()
        {
            if (pointerDrag == null)
            {
                return;
            }

            IPlayerPointerDrag drag = pointerDrag;
            pointerDrag = null;
            drag.CancelPointerDrag();
            SetControlLocked(false);
        }

        private void CancelCarry()
        {
            if (carryTarget == null)
            {
                return;
            }

            IPlayerCarryTarget target = carryTarget;
            carryTarget = null;
            target.CancelCarry();
        }
        private float GetSwimSpeedMultiplier()
        {
            float result = 1f;
            foreach (float multiplier in waterSpeedMultipliers.Values)
            {
                result = Mathf.Min(result, multiplier);
            }

            return result;
        }

        private Vector2 GetWaterVelocity()
        {
            Vector2 result = Vector2.zero;
            foreach (Vector2 velocity in waterVelocities.Values)
            {
                result += velocity;
            }

            return result;
        }

        private float GetBuoyancy()
        {
            float result = 0f;
            foreach (float value in waterBuoyancies.Values)
            {
                result = Mathf.Max(result, value);
            }

            return result;
        }

        private float GetClimbSpeedMultiplier()
        {
            float result = 1f;
            foreach (float multiplier in
                     climbSpeedMultipliers.Values)
            {
                result = Mathf.Min(result, multiplier);
            }

            return result;
        }

        private float GetClimbInwardDirection()
        {
            foreach (float direction in climbInwardDirections.Values)
                if (Mathf.Abs(direction) > .01f)
                    return direction;
            return 0f;
        }

        private float GetClimbSlideSpeed()
        {
            return GetClimbSpeedValue(
                climbSlideSpeeds,
                climbSlideSpeed);
        }

        private float GetClimbFastSlideSpeed()
        {
            return GetClimbSpeedValue(
                climbFastSlideSpeeds,
                climbDownSpeed);
        }

        private static void SetClimbSpeedValue(
            Dictionary<int, float> speeds,
            Component source,
            float speed)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            speeds[id] = Mathf.Max(0f, speed);
        }

        private static float GetClimbSpeedValue(
            Dictionary<int, float> speeds,
            float fallback)
        {
            float result = fallback;
            foreach (float speed in speeds.Values)
            {
                result = Mathf.Min(result, speed);
            }

            return result;
        }

        private void TryApplyLandingBounce(
            float fallDistance,
            float downwardSpeed,
            int groundHitCount)
        {
            if (!IsGrounded ||
                fallDistance <= .2f)
            {
                return;
            }

            for (int index = 0;
                 index < groundHitCount;
                 index++)
            {
                Collider collider = groundHits[index].collider;
                IPlayerBounceSurface surface = collider != null
                    ? collider.GetComponentInParent<
                        IPlayerBounceSurface>()
                    : null;
                if (surface == null ||
                    !surface.IsBounceSurfaceAvailable ||
                    !surface.TryGetBounceSpeed(
                        this,
                        fallDistance,
                        downwardSpeed,
                        out float verticalSpeed) ||
                    verticalSpeed <= 0f)
                {
                    continue;
                }

                requestedBounceSpeed = Mathf.Max(
                    requestedBounceSpeed,
                    verticalSpeed);
                return;
            }
        }

        private void FixedUpdate()
        {
            if (dead)
            {
                motor.linearVelocity = Vector3.zero;
                motor.angularVelocity = Vector3.zero;
                return;
            }

            if (pendingPlatformDelta.sqrMagnitude > .000001f)
            {
                motor.position += pendingPlatformDelta;
                pendingPlatformDelta = Vector3.zero;
            }

            float velocityAtStepStart = motor != null
                ? motor.linearVelocity.y
                : 0f;
            landedThisStep = false;
            bool wasGrounded = IsGrounded;
            IsGrounded = false;
            Bounds bounds = capsule.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * .85f;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + .05f, bounds.center.z);
            int count = Physics.SphereCastNonAlloc(origin, radius, Vector3.down, groundHits,
                .13f, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (groundHits[i].collider.attachedRigidbody != motor && groundHits[i].normal.y > .5f && motor.linearVelocity.y <= .1f)
                    IsGrounded = true;
            if (IsGrounded) groundedUntil = Time.time + coyoteTime;
            if (IsGrounded)
            {
                if (!wasGrounded)
                {
                    lastAirTime = airborneSince >= 0f
                        ? Time.time - airborneSince
                        : 0f;
                    lastLandingTime = Time.time;
                    lastLandingFallDistance = Mathf.Max(
                        currentFallDistance,
                        highestAirPosition - transform.position.y);
                    lastLandingDownwardSpeed = Mathf.Max(
                        0f,
                        -velocityAtStepStart);
                    if (ShouldDieFromFall(
                            lastLandingFallDistance,
                            count))
                    {
                        EnterDeath();
                        return;
                    }

                    TryApplyLandingBounce(
                        lastLandingFallDistance,
                        lastLandingDownwardSpeed,
                        count);
                    highestAirPosition = transform.position.y;
                    currentFallDistance = 0f;
                    enteredWaterDuringFall = false;
                    enteredClimbDuringFall = false;
                    landedThisStep = true;
                    Emit(AchievementSignalIds.PlayerLanded);
                }
                else
                {
                    highestAirPosition = transform.position.y;
                }

                airborneSince = -1f;
            }
            else
            {
                if (wasGrounded)
                {
                    airborneSince = Time.time;
                    enteredWaterDuringFall = false;
                    enteredClimbDuringFall = false;
                }

                if (airborneSince >= 0f)
                {
                    lastAirTime = Mathf.Max(
                        lastAirTime,
                        Time.time - airborneSince);
                }

                highestAirPosition = Mathf.Max(
                    highestAirPosition,
                    transform.position.y);
                currentFallDistance = Mathf.Max(
                    currentFallDistance,
                    highestAirPosition - transform.position.y);
            }
            Vector3 velocity = motor.linearVelocity;
            if (requestedBounceSpeed >= 0f)
            {
                ApplyEnvironmentLaunch(ref velocity, requestedBounceSpeed);
                requestedBounceSpeed = -1f;
                IsGrounded = false;
                airborneSince = Time.time;
            }
            if (movement.y < -.1f &&
                IsOnBounceSurface &&
                velocity.y > 0f)
            {
                velocity.y = 0f;
                requestedBounceSpeed = -1f;
                requestedJumpBoost = -1f;
                environmentLaunchUntil = 0f;
            }
            bool blocked = IsControlLocked || (runner.IsPlaying && runner.CurrentAction != null && runner.CurrentAction.LockMovement);
            float climbInward = GetClimbInwardDirection();
            bool climbKickRequested = !blocked && IsClimbing &&
                                      jumpUntil >= Time.time &&
                                      movement.x * climbInward > .1f;
            bool jumpRequested = !blocked &&
                                 !climbKickRequested &&
                                 jumpUntil >= Time.time &&
                                 groundedUntil >= Time.time;
            if (climbKickRequested)
            {
                climbReattachUntil = Time.time + climbKickDetachSeconds;
                climbKickHorizontalControlUntil = Time.time +
                    climbKickDetachSeconds * .5f;
                bool nearTop = IsNearClimbTop();
                Vector2 kickVelocity = GetClimbKickVelocity(climbInward, nearTop);
                velocity.x = kickVelocity.x;
                velocity.y = kickVelocity.y;
                climbKickFallProtectionUntil = Time.time + climbKickFallProtectionSeconds;
                velocity.z = 0f;
                jumpUntil = groundedUntil = -1f;
                IsGrounded = false;
                airborneSince = Time.time;
                motor.linearVelocity = velocity;
                Emit(AchievementSignalIds.PlayerJumped);
                return;
            }
            if (jumpRequested)
            {
                if (climbSources.Count > 0)
                    climbReattachUntil = Time.time + climbKickDetachSeconds;
            }

            if (IsClimbing)
            {
                float climbMultiplier =
                    GetClimbSpeedMultiplier();
                float horizontalTarget = blocked
                    ? 0f
                    : movement.x * climbInward > .1f
                        ? 0f
                    : Mathf.Clamp(movement.x, -1f, 1f) *
                      climbHorizontalSpeed *
                      climbMultiplier;
                float verticalTarget;
                if (blocked)
                {
                    verticalTarget = 0f;
                }
                else if (movement.y > .01f)
                {
                    verticalTarget =
                        climbSpeed * climbMultiplier;
                }
                else if (movement.y < -.01f)
                {
                    verticalTarget =
                        -GetClimbFastSlideSpeed() *
                        climbMultiplier;
                }
                else if (movement.x * climbInward > .1f)
                {
                    verticalTarget = climbSpeed * climbMultiplier;
                }
                else if (jumpHeld)
                {
                    verticalTarget = 0f;
                }
                else
                {
                    verticalTarget =
                        -GetClimbSlideSpeed() *
                        climbMultiplier;
                }

                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    horizontalTarget,
                    climbAcceleration * Time.fixedDeltaTime);
                if (verticalTarget <= 0f && velocity.y > 0f)
                {
                    velocity.y = 0f;
                }

                velocity.y = Mathf.MoveTowards(
                    velocity.y,
                    verticalTarget,
                    climbAcceleration * Time.fixedDeltaTime);
                velocity.z = 0f;
                jumpUntil = -1f;
                motor.linearVelocity = velocity;
                return;
            }
            if (IsSwimming && Time.time >= environmentLaunchUntil)
            {
                Vector2 externalVelocity = GetWaterVelocity();
                float buoyancy = GetBuoyancy();
                float horizontalTarget = blocked
                    ? 0f
                    : Mathf.Clamp(movement.x, -1f, 1f) *
                      swimSpeed * GetSwimSpeedMultiplier() +
                      externalVelocity.x;
                float verticalTarget = blocked
                    ? 0f
                    : jumpHeld
                        ? swimRiseSpeed
                        : -swimSinkSpeed;
                if (!blocked)
                {
                    verticalTarget += externalVelocity.y +
                                      buoyancy;
                }

                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    horizontalTarget,
                    swimAcceleration * Time.fixedDeltaTime);
                velocity.y = Mathf.MoveTowards(
                    velocity.y,
                    verticalTarget,
                    swimVerticalAcceleration *
                    Time.fixedDeltaTime);
                velocity.z = 0f;
                jumpUntil = -1f;
                motor.linearVelocity = velocity;
                return;
            }
            if (blocked)
            {
                velocity.x = 0;
            }
            else if (Time.time >= climbKickHorizontalControlUntil && Time.time >= environmentLaunchUntil)
            {
                float targetSpeed = movement.x * moveSpeed *
                                    (sprint ? sprintMultiplier : 1);
                bool accelerating =
                    Mathf.Abs(velocity.x) < Mathf.Abs(targetSpeed) &&
                    (Mathf.Approximately(velocity.x, 0f) ||
                     Mathf.Sign(velocity.x) == Mathf.Sign(targetSpeed));
                float curveMultiplier = 1f;
                if (accelerating &&
                    accelerationResponseCurve != null &&
                    Mathf.Abs(targetSpeed) > 0.0001f)
                {
                    float normalizedSpeed = Mathf.InverseLerp(
                        0f,
                        Mathf.Abs(targetSpeed),
                        Mathf.Abs(velocity.x));
                    curveMultiplier = Mathf.Max(
                        0f,
                        accelerationResponseCurve.Evaluate(
                            normalizedSpeed));
                }

                float rate = accelerating
                    ? moveAcceleration * curveMultiplier
                    : moveDeceleration;
                velocity.x = rate <= 0f
                    ? targetSpeed
                    : Mathf.MoveTowards(
                        velocity.x,
                        targetSpeed,
                        rate * Time.fixedDeltaTime);
            }

            velocity.z = 0;
            if (jumpRequested)
            {
                float jumpVelocity =
                    jumpSpeed *
                    NextJumpBounceService.ConsumeMultiplier();
                if (requestedJumpBoost >= 0f)
                {
                    jumpVelocity = Mathf.Max(
                        jumpVelocity,
                        requestedJumpBoost);
                    requestedJumpBoost = -1f;
                }

                velocity.y = jumpVelocity;
                jumpUntil = groundedUntil = -1; IsGrounded = false;
                airborneSince = Time.time;
                Emit(AchievementSignalIds.PlayerJumped);
            }

            if (IsGrounded && velocity.y < 0f)
            {
                velocity.y = 0f;
            }
            else
            {
                float gravityMultiplier = velocity.y > 0f
                    ? jumpHeld
                        ? jumpGravityMultiplier
                        : jumpReleaseGravityMultiplier
                    : fallGravityMultiplier;
                if (Time.time < environmentLaunchUntil && velocity.y > 0f)
                    gravityMultiplier = jumpGravityMultiplier;
                bool protectedClimbFall = Time.time < climbKickFallProtectionUntil && velocity.y <= 0f;
                if (protectedClimbFall) gravityMultiplier = Mathf.Min(gravityMultiplier, .65f);
                velocity.y += Physics.gravity.y *
                              gravityMultiplier *
                              Time.fixedDeltaTime;
                if (maxFallSpeed > 0f &&
                    velocity.y < -maxFallSpeed)
                {
                    velocity.y = -maxFallSpeed;
                }
                if (protectedClimbFall && climbKickMaxFallSpeed > 0f)
                    velocity.y = Mathf.Max(velocity.y, -climbKickMaxFallSpeed);
            }

            motor.linearVelocity = velocity;
        }

        private bool ShouldDieFromFall(
            float fallDistance,
            int groundHitCount)
        {
            if (dead ||
                lethalFallHeightBlocks <= 0f ||
                IsInvulnerable ||
                fallDistance + .001f <
                lethalFallHeightBlocks * gridCellWorldSize)
            {
                return false;
            }

            if (enteredWaterDuringFall ||
                enteredClimbDuringFall ||
                IsSwimming ||
                IsClimbing)
            {
                return false;
            }

            return !HasAvailableBounceSurface(groundHitCount);
        }

        private bool HasAvailableBounceSurface(
            int groundHitCount)
        {
            for (int index = 0;
                 index < groundHitCount;
                 index++)
            {
                Collider collider = groundHits[index].collider;
                if (collider == null)
                {
                    continue;
                }

                IPlayerBounceSurface surface =
                    collider.GetComponentInParent<
                        IPlayerBounceSurface>();
                if (surface != null &&
                    surface.IsBounceSurfaceAvailable)
                {
                    return true;
                }
            }

            return false;
        }
        public bool TryPlayAction(ActSO action)
        {
            if (IsControlLocked || action == null) return false;
            ActSO current = runner.CurrentAction;
            if (runner.IsPlaying && current != null && (!current.Interruptible || action.Priority < current.Priority)) return false;
            if (!runner.Play(action)) return false;
            ActionStarted?.Invoke(action); Emit(AchievementSignalIds.PlayerActionStarted); return true;
        }
        private void Completed(ActSO action)
        {
            ActionCompleted?.Invoke(action); Emit(AchievementSignalIds.PlayerActionCompleted);
            if (action.DefaultNextAction != null) TryPlayAction(action.DefaultNextAction);
        }

        public void EnterDeath()
        {
            if (dead)
            {
                return;
            }

            bool wasLocked = IsControlLocked;
            deathPosition = transform.position;
            dead = true;
            ClearDeathControlState();
            runner?.Stop();
            if (deathAction != null &&
                deathAction.Timeline != null &&
                runner != null &&
                runner.Play(deathAction, DirectorWrapMode.Hold))
            {
                ActionStarted?.Invoke(deathAction);
            }

            if (!wasLocked)
            {
                ControlLockChanged?.Invoke(true);
            }
        }

        public void RequestRespawn()
        {
            if (!dead)
            {
                return;
            }

            Vector3 targetPosition;
            if (PlayerRespawnPointRegistry.TryFindPreferred(
                    deathPosition,
                    out IPlayerRespawnPoint registeredPoint))
            {
                targetPosition = FindSafeRespawnPosition(
                    registeredPoint.RespawnPosition,
                    registeredPoint.RespawnRadiusBlocks,
                    deathPosition);
            }
            else if (respawnPoint != null)
            {
                targetPosition = FindSafeRespawnPosition(
                    respawnPoint.position,
                    respawnRandomRadiusBlocks,
                    deathPosition);
            }
            else
            {
                targetPosition = FindSafeRespawnPosition(
                    FindRandomRespawnPosition(
                        deathPosition,
                        respawnRandomRadiusBlocks),
                    respawnRandomRadiusBlocks,
                    initialRespawnPosition);
            }
            if (motor != null)
            {
                motor.position = targetPosition;
                motor.linearVelocity = Vector3.zero;
                motor.angularVelocity = Vector3.zero;
            }

            if (health != null)
            {
                health.ResetHealth();
            }
            else
            {
                ReviveFromDeath();
            }

            if (dead)
            {
                ReviveFromDeath();
            }

            invulnerableUntil =
                Time.time +
                Mathf.Max(0f, respawnInvulnerabilitySeconds);
        }

        private Vector3 FindRandomRespawnPosition(
            Vector3 origin,
            float radiusBlocks)
        {
            Vector2 offset = UnityEngine.Random.insideUnitCircle *
                             Mathf.Max(
                                 .1f,
                                 radiusBlocks *
                                 gridCellWorldSize);
            return origin + new Vector3(
                offset.x,
                offset.y,
                0f);
        }

        private Vector3 FindSafeRespawnPosition(
            Vector3 preferred,
            float radiusBlocks,
            Vector3 fallback)
        {
            if (TryGetGroundedRespawnPosition(
                    preferred,
                    out Vector3 groundedPreferred))
            {
                return groundedPreferred;
            }

            if (radiusBlocks > 0f)
            {
                for (int index = 0;
                     index < Mathf.Max(1, respawnRandomAttempts);
                     index++)
                {
                    Vector3 candidate = FindRandomRespawnPosition(
                        preferred,
                        radiusBlocks);
                    if (TryGetGroundedRespawnPosition(
                            candidate,
                            out Vector3 groundedCandidate))
                    {
                        return groundedCandidate;
                    }
                }
            }

            if (TryGetGroundedRespawnPosition(
                    fallback,
                    out Vector3 groundedFallback))
            {
                return groundedFallback;
            }

            return TryGetGroundedRespawnPosition(
                    initialRespawnPosition,
                    out Vector3 groundedInitial)
                ? groundedInitial
                : initialRespawnPosition;
        }

        private bool TryGetGroundedRespawnPosition(
            Vector3 candidate,
            out Vector3 grounded)
        {
            grounded = candidate;
            float maxDrop = Mathf.Max(
                gridCellWorldSize,
                gridCellWorldSize * 6f);
            if (!Physics.Raycast(
                    candidate + Vector3.up * .1f,
                    Vector3.down,
                    out RaycastHit hit,
                    maxDrop,
                    ~0,
                    QueryTriggerInteraction.Ignore) ||
                hit.collider == null ||
                hit.collider.transform.root == transform.root)
            {
                return false;
            }

            float groundOffset = capsule != null
                ? transform.position.y - capsule.bounds.min.y
                : 1f;
            grounded = new Vector3(
                candidate.x,
                hit.point.y + groundOffset,
                candidate.z);
            return IsRespawnPositionSafe(grounded);
        }

        private bool IsRespawnPositionSafe(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(
                position,
                Mathf.Max(.05f, respawnSafetyRadius),
                respawnOverlapBuffer,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider hit = respawnOverlapBuffer[index];
                if (hit == null ||
                    hit.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.GetComponentInParent<LavaHazardFeature>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        private void OnDied()
        {
            EnterDeath();
        }

        private void OnRevived()
        {
            ReviveFromDeath();
        }

        private void ReviveFromDeath()
        {
            if (!dead)
            {
                return;
            }

            dead = false;
            runner?.Stop();
            ClearDeathControlState();
            ControlLockChanged?.Invoke(false);
        }

        private void ClearDeathControlState()
        {
            ClearBufferedInput();
            sprint = false;
            groundedUntil = -1f;
            airborneSince = -1f;
            requestedBounceSpeed = -1f;
            requestedJumpBoost = -1f;
            pendingPlatformDelta = Vector3.zero;
            waterSources.Clear();
            waterSpeedMultipliers.Clear();
            waterVelocities.Clear();
            waterBuoyancies.Clear();
            climbSpeedMultipliers.Clear();
            climbSlideSpeeds.Clear();
            climbFastSlideSpeeds.Clear();
            climbSources.Clear();
            climbInwardDirections.Clear();
            climbReattachUntil = 0f;
            climbKickHorizontalControlUntil = 0f;
            climbKickFallProtectionUntil = environmentLaunchUntil = 0f;
            climbTopHeights.Clear();
            bounceSurfaces.Clear();
            enteredWaterDuringFall = false;
            enteredClimbDuringFall = false;
            currentFallDistance = 0f;
            highestAirPosition = transform.position.y;
            invulnerableUntil = 0f;
            ReleaseHeldInteraction();
            CancelCarry();
            ForceEndPointerDrag();
            colorWheel?.CloseWheel();
            if (motor != null)
            {
                motor.linearVelocity = Vector3.zero;
                motor.angularVelocity = Vector3.zero;
            }
        }

        public void SetControlLocked(bool locked)
        {
            bool previous = IsControlLocked;
            lockDepth = locked ? lockDepth + 1 : Mathf.Max(0, lockDepth - 1);
            if (previous != IsControlLocked) ControlLockChanged?.Invoke(IsControlLocked);
        }

        public IDisposable AcquireControlLock(object owner)
        {
            ReleaseHeldInteraction();
            CancelCarry();
            ForceEndPointerDrag();
            colorWheel?.CloseWheel();
            ClearBufferedInput();
            sprint = false;
            SetControlLocked(true);
            return new ControlLockHandle(this);
        }

        private void ReleaseControlLock()
        {
            SetControlLocked(false);
        }

        private sealed class ControlLockHandle : IDisposable
        {
            private PlayerController owner;

            public ControlLockHandle(PlayerController owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner == null)
                {
                    return;
                }

                PlayerController current = owner;
                owner = null;
                current.ReleaseControlLock();
            }
        }

        public void ReceiveTimelineSignal() => TimelineSignalReceived?.Invoke();
        private void Emit(string id) => GameplaySignalHub.Emit(id, gameObject);
    }
}
