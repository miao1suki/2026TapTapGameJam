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
            DrawColliderRange(
                shape,
                (gizmoType & GizmoType.Selected) != 0);
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

        private static void DrawColliderRange(
            Collider collider,
            bool selected)
        {
            if (collider == null || !collider.enabled)
            {
                return;
            }

            Matrix4x4 previousMatrix = Handles.matrix;
            Color previousColor = Handles.color;
            Handles.color = selected
                ? new Color(1f, .78f, .3f, .9f)
                : new Color(1f, .78f, .3f, .45f);
            if (collider is BoxCollider box)
            {
                Handles.matrix =
                    box.transform.localToWorldMatrix;
                DrawDottedBox(
                    new Bounds(
                        box.center,
                        box.size));
            }
            else
            {
                Handles.matrix = Matrix4x4.identity;
                DrawDottedBox(collider.bounds);
            }
            Handles.matrix = previousMatrix;
            Handles.color = previousColor;
        }

        private static void DrawDottedBox(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z),
                new Vector3(min.x, max.y, max.z),
            };
            int[,] edges =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
            };
            for (int index = 0;
                 index < edges.GetLength(0);
                 index++)
            {
                Handles.DrawDottedLine(
                    corners[edges[index, 0]],
                    corners[edges[index, 1]],
                    5f);
            }
        }
    }
}
