using System.Collections.Generic;
using UnityEngine;

namespace InteractiveWater
{
    /// <summary>
    /// Requests the shared underlay capture while a standalone cross-section water panel is active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaterUnderlayCaptureRequester : MonoBehaviour
    {
        private static readonly HashSet<WaterUnderlayCaptureRequester> activeRequests = new();

        public static bool HasActiveRequests => activeRequests.Count > 0;

        private void OnEnable()
        {
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
