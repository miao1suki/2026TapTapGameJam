using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.Player
{
    public enum PlayerStateId
    {
        Normal = 0,
        Swimming = 1,
        Action = 2,
        Locked = 3,
        Climbing = 4
    }

    /// <summary>Basic physical motor. No projected platforms, ladders or rope dependencies.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerActionRunner))]
    [DisallowMultipleComponent]
    public sealed class PlayerController :
        MonoBehaviour,
        IAchievementSignalProvider
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
        private int lockDepth;
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
        private readonly HashSet<int> bounceSurfaces =
            new HashSet<int>();
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private static PhysicsMaterial zeroFrictionMaterial;
        public Rigidbody Motor => motor;
        public bool IsGrounded { get; private set; }
        public bool IsControlLocked => lockDepth > 0;
        public bool IsSwimming => waterSources.Count > 0;
        public bool IsClimbing =>
            climbSources.Count > 0 &&
            (!IsGrounded || movement.y > .01f);
        public PlayerStateId CurrentStateId => IsControlLocked ? PlayerStateId.Locked :
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
        public bool IsOnBounceSurface => bounceSurfaces.Count > 0;
        public bool LandedThisStep => landedThisStep;
        public string GravityMode
        {
            get
            {
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
        private void OnEnable() { runner = GetComponent<PlayerActionRunner>(); runner.Completed += Completed; }
        private void OnDisable()
        {
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
            climbSpeedMultipliers.Clear();
            climbSlideSpeeds.Clear();
            climbFastSlideSpeeds.Clear();
            climbSources.Clear();
        }
        public void EnterWater(Component source)
        {
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
            if (source == null)
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
            if (source == null)
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
            requestedBounceSpeed = Mathf.Max(
                requestedBounceSpeed,
                verticalSpeed);
        }

        public void ApplyVerticalBounceImmediate(float verticalSpeed)
        {
            requestedBounceSpeed = -1f;
            if (motor == null)
            {
                return;
            }

            Vector3 velocity = motor.linearVelocity;
            velocity.y = Mathf.Max(velocity.y, verticalSpeed);
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
            float gravityMultiplier = jumpHeld
                ? jumpGravityMultiplier
                : jumpReleaseGravityMultiplier;
            float gravity = Mathf.Max(
                .01f,
                Mathf.Abs(Physics.gravity.y) * gravityMultiplier);
            return initialSpeed * initialSpeed / (2f * gravity);
        }

        public void QueuePlatformDelta(Vector3 delta)
        {
            pendingPlatformDelta += delta;
        }

        public void ApplyJumpBoost(float verticalSpeed)
        {
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
            if (source != null) climbSources.Add(source.GetInstanceID());
        }
        public void ExitClimb(Component source)
        {
            if (source != null) climbSources.Remove(source.GetInstanceID());
        }
        public void SetMoveInput(Vector2 value)
        {
            movement = value;
        }
        public void SetSprintInput(bool value)
        {
            sprint = value;
        }
        public void RequestJump()
        {
            jumpUntil = Time.time + jumpBufferTime;
            jumpHeld = true;
        }
        public void SetJumpHeld(bool value)
        {
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
            colorWheel?.Tick(pointerPosition);
        }

        public void UpdateColorWheelInput(
            bool holdMode,
            bool pressedThisFrame,
            bool releasedThisFrame,
            bool triggeredThisFrame,
            Vector2 pointerPosition)
        {
            colorWheel?.UpdateInput(
                holdMode,
                pressedThisFrame,
                releasedThisFrame,
                triggeredThisFrame,
                pointerPosition);
        }

        public void ToggleColorWheel()
        {
            colorWheel?.ToggleWheel();
        }

        public void CloseColorWheel()
        {
            colorWheel?.CloseWheel();
        }

        public void CommitColorWheelSelection()
        {
            colorWheel?.CommitWheelSelection();
        }

        public void TryUseSelectedColorAbility()
        {
            colorWheel?.TryUseSelectedAbility();
        }

        public void BeginPointerDrag(IPlayerPointerDrag drag)
        {
            if (drag == null || pointerDrag != null)
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
            if (pointerDrag == null || camera == null)
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
                    TryApplyLandingBounce(
                        lastLandingFallDistance,
                        lastLandingDownwardSpeed,
                        count);
                    highestAirPosition = transform.position.y;
                    currentFallDistance = 0f;
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
                velocity.y = Mathf.Max(velocity.y, requestedBounceSpeed);
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
            }
            bool blocked = IsControlLocked || (runner.IsPlaying && runner.CurrentAction != null && runner.CurrentAction.LockMovement);
            bool jumpRequested = !blocked &&
                                 jumpUntil >= Time.time &&
                                 groundedUntil >= Time.time;
            if (jumpRequested)
            {
                climbSources.Clear();
            }

            if (IsClimbing)
            {
                float climbMultiplier =
                    GetClimbSpeedMultiplier();
                float horizontalTarget = blocked
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
            if (IsSwimming)
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
            else
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
                velocity.y += Physics.gravity.y *
                              gravityMultiplier *
                              Time.fixedDeltaTime;
                if (maxFallSpeed > 0f &&
                    velocity.y < -maxFallSpeed)
                {
                    velocity.y = -maxFallSpeed;
                }
            }

            motor.linearVelocity = velocity;
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
        public void SetControlLocked(bool locked)
        {
            bool previous = IsControlLocked;
            lockDepth = locked ? lockDepth + 1 : Mathf.Max(0, lockDepth - 1);
            if (previous != IsControlLocked) ControlLockChanged?.Invoke(IsControlLocked);
        }
        public void ReceiveTimelineSignal() => TimelineSignalReceived?.Invoke();
        private void Emit(string id) => GameplaySignalHub.Emit(id, gameObject);
    }
}
