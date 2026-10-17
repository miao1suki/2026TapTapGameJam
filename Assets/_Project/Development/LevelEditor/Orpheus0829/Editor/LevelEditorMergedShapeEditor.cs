using UnityEditor;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    [CustomEditor(typeof(LevelEditorMergedShape))]
    public sealed class LevelEditorMergedShapeEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var shape = (LevelEditorMergedShape)target;
            root.Add(new HelpBox("占格形状由关卡存档生成。请在关卡编辑器中合并、解除合并或修改布局。", HelpBoxMessageType.Info));
            var details = new Foldout { text = "形状信息", value = true };
            details.Add(new Label($"外接尺寸：{shape.Dimensions.x} × {shape.Dimensions.y} 格"));
            int count = 0; foreach (var rect in shape.Rectangles) count += rect.width * rect.height;
            details.Add(new Label($"实际占格：{count} 格"));
            details.Add(new Label($"压缩区域：{shape.Rectangles.Count} 个"));
            details.Add(new Label("未占用区域：保留空洞，无碰撞"));
            root.Add(details); return root;
        }
    }
}
