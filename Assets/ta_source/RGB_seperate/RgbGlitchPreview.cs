using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    public sealed class RgbGlitchPreview : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float fearLevel = 0.45f;
        [SerializeField] private bool automaticCycle = true;
        [SerializeField, Range(0f, 1f)] private float vignetteDarkness = 0.65f;
        [SerializeField, Range(1f, 40f)] private float shakeRate = 15f;
        [SerializeField, Range(0f, 0.04f)] private float shakeStrength = 0.012f;
        [SerializeField, Range(0f, 24f)] private float colorSeparationPixels = 7f;
        [SerializeField, Range(0f, 1f)] private float colorSeparationJitter = 0.7f;

        private float lastFear = -1f;
        private float cycleTime;
        private int cycleStep;
        private void Update()
        {
            // A gameplay player owns the shared pass; the standalone preview must never compete with it.
            if (RgbGlitchSceneDriver.Active != null) return;
            RgbGlitchEffect.SetParameters(vignetteDarkness, shakeRate,
                shakeStrength, colorSeparationPixels, colorSeparationJitter);
            if (!Mathf.Approximately(fearLevel, lastFear))
            {
                RgbGlitchEffect.SetFear(fearLevel, 0f);
                lastFear = fearLevel;
            }

            if (!automaticCycle) return;
            cycleTime += Time.unscaledDeltaTime;
            if (cycleStep == 0 && cycleTime >= 2f)
            {
                RgbGlitchEffect.PlayDeath();
                cycleStep = 1;
            }
            else if (cycleStep == 1 && cycleTime >= 4f)
            {
                RgbGlitchEffect.PlayRespawn();
                cycleStep = 2;
            }
            else if (cycleTime >= 6f)
            {
                cycleTime = 0f;
                cycleStep = 0;
            }
        }

        private void OnDisable()
        {
            if (RgbGlitchSceneDriver.Active != null) return;
            RgbGlitchEffect.ResetEffect();
            RgbGlitchEffect.UseRendererDefaults();
        }
    }
}
