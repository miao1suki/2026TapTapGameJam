using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.Interactions.Editor
{
    [CustomEditor(typeof(InteractionObject))]
    public sealed class InteractionObjectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("物体交互入口"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            root.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "definition",
                "物体交互定义",
                "交互图属于物体；颜色只保存在定义和运行时属性中。"));
            root.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "currentColorTypeId",
                "当前颜色覆盖"));
            InteractionObject source = (InteractionObject)target;
            root.Add(new Button(() =>
            {
                InteractionManagerWindow.OpenForObject(source.gameObject);
            })
            {
                text = "打开物体交互管理器"
            });
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
