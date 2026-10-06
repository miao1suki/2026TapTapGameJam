#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.InputSystem;

namespace Project.InputAbstraction.Editor
{
    [InitializeOnLoad]
    internal static class InputSystemPlayModeFocusPolicy
    {
        static InputSystemPlayModeFocusPolicy()
        {
            Apply();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.EnteredPlayMode)
            {
                Apply();
            }
        }

        private static void Apply()
        {
            if (InputSystem.settings == null)
            {
                return;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode
                    .AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
        }
    }
}
#endif
