using System;
using System.Collections.Generic;
using Project.Mechanisms;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "按钮锁",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IMechanismSignalSource) },
        Writes = new[] { BlockChannel.Power })]
    public sealed class ButtonLockFeature : BlockFeature,
        IMechanismSignalSource
    {
        [BlockParameter(
            Label = "初始开启",
            Group = "按钮设置",
            Order = 0)]
        [SerializeField] private bool startsOn;

        [BlockParameter(
            Label = "关闭状态材质",
            Group = "按钮设置",
            Order = 1,
            Tooltip = "按钮切换为关闭状态时使用的材质。")]
        [SerializeField] private Material offMaterial;

        [BlockParameter(
            Label = "开启状态材质",
            Group = "按钮设置",
            Order = 2)]
        [SerializeField] private Material onMaterial;

        [BlockParameter(
            Label = "状态渲染器",
            Group = "按钮设置",
            Order = 3,
            Tooltip = "留空时自动查找第一个子渲染器。")]
        [SerializeField] private Renderer targetRenderer;

        [BlockParameter(
            Label = "脚部容差（格）",
            Group = "按钮设置",
            Order = 4,
            Tooltip = "玩家脚部或三色方块底部进入按钮表面时触发。")]
        [SerializeField, Min(0f)] private float footTolerance = .15f;

        [BlockParameter(Label = "触发模式", Group = "按钮设置", Order = 5)]
        [SerializeField] private DoorButtonMode mode;

        private bool isOn;
        private bool initialized;
        private BoxCollider pressTrigger;
        private Collider[] contacts = new Collider[16];

        public bool IsSignalActive => isOn && CanRunFeature;
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
            if (!initialized) { isOn = startsOn; initialized = true; }
            pressTrigger = GetComponent<BoxCollider>();
            if (pressTrigger != null)
            {
                pressTrigger.isTrigger = true;
                pressTrigger.center = new Vector3(0f, -.35f, 0f);
                pressTrigger.size = new Vector3(.6f, .3f, .6f);
            }
            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponentInChildren<Renderer>();
            }

            ApplyMaterial();
        }

        private void FixedUpdate()
        {
            if (!CanRunFeature || pressTrigger == null || !pressTrigger.enabled) return;
            if (mode == DoorButtonMode.Latched && isOn) return;
            bool pressed = DoorButton.FindPressureActor(pressTrigger,
                footTolerance * GridCellWorldSize, ref contacts) != null;
            if (pressed || mode == DoorButtonMode.Held) SetOn(pressed);
        }

        // Kept for source compatibility; buttons are never keyboard interaction targets.
        public bool CanInteract(GameObject interactor) => false;
        public bool TryInteract(GameObject interactor) => false;

        protected override void OnResetForRoom()
        {
            if (mode == DoorButtonMode.Latched) return;
            isOn = startsOn;
            ApplyMaterial();
            SignalChanged?.Invoke(IsSignalActive);
        }

        public void Toggle()
        {
            if (!CanRunFeature)
            {
                return;
            }

            isOn = !isOn;
            ApplyMaterial();
            SignalChanged?.Invoke(IsSignalActive);
        }

        public void SetOn(bool value)
        {
            if (!CanRunFeature || isOn == value)
            {
                return;
            }

            isOn = value;
            ApplyMaterial();
            SignalChanged?.Invoke(IsSignalActive);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "按钮状态",
                IsSignalActive ? "开启" : "关闭"));
            values.Add(new BlockDebugValue(
                "状态材质",
                isOn
                    ? onMaterial != null ? onMaterial.name : "未设置"
                    : offMaterial != null ? offMaterial.name : "未设置"));
            values.Add(new BlockDebugValue(
                "触发模式",
                mode == DoorButtonMode.Latched ? "一次按下保持" : "持续压住"));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "切换按钮状态",
                Toggle,
                Application.isPlaying));
        }

        private void ApplyMaterial()
        {
            if (targetRenderer == null)
            {
                return;
            }

            Material material = isOn ? onMaterial : offMaterial;
            if (material != null)
            {
                targetRenderer.sharedMaterial = material;
            }
        }
    }
}
