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
            "只向 BossController 下发向右移动指令；接管瞬间记录 Boss 当前 Y，并保持该高度右移，不直接修改 Rigidbody。"));

        Foldout route = ProjectInspectorUtility.CreateFoldout(
            "右移路线",
            true);
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moving",
            "启用右移"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "waitForBossMotionEnabled",
            "等待 Boss 运动开启",
            "开启后，等 Timeline 调用 SetTrackingEnabled(true) 才开始右移。"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startDelaySeconds",
            "启动延迟"));
        route.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "invulnerableMoveSpeedMultiplier",
            "无敌时右移速度倍率",
            "Boss 受伤进入无敌后右移速度的倍率；1 为不减速，0.5 为半速，0 为暂时停止。"));
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
            "当前运行高度",
            () => executor != null
                ? executor.RightMoveHeightY.ToString("0.###")
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "无敌减速倍率",
            () => executor != null
                ? executor.InvulnerableMoveSpeedMultiplier.ToString("0.##")
                : "无"));
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
