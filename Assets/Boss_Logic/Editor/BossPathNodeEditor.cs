using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossPathNode))]
public sealed class BossPathNodeEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 寻路节点"));
        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "m_Script",
            "脚本"));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "节点之间的连线是有向的。把回环路线的最后一个节点连回起点即可形成循环。"));

        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "links",
            "后继节点",
            "每个后继包含目标节点、通行类型和路径成本。"));

        root.Add(new Button(AddSelectedNode)
        {
            text = "添加选中节点为“跑动”后继"
        });

        root.Add(new Button(() =>
        {
            AddSelectedNodeWithKind(BossPathLinkKind.Jump);
        })
        {
            text = "添加选中节点为“跳跃”后继"
        });
        root.Add(new Button(() =>
        {
            AddSelectedNodeWithKind(BossPathLinkKind.Climb);
        })
        {
            text = "添加选中节点为“爬升”后继"
        });
        root.Add(new Button(() =>
        {
            AddSelectedNodeWithKind(BossPathLinkKind.Drop);
        })
        {
            text = "添加选中节点为“下落”后继"
        });

        BossPathNode node = target as BossPathNode;
        root.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "后继数量",
            () => node != null
                ? node.Links.Count.ToString()
                : "无"));
        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }

    private void AddSelectedNode()
    {
        AddSelectedNodeWithKind(BossPathLinkKind.Run);
    }

    private void AddSelectedNodeWithKind(
        BossPathLinkKind kind)
    {
        BossPathNode node = target as BossPathNode;
        BossPathNode selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<BossPathNode>()
            : null;
        if (node == null || selected == null || selected == node)
        {
            return;
        }

        Undo.RecordObject(node, "添加 Boss 寻路后继");
        node.AddLink(selected, kind);
        EditorUtility.SetDirty(node);
        serializedObject.Update();
    }
}
