using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum MovingPlatformRoute
    {
        [InspectorName("平面游荡")] Horizontal = 0,
        [InspectorName("上下游荡")] Vertical = 1
    }

    public enum MovingPlatformDriveMode
    {
        [InspectorName("自动游荡")] Automatic = 0,
        [InspectorName("信号驱动")] SignalControlled = 1,
        [InspectorName("曲柄联动")] CrankControlled = 2
    }

    public enum PlatformSignalLogic
    {
        [InspectorName("任一激活")] Any = 0,
        [InspectorName("全部激活")] All = 1
    }

    [BlockFeature(
        DisplayName = "移动平台",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Movement,
        Interactions = BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Motion })]
    [DefaultExecutionOrder(-150)]
    public sealed class MovingPlatformFeature : BlockFeature
    {
        [BlockParameter(
            Label = "路线类型",
            Group = "移动路线",
            Order = 0)]
        [SerializeField] private MovingPlatformRoute route =
            MovingPlatformRoute.Horizontal;

        [BlockParameter(
            Label = "游荡距离（格）",
            Group = "移动路线",
            Order = 1)]
        [SerializeField, Min(1)] private int travelBlocks = 4;

        [BlockParameter(
            Label = "单程时间",
            Group = "移动设置",
            Order = 0)]
        [SerializeField, Min(.05f)] private float travelDuration = 2f;

        [BlockParameter(
            Label = "端点停留",
            Group = "移动设置",
            Order = 1)]
        [SerializeField, Min(0f)] private float holdAtEnds = .25f;

        [BlockParameter(
            Label = "移动曲线",
            Group = "移动设置",
            Order = 2)]
        [SerializeField] private AnimationCurve easing =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [BlockParameter(
            Label = "碰到方块后反向",
            Group = "移动设置",
            Order = 3,
            Tooltip = "移动方向前方碰到非玩家碰撞体时，平台立即掉头。")]
        [SerializeField] private bool reverseOnBlock = true;

        [BlockParameter(
            Label = "驱动方式",
            Group = "控制设置",
            Order = 0)]
        [SerializeField] private MovingPlatformDriveMode driveMode =
            MovingPlatformDriveMode.SignalControlled;

        [BlockParameter(Label = "控制曲柄", Group = "控制目标", Order = 0,
            Tooltip = "多个平台可绑定同一个曲柄，按各自路线和距离同步升降。",
            VisibleWhenField = "driveMode", VisibleWhenValue = (int)MovingPlatformDriveMode.CrankControlled)]
        [SerializeField] private CrankPlatformFeature crankController;
        public CrankPlatformFeature CrankController => crankController;
        public bool IsCrankControlled => driveMode == MovingPlatformDriveMode.CrankControlled;
        public bool IsDriveAvailable => CanRunFeature;
        public bool HasStandingPlayer { get { RefreshStandingPlayers(); return carriedPlayers.Count > 0; } }
        public void BindCrank(CrankPlatformFeature source)
        {
            if (crankController != null) crankController.UnregisterPlatform(this);
            UnsubscribeControls();
            driveMode = MovingPlatformDriveMode.CrankControlled;
            linkedControls = System.Array.Empty<MonoBehaviour>();
            crankController = source;
            if (Application.isPlaying && source != null) source.RegisterPlatform(this);
        }
        public void SnapToCrankProgress()
        {
            if (IsAttached && IsCrankControlled && crankController != null)
                transform.localPosition = origin + GetOffset() * crankController.Progress;
        }

        [BlockParameter(
            Label = "初始启动",
            Group = "控制设置",
            Order = 1,
            VisibleWhenField = "driveMode",
            VisibleWhenValue = (int)MovingPlatformDriveMode.Automatic)]
        [SerializeField] private bool startsActive = true;

        [BlockParameter(
            Label = "信号来源（基座/按钮/曲柄）",
            Group = "控制目标",
            Order = 0,
            Tooltip = "平台统一订阅这些 IMechanismSignalSource；基座、按钮和曲柄都从平台这一侧绑定。",
            VisibleWhenField = "driveMode",
            VisibleWhenValue = (int)MovingPlatformDriveMode.SignalControlled)]
        [SerializeField] private MonoBehaviour[] linkedControls =
            System.Array.Empty<MonoBehaviour>();

        [BlockParameter(
            Label = "可承载标签",
            Group = "载人设置",
            Order = 0)]
        [SerializeField] private string carryPlayerTag = "Player";

        [BlockParameter(
            Label = "激活规则",
            Group = "控制目标",
            Order = 1,
            VisibleWhenField = "driveMode",
            VisibleWhenValue = (int)MovingPlatformDriveMode.SignalControlled)]
        [SerializeField] private PlatformSignalLogic signalLogic =
            PlatformSignalLogic.Any;

        private readonly HashSet<PlayerController> carriedPlayers =
            new HashSet<PlayerController>();
        private readonly RaycastHit[] platformHitBuffer =
            new RaycastHit[16];
        private Vector3 origin;
        private bool active;
        private float progress;
        private int travelDirection = 1;
        private float holdRemaining;
        public bool IsMoving => CanRunFeature && active;
        public Vector3 PreviewStart => Application.isPlaying ? transform.parent != null ? transform.parent.TransformPoint(origin) : origin : transform.position;
        public Vector3 PreviewEnd => PreviewStart + (transform.parent != null ? transform.parent.TransformVector(GetOffset()) : GetOffset());
        public IReadOnlyList<MonoBehaviour> LinkedControls => linkedControls;
        public void BindBase(MechanismBaseFeature source)
        {
            if (crankController != null) crankController.UnregisterPlatform(this);
            crankController = null;
            UnsubscribeControls();
            driveMode = MovingPlatformDriveMode.SignalControlled;
            linkedControls = source != null ? new MonoBehaviour[] { source } : System.Array.Empty<MonoBehaviour>();
            if (Application.isPlaying) { SubscribeControls(); EvaluateLinkedControls(); }
        }

        protected override void OnAttach()
        {
            origin = transform.localPosition;
            progress = 0f;
            travelDirection = 1;
            holdRemaining = 0f;
            active = driveMode == MovingPlatformDriveMode.Automatic &&
                     startsActive;
            carriedPlayers.Clear();
            SubscribeControls();
            if (IsCrankControlled && crankController != null) crankController.RegisterPlatform(this);
            EvaluateLinkedControls();
        }

        protected override void OnDetach()
        {
            UnsubscribeControls();
            if (crankController != null) crankController.UnregisterPlatform(this);
            carriedPlayers.Clear();
            RestoreOrigin();
        }

        protected override void OnTick(float deltaTime)
        {
            EvaluateLinkedControls();
        }

        private void FixedUpdate()
        {
            if (CanRunFeature) TickMotion(Time.fixedDeltaTime);
        }

        private void TickMotion(float deltaTime)
        {
            if (IsCrankControlled)
            {
                active = crankController != null && crankController.IsDriveAvailable;
                if (!active) return;
                Vector3 desiredPosition = origin + GetOffset() * crankController.Progress;
                Vector3 motion = desiredPosition - transform.localPosition;
                RefreshStandingPlayers();
                transform.localPosition = desiredPosition;
                CarryPlayers(transform.parent != null ? transform.parent.TransformVector(motion) : motion);
                return;
            }
            EvaluateLinkedControls();
            if (!active)
            {
                return;
            }

            if (holdRemaining > 0f)
            {
                holdRemaining = Mathf.Max(
                    0f,
                    holdRemaining - deltaTime);
                return;
            }

            float travel = Mathf.Max(.05f, travelDuration);
            float hold = Mathf.Max(0f, holdAtEnds);
            float previousProgress = progress;
            progress += travelDirection *
                        (deltaTime / travel);
            if (progress >= 1f)
            {
                progress = 1f;
                travelDirection = -1;
                holdRemaining = hold;
            }
            else if (progress <= 0f)
            {
                progress = 0f;
                travelDirection = 1;
                holdRemaining = hold;
            }

            float curved = easing != null &&
                           easing.length > 0
                ? easing.Evaluate(Mathf.Clamp01(progress))
                : progress;
            Vector3 before = transform.localPosition;
            Vector3 desired =
                origin + GetOffset() * curved;
            Vector3 delta = desired - before;
            if (reverseOnBlock && IsBlockedAlong(delta))
            {
                progress = previousProgress;
                holdRemaining = 0f;
                travelDirection *= -1;
                return;
            }

            RefreshStandingPlayers();
            transform.localPosition = desired;
            CarryPlayers(transform.parent != null ? transform.parent.TransformVector(delta) : delta);
        }

        protected override void OnResetForRoom()
        {
            progress = 0f;
            travelDirection = 1;
            holdRemaining = 0f;
            active = driveMode == MovingPlatformDriveMode.Automatic &&
                     startsActive;
            carriedPlayers.Clear();
            RestoreOrigin();
            EvaluateLinkedControls();
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "运行状态",
                active ? "移动中" : "已停止"));
            values.Add(new BlockDebugValue(
                "路线",
                route == MovingPlatformRoute.Horizontal
                    ? "平面游荡"
                    : "上下游荡"));
            values.Add(new BlockDebugValue(
                "承载玩家",
                carriedPlayers.Count));
            values.Add(new BlockDebugValue(
                "开关规则",
                signalLogic == PlatformSignalLogic.Any
                    ? "任一激活"
                    : "全部激活"));
            values.Add(new BlockDebugValue(
                "碰到方块反向",
                reverseOnBlock ? "开启" : "关闭"));
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!CanRunFeature)
            {
                return;
            }

            PlayerController player = ResolveTopPlayer(collision);
            if (player == null)
            {
                return;
            }

            carriedPlayers.Add(player);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (!CanRunFeature || collision == null)
            {
                return;
            }

            PlayerController player =
                collision.collider.GetComponentInParent<
                    PlayerController>();
            if (player == null ||
                !player.gameObject.CompareTag(carryPlayerTag))
            {
                return;
            }

            if (IsTopContact(collision, player))
            {
                carriedPlayers.Add(player);
            }
            else
            {
                carriedPlayers.Remove(player);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            PlayerController player =
                collision.collider.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                carriedPlayers.Remove(player);
            }
        }

        private Vector3 GetOffset()
        {
            float cellSize = GridCellWorldSize;
            return route == MovingPlatformRoute.Horizontal
                ? Vector3.right * travelBlocks * cellSize
                : Vector3.up * travelBlocks * cellSize;
        }

        private bool IsBlockedAlong(Vector3 delta)
        {
            if (delta.sqrMagnitude <= .000001f)
            {
                return false;
            }

            Collider platformCollider = GetComponent<Collider>();
            if (platformCollider == null)
            {
                return false;
            }

            Bounds bounds = platformCollider.bounds;
            int count = Physics.BoxCastNonAlloc(
                bounds.center,
                bounds.extents * .96f,
                delta.normalized,
                platformHitBuffer,
                transform.rotation,
                delta.magnitude,
                ~0,
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = platformHitBuffer[index];
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    IsCarriedPlayer(hit.collider))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private bool IsCarriedPlayer(Collider collider)
        {
            PlayerController player =
                collider.GetComponentInParent<PlayerController>();
            return player != null &&
                   player.gameObject.CompareTag(carryPlayerTag);
        }

        private void CarryPlayers(Vector3 delta)
        {
            if (delta.sqrMagnitude <= .000001f)
            {
                return;
            }

            foreach (PlayerController player in carriedPlayers)
            {
                if (player == null)
                {
                    continue;
                }

                player.QueuePlatformDelta(delta);
            }
        }

        private void RefreshStandingPlayers()
        {
            carriedPlayers.Clear();
            Collider surface = GetComponent<Collider>();
            if (surface == null || !surface.enabled || surface.isTrigger) return;
            Bounds top = surface.bounds;
            float tolerance = GridCellWorldSize * .12f;
            foreach (PlayerController player in PlayerController.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene || player.VerticalVelocity > .1f || !player.CompareTag(carryPlayerTag)) continue;
                Collider body = player.GetComponent<Collider>();
                if (body == null || !body.enabled || Physics.GetIgnoreCollision(surface, body)) continue;
                Bounds bounds = body.bounds;
                Vector3 physicsOffset = player.Motor.position - player.transform.position;
                float feet = bounds.min.y + physicsOffset.y;
                Vector3 center = bounds.center + physicsOffset;
                if (Mathf.Abs(feet - top.max.y) <= tolerance && center.x >= top.min.x - tolerance && center.x <= top.max.x + tolerance &&
                    center.z >= top.min.z - tolerance && center.z <= top.max.z + tolerance) carriedPlayers.Add(player);
            }
        }

        private PlayerController ResolveTopPlayer(
            Collision collision)
        {
            if (collision == null)
            {
                return null;
            }

            PlayerController player =
                collision.collider.GetComponentInParent<
                    PlayerController>();
            return player != null &&
                   player.gameObject.CompareTag(carryPlayerTag) &&
                   player.IsGrounded &&
                   IsTopContact(collision, player)
                ? player
                : null;
        }

        private bool IsTopContact(
            Collision collision,
            PlayerController player)
        {
            Collider platformCollider = GetComponent<Collider>();
            Collider playerCollider =
                player.GetComponent<Collider>();
            if (platformCollider != null &&
                playerCollider != null)
            {
                Bounds platformBounds =
                    platformCollider.bounds;
                Bounds playerBounds = playerCollider.bounds;
                float tolerance = Mathf.Max(
                    .08f,
                    GridCellWorldSize * .12f);
                bool feetOnTop =
                    playerBounds.min.y >=
                    platformBounds.max.y - tolerance &&
                    playerBounds.min.y <=
                    platformBounds.max.y + tolerance;
                bool horizontallyOverPlatform =
                    playerBounds.center.x >=
                    platformBounds.min.x - tolerance &&
                    playerBounds.center.x <=
                    platformBounds.max.x + tolerance &&
                    playerBounds.center.z >=
                    platformBounds.min.z - tolerance &&
                    playerBounds.center.z <=
                    platformBounds.max.z + tolerance;
                if (feetOnTop && horizontallyOverPlatform)
                {
                    return true;
                }
            }

            ContactPoint[] contacts = collision.contacts;
            for (int index = 0;
                 index < contacts.Length;
                 index++)
            {
                if (contacts[index].normal.y > .8f)
                {
                    return true;
                }
            }

            return false;
        }

        private void SubscribeControls()
        {
            if (linkedControls == null)
            {
                return;
            }

            for (int index = 0;
                 index < linkedControls.Length;
                 index++)
            {
                if (linkedControls[index] is
                    IMechanismSignalSource source)
                {
                    source.SignalChanged += OnControlSignalChanged;
                }
            }
        }

        private void UnsubscribeControls()
        {
            if (linkedControls == null)
            {
                return;
            }

            for (int index = 0;
                 index < linkedControls.Length;
                 index++)
            {
                if (linkedControls[index] is
                    IMechanismSignalSource source)
                {
                    source.SignalChanged -= OnControlSignalChanged;
                }
            }
        }

        private void OnControlSignalChanged(bool activeValue)
        {
            EvaluateLinkedControls();
        }

        private void EvaluateLinkedControls()
        {
            if (driveMode != MovingPlatformDriveMode.SignalControlled)
            {
                return;
            }

            if (linkedControls == null || linkedControls.Length == 0) { active = false; return; }

            int activeCount = 0;
            int sourceCount = 0;
            for (int index = 0;
                 index < linkedControls.Length;
                 index++)
            {
                if (!(linkedControls[index] is
                      IMechanismSignalSource source))
                {
                    continue;
                }

                sourceCount++;
                if (source.IsSignalActive)
                {
                    activeCount++;
                }
            }

            active = signalLogic == PlatformSignalLogic.Any
                ? activeCount > 0
                : sourceCount > 0 && activeCount == sourceCount;
        }

        private void RestoreOrigin()
        {
            transform.localPosition = origin;
        }
    }
}
