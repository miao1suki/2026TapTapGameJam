using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.GridArt.Editor
{
    /// <summary>Context-scoped N: never captures text entry, Play Mode or an unfinished art stroke.</summary>
    [InitializeOnLoad]
    public static class GridArtWorkspaceShortcut
    {
        private static readonly WorkspaceContext Context = new WorkspaceContext();

        static GridArtWorkspaceShortcut()
        {
            ShortcutManager.RegisterContext(Context);
            AssemblyReloadEvents.beforeAssemblyReload += () => ShortcutManager.UnregisterContext(Context);
        }

        public sealed class WorkspaceContext : IShortcutContext
        {
            public bool active => CanToggle(EditorWindow.focusedWindow);
        }

        internal static bool CanToggle(EditorWindow focused)
        {
            if (focused == null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorGUIUtility.editingTextField || GridArtScenePainter.IsStroking ||
                Resources.FindObjectsOfTypeAll<GridArtWindow>().Any(window => window.IsStroking)) return false;
            // The planning editor lives in another editor assembly. Limit the bridge to
            // its public window type name rather than introducing a reverse dependency.
            if (focused is not SceneView && focused is not GridArtWindow && focused.GetType().Name != "PlanningEditorWindow") return false;
            for (var item = focused.rootVisualElement.focusController?.focusedElement as VisualElement;
                item != null; item = item.parent)
                if (item is TextElement || item is TextField || item.ClassListContains("unity-text-input") ||
                    item.ClassListContains("unity-base-text-field") || item.ClassListContains("unity-base-text-field__input")) return false;
            return true;
        }

        [Shortcut("2026TapTap/关卡美术/开关工作台", typeof(WorkspaceContext), KeyCode.N)]
        private static void Toggle(ShortcutArguments args)
        {
            if (!CanToggle(EditorWindow.focusedWindow)) return;
            GridArtWindow open = Resources.FindObjectsOfTypeAll<GridArtWindow>().FirstOrDefault();
            if (open != null) open.Close();
            else GridArtWindow.Open();
        }
    }
}
