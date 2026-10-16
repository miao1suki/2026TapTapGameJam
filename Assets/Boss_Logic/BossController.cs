using System;
using Project.Player;
using UnityEngine;

namespace Project.BossLogic
{
    public enum BossStateId
    {
        Chase = 0,
        Hit = 1,
        Death = 2,
        Jump = 3
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class BossController : MonoBehaviour, IDamageable
    {
        [Header("追击")]
        [SerializeField] private Transform target;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private bool trackingEnabled = true;
        [SerializeField, Min(0f)] private float targetRefreshSeconds = .5f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float stopDistance = .15f;
        [SerializeField] private bool chaseVertical;
        [SerializeField] private bool faceTarget = true;

        [Header("跳跃")]
        [SerializeField, Min(0.05f)] private float jumpDuration = .65f;
        [SerializeField, Min(0f)] private float jumpArcHeight = 2f;
        [SerializeField, Min(0f)] private float jumpCooldownSeconds = .2f;
        [SerializeField] private bool faceJumpDirection = true;
        [SerializeField] private bool clearVelocityOnLanding = true;

        [Header("受击")]
        [SerializeField, Min(1f)] private float maxHealth = 5f;
        [SerializeField, Min(0f)] private float startingHealth = 5f;
        [SerializeField, Min(0f)] private float hitStunSeconds = .35f;
        [SerializeField, Min(0f)] private float hitKnockbackSpeed = 4f;

        [Header("死亡")]
        [SerializeField] private bool destroyAfterDeath;
        [SerializeField, Min(0f)] private float destroyDelaySeconds = 2f;

        private Rigidbody body;
        private float currentHealth;
        private float hitUntil;
        private float nextTargetRefreshTime;
        private float nextJumpTime;
        private int facingDirection = 1;
        private bool dead;
        private BossStateId state = BossStateId.Chase;
        private bool jumpActive;
        private float jumpElapsed;
        private float activeJumpDuration;
        private float activeJumpArcHeight;
        private Vector3 jumpStartPosition;
        private Vector3 jumpTargetPosition;
        private bool previousKinematic;
        private bool previousUseGravity;
        private RigidbodyInterpolation previousInterpolation;
        private CollisionDetectionMode previousCollisionDetectionMode;

        public event Action<BossStateId, BossStateId> StateChanged;
        public event Action<float, float> Damaged;
        public event Action Died;

        public BossStateId State => state;
        public bool IsDead => dead;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public Transform Target => target;
        public bool TrackingEnabled => trackingEnabled;
        public int FacingDirection => facingDirection;
        public Rigidbody Body => body;
        public bool IsJumping => jumpActive;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.freezeRotation = true;
            body.constraints |= RigidbodyConstraints.FreezePositionZ;
            currentHealth = Mathf.Clamp(
                startingHealth <= 0f ? maxHealth : startingHealth,
                0f,
                maxHealth);
            ResolveTarget(true);
            SetState(BossStateId.Chase);
        }

        private void Update()
        {
            if (dead)
            {
                return;
            }

            if (trackingEnabled)
            {
                ResolveTarget(false);
            }

            if (state == BossStateId.Hit &&
                Time.time >= hitUntil)
            {
                SetState(BossStateId.Chase);
            }
        }

        private void FixedUpdate()
        {
            if (body == null || dead)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (jumpActive)
            {
                TickJump();
                return;
            }

            if (state != BossStateId.Chase ||
                !trackingEnabled ||
                target == null)
            {
                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    0f,
                    acceleration * Time.fixedDeltaTime);
                if (chaseVertical)
                {
                    velocity.y = Mathf.MoveTowards(
                        velocity.y,
                        0f,
                        acceleration * Time.fixedDeltaTime);
                }

                body.linearVelocity = velocity;
                return;
            }

            Vector3 offset = target.position - transform.position;
            offset.z = 0f;
            if (offset.sqrMagnitude <= stopDistance * stopDistance)
            {
                velocity.x = Mathf.MoveTowards(
                    velocity.x,
                    0f,
                    acceleration * Time.fixedDeltaTime);
                if (chaseVertical)
                {
                    velocity.y = Mathf.MoveTowards(
                        velocity.y,
                        0f,
                        acceleration * Time.fixedDeltaTime);
                }

                body.linearVelocity = velocity;
                return;
            }

            Vector3 direction = offset.normalized;
            float targetX = direction.x * moveSpeed;
            velocity.x = Mathf.MoveTowards(
                velocity.x,
                targetX,
                acceleration * Time.fixedDeltaTime);
            if (chaseVertical)
            {
                velocity.y = Mathf.MoveTowards(
                    velocity.y,
                    direction.y * moveSpeed,
                    acceleration * Time.fixedDeltaTime);
            }
            velocity.z = 0f;
            body.linearVelocity = velocity;

            if (faceTarget &&
                Mathf.Abs(direction.x) > .01f)
            {
                facingDirection = direction.x >= 0f ? 1 : -1;
            }
        }

        private void OnDisable()
        {
            CancelJump();
        }

        public void SetTarget(Transform value)
        {
            target = value;
        }

        public void SetTrackingEnabled(bool value)
        {
            trackingEnabled = value;
            if (!value && body != null && !jumpActive)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.x = 0f;
                if (chaseVertical)
                {
                    velocity.y = 0f;
                }

                body.linearVelocity = velocity;
            }
        }

        public bool RequestJump(
            Vector2 direction,
            float distance,
            float arcHeight = -1f,
            float duration = -1f)
        {
            Vector3 worldDirection = new Vector3(
                direction.x,
                direction.y,
                0f);
            if (worldDirection.sqrMagnitude <= .0001f || distance <= 0f)
            {
                return false;
            }

            Vector3 start = body != null
                ? body.position
                : transform.position;
            Vector3 target =
                start + worldDirection.normalized * distance;
            return RequestJumpTo(target, arcHeight, duration);
        }

        public bool RequestJumpTo(
            Vector3 worldTargetPosition,
            float arcHeight = -1f,
            float duration = -1f)
        {
            if (dead ||
                jumpActive ||
                Time.time < nextJumpTime ||
                body == null)
            {
                return false;
            }

            float resolvedArcHeight =
                arcHeight >= 0f ? arcHeight : jumpArcHeight;
            float resolvedDuration =
                duration > 0f ? duration : jumpDuration;
            if (resolvedDuration <= 0f)
            {
                return false;
            }

            jumpStartPosition = body.position;
            jumpTargetPosition = worldTargetPosition;
            activeJumpArcHeight = Mathf.Max(0f, resolvedArcHeight);
            activeJumpDuration = Mathf.Max(.05f, resolvedDuration);
            jumpElapsed = 0f;
            jumpActive = true;

            previousKinematic = body.isKinematic;
            previousUseGravity = body.useGravity;
            previousInterpolation = body.interpolation;
            previousCollisionDetectionMode = body.collisionDetectionMode;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            body.position = jumpStartPosition;
            transform.position = jumpStartPosition;

            if (faceJumpDirection)
            {
                float horizontal =
                    jumpTargetPosition.x - jumpStartPosition.x;
                if (Mathf.Abs(horizontal) > .01f)
                {
                    facingDirection = horizontal >= 0f ? 1 : -1;
                }
            }

            SetState(BossStateId.Jump);
            return true;
        }

        public void CancelJump(bool snapToTarget = false)
        {
            if (!jumpActive)
            {
                return;
            }

            Vector3 current = snapToTarget
                ? jumpTargetPosition
                : body != null
                    ? body.position
                    : transform.position;
            RestoreJumpPhysics(current);
            jumpActive = false;
        }

        public void TeleportTo(
            Vector3 worldPosition,
            Quaternion worldRotation,
            bool clearMotion = true)
        {
            CancelJump();
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            if (body != null)
            {
                body.position = worldPosition;
                body.rotation = worldRotation;
                if (clearMotion)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.WakeUp();
            }
        }

        public void TakeDamage(float amount)
        {
            ReceiveHit(amount, Vector3.zero);
        }

        public void ReceiveHit(float amount, Vector3 knockback)
        {
            if (dead || amount <= 0f)
            {
                return;
            }

            CancelJump();
            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke(amount, currentHealth);
            if (currentHealth <= 0f)
            {
                EnterDeath(knockback);
                return;
            }

            if (knockback.sqrMagnitude <= 0f &&
                hitKnockbackSpeed > 0f)
            {
                knockback = new Vector3(
                    -facingDirection * hitKnockbackSpeed,
                    0f,
                    0f);
            }

            if (body != null && knockback.sqrMagnitude > 0f)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.x = knockback.x;
                velocity.y = knockback.y;
                body.linearVelocity = velocity;
            }

            hitUntil = Time.time + Mathf.Max(0f, hitStunSeconds);
            SetState(BossStateId.Hit);
        }

        public void Kill()
        {
            EnterDeath(Vector3.zero);
        }

        private void EnterDeath(Vector3 knockback)
        {
            if (dead)
            {
                return;
            }

            currentHealth = 0f;
            dead = true;
            CancelJump();
            SetState(BossStateId.Death);
            if (body != null)
            {
                Vector3 velocity = Vector3.zero;
                if (knockback.sqrMagnitude > 0f)
                {
                    velocity = knockback;
                }

                body.linearVelocity = velocity;
            }

            Died?.Invoke();
            if (destroyAfterDeath)
            {
                Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
            }
        }

        private void TickJump()
        {
            jumpElapsed += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(
                jumpElapsed / activeJumpDuration);
            Vector3 position = Vector3.Lerp(
                jumpStartPosition,
                jumpTargetPosition,
                progress);
            position.y +=
                4f *
                activeJumpArcHeight *
                progress *
                (1f - progress);
            body.MovePosition(position);

            if (progress >= 1f)
            {
                FinishJump();
            }
        }

        private void FinishJump()
        {
            RestoreJumpPhysics(jumpTargetPosition);
            jumpActive = false;
            nextJumpTime = Time.time + jumpCooldownSeconds;
            if (!dead)
            {
                SetState(BossStateId.Chase);
            }
        }

        private void RestoreJumpPhysics(Vector3 position)
        {
            if (body == null)
            {
                transform.position = position;
                return;
            }

            body.position = position;
            transform.position = position;
            body.useGravity = previousUseGravity;
            body.isKinematic = previousKinematic;
            body.interpolation = previousInterpolation;
            body.collisionDetectionMode =
                previousCollisionDetectionMode;
            if (clearVelocityOnLanding)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.WakeUp();
        }

        private void ResolveTarget(bool force)
        {
            if (target != null && !force)
            {
                return;
            }

            if (!force && Time.time < nextTargetRefreshTime)
            {
                return;
            }

            nextTargetRefreshTime =
                Time.time + Mathf.Max(.05f, targetRefreshSeconds);
            PlayerController player = ProjectDiscovery.FindFirst<PlayerController>();
            if (player != null &&
                (string.IsNullOrWhiteSpace(targetTag) ||
                 player.CompareTag(targetTag)))
            {
                target = player.transform;
                return;
            }

            GameObject tagged = string.IsNullOrWhiteSpace(targetTag)
                ? null
                : GameObject.FindGameObjectWithTag(targetTag);
            target = tagged != null ? tagged.transform : null;
        }

        private void SetState(BossStateId next)
        {
            if (state == next)
            {
                return;
            }

            BossStateId previous = state;
            state = next;
            StateChanged?.Invoke(previous, next);
        }
    }
}
