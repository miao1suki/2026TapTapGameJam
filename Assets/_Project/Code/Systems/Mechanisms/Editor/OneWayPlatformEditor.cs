using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Mechanisms.Editor
{
    [CustomEditor(typeof(OneWayPlatform)), CanEditMultipleObjects]
    public sealed class OneWayPlatformEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("单向平台"));
            var foldout = new Foldout { text = "碰撞设置", value = true };
            foldout.Add(new PropertyField(serializedObject.FindProperty("topToleranceCells"), "顶面容差（格）"));
            root.Add(foldout);
            return root;
        }
    }
}
