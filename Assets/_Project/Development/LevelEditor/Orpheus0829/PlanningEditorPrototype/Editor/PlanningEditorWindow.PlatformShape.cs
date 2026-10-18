using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    public sealed partial class PlanningEditorWindow
    {
        [SerializeField] private bool newPlatformVertical;
        [SerializeField] private int newPlatformLength = 3;
        private Label selectedPlatformShapeLabel;
        private Button applyPlatformShapeButton;

        private VisualElement BuildPlatformShapeTools()
        {
            var group = new Foldout { text = "平台形状 · 横放 / 竖放 / 单格", value = true, name = "PlanningPlatformShapeTools" };
            group.style.marginLeft = group.style.marginRight = 8f;
            group.style.marginBottom = 6f;
            var direction = new DropdownField("长边朝向", new List<string> { "横向平台", "竖向平台" }, newPlatformVertical ? 1 : 0)
            { name = "PlanningPlatformBodyOrientation", tooltip = "只控制平台本体形状；向上/下/左/右移动请使用平台的移动路线参数。" };
            var length = new IntegerField("长度（格）") { value = Mathf.Clamp(newPlatformLength, 1, 256), isDelayed = true, name = "PlanningPlatformBodyLength" };
            length.tooltip = "可填1（单格）、3、5等长度。原图保持16美术像素/格：短平台裁片、长平台接片，不挤压图像。";
            void UpdatePlacement()
            {
                newPlatformVertical = direction.index == 1;
                newPlatformLength = Mathf.Clamp(length.value, 1, 256);
                length.SetValueWithoutNotify(newPlatformLength);
                canvas?.SetPlatformPlacementShape(newPlatformVertical, newPlatformLength);
            }
            direction.RegisterValueChangedCallback(_ => UpdatePlacement());
            length.RegisterValueChangedCallback(_ => UpdatePlacement());
            group.Add(direction); group.Add(length);
            var sizes = Row();
            foreach (int count in new[] { 1, 3, 5 })
            {
                int cells = count;
                var quick = TopButton(cells + "格", () => { length.SetValueWithoutNotify(cells); UpdatePlacement(); });
                quick.style.flexGrow = 1f; sizes.Add(quick);
            }
            group.Add(sizes);
            var hint = new Label("选中移动/升降平台后绘制即可；已有平台不会自动改变。") { name = "PlanningPlatformShapeHint" };
            hint.style.whiteSpace = WhiteSpace.Normal; hint.style.fontSize = 11;
            group.Add(hint);
            selectedPlatformShapeLabel = new Label(); group.Add(selectedPlatformShapeLabel);
            applyPlatformShapeButton = TopButton("将上方形状用于选中平台", () => canvas?.SetSelectedPlatformShape(newPlatformVertical, newPlatformLength));
            applyPlatformShapeButton.name = "PlanningApplySelectedPlatformShape";
            applyPlatformShapeButton.tooltip = "明确调整此平台本体占格，保留位置、ID、路线、基座/按钮/曲柄绑定；与实体重叠时拒绝，不影响其他旧平台。";
            group.Add(applyPlatformShapeButton);
            UpdatePlacement(); RefreshPlatformShapeSelection();
            return group;
        }

        private void RefreshPlatformShapeSelection()
        {
            if (selectedPlatformShapeLabel == null || applyPlatformShapeButton == null) return;
            bool found = canvas != null && canvas.TryGetSelectedPlatformShape(out bool _, out int _);
            applyPlatformShapeButton.SetEnabled(found);
            if (found && canvas.TryGetSelectedPlatformShape(out bool vertical, out int length))
                selectedPlatformShapeLabel.text = $"当前选中：{(vertical ? "竖向" : "横向")} {length}格（移动方向独立）";
            else selectedPlatformShapeLabel.text = "先用“选择”点中平台，可在此改已有平台形状。";
        }
    }
}
