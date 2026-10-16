using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossPathExecutor))]
public sealed class BossPathExecutorEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 寻路马夫"));
        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "m_Script",
            "脚本"));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "只读取 BossPathfinder 的带类型路径，并向 BossController 下发跑动、跳跃、爬升和下落指令；不直接操作 Rigidbody。"));

        Foldout move = ProjectInspectorUtility.CreateFoldout(
            "移动设置",
            true);
        move.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "execute",
            "启用执行"));
        move.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moveSpeed",
            "跑动速度"));
        move.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "climbSpeed",
            "爬升速度"));
        move.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "arriveDistance",
            "节点到达距离"));
        root.Add(move);

        Foldout auto = ProjectInspectorUtility.CreateFoldout(
            "自动越障",
            true);
        auto.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoJumpHeight",
            "自动跳跃高度差",
            "跑动节点如果高出当前高度超过该值，就自动改用跳跃。"));
        auto.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoDropHeight",
            "自动下落高度差",
            "跑动节点如果低于当前高度超过该值，就自动改用下落。"));
        auto.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoJumpOnObstacle",
            "遇到阻挡自动跳",
            "跑动方向前方检测到方块时自动改跳。"));
        auto.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "obstacleProbePadding",
            "阻挡探测余量"));
        auto.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "noAutoJumpNearTarget",
            "靠近玩家不自动跳",
            "距离玩家横向小于该值时，不再把前方对象误判成障碍。"));
        auto.Add(ProjectInspectorUtility.CreateHelp(
            "正常同高度节点仍按跑动处理；上坡和台阶不再持续水平顶墙。",
            HelpBoxMessageType.None));
        root.Add(auto);

        Foldout jump = ProjectInspectorUtility.CreateFoldout(
            "跳跃设置",
            true);
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpRetryDelay",
            "跳跃重试间隔"));
        root.Add(jump);

        BossPathExecutor executor = target as BossPathExecutor;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "执行状态",
            () => executor != null && executor.IsExecuting
                ? "执行中"
                : "已停止"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "当前路径段",
            () => executor != null
                ? executor.StepIndex.ToString()
                : "无"));
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
