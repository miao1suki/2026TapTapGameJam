using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerActionRunner))]
    public sealed class PlayerActionRunnerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(PlayerInspectorFields.CreateScriptField(
                serializedObject));

            Foldout playback = PlayerInspectorFields.CreateFoldout(
                "动作播放");
            playback.Add(PlayerInspectorFields.Create(
                serializedObject,
                "director",
                "播放导演",
                "播放 ActSO 中 Timeline 资产的 PlayableDirector。"));
            root.Add(playback);

            root.Bind(serializedObject);
            return root;
        }
    }
}
