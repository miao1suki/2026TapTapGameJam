using UnityEditor;
using UnityEditor.SceneManagement;

namespace Project.GameFlow.Editor
{
    [InitializeOnLoad]
    public static class GameFlowEditorPlayBridge
    {
        static GameFlowEditorPlayBridge()
        {
            EditorApplication.delayCall += ApplyStartScenePreference;
        }

        public static void ApplyStartScenePreference()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorPrefs.DeleteKey("2026TapTap.GameFlow.AutoBootstrapOnPlay");
        }
    }
}
