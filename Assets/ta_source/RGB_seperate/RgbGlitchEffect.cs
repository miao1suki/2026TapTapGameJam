using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    /// <summary>Full-screen fear and death effect. Calls are safe without a scene object.</summary>
    public sealed class RgbGlitchEffect : MonoBehaviour
    {
        private struct Fade
        {
            public float value;
            private float start;
            private float target;
            private float elapsed;
            private float duration;

            public void Set(float next, float seconds)
            {
                start = value;
                target = Mathf.Clamp01(next);
                elapsed = 0f;
                duration = Mathf.Max(0f, seconds);
                if (duration == 0f) value = target;
            }

            public void Tick(float deltaTime)
            {
                if (elapsed >= duration) return;
                elapsed = Mathf.Min(duration, elapsed + deltaTime);
                value = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            }
        }

        private static RgbGlitchEffect instance;
        private static bool hasParameterOverride;
        private static Vector4 parameterOverride;
        private static float separationJitterOverride;
        private Fade fear;
        private Fade death;

        public static float Intensity => instance != null ? Mathf.Max(instance.fear.value, instance.death.value) : 0f;

        internal static float DeathIntensity => instance != null ? instance.death.value : 0f;

        /// <summary>Enable the mild Boss preset on the current player's lifecycle driver, if present.</summary>
        public static bool BeginBossFear(float seconds = 1f)
        {
            if (RgbGlitchSceneDriver.Active == null) return false;
            RgbGlitchSceneDriver.Active.BeginBossFear(seconds);
            return true;
        }

        /// <summary>Return to the authored normal baseline; never creates a player or scene rig.</summary>
        public static bool EndBossFear(float seconds = 1f)
        {
            if (RgbGlitchSceneDriver.Active == null) return false;
            RgbGlitchSceneDriver.Active.EndBossFear(seconds);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hasParameterOverride = false;
        }

        private static RgbGlitchEffect EnsureCreated()
        {
            if (instance != null) return instance;
            var effectObject = new GameObject("RGB Glitch Effect");
            instance = effectObject.AddComponent<RgbGlitchEffect>();
            DontDestroyOnLoad(effectObject);
            return instance;
        }

        /// <summary>Set the persistent fear baseline from 0 (clear) to 1 (maximum).</summary>
        public static void SetFear(float level, float seconds = 0.6f) =>
            EnsureCreated().fear.Set(level, seconds);

        /// <summary>Increase the death layer to maximum while leaving the fear baseline intact.</summary>
        public static void PlayDeath(float seconds = 0f) =>
            EnsureCreated().death.Set(1f, seconds);

        /// <summary>Fade the death layer out, returning to the current fear baseline.</summary>
        public static void PlayRespawn(float seconds = 1.2f) =>
            EnsureCreated().death.Set(0f, seconds);

        /// <summary>Override renderer tuning until UseRendererDefaults is called.</summary>
        public static void SetParameters(float vignetteDarkness, float shakeRate,
            float shakeStrength, float colorSeparationPixels)
        {
            SetParameters(vignetteDarkness, shakeRate, shakeStrength,
                colorSeparationPixels, 0.7f);
        }

        public static void SetParameters(float vignetteDarkness, float shakeRate,
            float shakeStrength, float colorSeparationPixels, float colorSeparationJitter)
        {
            parameterOverride = new Vector4(
                Mathf.Clamp01(vignetteDarkness),
                Mathf.Clamp(shakeRate, 1f, 40f),
                Mathf.Clamp(shakeStrength, 0f, 0.04f),
                Mathf.Clamp(colorSeparationPixels, 0f, 24f));
            separationJitterOverride = Mathf.Clamp01(colorSeparationJitter);
            hasParameterOverride = true;
        }

        /// <summary>Remove the runtime override and use the active Renderer Asset values.</summary>
        public static void UseRendererDefaults() => hasParameterOverride = false;

        internal static bool TryGetParameters(out Vector4 parameters, out float colorSeparationJitter)
        {
            parameters = parameterOverride;
            colorSeparationJitter = separationJitterOverride;
            return hasParameterOverride;
        }

        /// <summary>Immediately clear both layers, such as when leaving gameplay.</summary>
        public static void ResetEffect()
        {
            if (instance == null) return;
            instance.fear.Set(0f, 0f);
            instance.death.Set(0f, 0f);
        }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void Tick(float deltaTime)
        {
            fear.Tick(deltaTime);
            death.Tick(deltaTime);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
