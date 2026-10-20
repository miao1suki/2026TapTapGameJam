using UnityEditor;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    /// <summary>Camera-local culling for read-only authoring gizmos, never gameplay.</summary>
    public static class ScenePreviewCulling
    {
        private static readonly Plane[] planes = new Plane[6];
        private static Camera lastCamera;
        private static Matrix4x4 lastView, lastProjection;

        private static bool Prepare()
        {
            var view = SceneView.currentDrawingSceneView;
            Camera camera = view != null ? view.camera : null;
            if (camera == null) return false;
            Matrix4x4 worldToCamera = camera.worldToCameraMatrix, projection = camera.projectionMatrix;
            if (lastCamera != camera || lastView != worldToCamera || lastProjection != projection)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                lastCamera = camera; lastView = worldToCamera; lastProjection = projection;
            }
            return true;
        }

        public static bool Visible(Bounds bounds) => !Prepare() || GeometryUtility.TestPlanesAABB(planes, bounds);

        public static void DrawDottedLine(Vector3 from, Vector3 to, float spacing)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint) return;
            if (Prepare() && !ClipLine(planes, ref from, ref to)) return;
            Handles.DrawDottedLine(from, to, spacing);
        }

        // Clip before Handles builds dotted geometry. Testing endpoints alone is
        // wrong: a long link can cross the view with both endpoints offscreen.
        public static bool ClipLine(Plane[] frustum, ref Vector3 from, ref Vector3 to)
        {
            Vector3 start = from, delta = to - from;
            float low = 0, high = 1;
            foreach (Plane plane in frustum)
            {
                float distance = plane.GetDistanceToPoint(start), slope = Vector3.Dot(plane.normal, delta);
                if (Mathf.Abs(slope) < 1e-8f) { if (distance < 0) return false; continue; }
                float crossing = -distance / slope;
                if (slope > 0) low = Mathf.Max(low, crossing); else high = Mathf.Min(high, crossing);
                if (low > high) return false;
            }
            from = start + delta * low; to = start + delta * high;
            return true;
        }
    }
}
