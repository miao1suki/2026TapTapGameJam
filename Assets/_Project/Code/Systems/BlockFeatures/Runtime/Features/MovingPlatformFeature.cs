using System.Collections.Generic;
using Project.Player;
using Project.Audio;
using Project.Mechanisms;
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

    public enum MovingPlatformSignalMotion
    {
        [InspectorName("充能时往返，断电停留")] PoweredPingPong = 0,
        [InspectorName("充能到终点，断电回原位")] PoweredEndpoint = 1
    }

    public enum MovingPlatformTimelineTrigger
    {
        [InspectorName("关闭")] Disabled = 0,
        [InspectorName("收到充能时")] Powered = 1,
        [InspectorName("开始移动时")] MovementStarted = 2,
        [InspectorName("到达路径端点时")] EndpointReached = 3
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
        [BlockParameter(Label = "路线配置", Group = "移动路线", Order = -1,
            Tooltip = "单向路线从布置位置出发后往返；双侧路线可分别设置两端距离。原路线保留旧存档配置。")]
        [SerializeField] private MovingPlatformPathMode pathMode;

        [BlockParameter(
            Label = "路线类型",
            Group = "移动路线",
            Order = 0, VisibleWhenField = "pathMode", VisibleWhenValue = 0)]
        [SerializeField] private MovingPlatformRoute route =
            MovingPlatformRoute.Horizontal;

        [BlockParameter(
            Label = "游荡距离（格）",
            Group = "移动路线",
            Order = 1, VisibleWhenField = "pathMode", VisibleWhenValue = 0)]
        [SerializeField, Min(1)] private int travelBlocks = 4;

        [BlockParameter(
            Label = "反向路线",
            Group = "移动路线",
            Order = 2,
            VisibleWhenField = "pathMode", VisibleWhenValue = 0,
            Tooltip = "关闭时向右或向上移动；开启时向左或向下移动。作者放置的位置始终为起点。")]
        [SerializeField] private bool reverseRoute;

        [BlockParameter(Label = "左距离（格）", Group = "移动路线", Order = 3,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 2, 5 },
            Tooltip = "向左路线的总距离，或移动中心至左端的距离；0表示不向该侧移动。")]
        [SerializeField, Min(0f)] private float leftDistance = 4f;
        [BlockParameter(Label = "右距离（格）", Group = "移动路线", Order = 4,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 1, 5 },
            Tooltip = "向右路线的总距离，或移动中心至右端的距离；0表示不向该侧移动。")]
        [SerializeField, Min(0f)] private float rightDistance = 4f;
        [BlockParameter(Label = "下距离（格）", Group = "移动路线", Order = 5,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 4, 6 },
            Tooltip = "向下路线的总距离，或移动中心至下端的距离；0表示不向该侧移动。")]
        [SerializeField, Min(0f)] private float downDistance = 4f;
        [BlockParameter(Label = "上距离（格）", Group = "移动路线", Order = 6,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 3, 6 },
            Tooltip = "向上路线的总距离，或移动中心至上端的距离；0表示不向该侧移动。")]
        [SerializeField, Min(0f)] private float upDistance = 4f;
        [BlockParameter(Label = "中心偏移（格）", Group = "移动路线", Order = 7,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 5, 6 },
            Tooltip = "相对布置位置沿路线偏移中心，正数向右/上，负数向左/下。自动限制在两侧距离内，保证初始位置在路线上且不瞬移。")]
        [SerializeField] private float centerOffset;
        [BlockParameter(Label = "首次向左 / 向下", Group = "移动路线", Order = 8,
            VisibleWhenField = "pathMode", VisibleWhenValues = new[] { 5, 6 },
            Tooltip = "决定首次前往哪一端。曲柄0%保留布置位置，100%到达该方向端点，松开沿原路返回。")]
        [SerializeField] private bool startNegative;

        public MovingPlatformPath Path => MovingPlatformPath.Resolve(pathMode, route, travelBlocks, reverseRoute,
            leftDistance, rightDistance, downDistance, upDistance, centerOffset, startNegative);

        [BlockParameter(
            Label = "单程时间",
            Group = "移动设置",
            Order = 0)]
        [SerializeField, Min(.05f)] private float travelDuration = 2f;

        [BlockParameter(
            Label = "端点停留",
            Group = "移动设置",
            Order = 1, Tooltip = "往返模式到端点后的等待时间；充能到终点模式不使用此时间，断电立即开始回程。")]
        [SerializeField, Min(0f)] private float holdAtEnds = .25f;

        [BlockParameter(
            Label = "移动曲线",
            Group = "移动设置",
            Order = 2)]
        [SerializeField] private AnimationCurve easing =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [BlockParameter(
            Label = "遇实体阻挡检测",
            Group = "移动设置",
            Order = 3,
            Tooltip = "检测前方非玩家实体：往返模式遇障掉头；充能到终点模式遇障等待，障碍清除后继续向目标移动。")]
        [SerializeField] private bool reverseOnBlock = true;

        [BlockParameter(
            Label = "驱动方式",
            Group = "控制设置",
            Order = 0)]
        [SerializeField] private MovingPlatformDriveMode driveMode =
            MovingPlatformDriveMode.SignalControlled;

        [BlockParameter(Label = "充能移动模式", Group = "控制设置", Order = 2,
            VisibleWhenField = "driveMode", VisibleWhenValue = (int)MovingPlatformDriveMode.SignalControlled,
            Tooltip = "往返模式断电停在当前位置；终点模式充能时到首程端点并停留，断电沿原路回到布置位置。曲柄仍按自身进度联动。")]
        [SerializeField] private MovingPlatformSignalMotion signalMotion;
        public MovingPlatformSignalMotion SignalMotion => signalMotion;

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

        [BlockParameter(Label = "Timeline触发", Group = "可选演出", Order = -2,
            Tooltip = "默认关闭。复用门的相机Timeline，聚焦本平台；开始移动/到端点按状态边沿触发，不会每个物理步重复。")]
        [SerializeField] private MovingPlatformTimelineTrigger timelineTrigger;
        [BlockParameter(Label = "演出锁定玩家", Group = "可选演出", Order = -1,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2, 3 })]
        [SerializeField] private bool lockPlayerDuringTimeline = true;
        [BlockParameter(Label = "镜头移动时间", Group = "可选演出", Order = 0,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2, 3 })]
        [SerializeField, Min(.1f)] private float cameraMoveDuration = 1f;
        [BlockParameter(Label = "镜头停留时间", Group = "可选演出", Order = 1,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2, 3 })]
        [SerializeField, Min(0f)] private float cameraHoldDuration = 1.2f;
        [BlockParameter(Label = "镜头返回时间", Group = "可选演出", Order = 2,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2, 3 })]
        [SerializeField, Min(0f)] private float cameraReturnDuration = .7f;
        private MechanismCameraShot cameraShot;
        private bool previousTimelinePower, previousTimelineMoving, reachedEndpoint;
        public MovingPlatformTimelineTrigger TimelineTrigger => timelineTrigger;
        public bool IsTimelinePlaying => cameraShot != null && cameraShot.IsPlaying;

        private readonly HashSet<PlayerController> carriedPlayers =
            new HashSet<PlayerController>();
        private readonly RaycastHit[] platformHitBuffer =
            new RaycastHit[16];
        private Vector3 origin;
        private bool active;
        private float progress;
        private int travelDirection = 1;
        private float legStart, legEnd;
        private float holdRemaining;
        private bool movementAudioPlaying;
        private Collider[] platformSurfaces;
        private bool UsesPoweredEndpoint => driveMode == MovingPlatformDriveMode.SignalControlled &&
            signalMotion == MovingPlatformSignalMotion.PoweredEndpoint;
        public bool IsMoving => CanRunFeature && (UsesPoweredEndpoint
            ? Mathf.Abs(Vector3.Dot(transform.localPosition - origin, Path.Axis) / GridCellWorldSize - (active ? Path.FirstEnd : 0f)) > .00001f
            : active);
        public Vector3 PreviewOrigin => Application.isPlaying && IsAttached
            ? transform.parent != null ? transform.parent.TransformPoint(origin) : origin : transform.position;
        public Vector3 PreviewStart => PreviewOrigin + PathWorldOffset(Path.PreviewFrom);
        public Vector3 PreviewEnd => PreviewOrigin + PathWorldOffset(Path.PreviewTo);
        public Vector3 PreviewCenter => PreviewOrigin + PathWorldOffset(Path.Axis * Path.Center);
        public Vector3 PreviewFirstTarget => PreviewOrigin + PathWorldOffset(Path.Axis * Path.FirstEnd);
        private Vector3 PathWorldOffset(Vector2 cells) => transform.parent != null
            ? transform.parent.TransformVector((Vector3)cells * PreviewCellSize) : (Vector3)cells * PreviewCellSize;
        private float PreviewCellSize => Application.isPlaying && IsAttached ? GridCellWorldSize : GridCellSizeUtility.Resolve(this);
        public IReadOnlyList<MonoBehaviour> LinkedControls => linkedControls;
        public void BindSignalSources(
            IReadOnlyList<MonoBehaviour> sources)
        {
            if (crankController != null) crankController.UnregisterPlatform(this);
            crankController = null;
            UnsubscribeControls();
            driveMode = MovingPlatformDriveMode.SignalControlled;
            if (sources == null)
            {
                linkedControls = System.Array.Empty<MonoBehaviour>();
            }
            else
            {
                var next = new List<MonoBehaviour>();
                for (int index = 0; index < sources.Count; index++)
                {
                    MonoBehaviour source = sources[index];
                    if (source != null &&
                        source is IMechanismSignalSource &&
                        !next.Contains(source))
                    {
                        next.Add(source);
                    }
                }

                linkedControls = next.ToArray();
            }

            if (Application.isPlaying)
            {
                SubscribeControls();
                EvaluateLinkedControls();
            }
        }

        public void BindBase(MechanismBaseFeature source)
        {
            BindSignalSources(source != null
                ? new MonoBehaviour[] { source }
                : System.Array.Empty<MonoBehaviour>());
        }

        protected override void OnAttach()
        {
            ResetTimeline();
            platformSurfaces = GetComponents<Collider>();
            origin = transform.localPosition;
            ResetMotion();
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
            ResetTimeline();
            SetMovementAudio(false);
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
            Vector3 before = transform.position;
            reachedEndpoint = false;
            if (CanRunFeature) TickMotion(Time.fixedDeltaTime);
            bool moved = CanRunFeature && (transform.position - before).sqrMagnitude > .00000001f;
            SetMovementAudio(moved);
            TickTimeline(moved);
        }

        private void TickTimeline(bool moved)
        {
            bool powered = CanRunFeature && driveMode == MovingPlatformDriveMode.SignalControlled && active;
            if (timelineTrigger == MovingPlatformTimelineTrigger.Disabled || !CanRunFeature)
            {
                if (IsTimelinePlaying) ResetTimeline();
            }
            else if ((timelineTrigger == MovingPlatformTimelineTrigger.Powered && powered && !previousTimelinePower) ||
                (timelineTrigger == MovingPlatformTimelineTrigger.MovementStarted && moved && !previousTimelineMoving) ||
                (timelineTrigger == MovingPlatformTimelineTrigger.EndpointReached && reachedEndpoint))
            {
                cameraShot ??= new MechanismCameraShot(this);
                cameraShot.TryPlay(null, Vector3.zero, cameraMoveDuration, cameraHoldDuration,
                    cameraReturnDuration, lockPlayerDuringTimeline);
            }
            previousTimelinePower = powered;
            previousTimelineMoving = moved;
        }

        private void ResetTimeline()
        {
            cameraShot?.Dispose();
            cameraShot = null;
            previousTimelinePower = previousTimelineMoving = reachedEndpoint = false;
        }

        private void SetMovementAudio(bool moving)
        {
            if (movementAudioPlaying == moving) return;
            movementAudioPlaying = moving && AudioCuePlayer.PlayLoop(AudioCueIds.PlatformMoveLoop, this, .65f);
            if (!moving) AudioManager.Existing?.StopLoop(this);
        }

        private void TickMotion(float deltaTime)
        {
            if (IsCrankControlled)
            {
                active = crankController != null && crankController.IsDriveAvailable;
                if (!active) return;
                Vector3 desiredPosition = origin + GetOffset() * crankController.Progress;
                Vector3 motion = desiredPosition - transform.localPosition;
                if (motion.sqrMagnitude > .00000001f &&
                    (crankController.Progress <= .00001f || crankController.Progress >= .99999f)) reachedEndpoint = true;
                RefreshStandingPlayers();
                transform.localPosition = desiredPosition;
                CarryPlayers(transform.parent != null ? transform.parent.TransformVector(motion) : motion);
                return;
            }
            EvaluateLinkedControls();
            if (UsesPoweredEndpoint)
            {
                TickPoweredEndpoint(deltaTime);
                return;
            }
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

            MovingPlatformPath path = Path;
            if (path.Span <= .00001f) return;
            float travel = Mathf.Max(.05f, travelDuration) * Mathf.Abs(legEnd - legStart) / path.Span;
            float hold = Mathf.Max(0f, holdAtEnds);
            progress = Mathf.Min(1f, progress + deltaTime / Mathf.Max(.0001f, travel));

            float curved = easing != null &&
                           easing.length > 0
                ? Mathf.Clamp01(easing.Evaluate(progress))
                : progress;
            if (pathMode == MovingPlatformPathMode.Legacy && legEnd == 0f && easing != null && easing.length > 0)
                curved = 1f - Mathf.Clamp01(easing.Evaluate(1f - progress));
            Vector3 before = transform.localPosition;
            Vector3 desired =
                origin + (Vector3)path.Axis * (Mathf.Lerp(legStart, legEnd, curved) * GridCellWorldSize);
            Vector3 delta = desired - before;
            if (reverseOnBlock && IsBlockedAlong(delta))
            {
                holdRemaining = 0f;
                travelDirection *= -1;
                legStart = Vector3.Dot(before - origin, path.Axis) / GridCellWorldSize;
                legEnd = travelDirection > 0 ? path.Maximum : path.Minimum;
                progress = 0f;
                return;
            }

            RefreshStandingPlayers();
            transform.localPosition = desired;
            CarryPlayers(transform.parent != null ? transform.parent.TransformVector(delta) : delta);
            if (progress >= 1f)
            {
                reachedEndpoint = true;
                legStart = legEnd;
                travelDirection *= -1;
                legEnd = travelDirection > 0 ? path.Maximum : path.Minimum;
                progress = 0f;
                holdRemaining = hold;
            }
        }

        private void TickPoweredEndpoint(float deltaTime)
        {
            MovingPlatformPath path = Path;
            float target = active ? path.FirstEnd : 0f;
            float current = Vector3.Dot(transform.localPosition - origin, path.Axis) / GridCellWorldSize;
            if (Mathf.Abs(current - target) < .00001f) return;
            if (!Mathf.Approximately(legEnd, target))
            {
                legStart = current;
                legEnd = target;
                progress = 0f;
            }
            float duration = Mathf.Max(.05f, travelDuration) * Mathf.Abs(legEnd - legStart) / Mathf.Max(.00001f, path.Span);
            float nextProgress = Mathf.Min(1f, progress + deltaTime / Mathf.Max(.0001f, duration));
            float curved = easing != null && easing.length > 0
                ? Mathf.Clamp01(easing.Evaluate(nextProgress)) : nextProgress;
            Vector3 desired = origin + (Vector3)path.Axis * (Mathf.Lerp(legStart, legEnd, curved) * GridCellWorldSize);
            Vector3 delta = desired - transform.localPosition;
            if (reverseOnBlock && IsBlockedAlong(delta))
            {
                // Retry the remaining route when clear; never reverse away from the signal's target.
                legStart = current;
                progress = 0f;
                return;
            }
            RefreshStandingPlayers();
            transform.localPosition = desired;
            progress = nextProgress;
            if (nextProgress >= 1f) reachedEndpoint = true;
            CarryPlayers(transform.parent != null ? transform.parent.TransformVector(delta) : delta);
        }

        protected override void OnResetForRoom()
        {
            ResetTimeline();
            SetMovementAudio(false);
            ResetMotion();
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
                IsMoving ? (UsesPoweredEndpoint && !active ? "返回原位" : "移动中") : "已停止"));
            values.Add(new BlockDebugValue("可选Timeline", IsTimelinePlaying ? "播放中" :
                timelineTrigger == MovingPlatformTimelineTrigger.Disabled ? "关闭" : "等待触发"));
            if (driveMode == MovingPlatformDriveMode.SignalControlled)
                values.Add(new BlockDebugValue("充能移动模式", signalMotion == MovingPlatformSignalMotion.PoweredEndpoint
                    ? "充能到终点，断电回原位" : "充能时往返，断电停留"));
            values.Add(new BlockDebugValue(
                "路线",
                Path.Axis.x > 0f ? "水平往返" : "竖直往返"));
            values.Add(new BlockDebugValue("负向端点（格）", Path.Minimum));
            values.Add(new BlockDebugValue("正向端点（格）", Path.Maximum));
            values.Add(new BlockDebugValue("实际中心偏移（格）", Path.Center));
            values.Add(new BlockDebugValue(
                "承载玩家",
                carriedPlayers.Count));
            values.Add(new BlockDebugValue(
                "开关规则",
                signalLogic == PlatformSignalLogic.Any
                    ? "任一激活"
                    : "全部激活"));
            values.Add(new BlockDebugValue(
                "遇障行为",
                !reverseOnBlock ? "不检测" : UsesPoweredEndpoint ? "等待通路" : "反向移动"));
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
            MovingPlatformPath path = Path;
            return (Vector3)path.Axis * (path.FirstEnd * GridCellWorldSize);
        }

        private void ResetMotion()
        {
            MovingPlatformPath path = Path;
            progress = 0f;
            legStart = 0f;
            travelDirection = path.StartsNegative ? -1 : 1;
            legEnd = path.FirstEnd;
        }

        private bool IsBlockedAlong(Vector3 delta)
        {
            if (delta.sqrMagnitude <= .000001f)
            {
                return false;
            }

            foreach (Collider surface in platformSurfaces)
            {
                if (surface == null || !surface.enabled || surface.isTrigger) continue;
                Bounds bounds = surface.bounds;
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
                        IsCarriedPlayer(hit.collider)) continue;
                    return true;
                }
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
            foreach (Collider surface in platformSurfaces)
            {
                if (surface == null || !surface.enabled || surface.isTrigger) continue;
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
            Collider platformCollider = collision.contactCount > 0 ? collision.GetContact(0).thisCollider : GetComponent<Collider>();
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
