using Project.BlockFeatures;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(TeleportAnchorFeature))]
public sealed class TeleportAnchorFeatureEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        TeleportAnchorFeature component =
            target as TeleportAnchorFeature;
        VisualElement root =
            ProjectInspectorUtility.CreateRoot(serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "传送目标方块"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "填写任意传送 ID。运行时通过 TeleportAnchorFeature.TeleportObject(对象, ID) 把对象移动到对应目标方块的位置。方块默认无碰撞且不可见。"));

        Foldout identity =
            ProjectInspectorUtility.CreateFoldout(
                "传送设置",
                true);
        identity.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "teleportId",
            "传送 ID"));
        identity.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "disableCollidersOnEnable",
            "启用时关闭碰撞"));
        identity.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hideRenderersOnEnable",
            "启用时隐藏渲染"));
        root.Add(identity);

        Foldout debug =
            ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                true);
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "注册状态",
            () => component != null &&
                  component.IsRegistered
                ? "已注册"
                : "未注册"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "全局目标数",
            () => TeleportAnchorFeature.RegistryCount
                .ToString()));
        debug.Add(new Button(() =>
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");
            if (component != null && player != null)
            {
                TeleportAnchorFeature.TeleportObject(
                    player,
                    component.TeleportId);
            }
        })
        {
            text = "调试：把 Player 传送到此目标"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
