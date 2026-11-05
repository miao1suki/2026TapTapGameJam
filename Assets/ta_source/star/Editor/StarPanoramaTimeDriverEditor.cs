using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace TATools.StarPanorama.Editor
{
    [CustomEditor(typeof(StarPanoramaTimeDriver)), CanEditMultipleObjects]
    public sealed class StarPanoramaTimeDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("使用平面背景显示原星空材质，自动适配正交相机，不依赖天空盒或立体相机。", HelpBoxMessageType.Info));
            root.Add(new PropertyField(serializedObject.FindProperty("backgroundMaterial"), "星空材质"));
            root.Add(new PropertyField(serializedObject.FindProperty("useUnscaledTime"), "暂停时继续流转"));
            return root;
        }
    }
}
