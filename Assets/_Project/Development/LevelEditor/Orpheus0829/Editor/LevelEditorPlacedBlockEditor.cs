using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    [CustomEditor(typeof(LevelEditorPlacedBlock))]
    public sealed class LevelEditorPlacedBlockEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "关卡编辑器方块记录"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));

            LevelEditorPlacedBlock block =
                (LevelEditorPlacedBlock)target;
            Foldout identity = ProjectInspectorUtility.CreateFoldout(
                "栏目信息",
                true);
            identity.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "栏目名称",
                () => block != null ? block.EntryName : "无"));
            identity.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "方块类型",
                () => block != null && block.IsProp
                    ? "道具"
                    : "方块"));
            identity.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "栏目颜色",
                () => block != null && block.HasColorData
                    ? block.EntryColor.ToString()
                    : "无颜色数据"));
            root.Add(identity);

            Foldout grid = ProjectInspectorUtility.CreateFoldout(
                "网格记录",
                true);
            grid.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "起始格",
                () => block != null
                    ? block.Cell.ToString()
                    : "无"));
            grid.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "占用尺寸",
                () => block != null
                    ? block.SizeCells.ToString()
                    : "无"));
            root.Add(grid);

            root.Add(ProjectInspectorUtility.CreateHelp(
                "这些值由关卡编辑器维护；请在规划编辑器中修改栏目或重新放置。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
