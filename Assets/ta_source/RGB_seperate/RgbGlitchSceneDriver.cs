using Project.Player;
using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    [DisallowMultipleComponent]
    public sealed class RgbGlitchSceneDriver : MonoBehaviour
    {
        [SerializeField] private PlayerController targetPlayer;
        [SerializeField, Range(0f, 1f)] private float fearLevel = 0.45f;
        [SerializeField, Range(0f, 1f)] private float vignetteDarkness = 0.65f;
        [SerializeField, Range(1f, 40f)] private float shakeRate = 15f;
        [SerializeField, Range(0f, 0.04f)] private float shakeStrength = 0.012f;
        [SerializeField, Range(0f, 24f)] private float colorSeparationPixels = 7f;
        [SerializeField, Min(0f)] private float deathFadeSeconds = 0.25f;
        [SerializeField, Min(0f)] private float respawnFadeSeconds = 0.7f;

        private PlayerController boundPlayer;
        private bool wasDead;
        private float appliedFear = -1f;
        private Vector4 appliedParameters;
        private bool hasAppliedParameters;

        public void PlayDeath()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                RgbGlitchEffect.PlayDeath(deathFadeSeconds);
        }

        public void PlayRespawn()
        {
            if (Application.isPlaying && isActiveAndEnabled)
                RgbGlitchEffect.PlayRespawn(respawnFadeSeconds);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            appliedFear = -1f;
            hasAppliedParameters = false;
            ApplyParameters();
            ApplyFear();
            BindPlayer();
        }

        private void Update()
        {
            ApplyParameters();
            ApplyFear();
            BindPlayer();
            if (boundPlayer == null) return;

            bool isDead = boundPlayer.IsDead;
            if (isDead == wasDead) return;
            wasDead = isDead;
            if (isDead) PlayDeath();
            else PlayRespawn();
        }

        private void OnDisable()
        {
            UnbindPlayer();
            appliedFear = -1f;
            hasAppliedParameters = false;
            if (!Application.isPlaying) return;
            RgbGlitchEffect.ResetEffect();
            RgbGlitchEffect.UseRendererDefaults();
        }

        private void ApplyParameters()
        {
            var parameters = new Vector4(vignetteDarkness, shakeRate,
                shakeStrength, colorSeparationPixels);
            if (hasAppliedParameters && parameters == appliedParameters) return;
            RgbGlitchEffect.SetParameters(parameters.x, parameters.y,
                parameters.z, parameters.w);
            appliedParameters = parameters;
            hasAppliedParameters = true;
        }

        private void ApplyFear()
        {
            if (Mathf.Approximately(fearLevel, appliedFear)) return;
            RgbGlitchEffect.SetFear(fearLevel, 0f);
            appliedFear = fearLevel;
        }

        private void BindPlayer()
        {
            PlayerController candidate = targetPlayer != null
                ? targetPlayer
                : boundPlayer != null ? boundPlayer : FindFirstObjectByType<PlayerController>();
            if (candidate == boundPlayer) return;

            UnbindPlayer();
            if (candidate == null) return;
            boundPlayer = candidate;
            boundPlayer.CameraFollowRelocated += HandleRespawn;
            wasDead = boundPlayer.IsDead;
            if (wasDead) RgbGlitchEffect.PlayDeath(0f);
        }

        private void HandleRespawn()
        {
            wasDead = false;
            PlayRespawn();
        }

        private void UnbindPlayer()
        {
            if (boundPlayer != null) boundPlayer.CameraFollowRelocated -= HandleRespawn;
            boundPlayer = null;
        }
    }
}
