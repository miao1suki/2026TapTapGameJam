using UnityEngine;

namespace Project.Pixelization
{
    public static class PixelizationControl
    {
        public static bool OutlineEnabled { get; private set; }
        public static void SetOutlineEnabled(bool enabled) => OutlineEnabled = enabled;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => OutlineEnabled = false;
    }
}
