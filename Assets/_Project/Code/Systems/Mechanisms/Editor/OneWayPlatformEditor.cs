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
            var appearance = new Foldout { text = "美术颜色", value = true };
            appearance.Add(new PropertyField(serializedObject.FindProperty("appearanceColor"), "平台颜色"));
            appearance.Add(new HelpBox("三色平台玩法完全相同，始终可站立；颜色仅用于外观和颜色褪色层。", HelpBoxMessageType.Info));
            root.Add(appearance);
            var foldout = new Foldout { text = "碰撞设置", value = true };
            foldout.Add(new PropertyField(serializedObject.FindProperty("topToleranceCells"), "顶面容差（格）"));
            root.Add(foldout);
            return root;
        }
    }
}
