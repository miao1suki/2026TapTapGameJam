using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures.Power
{
    public enum BlockPowerRole
    {
        [InspectorName("开关")] Switch = 0,
        [InspectorName("信号源")] SignalSource = 1
    }

    public enum BlockPowerSignalPattern
    {
        [InspectorName("单次信号")] Once = 0,
        [InspectorName("持续信号")] Continuous = 1,
        [InspectorName("脉冲信号")] Pulse = 2
    }

    public sealed class BlockPowerActivationSignal : IBlockSignal
    {
        public BlockPowerActivationSignal(
            int value,
            int rangeBlocks,
            bool active,
            BlockPowerSignalPattern pattern)
        {
            Value = value;
            RangeBlocks = rangeBlocks;
            Active = active;
            Pattern = pattern;
        }

        public int Value { get; }
        public int RangeBlocks { get; }
        public bool Active { get; }
        public BlockPowerSignalPattern Pattern { get; }
    }

    /// <summary>
    /// 只负责把外部激发信号送入方块的信号总线。
    /// 动力网络、容量、消耗和仲裁暂不在这一层实现。
    /// </summary>
    public interface IBlockPowerSignalTransmitter
    {
        void TransmitActivationSignal<T>(T signal)
            where T : class, IBlockSignal;
    }

    /// <summary>
    /// 开关模式提供给交互系统的开关接口。
    /// </summary>
    public interface IBlockPowerSwitch
    {
        bool IsOn { get; }
        void SetPower(bool value);
        void Toggle();
    }

    [BlockFeature(
        DisplayName = "动力信号组件",
        Phase = BlockFeaturePhase.Reaction,
        Order = 0,
        MaxPerBlock = 1,
        Provides = new[]
        {
            typeof(IBlockPowerSignalTransmitter),
            typeof(IBlockPowerSwitch)
        },
        Writes = new[] { BlockChannel.Power })]
    public sealed class BlockPowerFeature : BlockFeature,
        IBlockPowerSignalTransmitter,
        IBlockPowerSwitch
    {
        [BlockParameter(
            Label = "角色",
            Group = "信号",
            Order = 0,
            Tooltip = "开关需要交互；信号源在放置后自行开始工作。")]
        [SerializeField] private BlockPowerRole role =
            BlockPowerRole.SignalSource;

        [BlockParameter(
            Label = "信号类型",
            Group = "信号",
            Order = 1,
            Tooltip = "单次、持续或按间隔重复的脉冲。")]
        [SerializeField] private BlockPowerSignalPattern signalPattern =
            BlockPowerSignalPattern.Once;

        [BlockParameter(
            Label = "初始开启",
            Group = "信号",
            Order = 2,
            VisibleWhenField = nameof(role),
            VisibleWhenValue = (int)BlockPowerRole.Switch,
            Tooltip = "开关模式进入场景时是否默认开启。")]
        [SerializeField] private bool switchStartsOn = false;

        [BlockParameter(
            Label = "脉冲间隔（秒）",
            Group = "信号",
            Order = 3,
            VisibleWhenField = nameof(signalPattern),
            VisibleWhenValue = (int)BlockPowerSignalPattern.Pulse,
            Tooltip = "脉冲信号每隔多少秒发送一次。")]
        [SerializeField, Min(.05f)] private float pulseInterval = .5f;

        [BlockParameter(
            Label = "传播距离（格）",
            Group = "信号",
            Order = 4,
            Tooltip = "信号可覆盖的方块距离；0 只发送给自身。")]
        [SerializeField, Min(0)] private int signalRangeBlocks = 1;

        [BlockParameter(
            Label = "输出调试日志",
            Group = "调试参数",
            Order = 0,
            Tooltip = "发送激发信号时输出日志。")]
        [SerializeField] private bool debugLogActivation = true;

        [BlockParameter(
            Label = "测试信号值",
            Group = "调试参数",
            Order = 1,
            Tooltip = "调试信号的整数载荷；当前不是正式动力值。")]
        [SerializeField] private int debugSignalValue = 1;

        [SerializeField, HideInInspector] private bool isOn;
        [SerializeField, HideInInspector] private float pulseTimer;
        [SerializeField, HideInInspector] private int debugActivationCount;
        private readonly Collider[] overlapBuffer = new Collider[32];

        public BlockPowerRole Role => role;
        public BlockPowerSignalPattern SignalPattern => signalPattern;
        public bool IsOn => isOn;
        public int DebugActivationCount => debugActivationCount;
        public int SignalRangeBlocks => signalRangeBlocks;

        public string RoleDisplay =>
            role == BlockPowerRole.Switch ? "开关" : "信号源";
        public string PatternDisplay
        {
            get
            {
                switch (signalPattern)
                {
                    case BlockPowerSignalPattern.Once:
                        return "单次信号";
                    case BlockPowerSignalPattern.Continuous:
                        return "持续信号";
                    case BlockPowerSignalPattern.Pulse:
                        return "脉冲信号";
                    default:
                        return signalPattern.ToString();
                }
            }
        }

        protected override void OnAttach()
        {
            isOn = role == BlockPowerRole.SignalSource ||
                   switchStartsOn;
            pulseTimer = Mathf.Max(.05f, pulseInterval);
            if (isOn)
            {
                StartPattern();
            }
        }

        protected override void OnTick(float deltaTime)
        {
            if (!isOn ||
                signalPattern != BlockPowerSignalPattern.Pulse)
            {
                return;
            }

            pulseTimer -= deltaTime;
            if (pulseTimer > 0f)
            {
                return;
            }

            pulseTimer += Mathf.Max(.05f, pulseInterval);
            SendPowerSignal(true);
        }

        public void SetPower(bool value)
        {
            if (role != BlockPowerRole.Switch || isOn == value)
            {
                return;
            }

            isOn = value;
            if (isOn)
            {
                StartPattern();
            }
            else
            {
                StopPattern();
            }
        }

        public void Toggle()
        {
            SetPower(!isOn);
        }

        /// <summary>
        /// 开关模式由交互系统调用；信号源模式忽略切换。
        /// </summary>
        public void TriggerInteraction()
        {
            if (role == BlockPowerRole.Switch)
            {
                Toggle();
            }
        }

        public void TransmitActivationSignal<T>(T signal)
            where T : class, IBlockSignal
        {
            Context?.Signals.Emit(signal);
        }

        [ContextMenu("发送一次激发信号")]
        public void SendDebugActivationSignal()
        {
            SendPowerSignal(true);
        }

        [ContextMenu("重置调试计数")]
        public void ResetDebugState()
        {
            debugActivationCount = 0;
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue("角色", RoleDisplay));
            values.Add(new BlockDebugValue(
                "信号类型",
                PatternDisplay));
            values.Add(new BlockDebugValue(
                "开关状态",
                role == BlockPowerRole.Switch
                    ? isOn ? "开启" : "关闭"
                    : "不使用交互开关"));
            values.Add(new BlockDebugValue(
                "传播距离",
                $"{signalRangeBlocks} 格"));
            values.Add(new BlockDebugValue(
                "脉冲计时",
                signalPattern == BlockPowerSignalPattern.Pulse
                    ? pulseTimer.ToString("0.##") + "s"
                    : "不适用"));
            values.Add(new BlockDebugValue(
                "激发计数",
                debugActivationCount));
            values.Add(new BlockDebugValue(
                "实现状态",
                "仅信号转发 · 网络/容量/仲裁待施工"));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            if (role == BlockPowerRole.Switch)
            {
                actions.Add(new BlockDebugAction(
                    "模拟交互开关",
                    TriggerInteraction));
            }

            actions.Add(new BlockDebugAction(
                "强制发送一次",
                SendDebugActivationSignal));
            actions.Add(new BlockDebugAction(
                "重置调试计数",
                ResetDebugState));
        }

        private void StartPattern()
        {
            switch (signalPattern)
            {
                case BlockPowerSignalPattern.Once:
                case BlockPowerSignalPattern.Continuous:
                    SendPowerSignal(true);
                    break;
                case BlockPowerSignalPattern.Pulse:
                    pulseTimer = 0f;
                    break;
            }
        }

        private void StopPattern()
        {
            if (signalPattern == BlockPowerSignalPattern.Continuous)
            {
                SendPowerSignal(false);
            }
        }

        private void SendPowerSignal(bool active)
        {
            debugActivationCount++;
            var signal = new BlockPowerActivationSignal(
                debugSignalValue,
                signalRangeBlocks,
                active,
                signalPattern);
            TransmitActivationSignal(signal);
            PropagateToNearbyBlocks(signal);
            if (debugLogActivation)
            {
                Debug.Log(
                    $"[BlockPower] Role={RoleDisplay} " +
                    $"Pattern={PatternDisplay} Active={active} " +
                    $"Value={signal.Value} Range={signal.RangeBlocks} " +
                    $"Count={debugActivationCount}",
                    this);
            }

            if (!Application.isPlaying && Context != null)
            {
                Context.Runtime.FlushPending();
            }
        }

        private void PropagateToNearbyBlocks(
            BlockPowerActivationSignal signal)
        {
            if (signal.RangeBlocks <= 0 || Context == null)
            {
                return;
            }

            float radius = ResolveBlockSize() * signal.RangeBlocks;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position,
                radius,
                overlapBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
            var targets = new HashSet<BlockRuntime>();
            for (int index = 0; index < count; index++)
            {
                Collider hit = overlapBuffer[index];
                if (hit == null)
                {
                    continue;
                }

                BlockRuntime target =
                    hit.GetComponentInParent<BlockRuntime>();
                if (target == null ||
                    target == Context.Runtime ||
                    !targets.Add(target))
                {
                    continue;
                }

                target.ReceiveExternalSignal(signal);
                if (!Application.isPlaying)
                {
                    target.FlushPending();
                }
            }
        }

        private float ResolveBlockSize()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box == null)
            {
                Vector3 scale = transform.lossyScale;
                return Mathf.Max(.01f, Mathf.Max(
                    Mathf.Abs(scale.x),
                    Mathf.Max(
                        Mathf.Abs(scale.y),
                        Mathf.Abs(scale.z))));
            }

            Vector3 size = box.size;
            Vector3 lossy = transform.lossyScale;
            return Mathf.Max(.01f, Mathf.Max(
                Mathf.Abs(size.x * lossy.x),
                Mathf.Max(
                    Mathf.Abs(size.y * lossy.y),
                    Mathf.Abs(size.z * lossy.z))));
        }

        // TODO: 定义正式动力协议、容量、输入输出与消耗。
        // TODO: 接入 Port/Link、供电方向和连接生命周期。
        // TODO: 实现优先级、Channel 仲裁和失效原因。
        // TODO: 补充连线可视化、动画、特效和音效。
    }
}
