using UnityEditor;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    /// <summary>Editor-only marker. Never adds a renderer or image material to the game.</summary>
    public static class ContactTimelineEditorPreview
    {
        public const string TexturePath = "Assets/_Project/Resources/UI/棍母.png";
        private static Texture2D texture;
        public static Texture2D Texture
        {
            get
            {
                if (texture == null) texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
                return texture;
            }
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawMarker(ContactTimelineTrigger trigger, GizmoType gizmoType)
        {
            if (Application.isPlaying || SceneView.currentDrawingSceneView == null || trigger == null) return;
            Texture2D texture = Texture;
            Collider shape = trigger.GetComponent<Collider>();
            if (texture == null || shape == null) return;
            Bounds bounds = shape.bounds;
            Camera camera = SceneView.currentDrawingSceneView.camera;
            if (camera.WorldToViewportPoint(bounds.center).z <= 0f) return;
            Vector2 a = HandleUtility.WorldToGUIPoint(bounds.center - camera.transform.right * bounds.extents.x - camera.transform.up * bounds.extents.y);
            Vector2 b = HandleUtility.WorldToGUIPoint(bounds.center + camera.transform.right * bounds.extents.x + camera.transform.up * bounds.extents.y);
            var rect = Rect.MinMaxRect(Mathf.Min(a.x,b.x), Mathf.Min(a.y,b.y), Mathf.Max(a.x,b.x), Mathf.Max(a.y,b.y));
            Handles.BeginGUI();
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .65f);
            GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit);
            GUI.color = previous;
            Handles.EndGUI();
        }
    }
}
