using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Only controls the layer-based HSV pass; never touches Renderer, materials or gameplay.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class HSVColorFadeManager : MonoBehaviour
    {
        private sealed class State
        {
            public float saturation, white, fromSaturation, fromWhite, targetSaturation, targetWhite, elapsed, duration;
        }
        private static HSVColorFadeManager instance;
        private readonly Dictionary<string, State> states = new Dictionary<string, State>();
        [SerializeField, Min(.01f)] private float defaultDuration = 5f;
        [SerializeField, Min(1f)] private float durationMultiplier = 3f;
        public static HSVColorFadeManager Instance => EnsureCreated();
        public static HSVColorFadeManager EnsureCreated()
        {
            if (instance != null) return instance;
            var existing = ProjectDiscovery.FindFirst<HSVColorFadeManager>();
            if (existing != null) return existing;
            return new GameObject("HSV Color Fade Manager").AddComponent<HSVColorFadeManager>();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        public float GetEffectiveDuration(float duration = -1f) =>
            duration < 0f ? defaultDuration : duration > 0f ? duration * durationMultiplier : 0f;
        public float GetSaturation(string id) => states.TryGetValue(id, out var state) ? state.saturation : 0f;
        public float GetWhiteAmount(string id) => states.TryGetValue(id, out var state) ? state.white : 0f;

        /// <summary>Seconds are literal, without the appearance duration multiplier. Zero writes synchronously.</summary>
        public void SetHsv(string id, float saturation, float whiteAmount, float seconds = 0f)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (!states.TryGetValue(id, out var state)) { state = new State(); states.Add(id, state); }
            saturation = Mathf.Clamp01(saturation);
            whiteAmount = Mathf.Clamp01(whiteAmount);
            if (state.targetSaturation == saturation && state.targetWhite == whiteAmount &&
                state.duration > 0f && seconds > 0f) return;
            state.fromSaturation = state.saturation;
            state.fromWhite = state.white;
            state.targetSaturation = saturation;
            state.targetWhite = whiteAmount;
            state.elapsed = 0f;
            state.duration = Mathf.Max(0f, seconds);
            if (seconds <= 0f) { state.saturation = saturation; state.white = whiteAmount; }
        }

        /// <summary>Pure HSV convenience API. Does not unlock objects or switch appearances.</summary>
        public void SetColorFaded(string id, bool faded, float duration = -1f) =>
            SetHsv(id, faded ? 0f : 1f, 0f, GetEffectiveDuration(duration));
        public void ResetAll() => states.Clear();
        private void Update()
        {
            foreach (var state in states.Values)
            {
                if (state.elapsed >= state.duration) continue;
                state.elapsed = Mathf.Min(state.duration, state.elapsed + Time.unscaledDeltaTime);
                float t = Mathf.SmoothStep(0f, 1f, state.elapsed / state.duration);
                state.saturation = Mathf.Lerp(state.fromSaturation, state.targetSaturation, t);
                state.white = Mathf.Lerp(state.fromWhite, state.targetWhite, t);
            }
        }
    }
}
