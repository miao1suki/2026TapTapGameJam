using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>Presentation only. Offscreen gameplay, contacts and global unlocks keep running.</summary>
    public static class ColorPresentationVisibility
    {
        private static readonly Plane[] planes = new Plane[6];
        private static Camera camera;
        private static int cameraFrame = -1;
        private static Matrix4x4 viewProjection;
        private static bool planesValid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            camera = null;
            cameraFrame = -1;
            planesValid = false;
        }

        public static bool IsNearCamera(Bounds bounds, float cellSize)
        {
            if (!Application.isPlaying) return true;
            if (cameraFrame != Time.frameCount)
            {
                cameraFrame = Time.frameCount;
                camera = Camera.main;
            }
            // Non-camera gameplay fixtures also use the same presentation code.
            if (!camera || !camera.isActiveAndEnabled) return true;
            Matrix4x4 next = camera.projectionMatrix * camera.worldToCameraMatrix;
            if (!planesValid || next != viewProjection)
            {
                viewProjection = next;
                GeometryUtility.CalculateFrustumPlanes(next, planes);
                planesValid = true;
            }
            bounds.Expand(Mathf.Max(.01f, cellSize) * 4f);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
    }
}
