using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossPathfinder))]
public sealed class BossPathfinderEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 追击寻路器"));
        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "m_Script",
            "脚本"));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "在 Boss 寻路节点图上实时计算到玩家的有向路径。这里只计算路径，不负责移动、跳跃或道具使用。"));

        Foldout targetSection = ProjectInspectorUtility.CreateFoldout(
            "追踪目标",
            true);
        targetSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "target",
            "目标对象",
            "留空时按下面的 Tag 查找。"));
        targetSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetTag",
            "目标标签"));
        targetSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoResolveTarget",
            "自动解析目标"));
        root.Add(targetSection);

        Foldout repath = ProjectInspectorUtility.CreateFoldout(
            "路径重算",
            true);
        repath.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "autoRepath",
            "自动重算路径"));
        repath.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "repathInterval",
            "重算间隔（秒）"));
        repath.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "nodeSearchRadius",
            "节点搜索半径"));
        repath.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hazardPolicy",
            "危险边规则",
            "当前 Boss 默认直接穿过危险边，不为危险绕路。"));
        root.Add(repath);

        Foldout help = ProjectInspectorUtility.CreateFoldout(
            "路径节点说明",
            true);
        help.Add(ProjectInspectorUtility.CreateHelp(
            "每段有向边代表 Boss 可以走、爬、跳、下落、乘平台或穿过危险。回环地图只需要把最后节点连回起点。"));
        root.Add(help);

        BossPathfinder pathfinder = target as BossPathfinder;
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "路径状态",
            () => pathfinder != null && pathfinder.HasPath
                ? $"有效 · {pathfinder.CurrentPath.Count} 个节点"
                : "无路径"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "起始节点",
            () => pathfinder != null &&
                  pathfinder.StartNode != null
                ? pathfinder.StartNode.name
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "目标节点",
            () => pathfinder != null &&
                  pathfinder.GoalNode != null
                ? pathfinder.GoalNode.name
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "路径版本",
            () => pathfinder != null
                ? pathfinder.PathVersion.ToString()
                : "无"));
        debug.Add(new Button(() => pathfinder?.RequestRepath())
        {
            text = "立即重算路径"
        });
        debug.Add(new Button(() => pathfinder?.ClearPath())
        {
            text = "清空当前路径"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
