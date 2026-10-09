using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Owns neutral-to-white handoff and white-to-color presentation, not gameplay activation.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class HSVColorFadeManager : MonoBehaviour
    {
        private sealed class FadeState
        {
            public float progress;
            public float target;
            public float speed;
            public bool realAppearance;
        }

        private static HSVColorFadeManager instance;
        private readonly Dictionary<string, FadeState> states = new Dictionary<string, FadeState>(StringComparer.Ordinal);
        [SerializeField, Min(0.01f)] private float defaultDuration = 5f;
        [SerializeField, Min(1f), Tooltip("对所有非立即褪色请求的时长倍率，也作用于钥匙预制体已有配置。")]
        private float durationMultiplier = 3f;
        private const float HandoffProgress = .3f;
        public event Action<string> AppearanceChanged;

        public static HSVColorFadeManager Instance => EnsureCreated();

        public static HSVColorFadeManager EnsureCreated()
        {
            if (instance != null) return instance;
            var existing =
                ProjectDiscovery.FindFirst<HSVColorFadeManager>();
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
            float seconds = GetEffectiveDuration(duration);
            if (seconds <= 0f)
            {
                state.progress = state.target;
                state.realAppearance = !faded;
            }
            else state.speed = 1f / seconds;
            AppearanceChanged?.Invoke(typeId);
        }

        public float GetEffectiveDuration(float duration = -1f) =>
            duration < 0f ? defaultDuration : (duration > 0f ? duration * durationMultiplier : 0f);

        public float GetSaturation(string typeId) =>
            states.TryGetValue(typeId, out var state)
                ? Mathf.InverseLerp(HandoffProgress, 1f, state.progress) : 0f;

        public bool ShowsUnlockedAppearance(string typeId) =>
            states.TryGetValue(typeId, out var state) && state.realAppearance;

        public float GetWhiteAmount(string typeId)
        {
            if (!states.TryGetValue(typeId, out var state)) return 0f;
            return state.progress < HandoffProgress
                ? state.progress / HandoffProgress
                : 1f - GetSaturation(typeId);
        }

        public void ResetAll()
        {
            states.Clear();
            AppearanceChanged?.Invoke(null);
        }

        private void Update()
        {
            foreach (var pair in states)
            {
                var state = pair.Value;
                // Render one pure-white frame of each appearance at the handoff.
                if (state.target > state.progress && state.progress == HandoffProgress && !state.realAppearance)
                {
                    state.realAppearance = true;
                    AppearanceChanged?.Invoke(pair.Key);
                    continue;
                }
                float previous = state.progress;
                state.progress = Mathf.MoveTowards(
                    state.progress, state.target, state.speed * Time.unscaledDeltaTime);
                if (previous < HandoffProgress && state.progress >= HandoffProgress && state.target > previous)
                    state.progress = HandoffProgress;
                if (state.realAppearance && state.progress < HandoffProgress)
                {
                    state.realAppearance = false;
                    AppearanceChanged?.Invoke(pair.Key);
                }
            }
        }
    }
}
