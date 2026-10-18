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

        Foldout playerPause = ProjectInspectorUtility.CreateFoldout(
            "玩家复活暂停",
            true);
        playerPause.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "pauseWhilePlayerRespawning",
            "玩家死亡时暂停右移",
            "玩家死亡信号立即停止右移；直接复活完成或过场复活最后动画结束后恢复。"));
        playerPause.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "playerTag",
            "玩家标签"));
        root.Add(playerPause);

        Foldout verticalTracking =
            ProjectInspectorUtility.CreateFoldout(
                "弱垂直追踪",
                true);
        verticalTracking.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "enableWeakVerticalTracking",
            "启用弱垂直追踪",
            "Boss 只缓慢靠近玩家的高度，不会强制与玩家完全同高。"));
        verticalTracking.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "verticalDeadbandBlocks",
            "高度死区（格）",
            "玩家与 Boss 高度差在该范围内时完全不改变高度。"));
        verticalTracking.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "verticalTrackSpeedBlocksPerSecond",
            "垂直追踪速度（格/秒）",
            "超出死区后的最大高度变化速度。"));
        root.Add(verticalTracking);

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
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "玩家复活暂停",
            () => executor != null &&
                  executor.IsPausedForPlayerRespawn
                ? "是"
                : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "弱垂直追踪",
            () => executor != null &&
                  executor.IsWeakVerticalTrackingEnabled
                ? $"{executor.VerticalTrackSpeedBlocksPerSecond:0.###} 格/秒 · 死区 {executor.VerticalDeadbandBlocks:0.###}"
                : "关闭"));
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
