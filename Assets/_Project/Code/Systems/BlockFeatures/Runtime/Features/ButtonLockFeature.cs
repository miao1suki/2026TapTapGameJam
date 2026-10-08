using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using Project.Player;
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
        IInteractionTarget,
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
            Label = "交互检测半径（格）",
            Group = "按钮设置",
            Order = 4,
            Tooltip = "以按钮为中心检测玩家距离；1 表示一格，0.5 表示半格。")]
        [SerializeField, Min(0f)] private float interactionRadiusGrid = 1f;

        private bool isOn;

        public bool IsSignalActive => isOn && CanRunFeature;
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
            isOn = startsOn;
            if (targetRenderer == null)
            {
                targetRenderer =
                    GetComponentInChildren<Renderer>();
            }

            ApplyMaterial();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!CanRunFeature || interactor == null)
            {
                return false;
            }

            PlayerController player =
                interactor.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return false;
            }

            float radius =
                Mathf.Max(0f, interactionRadiusGrid) *
                GridCellWorldSize;
            return Vector3.Distance(
                       transform.position,
                       player.transform.position) <=
                   radius;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            Toggle();
            return true;
        }

        protected override void OnResetForRoom()
        {
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
                "交互检测半径",
                $"{interactionRadiusGrid:0.###} 格"));
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
