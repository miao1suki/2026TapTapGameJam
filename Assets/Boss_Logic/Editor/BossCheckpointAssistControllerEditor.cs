using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossCheckpointAssistController))]
public sealed class BossCheckpointAssistControllerEditor :
    Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(
                serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 出生点弱补助"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "玩家活着首次激活出生点时，让 Boss 小幅加速追到该出生点配置的 Boss 位置。玩家死亡不触发，继续使用传送重置。"));

        Foldout assist = ProjectInspectorUtility.CreateFoldout(
            "弱补助参数",
            true);
        Add(assist, "horizontalSpeedMultiplier", "水平速度倍率");
        Add(assist, "maximumDurationSeconds", "最长补助时间");
        Add(assist, "horizontalToleranceBlocks", "到达容差（格）");
        Add(
            assist,
            "verticalAssistSpeedBlocksPerSecond",
            "垂直辅助速度（格/秒）");
        root.Add(assist);

        BossCheckpointAssistController controller =
            target as BossCheckpointAssistController;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "当前补助出生点",
            () => controller != null &&
                  controller.ActiveAnchor != null
                ? controller.ActiveAnchor.DisplayLabel
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "马夫正在补助",
            () => controller != null &&
                  controller.Mover != null &&
                  controller.Mover.IsCheckpointAssistActive
                ? "是"
                : "否"));
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void Add(
        VisualElement parent,
        string propertyName,
        string label)
    {
        parent.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            propertyName,
            label));
    }
}
