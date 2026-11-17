using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal static class SceneWorkbenchInput
    {
        private static readonly Dictionary<SceneView, VisualElement> Panels = new();
        internal static void Register(SceneView view, VisualElement panel) { if (view != null) Panels[view] = panel; }
        internal static void Unregister(SceneView view, VisualElement panel)
        {
            if (view != null && Panels.TryGetValue(view, out var value) && value == panel) Panels.Remove(view);
        }

        internal static bool IsOverPanel(SceneView view, Event evt)
        {
            if (view == null || evt == null || !Panels.TryGetValue(view, out var panel) || panel.panel == null) return false;
            // duringSceneGui uses IMGUI coordinates; Overlay bounds use editor panel coordinates.
            Vector2 point = GUIUtility.GUIToScreenPoint(evt.mousePosition) - view.position.position;
            return Contains(panel.worldBound, point);
        }

        internal static bool Contains(Rect bounds, Vector2 point)
        {
            // Include Unity's native draggable Overlay title immediately above our content.
            bounds.yMin -= 24;
            return bounds.Contains(point);
        }
    }
}
