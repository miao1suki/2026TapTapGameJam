using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    [DisallowMultipleComponent]
    public sealed class RgbGlitchDeathDemo : MonoBehaviour
    {
        private const float DeathAtSeconds = 2f;
        private const float RespawnAtSeconds = 4f;

        private RgbGlitchSceneDriver driver;
        private float elapsed;
        private int phase;

        private void OnEnable()
        {
            elapsed = 0f;
            phase = 0;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (driver == null) driver = FindFirstObjectByType<RgbGlitchSceneDriver>();
            if (driver == null) return;

            elapsed += Time.unscaledDeltaTime;
            if (phase == 0 && elapsed >= DeathAtSeconds)
            {
                driver.PlayDeath();
                phase = 1;
            }
            else if (phase == 1 && elapsed >= RespawnAtSeconds)
            {
                driver.PlayRespawn();
                phase = 2;
            }
        }
    }
}
