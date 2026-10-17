using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerSpritePresentation))]
    public sealed class PlayerSpritePresentationEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            root.Add(new HelpBox("外观按16个有效美术像素对应1格显示，碰撞尺寸独立。垂直偏移以碰撞体脚底为基准。", HelpBoxMessageType.Info));
            var artwork = new Foldout { text = "动画与外观", value = true };
            root.Add(artwork);
            foreach (string name in new[] { "animationSet", "visual", "spriteMaterial", "hidePlaceholder", "faceCamera", "verticalOffsetCells" })
                artwork.Add(ProjectInspectorUtility.CreateProperty(serializedObject, name));
            return root;
        }
    }
    [CustomEditor(typeof(PlayerSpriteAnimationSet))]
    public sealed class PlayerSpriteAnimationSetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "clips", "动画"));
            return root;
        }
    }
}
