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
        private Vector2 movement;
        private bool sprint;
        private float jumpUntil = -1, groundedUntil = -1;
        private float airborneSince = -1f;
        private bool jumpHeld;
        private int lockDepth;
        private float requestedBounceSpeed = -1f;
        private readonly HashSet<int> waterSources = new HashSet<int>();
        private readonly Dictionary<int, float> waterSpeedMultipliers =
            new Dictionary<int, float>();
        private readonly HashSet<int> fallDamageImmunitySources =
            new HashSet<int>();
        private readonly HashSet<int> climbSources = new HashSet<int>();
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        private static PhysicsMaterial zeroFrictionMaterial;
        public Rigidbody Motor => motor;
        public bool IsGrounded { get; private set; }
        public bool IsControlLocked => lockDepth > 0;
        public bool IsSwimming => waterSources.Count > 0;
        public bool IsFallDamageImmune => fallDamageImmunitySources.Count > 0;
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
            if (!GetComponent<PlayerInteractionSensor>()) gameObject.AddComponent<PlayerInteractionSensor>();
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
            lockDepth = 0; movement = Vector2.zero; jumpUntil = -1;
            jumpHeld = false;
            waterSources.Clear();
            waterSpeedMultipliers.Clear();
            fallDamageImmunitySources.Clear();
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

        public void SetFallDamageImmune(Component source, bool value)
        {
            if (source == null)
            {
                return;
            }

            int id = source.GetInstanceID();
            if (value)
            {
                fallDamageImmunitySources.Add(id);
            }
            else
            {
                fallDamageImmunitySources.Remove(id);
            }
        }

        public void ApplyVerticalBounce(float verticalSpeed)
        {
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

        private void FixedUpdate()
        {
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
                airborneSince = -1f;
            }
            else if (wasGrounded)
            {
                airborneSince = Time.time;
            }

            if (IsGrounded && !wasGrounded) Emit(AchievementSignalIds.PlayerLanded);
            Vector3 velocity = motor.linearVelocity;
            if (requestedBounceSpeed >= 0f)
            {
                velocity.y = Mathf.Max(velocity.y, requestedBounceSpeed);
                requestedBounceSpeed = -1f;
                IsGrounded = false;
                airborneSince = Time.time;
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
                float horizontalTarget = blocked
                    ? 0f
                    : Mathf.Clamp(movement.x, -1f, 1f) *
                      climbHorizontalSpeed;
                float verticalTarget;
                if (blocked)
                {
                    verticalTarget = 0f;
                }
                else if (movement.y > .01f)
                {
                    verticalTarget = climbSpeed;
                }
                else if (movement.y < -.01f)
                {
                    verticalTarget = -climbDownSpeed;
                }
                else
                {
                    verticalTarget = -climbSlideSpeed;
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
                float horizontalTarget = blocked
                    ? 0f
                    : Mathf.Clamp(movement.x, -1f, 1f) *
                      swimSpeed * GetSwimSpeedMultiplier();
                float verticalTarget = blocked
                    ? 0f
                    : jumpHeld
                        ? swimRiseSpeed
                        : -swimSinkSpeed;
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
                velocity.y = jumpSpeed * NextJumpBounceService.ConsumeMultiplier();
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
