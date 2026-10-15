using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(DialogueBubbleDefaults))]
public sealed class DialogueBubbleDefaultsInspector : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("对话气泡默认配置"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "对话轨道未指定素材时会读取此资源。" +
            "字体必须指向 Cubic_11 SDF。"));

        Foldout assets = ProjectInspectorUtility.CreateFoldout(
            "默认素材",
            true);
        assets.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleSprite",
            "气泡 Sprite"));
        assets.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bubbleMaterial",
            "气泡材质"));
        assets.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "font",
            "默认字体"));
        root.Add(assets);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
