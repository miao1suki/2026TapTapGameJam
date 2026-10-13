using System;
using System.Collections.Generic;
using Project.Audio;
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
        Death = 5,
        HorizontalClimbing = 6
    }

    /// <summary>Basic physical motor. No projected platforms, ladders or rope dependencies.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerActionRunner))]
    [DisallowMultipleComponent]
    public sealed class PlayerController :
        MonoBehaviour,
        IAchievementSignalProvider,
        IPlayerControlLockTarget,
        IPlayerCarryHost
    {
        private static readonly HashSet<PlayerController> activePlayers = new HashSet<PlayerController>();
        public static IEnumerable<PlayerController> ActivePlayers => activePlayers;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetPlayerRegistry() => activePlayers.Clear();
        [SerializeField, Min(.1f), InspectorName("普通跳跃上升（格）")]
        private float jumpRiseCells = 4f;
        private float carryPlantJumpCells;
        public bool CanCarryPlantJump => carryPlantJumpCells > 0f && IsCarrying && !IsCarryBusy;
        public void GrantCarryPlantJump(float cells) { if (IsCarrying) carryPlantJumpCells = Mathf.Max(.1f, cells); }
        public float CalculateGridRiseSpeed(float cells)
        {
            float g = GetVerticalGravity(true);
            return Mathf.Sqrt(2f * g * Mathf.Max(0f, cells) * gridCellWorldSize) + g * Time.fixedDeltaTime * .5f;
        }
        [SerializeField, Min(0)] private float moveSpeed = 5;
        [SerializeField, Min(.05f)] private float landFootstepInterval = .38f;
        [SerializeField, Min(.05f)] private float waterFootstepInterval = .46f;
        [SerializeField, Min(.05f)] private float climbSoundInterval = .5f;
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
        [SerializeField, Min(0)] private float jumpGravityMultiplier = 1.7f;
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
        [SerializeField, Min(0f)] private float carryPrepareSeconds = .1f;
        [SerializeField] private Vector2Int carryFootprintCells =
            Vector2Int.one;
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
        private readonly List<IPlayerCarryTarget> carryQueue =
            new List<IPlayerCarryTarget>();
        private readonly List<Vector3Int> carryFootprintBuffer =
            new List<Vector3Int>();
        private readonly Collider[] carryDropBuffer =
            new Collider[32];
        private readonly RaycastHit[] carryRayBuffer =
            new RaycastHit[32];
        private IPlayerCarryTarget selectedCarryTarget;
        private IPlayerCarryTarget highlightedCarryTarget;
        private float carryPrepareUntil = -1f;
        private bool carryPreparing;
        private bool carryDropAnimating;
        private bool carryResetting;
        private bool carryControlLockActive;
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
        private bool bounceHasExactHeight;
        private float requestedJumpBoost = -1f;
        private float nextMovementSoundTime;
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
        private struct HorizontalVineSurface
        {
            public Component source;
            public float rootY, speed, renewed;
        }
        private readonly Dictionary<int, HorizontalVineSurface> horizontalVines = new Dictionary<int, HorizontalVineSurface>();
        private readonly List<int> expiredHorizontalVines = new List<int>();
        public bool IsHorizontalClimbing => horizontalVines.Count > 0;
        public void SetHorizontalVineSurface(Component source, float rootY, float speed)
        {
            if (source == null || dead) return;
            horizontalVines[source.GetInstanceID()] = new HorizontalVineSurface { source = source, rootY = rootY, speed = speed, renewed = Time.fixedTime };
        }
        public void ClearHorizontalVineSurface(Component source)
        {
            if (source != null) horizontalVines.Remove(source.GetInstanceID());
        }
        public bool IsClimbing =>
            climbSources.Count > 0 &&
            Time.time >= climbReattachUntil &&
            (!IsGrounded || movement.y > .01f ||
             movement.x * GetClimbInwardDirection() > .1f);
        public PlayerStateId CurrentStateId => dead ? PlayerStateId.Death :
            IsControlLocked ? PlayerStateId.Locked :
            runner != null && runner.IsPlaying ? PlayerStateId.Action :
            IsHorizontalClimbing ? PlayerStateId.HorizontalClimbing :
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
                if (IsHorizontalClimbing) return "HorizontalClimbing";
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
        public bool IsCarrying =>
            selectedCarryTarget != null &&
            selectedCarryTarget.IsCarryActive;
        public bool IsCarryPreparing => carryPreparing;
        public bool IsCarryDropAnimating => carryDropAnimating;
        public bool IsCarryResetAnimating => carryResetting;
        public bool IsCarryBusy =>
            carryPreparing || carryDropAnimating || carryResetting;
        public int CarryQueueCount => carryQueue.Count;
        public Vector3 CarryAnchorWorldPosition
        {
            get
            {
                float top = capsule != null
                    ? capsule.bounds.max.y
                    : transform.position.y;
                return new Vector3(
                    transform.position.x,
                    top + gridCellWorldSize * .5f,
                    transform.position.z);
            }
        }
        public Vector3 CarryCenterWorldPosition =>
            transform.position;
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
        public event Action CarryPlacementRejected;
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
            capsule.radius = gridCellWorldSize * .5f / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.x));
            capsule.height = gridCellWorldSize / Mathf.Max(.001f, Mathf.Abs(transform.lossyScale.y));
            carryFootprintCells = Vector2Int.one;
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

        private void LateUpdate()
        {
            TickCarryState();
            if (selectedCarryTarget == null ||
                carryPreparing ||
                carryDropAnimating ||
                carryResetting)
            {
                return;
            }

            selectedCarryTarget.UpdateCarryAnchor(
                CarryAnchorWorldPosition);
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
            activePlayers.Add(this);
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
            activePlayers.Remove(this);
            if (health != null)
            {
                health.Died -= OnDied;
                health.Revived -= OnRevived;
            }

            if (runner != null) { runner.Completed -= Completed; runner.Stop(); }
            CancelCarryState();
            ReleaseHeldInteraction();
            lockDepth = 0; movement = Vector2.zero; jumpUntil = -1;
            jumpHeld = false;
            requestedJumpBoost = -1f;
            pendingPlatformDelta = Vector3.zero;
            waterSources.Clear();
            horizontalVines.Clear();
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

            bool wasInWater = waterSources.Count > 0;
            if (!IsGrounded)
            {
                enteredWaterDuringFall = true;
            }

            if (source != null) waterSources.Add(source.GetInstanceID());
            if (!wasInWater && waterSources.Count > 0)
            {
                AudioCuePlayer.Play(AudioCueIds.PlayerWaterEnter);
            }
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

        private void UpdateMovementAudio()
        {
            if (Time.time < nextMovementSoundTime)
            {
                return;
            }

            float horizontalSpeed = Mathf.Abs(motor.linearVelocity.x);
            bool moving = horizontalSpeed > .15f ||
                          Mathf.Abs(movement.x) > .1f;
            if (!moving)
            {
                return;
            }

            if (IsClimbing)
            {
                nextMovementSoundTime =
                    Time.time + climbSoundInterval;
                AudioCuePlayer.Play(AudioCueIds.PlayerClimb, .65f);
                return;
            }

            if (IsSwimming)
            {
                nextMovementSoundTime =
                    Time.time + waterFootstepInterval;
                AudioCuePlayer.Play(AudioCueIds.PlayerSwim, .65f);
                return;
            }

            if (IsGrounded)
            {
                nextMovementSoundTime =
                    Time.time + landFootstepInterval;
                AudioCuePlayer.Play(AudioCueIds.FootstepsLand, .55f);
            }
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
            float gravityMultiplier = jumpGravityMultiplier;
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
            if (source == null)
            {
                return;
            }

            bool wasInWater = waterSources.Count > 0;
            waterSources.Remove(source.GetInstanceID());
            if (wasInWater && waterSources.Count == 0)
            {
                AudioCuePlayer.Play(AudioCueIds.PlayerWaterExit);
            }
        }
        public void EnterClimb(Component source)
        {
            if (dead)
            {
                return;
            }

            bool wasClimbing = climbSources.Count > 0;
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

            if (!wasClimbing && climbSources.Count > 0)
            {
                AudioCuePlayer.Play(AudioCueIds.PlayerClimb);
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
            if (dead || IsHorizontalClimbing)
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

        public bool UpdateCarryInput(
            bool interactTriggered,
            bool dropLeftTriggered,
            bool dropRightTriggered,
            bool resetTriggered)
        {
            TickCarryState();
            if (dead)
            {
                CancelCarryState();
                return false;
            }

            if (selectedCarryTarget != null)
            {
                if (resetTriggered)
                {
                    ResetCarry();
                    return true;
                }

                if (carryPreparing ||
                    carryDropAnimating ||
                    carryResetting)
                {
                    return true;
                }

                if (dropLeftTriggered)
                {
                    TryDropCarry(Vector3Int.left);
                }
                else if (dropRightTriggered)
                {
                    TryDropCarry(Vector3Int.right);
                }

                return true;
            }

            if (Time.timeScale <= 0f)
            {
                return false;
            }

            if (IsControlLocked)
            {
                return false;
            }

            RefreshCarryQueue();
            if (!interactTriggered || carryQueue.Count == 0)
            {
                return false;
            }

            BeginCarryPreparation(carryQueue[0]);
            return true;
        }

        private void TickCarryState()
        {
            if (carryPreparing)
            {
                if (Time.time < carryPrepareUntil)
                {
                    return;
                }

                carryPreparing = false;
                if (!CanContinueCarry(selectedCarryTarget) ||
                    !selectedCarryTarget.BeginCarry(this))
                {
                    CancelCarryState();
                    return;
                }

                ReleaseCarryControlLock();
                return;
            }

            if (carryDropAnimating)
            {
                if (selectedCarryTarget == null ||
                    !selectedCarryTarget.IsCarryDropAnimating)
                {
                    FinishCarryDrop();
                }

                return;
            }

            if (carryResetting)
            {
                if (selectedCarryTarget == null ||
                    !selectedCarryTarget.IsCarryResetAnimating)
                {
                    FinishCarryReset();
                }

                return;
            }

            if (selectedCarryTarget != null &&
                !CanContinueCarry(selectedCarryTarget))
            {
                CancelCarryState();
            }
        }

        private void BeginCarryPreparation(
            IPlayerCarryTarget target)
        {
            carryPlantJumpCells = 0f;
            SetHighlightedCarryTarget(null);
            selectedCarryTarget = target;
            carryQueue.Clear();
            carryPreparing = true;
            carryPrepareUntil =
                Time.time + Mathf.Max(0f, carryPrepareSeconds);
            AcquireCarryControlLock();
        }

        private void TryDropCarry(Vector3Int direction)
        {
            if (selectedCarryTarget == null ||
                carryPreparing ||
                carryDropAnimating ||
                carryResetting)
            {
                return;
            }

            if (!TryResolveCarryDropCell(
                    direction,
                    out Vector3Int cell))
            {
                CarryPlacementRejected?.Invoke();
                return;
            }

            Transform targetTransform =
                selectedCarryTarget.CarryTransform;
            if (targetTransform == null ||
                !selectedCarryTarget.BeginCarryDrop(
                    this,
                    CellToWorld(
                        cell,
                        targetTransform.position.z)))
            {
                CarryPlacementRejected?.Invoke();
                return;
            }

            carryDropAnimating = true;
            AcquireCarryControlLock();
        }

        private void FinishCarryDrop()
        {
            carryPlantJumpCells = 0f;
            carryDropAnimating = false;
            selectedCarryTarget = null;
            carryQueue.Clear();
            ReleaseCarryControlLock();
            ClearBufferedInput();
        }

        private void FinishCarryReset()
        {
            carryResetting = false;
            selectedCarryTarget = null;
            carryQueue.Clear();
            ReleaseCarryControlLock();
            ClearBufferedInput();
        }

        private void ResetCarry()
        {
            if (selectedCarryTarget == null)
            {
                carryResetting = false;
                carryQueue.Clear();
                ReleaseCarryControlLock();
                return;
            }

            if (carryResetting)
            {
                return;
            }

            carryPreparing = false;
            carryDropAnimating = false;
            if (!selectedCarryTarget.BeginCarryReset(this))
            {
                CancelCarryState();
                return;
            }

            carryResetting = true;
            AcquireCarryControlLock();
        }

        private void CancelCarryState()
        {
            carryPlantJumpCells = 0f;
            carryPreparing = false;
            carryDropAnimating = false;
            carryResetting = false;
            carryPrepareUntil = -1f;
            selectedCarryTarget?.CancelCarry();
            selectedCarryTarget = null;
            carryQueue.Clear();
            SetHighlightedCarryTarget(null);
            ReleaseCarryControlLock();
        }

        private void AcquireCarryControlLock()
        {
            if (carryControlLockActive)
            {
                return;
            }

            carryControlLockActive = true;
            ReleaseHeldInteraction();
            colorWheel?.CloseWheel();
            ClearBufferedInput();
            SetControlLocked(true);
        }

        private void ReleaseCarryControlLock()
        {
            if (!carryControlLockActive)
            {
                return;
            }

            carryControlLockActive = false;
            SetControlLocked(false);
        }

        private bool CanContinueCarry(IPlayerCarryTarget target)
        {
            if (target == null)
            {
                return false;
            }

            if (target is Component component &&
                (component == null ||
                 !component.gameObject.activeInHierarchy))
            {
                return false;
            }

            return target.CanCarry(this);
        }

        private void RefreshCarryQueue()
        {
            carryQueue.Clear();
            IReadOnlyList<IPlayerCarryTarget> targets =
                PlayerCarryTargetRegistry.All;
            if (targets.Count == 0)
            {
                SetHighlightedCarryTarget(null);
                return;
            }

            GetCarryFootprintCells(carryFootprintBuffer);
            for (int index = 0; index < targets.Count; index++)
            {
                IPlayerCarryTarget target = targets[index];
                if (target == null ||
                    target.CarryTransform == null ||
                    !target.CanCarry(this))
                {
                    continue;
                }

                Vector3Int cell = WorldToCell(
                    target.CarryTransform.position);
                if (IsCardinalNeighbor(
                        carryFootprintBuffer,
                        cell))
                {
                    carryQueue.Add(target);
                }
            }

            if (carryQueue.Count > 1)
            {
                Vector3 playerPosition = transform.position;
                carryQueue.Sort((left, right) =>
                {
                    float leftDistance =
                        (left.CarryTransform.position - playerPosition)
                        .sqrMagnitude;
                    float rightDistance =
                        (right.CarryTransform.position - playerPosition)
                        .sqrMagnitude;
                    return leftDistance.CompareTo(rightDistance);
                });
            }

            SetHighlightedCarryTarget(
                carryQueue.Count > 0
                    ? carryQueue[0]
                    : null);
        }

        private void SetHighlightedCarryTarget(
            IPlayerCarryTarget target)
        {
            if (ReferenceEquals(highlightedCarryTarget, target))
            {
                return;
            }

            highlightedCarryTarget?.SetCarryIndicator(false);
            highlightedCarryTarget = target;
            highlightedCarryTarget?.SetCarryIndicator(true);
        }

        private static bool IsCardinalNeighbor(
            IReadOnlyList<Vector3Int> occupiedCells,
            Vector3Int targetCell)
        {
            for (int index = 0;
                 index < occupiedCells.Count;
                 index++)
            {
                Vector3Int delta =
                    targetCell - occupiedCells[index];
                if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private void GetCarryFootprintCells(
            List<Vector3Int> result)
        {
            result.Clear();
            int width = Mathf.Max(1, carryFootprintCells.x);
            int height = Mathf.Max(1, carryFootprintCells.y);
            float epsilon = gridCellWorldSize * .001f;
            Vector3 center = transform.position;
            Vector3 halfExtents = new Vector3(
                width * gridCellWorldSize * .5f,
                height * gridCellWorldSize * .5f,
                0f);
            Vector3Int min = WorldToCell(
                center - halfExtents + Vector3.one * epsilon);
            Vector3Int max = WorldToCell(
                center + halfExtents - Vector3.one * epsilon);
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    result.Add(new Vector3Int(x, y, 0));
                }
            }
        }

        private bool TryResolveCarryDropCell(
            Vector3Int direction,
            out Vector3Int result)
        {
            if (HasCarryBlockerInDirection(direction))
            {
                result = default;
                return false;
            }

            GetCarryFootprintCells(carryFootprintBuffer);
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int index = 0;
                 index < carryFootprintBuffer.Count;
                 index++)
            {
                Vector3Int cell = carryFootprintBuffer[index];
                minX = Mathf.Min(minX, cell.x);
                maxX = Mathf.Max(maxX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxY = Mathf.Max(maxY, cell.y);
            }

            for (int index = 0;
                 index < carryFootprintBuffer.Count;
                 index++)
            {
                Vector3Int occupied = carryFootprintBuffer[index];
                bool onOuterEdge =
                    direction.x < 0
                        ? occupied.x == minX
                        : direction.x > 0
                            ? occupied.x == maxX
                            : direction.y < 0
                                ? occupied.y == minY
                                : occupied.y == maxY;
                if (!onOuterEdge)
                {
                    continue;
                }

                Vector3Int candidate = occupied + direction;
                if (carryFootprintBuffer.Contains(candidate) ||
                    !IsCarryDropCellFree(candidate))
                {
                    continue;
                }

                result = candidate;
                return true;
            }

            result = default;
            return false;
        }

        private bool HasCarryBlockerInDirection(
            Vector3Int direction)
        {
            Vector3 rayDirection = new Vector3(
                direction.x,
                direction.y,
                0f);
            if (rayDirection.sqrMagnitude < .001f)
            {
                return true;
            }

            rayDirection.Normalize();
            int count = Physics.RaycastNonAlloc(
                new Ray(transform.position, rayDirection),
                carryRayBuffer,
                gridCellWorldSize * 1.1f,
                ~0,
                QueryTriggerInteraction.Collide);
            for (int index = 0; index < count; index++)
            {
                Collider collider = carryRayBuffer[index].collider;
                if (collider == null ||
                    BelongsToPlayer(collider.transform) ||
                    BelongsToSelectedCarryTarget(collider.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsCarryDropCellFree(Vector3Int cell)
        {
            if (IsCarryCellOccupiedByPlacedBlock(cell))
            {
                return false;
            }

            Vector3 center = CellToWorld(
                cell,
                transform.position.z);
            int count = Physics.OverlapBoxNonAlloc(
                center,
                Vector3.one * (gridCellWorldSize * .49f),
                carryDropBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                Collider collider = carryDropBuffer[index];
                if (collider == null ||
                    BelongsToPlayer(collider.transform) ||
                    BelongsToSelectedCarryTarget(collider.transform))
                {
                    continue;
                }

                return false;
            }

            IReadOnlyList<IPlayerCarryTarget> targets =
                PlayerCarryTargetRegistry.All;
            for (int index = 0; index < targets.Count; index++)
            {
                IPlayerCarryTarget target = targets[index];
                if (target == null ||
                    ReferenceEquals(target, selectedCarryTarget) ||
                    target.CarryTransform == null ||
                    !target.CanCarry(this))
                {
                    continue;
                }

                if (WorldToCell(target.CarryTransform.position) == cell)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCarryCellOccupiedByPlacedBlock(
            Vector3Int cell)
        {
            Vector2Int gridCell = new Vector2Int(cell.x, cell.y);
            IReadOnlyList<Project.LevelEditor.LevelEditorPlacedBlock>
                blocks =
                    ProjectDiscovery.FindAll<
                        Project.LevelEditor.LevelEditorPlacedBlock>(
                        true);
            for (int index = 0; index < blocks.Count; index++)
            {
                Project.LevelEditor.LevelEditorPlacedBlock block =
                    blocks[index];
                if (block == null ||
                    !block.isActiveAndEnabled ||
                    block.gameObject.scene != gameObject.scene ||
                    !block.ContainsCell(gridCell))
                {
                    continue;
                }

                if (BelongsToPlayer(block.transform) ||
                    BelongsToSelectedCarryTarget(block.transform))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool BelongsToPlayer(Transform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            return candidate == transform ||
                   candidate.IsChildOf(transform);
        }

        private bool BelongsToSelectedCarryTarget(
            Transform candidate)
        {
            if (candidate == null ||
                selectedCarryTarget is not Component target ||
                target == null)
            {
                return false;
            }

            return candidate == target.transform ||
                   candidate.IsChildOf(target.transform);
        }

        private Vector3Int WorldToCell(Vector3 position)
        {
            return new Vector3Int(
                Mathf.FloorToInt(position.x / gridCellWorldSize),
                Mathf.FloorToInt(position.y / gridCellWorldSize),
                0);
        }

        private Vector3 CellToWorld(
            Vector3Int cell,
            float z)
        {
            return new Vector3(
                (cell.x + .5f) * gridCellWorldSize,
                (cell.y + .5f) * gridCellWorldSize,
                z);
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
                bounceHasExactHeight = surface is BouncePlantFeature;
                return;
            }
        }

        private bool TickHorizontalVine()
        {
            expiredHorizontalVines.Clear();
            HorizontalVineSurface? chosen = null;
            foreach (var pair in horizontalVines)
            {
                var surface = pair.Value;
                if (surface.source == null || !surface.source.gameObject.activeInHierarchy ||
                    (surface.source is Behaviour behaviour && !behaviour.isActiveAndEnabled) || Time.fixedTime - surface.renewed > .08f)
                    expiredHorizontalVines.Add(pair.Key);
                else if (!chosen.HasValue || Mathf.Abs(surface.rootY - motor.position.y) < Mathf.Abs(chosen.Value.rootY - motor.position.y)) chosen = surface;
            }
            foreach (int id in expiredHorizontalVines) horizontalVines.Remove(id);
            if (!chosen.HasValue) return false;
            jumpUntil = groundedUntil = -1; jumpHeld = false; requestedBounceSpeed = -1; requestedJumpBoost = -1;
            bool blocked = IsControlLocked || (runner.IsPlaying && runner.CurrentAction != null && runner.CurrentAction.LockMovement);
            if (blocked) return false;
            HorizontalVineSurface selected = chosen.Value;
            Vector3 velocity = motor.linearVelocity;
            velocity.x = Mathf.Clamp(movement.x, -1, 1) * selected.speed;
            velocity.y = Mathf.Clamp((selected.rootY - motor.position.y) / Time.fixedDeltaTime, -3f, 3f);
            velocity.z = 0;
            motor.linearVelocity = velocity;
            IsGrounded = false; enteredClimbDuringFall = true; highestAirPosition = motor.position.y;
            return true;
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

            if (TickHorizontalVine()) return;

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
                if (groundHits[i].collider.attachedRigidbody != motor && !Physics.GetIgnoreCollision(capsule, groundHits[i].collider) && groundHits[i].normal.y > .5f && motor.linearVelocity.y <= .1f)
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
                if (bounceHasExactHeight) { velocity.y = requestedBounceSpeed; bounceHasExactHeight = false; }
                else ApplyEnvironmentLaunch(ref velocity, requestedBounceSpeed);
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
                                 (!IsCarrying || CanCarryPlantJump) &&
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
                AudioCuePlayer.Play(AudioCueIds.PlayerJump);
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
                    CalculateGridRiseSpeed(IsCarrying ? carryPlantJumpCells : jumpRiseCells) *
                    NextJumpBounceService.ConsumeMultiplier();
                if (IsCarrying) carryPlantJumpCells = 0f;
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
                AudioCuePlayer.Play(AudioCueIds.PlayerJump);
            }

            if (IsGrounded && velocity.y < 0f)
            {
                velocity.y = 0f;
            }
            else
            {
                float gravityMultiplier = velocity.y > 0f ? jumpGravityMultiplier : fallGravityMultiplier;
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

            UpdateMovementAudio();
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
            bool wasDead = dead;
            if (!dead) deathPosition = transform.position;
            carryPlantJumpCells = 0f;
            PrepareForRoomReset();
            runner?.Stop();
            ClearDeathControlState();
            Vector3 requestedPosition = initialRespawnPosition;
            if (PlayerRespawnPointRegistry.TryFindPreferred(this, deathPosition, out IPlayerRespawnPoint boundPoint))
                requestedPosition = boundPoint.RespawnPosition;
            else if (respawnPoint != null) requestedPosition = respawnPoint.position;
            Project.Rooms.RoomLifecycleManager.BeforeRespawn(this, requestedPosition);
            Vector3 targetPosition;
            if (PlayerRespawnPointRegistry.TryFindPreferred(
                    this,
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
                    initialRespawnPosition,
                    respawnRandomRadiusBlocks,
                    initialRespawnPosition);
            }
            if (motor != null)
            {
                motor.position = targetPosition;
                motor.linearVelocity = Vector3.zero;
                motor.angularVelocity = Vector3.zero;
            }
            transform.position = targetPosition;
            Physics.SyncTransforms();
            if (wasDead)
            {
                AudioCuePlayer.Play(AudioCueIds.PlayerRespawn);
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
            AudioCuePlayer.Play(AudioCueIds.PlayerDie);
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
            ControlLockChanged?.Invoke(IsControlLocked);
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
            horizontalVines.Clear();
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
            CancelCarryState();
            colorWheel?.CloseWheel();
            if (motor != null)
            {
                motor.linearVelocity = Vector3.zero;
                motor.angularVelocity = Vector3.zero;
            }
        }

        public void PrepareForRoomReset()
        {
            pendingPlatformDelta = Vector3.zero;
            ReleaseHeldInteraction();
            CancelCarryState();
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
            CancelCarryState();
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
