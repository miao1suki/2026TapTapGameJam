using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossRightMoveExecutor))]
public sealed class BossRightMoveExecutorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("Boss 右移马夫"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "只向 BossController 下发固定高度的向右移动指令，不直接修改 Rigidbody。"));

        Foldout route = ProjectInspectorUtility.CreateFoldout(
            "右移路线",
            true);
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moving",
            "启用右移"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "rightMoveHeightY",
            "右移世界 Y 坐标",
            "Boss 在右移期间固定的世界高度。"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "waitForBossMotionEnabled",
            "等待 Boss 运动开启",
            "开启后，等 Timeline 调用 SetTrackingEnabled(true) 才开始右移。"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startDelaySeconds",
            "启动延迟"));
        root.Add(route);

        BossRightMoveExecutor executor =
            target as BossRightMoveExecutor;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "正在右移",
            () => executor != null && executor.IsMoving
                ? "是"
                : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "Boss 运动开启",
            () => executor != null &&
                  executor.Controller != null &&
                  executor.Controller.TrackingEnabled
                ? "是"
                : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "当前配置高度",
            () => executor != null
                ? executor.RightMoveHeightY.ToString("0.###")
                : "无"));
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
