using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossNavAutoBaker))]
public sealed class BossNavAutoBakerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 自动寻路烘焙器"));
        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "m_Script",
            "脚本"));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "从关卡方块顶面和 Boss 3×3 体型自动生成运行时节点图；不修改场景存档。"));

        Foldout bake = ProjectInspectorUtility.CreateFoldout(
            "烘焙设置",
            true);
        bake.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bakeOnStart",
            "开始时自动烘焙"));
        bake.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bakeOnlyWhenNoManualNodes",
            "已有手摆节点时跳过"));
        bake.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "fallbackCellSize",
            "兜底格子尺寸"));
        root.Add(bake);

        Foldout body = ProjectInspectorUtility.CreateFoldout(
            "Boss 体型",
            true);
        body.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "agentWidth",
            "占格宽度"));
        body.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "agentHeight",
            "占格高度"));
        body.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "clearanceShrink",
            "净空收缩"));
        root.Add(body);

        Foldout traversal = ProjectInspectorUtility.CreateFoldout(
            "可通行范围",
            true);
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "stepUpHeight",
            "自动上台阶高度"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpUpHeight",
            "最大上跳高度"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpHorizontalDistance",
            "最大跳跃水平距离"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "dropHeight",
            "最大下落高度"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "dropHorizontalDistance",
            "最大下落水平距离"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "runHorizontalDistance",
            "最大跑动连接距离"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxEdgesPerNode",
            "每节点最大边数"));
        traversal.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxGeneratedNodes",
            "最大节点数"));
        root.Add(traversal);

        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        BossNavAutoBaker baker = target as BossNavAutoBaker;
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "运行时根节点",
            () => baker != null && baker.RuntimeRoot != null
                ? baker.RuntimeRoot.name
                : "尚未烘焙"));
        debug.Add(new Button(() => baker?.Bake())
        {
            text = "立即烘焙当前场景"
        });
        debug.Add(new Button(() => baker?.ClearRuntimeGraph())
        {
            text = "清除运行时烘焙图"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
