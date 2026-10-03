using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Only owns screen-space HSV saturation. It never changes block attributes or materials.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class HSVColorFadeManager : MonoBehaviour
    {
        private sealed class FadeState
        {
            public float saturation;
            public float target;
            public float speed;
        }

        private static HSVColorFadeManager instance;
        private readonly Dictionary<string, FadeState> states = new Dictionary<string, FadeState>(StringComparer.Ordinal);
        [SerializeField, Min(0.01f)] private float defaultDuration = 1.5f;

        public static HSVColorFadeManager Instance => EnsureCreated();

        public static HSVColorFadeManager EnsureCreated()
        {
            if (instance != null) return instance;
            var existing = FindFirstObjectByType<HSVColorFadeManager>();
            if (existing != null) return existing;
            return new GameObject("HSV Color Fade Manager").AddComponent<HSVColorFadeManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SetColorFaded(string typeId, bool faded, float duration = -1f)
        {
            if (string.IsNullOrEmpty(typeId)) return;
            if (!states.TryGetValue(typeId, out var state))
            {
                state = new FadeState();
                states.Add(typeId, state);
            }
            state.target = faded ? 0f : 1f;
            float seconds = duration < 0f ? defaultDuration : duration;
            if (seconds <= 0f) state.saturation = state.target;
            else state.speed = 1f / seconds;
        }

        public float GetSaturation(string typeId) =>
            states.TryGetValue(typeId, out var state) ? state.saturation : 0f;

        public void ResetAll() => states.Clear();

        private void Update()
        {
            foreach (var pair in states)
            {
                var state = pair.Value;
                state.saturation = Mathf.MoveTowards(
                    state.saturation, state.target, state.speed * Time.unscaledDeltaTime);
            }
        }
    }
}
