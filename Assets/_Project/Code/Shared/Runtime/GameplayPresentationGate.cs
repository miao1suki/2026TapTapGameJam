using System;
using UnityEngine;

namespace Project
{
    /// <summary>Suppresses gameplay interaction while a real level is presented behind a menu.
    /// Visual components remain enabled: this is not a room reset or a time-scale pause.</summary>
    public static class GameplayPresentationGate
    {
        private static int depth;
        public static bool IsSuppressed => depth > 0;
        // Physics ignore pairs can be lost when preview disables participant colliders.
        // Restore them once, after every owner has restored its participants.
        public static event Action GameplayRestored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { depth = 0; GameplayRestored = null; }

        public static IDisposable Suppress()
        {
            depth++;
            return new Lease();
        }

        private sealed class Lease : IDisposable
        {
            private bool disposed;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                depth = Mathf.Max(0, depth - 1);
                if (depth == 0) GameplayRestored?.Invoke();
            }
        }
    }
}
