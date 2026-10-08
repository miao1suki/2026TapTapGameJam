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
        [InspectorName("按钮操控")] SignalControlled = 1
    }

    public enum PlatformSignalLogic
    {
        [InspectorName("任一激活")] Any = 0,
        [InspectorName("全部激活")] All = 1
    }

    [BlockFeature(
        DisplayName = "自动移动平台",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Movement,
        Interactions = BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Motion })]
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
            MovingPlatformDriveMode.Automatic;

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
            EvaluateLinkedControls();
        }

        protected override void OnDetach()
        {
            UnsubscribeControls();
            carriedPlayers.Clear();
            RestoreOrigin();
        }

        protected override void OnTick(float deltaTime)
        {
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
                travelDirection *= -1;
                return;
            }

            transform.localPosition = desired;
            CarryPlayers(delta);
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
                    hit.collider.transform.root == transform.root ||
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
            if (driveMode != MovingPlatformDriveMode.SignalControlled ||
                linkedControls == null ||
                linkedControls.Length == 0)
            {
                return;
            }

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
