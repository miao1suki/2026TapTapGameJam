using System;
using System.Collections.Generic;
using Project.CameraModes;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.Player
{
    public enum PlayerStateId { Normal = 0, Action = 2, Locked = 3 }

    /// <summary>Basic physical motor. No projected platforms, ladders or rope dependencies.</summary>
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlayerActionRunner))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour, IAchievementSignalProvider
    {
        [SerializeField, Min(0)] private float moveSpeed = 5;
        [SerializeField, Min(0)] private float jumpSpeed = 7;
        [SerializeField, Min(0)] private float sprintMultiplier = 1.5f;
        [SerializeField, Min(0)] private float coyoteTime = .12f;
        [SerializeField, Min(0)] private float jumpBufferTime = .12f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private PlayerActionBinding[] actionBindings = Array.Empty<PlayerActionBinding>();
        private Rigidbody motor;
        private CapsuleCollider capsule;
        private PlayerActionRunner runner;
        private Vector2 movement;
        private bool sprint;
        private float jumpUntil = -1, groundedUntil = -1;
        private int lockDepth;
        private readonly RaycastHit[] groundHits = new RaycastHit[16];
        public Rigidbody Motor => motor;
        public bool IsGrounded { get; private set; }
        public bool IsControlLocked => lockDepth > 0;
        public PlayerStateId CurrentStateId => IsControlLocked ? PlayerStateId.Locked :
            runner != null && runner.IsPlaying ? PlayerStateId.Action : PlayerStateId.Normal;
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
            if (!GetComponent<PlayerInteractionSensor>()) gameObject.AddComponent<PlayerInteractionSensor>();
        }
        private void OnEnable() { runner = GetComponent<PlayerActionRunner>(); runner.Completed += Completed; }
        private void OnDisable()
        {
            if (runner != null) { runner.Completed -= Completed; runner.Stop(); }
            lockDepth = 0; movement = Vector2.zero; jumpUntil = -1;
        }
        private void Update()
        {
            if (IsControlLocked || Time.timeScale == 0) { movement = Vector2.zero; jumpUntil = -1; return; }
            movement = GameInput.ReadVector2(InputActionId.Move);
            sprint = GameInput.IsPressed(InputActionId.Sprint);
            if (GameInput.WasTriggeredThisFrame(InputActionId.Jump)) jumpUntil = Time.time + jumpBufferTime;
            foreach (PlayerActionBinding binding in actionBindings)
                if (binding.action != null && GameInput.WasTriggeredThisFrame(binding.inputAction)) TryPlayAction(binding.action);
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
            if (IsGrounded && !wasGrounded) Emit(AchievementSignalIds.PlayerLanded);
            Vector3 velocity = motor.linearVelocity;
            bool blocked = IsControlLocked || (runner.IsPlaying && runner.CurrentAction != null && runner.CurrentAction.LockMovement);
            velocity.x = blocked ? 0 : movement.x * moveSpeed * (sprint ? sprintMultiplier : 1);
            velocity.z = 0;
            if (!blocked && jumpUntil >= Time.time && groundedUntil >= Time.time)
            {
                velocity.y = jumpSpeed; jumpUntil = groundedUntil = -1; IsGrounded = false;
                Emit(AchievementSignalIds.PlayerJumped);
            }
            motor.linearVelocity = velocity;
        }
        public bool HasActionBinding(InputActionId id)
        {
            foreach (var binding in actionBindings) if (binding.inputAction == id && binding.action != null) return true;
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
