using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    /// <summary>Global menu hotkey, like planning M; preserves text entry and unfinished strokes.</summary>
    public static class GridArtWorkspaceShortcut
    {
        internal static bool CanToggle(EditorWindow focused)
        {
            if (focused == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorGUIUtility.editingTextField || GridArtScenePainter.IsStroking ||
                Resources.FindObjectsOfTypeAll<GridArtWindow>().Any(window => window.IsStroking)) return false;
            for (var item = focused.rootVisualElement.focusController?.focusedElement as VisualElement;
                item != null; item = item.parent)
                if (item is TextElement || item is TextField || item.ClassListContains("unity-text-input") ||
                    item.ClassListContains("unity-base-text-field") || item.ClassListContains("unity-base-text-field__input")) return false;
            return true;
        }

        [MenuItem("Tools/2026TapTap/关卡美术/切换窗口 _n")]
        private static void Toggle()
        {
            if (!CanToggle(EditorWindow.focusedWindow)) return;
            GridArtWindow open = Resources.FindObjectsOfTypeAll<GridArtWindow>().FirstOrDefault();
            if (open != null) open.Close();
            else GridArtWindow.Open();
        }
    }
}
