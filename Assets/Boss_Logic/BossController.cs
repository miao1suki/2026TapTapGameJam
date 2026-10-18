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

        [Header("碰撞清除")]
        [SerializeField] private bool destroyTouchedObjects = true;
        [SerializeField] private bool destroyOnlyWhileExternalMoving = true;
        [SerializeField] private bool useRoomResetForDestroyedObjects = true;
        [SerializeField, Min(0f)] private float lavaContactDamage = 1f;
        [SerializeField, Min(1)] private int maxHiddenObjectsPerFrame = 4;
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
        private float invulnerableUntil;
        private float invulnerabilityStartedAt;
        private bool healthSystemActive;
        private bool authoredColliderIsTrigger;
        private Vector3 authoredColliderSize;
        private bool authoredColliderSizeCaptured;
        private BossStateId state = BossStateId.Chase;
        private bool jumpActive;
        private float jumpElapsed;
        private float activeJumpDuration;
        private Vector3 jumpStartPosition;
        private Vector3 jumpTargetPosition;
        private readonly List<RendererFlashState> rendererFlashStates =
            new List<RendererFlashState>();
        private readonly Queue<PendingHiddenObject> pendingHiddenObjects =
            new Queue<PendingHiddenObject>();
        private readonly HashSet<int> pendingHiddenObjectIds =
            new HashSet<int>();

        private struct RendererFlashState
        {
            public Renderer renderer;
            public bool initiallyEnabled;
        }

        private struct PendingHiddenObject
        {
            public GameObject target;
            public int id;
            public RoomResetObject roomObject;
            public int resetRevision;
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

        private void LateUpdate()
        {
            int budget = Mathf.Max(1, maxHiddenObjectsPerFrame);
            while (budget > 0 &&
                   pendingHiddenObjects.Count > 0)
            {
                PendingHiddenObject pending =
                    pendingHiddenObjects.Dequeue();
                budget--;
                pendingHiddenObjectIds.Remove(pending.id);
                GameObject target = pending.target;
                if (target == null)
                {
                    continue;
                }

                if (!target.activeInHierarchy)
                {
                    continue;
                }

                // A room reset can run between collision and this budgeted pass.
                // Never hide a restored door/platform using a pre-reset contact.
                if (pending.roomObject != null &&
                    pending.roomObject.ResetRevision != pending.resetRevision)
                    continue;

                HideTouchedObject(target);
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
            if (CompareTag("Boss"))
            {
                BossRespawnStateRegistry.Register(this);
            }
        }

        private void OnDisable()
        {
            BossRespawnStateRegistry.Unregister(this);
            CancelJump();
            RestoreInvulnerabilityRenderers();
            pendingHiddenObjects.Clear();
            pendingHiddenObjectIds.Clear();
        }

        public void SetTarget(Transform value)
        {
            target = value;
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
            }

            currentHealth = nextHealth;
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
                !destroyTouchedObjects ||
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
                    player.EnterDeath();
                }

                return;
            }

            if (other.GetComponentInParent<BossController>() != null)
            {
                return;
            }

            RoomResetObject reset =
                other.GetComponentInParent<RoomResetObject>();
            LavaHazardFeature lava =
                other.GetComponentInParent<LavaHazardFeature>();
            GameObject target = reset != null
                ? reset.gameObject
                : other.attachedRigidbody != null
                    ? other.attachedRigidbody.gameObject
                    : other.gameObject;

            if (lava != null &&
                !IsInvulnerable &&
                lavaContactDamage > 0f)
            {
                ReceiveHit(lavaContactDamage, Vector3.zero);
            }

            QueueTouchedObject(target);
        }

        private void QueueTouchedObject(GameObject target)
        {
            if (target == null ||
                !target.activeInHierarchy)
            {
                return;
            }

            int id = target.GetInstanceID();
            if (!pendingHiddenObjectIds.Add(id))
            {
                return;
            }

            RoomResetObject roomObject = target.GetComponent<RoomResetObject>();
            pendingHiddenObjects.Enqueue(
                new PendingHiddenObject
                {
                    target = target,
                    id = id,
                    roomObject = roomObject,
                    resetRevision = roomObject != null ? roomObject.ResetRevision : 0
                });
        }

        private void HideTouchedObject(GameObject target)
        {
            if (useRoomResetForDestroyedObjects)
            {
                TemporaryObjectHideState state =
                    target.GetComponent<TemporaryObjectHideState>();
                if (state == null)
                {
                    state = target.AddComponent<
                        TemporaryObjectHideState>();
                }

                state.Hide();
                return;
            }

            target.SetActive(false);
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
            EndExecutionSequence();
            invulnerableUntil = 0f;
            pendingHiddenObjects.Clear();
            pendingHiddenObjectIds.Clear();
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
