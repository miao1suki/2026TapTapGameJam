using System;
using System.Collections.Generic;
using Project;
using Project.BlockFeatures;
using Project.Player;
using Project.Rooms;
using UnityEngine;
using UnityEngine.Events;

namespace Project.BossLogic
{
    public enum BossStateId
    {
        Chase = 0,
        Hit = 1,
        Death = 2,
        Jump = 3
    }

    [Serializable]
    public sealed class BossDefeatPresentationEvent :
        UnityEvent<BossController>
    {
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public sealed class BossController :
        MonoBehaviour,
        IDamageable,
        IBossRespawnStateReceiver
    {
        [Header("身份")]
        [SerializeField] private string bossDisplayName = "Boss";

        [Header("追击")]
        [SerializeField] private Transform target;
        [SerializeField] private string targetTag = "Player";
        [SerializeField] private bool enableAutonomousChase;
        [SerializeField] private bool trackingEnabled = true;
        [SerializeField, Min(0f)] private float targetRefreshSeconds = .5f;
        [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
        [SerializeField, Min(0f)] private float climbSpeed = 4.5f;
        [SerializeField, Min(0f)] private float acceleration = 18f;
        [SerializeField, Min(0f)] private float stopDistance = .15f;
        [SerializeField] private bool chaseVertical;
        [SerializeField] private bool faceTarget = true;

        [Header("Timeline 操作输入")]
        [SerializeField, Min(0f)] private float walkSpeed = 3f;

        [Header("跳跃")]
        [SerializeField, Min(0.05f)] private float jumpDuration = 1.4f;
        [SerializeField, Min(.1f)] private float jumpHorizontalSpeed = 5.5f;
        [SerializeField, Min(0f)] private float maxJumpRiseHeight = 6f;
        [SerializeField, Min(0f)] private float maxJumpVerticalSpeed = 12f;
        [SerializeField, Min(0f)] private float jumpCooldownSeconds = .2f;
        [SerializeField] private bool faceJumpDirection = true;
        [SerializeField] private bool clearVelocityOnLanding = true;

        [Header("受击")]
        [SerializeField, Min(1f)] private float maxHealth = 9f;
        [SerializeField, Min(0f)] private float startingHealth = 9f;
        [SerializeField, Min(0f)] private float invulnerabilitySeconds = 4f;
        [SerializeField, Min(.01f)] private float invulnerabilityFlashInterval = .08f;

        [Header("接触伤害")]
        [SerializeField] private bool destroyOnlyWhileExternalMoving = true;
        [SerializeField, Min(0f)] private float lavaContactDamage = 1f;
        [SerializeField, Min(1f)] private float passThroughColliderHeight = 12f;
        [SerializeField] private bool killPlayerOnContact = true;

        [Header("死亡")]
        [SerializeField] private bool destroyAfterDeath;
        [SerializeField, Min(0f)] private float destroyDelaySeconds = 2f;
        [SerializeField]
        private BossDefeatPresentationEvent
            defeatPresentationRequested =
                new BossDefeatPresentationEvent();

        private Rigidbody body;
        private Collider bodyCollider;
        private float currentHealth;
        private float nextTargetRefreshTime;
        private float nextJumpTime;
        private int facingDirection = 1;
        private bool dead;
        private bool externalMotionActive;
        private Vector2 externalMotionVelocity;
        private bool externalMotionControlsVertical;
        private bool externalHeightLocked;
        private float externalHeightY;
        private bool executionControlled;
        private bool executionMoving;
        private float executionMoveSpeed;
        private float executionHeightY;
        private bool executionGravityWasEnabled;
        private bool deathKinematicStateCaptured;
        private bool deathPreviousKinematic;
        private bool presentationFallStateCaptured;
        private bool presentationFallPreviousKinematic;
        private bool presentationFallPreviousGravity;
        private RigidbodyInterpolation
            presentationFallPreviousInterpolation;
        private CollisionDetectionMode
            presentationFallPreviousCollisionMode;
        private float invulnerableUntil;
        private float invulnerabilityStartedAt;
        private bool healthSystemActive;
        private bool authoredColliderIsTrigger;
        private Vector3 authoredColliderSize;
        private bool authoredColliderSizeCaptured;
        private PlayerController sprintLockedPlayer;
        private IDisposable sprintLockHandle;
        private BossStateId state = BossStateId.Chase;
        private bool jumpActive;
        private float jumpElapsed;
        private float activeJumpDuration;
        private Vector3 jumpStartPosition;
        private Vector3 jumpTargetPosition;
        private readonly List<RendererFlashState> rendererFlashStates =
            new List<RendererFlashState>();

        private struct RendererFlashState
        {
            public Renderer renderer;
            public bool initiallyEnabled;
        }

        public event Action<BossStateId, BossStateId> StateChanged;
        public event Action<float, float> Damaged;
        public event Action Died;
        public event Action<BossController>
            DefeatPresentationRequested;
        public event Action HealthSystemActivated;
        public event Action<float, Vector3>
            RespawnStateApplied;
        public event Action ExecutionSequenceTerminated;

        public BossStateId State => state;
        public string BossDisplayName =>
            string.IsNullOrWhiteSpace(bossDisplayName)
                ? gameObject.name
                : bossDisplayName.Trim();
        public bool IsDead => dead;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public Transform Target => target;
        public bool AutonomousChaseEnabled => enableAutonomousChase;
        public bool TrackingEnabled => trackingEnabled;
        public bool PlayerContactKillEnabled =>
            killPlayerOnContact;
        public int FacingDirection => facingDirection;
        public float MoveSpeed => moveSpeed;
        public float WalkSpeed => walkSpeed;
        public Rigidbody Body => body;
        public bool IsJumping => jumpActive;
        public bool IsExternallyControlled => externalMotionActive;
        public bool IsExecutionControlled =>
            executionControlled;
        public bool IsGrounded { get; private set; }
        public bool IsInvulnerable =>
            !dead && Time.time < invulnerableUntil;
        public bool IsHealthSystemActive => healthSystemActive;
        public bool IsBossRespawnStateAvailable =>
            isActiveAndEnabled;
        public Vector3 WorldPosition => transform.position;
        public float InvulnerabilityRemaining =>
            Mathf.Max(0f, invulnerableUntil - Time.time);
        public Collider BodyCollider => bodyCollider;
        public BossDefeatPresentationEvent DefeatPresentation =>
            defeatPresentationRequested;
        public float ConfiguredJumpDuration => jumpDuration;
        public float ConfiguredJumpHorizontalSpeed =>
            jumpHorizontalSpeed;
        public float ConfiguredMaxJumpRiseHeight =>
            maxJumpRiseHeight;
        public float ConfiguredMaxJumpVerticalSpeed =>
            maxJumpVerticalSpeed;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();
            authoredColliderIsTrigger = bodyCollider.isTrigger;
            if (bodyCollider is BoxCollider boxCollider)
            {
                authoredColliderSize = boxCollider.size;
                authoredColliderSizeCaptured = true;
            }
            body.freezeRotation = true;
            body.constraints |= RigidbodyConstraints.FreezePositionZ;
            currentHealth = Mathf.Clamp(
                startingHealth <= 0f ? maxHealth : startingHealth,
                0f,
                maxHealth);
            if (!TryGetComponent(out BossMusicRequest _))
            {
                gameObject.AddComponent<BossMusicRequest>();
            }
            ResolveTarget(true);
            SetState(BossStateId.Chase);
        }

        private void Update()
        {
            UpdateInvulnerabilityFlash();
            if (dead)
            {
                return;
            }

            UpdateGrounded();
            if (trackingEnabled)
            {
                ResolveTarget(false);
            }

        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic || dead)
            {
                return;
            }

            if (executionControlled)
            {
                TickExecutionSequence();
                return;
            }

            Vector3 velocity = body.linearVelocity;
            if (jumpActive)
            {
                if (externalMotionActive)
                {
                    ApplyExternalMotion();
                }

                TickJump();
                return;
            }

            if (externalMotionActive)
            {
                ApplyExternalMotion();
                return;
            }

            if (!enableAutonomousChase ||
                state != BossStateId.Chase ||
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

        private void OnEnable()
        {
            AcquirePlayerSprintLock();
            BossTravelProgress.Register(this);
            if (healthSystemActive && !dead) BossTravelProgress.BeginFight(this);
            if (CompareTag("Boss"))
            {
                BossRespawnStateRegistry.Register(this);
            }
        }

        private void OnDisable()
        {
            ReleasePlayerSprintLock();
            BossTravelProgress.Unregister(this);
            BossRespawnStateRegistry.Unregister(this);
            CancelJump();
            RestoreInvulnerabilityRenderers();
        }

        public void SetTarget(Transform value)
        {
            target = value;
            AcquirePlayerSprintLock();
        }

        public void SetAutonomousChaseEnabled(bool value)
        {
            enableAutonomousChase = value;
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

        public void SetPlayerContactKillEnabled(
            bool value)
        {
            killPlayerOnContact = value;
        }

        public void BeginPresentationFall()
        {
            if (dead || body == null)
            {
                return;
            }

            presentationFallPreviousKinematic =
                body.isKinematic;
            presentationFallPreviousGravity =
                body.useGravity;
            presentationFallPreviousInterpolation =
                body.interpolation;
            presentationFallPreviousCollisionMode =
                body.collisionDetectionMode;
            presentationFallStateCaptured = true;
            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }
            body.isKinematic = true;
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation =
                RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
        }

        public void SetPresentationFallPosition(
            Vector3 worldPosition)
        {
            transform.position = worldPosition;
            if (body != null)
            {
                body.position = worldPosition;
            }
        }

        public void EndPresentationFall(
            Vector3 worldPosition)
        {
            SetPresentationFallPosition(worldPosition);
            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }
            if (body != null && presentationFallStateCaptured)
            {
                body.isKinematic =
                    presentationFallPreviousKinematic;
                body.useGravity =
                    presentationFallPreviousGravity;
                body.interpolation =
                    presentationFallPreviousInterpolation;
                body.collisionDetectionMode =
                    presentationFallPreviousCollisionMode;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            presentationFallStateCaptured = false;
            ClearExternalMotion();
            ClearExternalHeight();
        }

        public void SetExternalMove(
            float direction,
            float speedMultiplier = 1f)
        {
            float horizontal = Mathf.Clamp(
                direction,
                -1f,
                1f) * moveSpeed;
            horizontal *= Mathf.Max(0f, speedMultiplier);
            SetExternalMotion(
                new Vector2(horizontal, 0f),
                false);
        }

        public void SetWalkInput(float direction)
        {
            float horizontal = Mathf.Clamp(
                direction,
                -1f,
                1f) * walkSpeed;
            SetExternalMotion(
                new Vector2(horizontal, 0f),
                false);
        }

        public void ClearWalkInput()
        {
            ClearExternalMotion();
        }

        public void SetExternalClimb(float direction)
        {
            float vertical = Mathf.Clamp(
                direction,
                -1f,
                1f) * climbSpeed;
            SetExternalMotion(
                new Vector2(0f, vertical),
                true);
        }

        public void SetExternalHeight(float worldY)
        {
            externalHeightY = worldY;
            externalHeightLocked = true;
        }

        public void ClearExternalHeight()
        {
            externalHeightLocked = false;
        }

        public void BeginExecutionSequence(
            float moveSpeedBlocksPerSecond)
        {
            if (dead || body == null)
            {
                return;
            }

            executionControlled = true;
            executionMoving = false;
            executionMoveSpeed =
                Mathf.Max(0f, moveSpeedBlocksPerSecond) *
                GridCellSizeUtility.Resolve(this);
            executionHeightY = transform.position.y;
            executionGravityWasEnabled = body.useGravity;
            body.useGravity = false;
            SetPassThroughTerrain(true);
            Vector3 velocity = body.linearVelocity;
            velocity.x = 0f;
            velocity.y = 0f;
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        public void StartExecutionMovement()
        {
            if (executionControlled && !dead)
            {
                executionMoving = true;
            }
        }

        public void EndExecutionSequence()
        {
            if (!executionControlled)
            {
                return;
            }

            executionControlled = false;
            executionMoving = false;
            executionMoveSpeed = 0f;
            if (body != null)
            {
                body.useGravity = executionGravityWasEnabled;
            }
            SetPassThroughTerrain(false);
        }

        public void SetPassThroughTerrain(bool value)
        {
            if (bodyCollider != null)
            {
                if (authoredColliderSizeCaptured &&
                    bodyCollider is BoxCollider boxCollider)
                {
                    float height = value
                        ? Mathf.Max(
                            authoredColliderSize.y,
                            passThroughColliderHeight)
                        : authoredColliderSize.y;
                    Vector3 size = new Vector3(
                        authoredColliderSize.x,
                        height,
                        authoredColliderSize.z);
                    if (boxCollider.size != size)
                    {
                        boxCollider.size = size;
                    }
                }

                bool target = value || authoredColliderIsTrigger;
                if (bodyCollider.isTrigger != target)
                {
                    bodyCollider.isTrigger = target;
                }
            }
        }

        private void SetExternalMotion(
            Vector2 velocity,
            bool controlsVertical)
        {
            externalMotionActive = true;
            externalMotionVelocity = velocity;
            externalMotionControlsVertical = controlsVertical;
        }

        public void ClearExternalMotion()
        {
            if (!externalMotionActive)
            {
                return;
            }

            externalMotionActive = false;
            externalMotionVelocity = Vector2.zero;
            externalMotionControlsVertical = true;
        }

        private void ApplyExternalMotion()
        {
            if (body == null)
            {
                return;
            }

            Vector3 velocity = body.linearVelocity;
            velocity.x = externalMotionVelocity.x;
            if (externalMotionControlsVertical)
            {
                velocity.y = externalMotionVelocity.y;
            }
            else if (externalHeightLocked)
            {
                velocity.y = 0f;
            }

            velocity.z = 0f;
            body.linearVelocity = velocity;

            if (externalHeightLocked)
            {
                Vector3 position = body.position;
                position.y = externalHeightY;
                body.position = position;
            }
        }

        private void TickExecutionSequence()
        {
            Vector3 velocity = body.linearVelocity;
            velocity.x = executionMoving
                ? executionMoveSpeed
                : 0f;
            velocity.y = Time.fixedDeltaTime > 0f
                ? (executionHeightY - body.position.y) /
                  Time.fixedDeltaTime
                : 0f;
            velocity.z = 0f;
            body.linearVelocity = velocity;
        }

        private void UpdateGrounded()
        {
            if (body == null || bodyCollider == null)
            {
                IsGrounded = false;
                return;
            }

            Bounds bounds = bodyCollider.bounds;
            Vector3 origin = new Vector3(
                bounds.center.x,
                bounds.min.y + .02f,
                bounds.center.z);
            IsGrounded = Physics.Raycast(
                origin,
                Vector3.down,
                .22f,
                ~0,
                QueryTriggerInteraction.Ignore);
        }

        public bool RequestJump(
            Vector2 direction,
            float distance)
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
            return RequestJumpTo(target);
        }

        public bool RequestJump()
        {
            if (body == null)
            {
                return false;
            }

            return RequestJumpTo(body.position, true);
        }

        public bool RequestJumpTo(
            Vector3 worldTargetPosition,
            bool preserveExternalHorizontal = false)
        {
            if (dead ||
                jumpActive ||
                Time.time < nextJumpTime ||
                body == null ||
                body.isKinematic)
            {
                return false;
            }

            jumpStartPosition = body.position;
            Vector3 delta = worldTargetPosition - jumpStartPosition;
            delta.z = 0f;
            float horizontalDistance = preserveExternalHorizontal
                ? 0f
                : Mathf.Abs(delta.x);
            float speed = jumpHorizontalSpeed;
            float resolvedDuration = Mathf.Max(
                jumpDuration,
                horizontalDistance / Mathf.Max(.01f, speed));
            float horizontalSpeed = horizontalDistance /
                                    Mathf.Max(.01f, resolvedDuration);

            if (delta.y > maxJumpRiseHeight)
            {
                return false;
            }

            float gravity = Mathf.Max(
                .01f,
                -Physics.gravity.y);
            float verticalSpeed =
                (delta.y +
                 .5f * gravity * resolvedDuration * resolvedDuration) /
                resolvedDuration;
            if (Mathf.Abs(verticalSpeed) > maxJumpVerticalSpeed)
            {
                return false;
            }

            jumpTargetPosition = worldTargetPosition;
            activeJumpDuration = Mathf.Max(.05f, resolvedDuration);
            jumpElapsed = 0f;
            jumpActive = true;
            if (!preserveExternalHorizontal)
            {
                externalMotionActive = false;
                externalMotionVelocity = Vector2.zero;
            }

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            body.position = jumpStartPosition;
            transform.position = jumpStartPosition;
            body.WakeUp();
            body.linearVelocity = new Vector3(
                preserveExternalHorizontal
                    ? 0f
                    : Mathf.Sign(delta.x) * horizontalSpeed,
                verticalSpeed,
                0f);

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

            jumpActive = false;
            if (body != null)
            {
                body.WakeUp();
            }
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
                if (clearMotion && !body.isKinematic)
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

        public void ActivateHealthSystem()
        {
            if (healthSystemActive || dead)
            {
                return;
            }

            healthSystemActive = true;
            BossTravelProgress.BeginFight(this);
            HealthSystemActivated?.Invoke();
        }

        public void ApplyBossRespawnState(
            float health,
            Vector3 worldPosition)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            float nextHealth = Mathf.Clamp(
                health,
                0f,
                maxHealth);
            if (nextHealth <= 0f)
            {
                TeleportTo(
                    worldPosition,
                    transform.rotation);
                if (dead)
                {
                    currentHealth = 0f;
                }
                else
                {
                    EnterDeath(Vector3.zero);
                }

                RespawnStateApplied?.Invoke(
                    nextHealth,
                    worldPosition);
                return;
            }

            if (dead)
            {
                RestoreBodyAfterDeath();
                dead = false;
                invulnerableUntil = 0f;
                RestoreInvulnerabilityRenderers();
                CancelJump();
                SetState(BossStateId.Chase);
                AcquirePlayerSprintLock();
            }

            currentHealth = nextHealth;
            BossTravelProgress.BeginFight(this);
            if (!healthSystemActive)
            {
                ActivateHealthSystem();
            }

            if (externalHeightLocked)
            {
                externalHeightY = worldPosition.y;
            }

            TeleportTo(
                worldPosition,
                transform.rotation);
            RespawnStateApplied?.Invoke(
                nextHealth,
                worldPosition);
        }

        public void PrepareForBossRespawn()
        {
            ClearLavaInRespawnSweep();
        }

        public void ReceiveHit(float amount, Vector3 knockback)
        {
            if (executionControlled &&
                !dead &&
                amount > 0f)
            {
                currentHealth = 0f;
                Damaged?.Invoke(amount, currentHealth);
                ExecutionSequenceTerminated?.Invoke();
                EnterDeath(knockback);
                return;
            }

            if (!healthSystemActive ||
                dead ||
                amount <= 0f ||
                IsInvulnerable)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            Damaged?.Invoke(amount, currentHealth);
            if (currentHealth <= 0f)
            {
                EnterDeath(knockback);
                return;
            }

            BeginInvulnerability();
        }

        public void Kill()
        {
            EnterDeath(Vector3.zero);
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTouchedObject(other);
        }

        private void OnCollisionEnter(Collision collision)
        {
            HandleTouchedObject(collision != null ? collision.collider : null);
        }

        private void HandleTouchedObject(Collider other)
        {
            if (dead ||
                other == null ||
                other.transform.IsChildOf(transform))
            {
                return;
            }

            if (destroyOnlyWhileExternalMoving &&
                !externalMotionActive &&
                !executionControlled)
            {
                return;
            }

            PlayerController player =
                other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (killPlayerOnContact &&
                    player.isActiveAndEnabled &&
                    !player.IsDead &&
                    !player.IsRespawning)
                {
                    player.EnterDeath(
                        PlayerDeathCause.Boss,
                        this);
                }

                return;
            }

            if (other.GetComponentInParent<BossController>() != null)
            {
                return;
            }

            LavaHazardFeature lava =
                other.GetComponentInParent<LavaHazardFeature>();
            if (lava != null &&
                !IsInvulnerable &&
                lavaContactDamage > 0f)
            {
                ReceiveHit(lavaContactDamage, Vector3.zero);
            }
        }

        private void BeginInvulnerability()
        {
            if (invulnerabilitySeconds <= 0f)
            {
                invulnerableUntil = 0f;
                RestoreInvulnerabilityRenderers();
                return;
            }

            CacheInvulnerabilityRenderers();
            invulnerabilityStartedAt = Time.time;
            invulnerableUntil =
                Time.time + invulnerabilitySeconds;
        }

        private void CacheInvulnerabilityRenderers()
        {
            rendererFlashStates.Clear();
            Renderer[] renderers =
                GetComponentsInChildren<Renderer>(true);
            HashSet<int> seen = new HashSet<int>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null ||
                    !seen.Add(renderer.GetInstanceID()))
                {
                    continue;
                }

                rendererFlashStates.Add(
                    new RendererFlashState
                    {
                        renderer = renderer,
                        initiallyEnabled = renderer.enabled
                    });
            }
        }

        private void UpdateInvulnerabilityFlash()
        {
            if (!IsInvulnerable)
            {
                RestoreInvulnerabilityRenderers();
                return;
            }

            float interval =
                Mathf.Max(.01f, invulnerabilityFlashInterval);
            int phase = Mathf.FloorToInt(
                (Time.time - invulnerabilityStartedAt) /
                interval);
            bool visible = (phase & 1) == 0;
            for (int index = 0;
                 index < rendererFlashStates.Count;
                 index++)
            {
                RendererFlashState state =
                    rendererFlashStates[index];
                if (state.renderer != null)
                {
                    state.renderer.enabled =
                        state.initiallyEnabled && visible;
                }
            }
        }

        private void RestoreInvulnerabilityRenderers()
        {
            for (int index = 0;
                 index < rendererFlashStates.Count;
                 index++)
            {
                RendererFlashState state =
                    rendererFlashStates[index];
                if (state.renderer != null)
                {
                    state.renderer.enabled =
                        state.initiallyEnabled;
                }
            }
        }

        private void EnterDeath(Vector3 knockback)
        {
            if (dead)
            {
                return;
            }

            currentHealth = 0f;
            dead = true;
            ReleasePlayerSprintLock();
            EndExecutionSequence();
            invulnerableUntil = 0f;
            RestoreInvulnerabilityRenderers();
            CancelJump();
            SetState(BossStateId.Death);
            if (body != null)
            {
                deathPreviousKinematic =
                    body.isKinematic;
                deathKinematicStateCaptured = true;
                if (!body.isKinematic)
                {
                    body.linearVelocity =
                        knockback.sqrMagnitude > 0f
                            ? knockback
                            : Vector3.zero;
                    body.angularVelocity =
                        Vector3.zero;
                    body.isKinematic = true;
                }
            }

            BossTravelProgress.Defeat(this);
            Died?.Invoke();
            DefeatPresentationRequested?.Invoke(this);
            defeatPresentationRequested?.Invoke(this);
            if (destroyAfterDeath)
            {
                Destroy(gameObject, Mathf.Max(0f, destroyDelaySeconds));
            }
        }

        private void RestoreBodyAfterDeath()
        {
            if (body == null ||
                !deathKinematicStateCaptured)
            {
                return;
            }

            if (body.isKinematic !=
                deathPreviousKinematic)
            {
                body.isKinematic =
                    deathPreviousKinematic;
            }
            deathKinematicStateCaptured = false;
            body.WakeUp();
        }

        private void TickJump()
        {
            jumpElapsed += Time.fixedDeltaTime;
            if (jumpElapsed >= activeJumpDuration)
            {
                FinishJump();
            }
        }

        private void FinishJump()
        {
            jumpActive = false;
            nextJumpTime = Time.time + jumpCooldownSeconds;
            if (body != null && clearVelocityOnLanding)
            {
                Vector3 velocity = body.linearVelocity;
                velocity.x = 0f;
                velocity.y = Mathf.Min(0f, velocity.y);
                body.linearVelocity = velocity;
                body.angularVelocity = Vector3.zero;
            }

            if (!dead)
            {
                SetState(BossStateId.Chase);
            }
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
                AcquirePlayerSprintLock();
                return;
            }

            GameObject tagged = string.IsNullOrWhiteSpace(targetTag)
                ? null
                : GameObject.FindGameObjectWithTag(targetTag);
            target = tagged != null ? tagged.transform : null;
            AcquirePlayerSprintLock();
        }

        private void AcquirePlayerSprintLock()
        {
            if (dead)
            {
                ReleasePlayerSprintLock();
                return;
            }

            PlayerController player =
                target != null
                    ? target.GetComponentInParent<PlayerController>()
                    : null;
            player ??=
                ProjectDiscovery.FindFirst<PlayerController>();
            if (player != null &&
                player.gameObject.scene != gameObject.scene)
            {
                player = null;
            }
            if (player == null)
            {
                return;
            }

            if (ReferenceEquals(
                    player,
                    sprintLockedPlayer) &&
                sprintLockHandle != null)
            {
                return;
            }

            ReleasePlayerSprintLock();
            sprintLockedPlayer = player;
            sprintLockHandle =
                player.AcquireSprintLock(this);
        }

        private void ReleasePlayerSprintLock()
        {
            sprintLockHandle?.Dispose();
            sprintLockHandle = null;
            sprintLockedPlayer = null;
        }

        private void ClearLavaInRespawnSweep()
        {
            if (!Application.isPlaying ||
                bodyCollider == null)
            {
                return;
            }

            Bounds bossBounds = bodyCollider.bounds;
            float leftX = bossBounds.min.x;
            float rightX = transform.position.x;
            if (rightX < leftX)
            {
                (leftX, rightX) =
                    (rightX, leftX);
            }

            IReadOnlyList<LavaHazardFeature> hazards =
                ProjectDiscovery.FindAll<LavaHazardFeature>(true);
            for (int index = 0;
                 index < hazards.Count;
                 index++)
            {
                LavaHazardFeature lava = hazards[index];
                if (lava == null ||
                    lava.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                if (!TryGetWorldBounds(
                        lava.gameObject,
                        out Bounds lavaBounds) ||
                    lavaBounds.max.x < leftX ||
                    lavaBounds.min.x > rightX)
                {
                    continue;
                }

                lava.gameObject.SetActive(false);
                Destroy(lava.gameObject);
            }
        }

        private static bool TryGetWorldBounds(
            GameObject owner,
            out Bounds bounds)
        {
            Collider[] colliders =
                owner.GetComponentsInChildren<Collider>(true);
            bool found = false;
            bounds = default;
            for (int index = 0;
                 index < colliders.Length;
                 index++)
            {
                Collider collider = colliders[index];
                if (collider == null)
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
                    bounds.Encapsulate(
                        collider.bounds);
                }
            }

            if (!found)
            {
                bounds = new Bounds(
                    owner.transform.position,
                    Vector3.zero);
            }
            return true;
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
