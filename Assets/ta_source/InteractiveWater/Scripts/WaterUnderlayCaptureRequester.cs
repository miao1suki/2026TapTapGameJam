using System.Collections.Generic;
using UnityEngine;

namespace InteractiveWater
{
    /// <summary>
    /// Requests the post-transparent scene-color copy and refraction draw for active pixel-water panels.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class WaterUnderlayCaptureRequester : MonoBehaviour
    {
        private static readonly HashSet<WaterUnderlayCaptureRequester> activeRequests = new();
        private static readonly Plane[] planes = new Plane[6];
        private Renderer panel;

        public static bool HasActiveRequests => activeRequests.Count > 0;

        public static bool HasVisibleRequests(Camera camera)
        {
            if (!camera || activeRequests.Count == 0) return false;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            foreach (var request in activeRequests)
                if (request != null && request.panel != null && request.panel.enabled &&
                    (camera.cullingMask & (1 << request.gameObject.layer)) != 0 &&
                    GeometryUtility.TestPlanesAABB(planes, request.panel.bounds)) return true;
            return false;
        }

        private void OnEnable()
        {
            panel = GetComponent<Renderer>();
            activeRequests.Add(this);
        }

        private void OnDisable()
        {
            activeRequests.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRequests()
        {
            activeRequests.Clear();
        }
    }
}
