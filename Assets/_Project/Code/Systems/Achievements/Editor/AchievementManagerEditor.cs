using UnityEditor;

namespace Project.Achievements.Editor
{
    [CustomEditor(typeof(AchievementManager))]
    public sealed class AchievementManagerEditor :
        UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            AchievementManager manager =
                (AchievementManager)target;
            MonoScript script =
                MonoScript.FromMonoBehaviour(manager);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "脚本",
                    script,
                    typeof(MonoScript),
                    false);
            }

            AchievementEditorHelp.DrawManagerHelp();
            EditorGUILayout.HelpBox(
                "运行时管理器由 AchievementManager.Instance 自动创建。",
                MessageType.Info);
        }
    }
}
