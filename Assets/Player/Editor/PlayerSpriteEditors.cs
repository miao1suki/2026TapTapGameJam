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
            foreach (string name in new[] { "animationSet", "visual", "spriteMaterial", "hidePlaceholder", "faceCamera", "verticalOffsetCells" })
                root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, name));
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
