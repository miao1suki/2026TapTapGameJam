using Project.Player;
using UnityEngine;

namespace TapTap.TA.RgbGlitch
{
    /// <summary>One lifecycle owner. The standard player's driver takes precedence over optional demo rigs.</summary>
    [DisallowMultipleComponent]
    public sealed class RgbGlitchSceneDriver : MonoBehaviour
    {
        [SerializeField] private PlayerController targetPlayer;
        [SerializeField, Range(0f, 1f)] private float fearLevel;
        [SerializeField, Range(0f, 1f)] private float vignetteDarkness = 0.65f;
        [SerializeField, Range(1f, 40f)] private float shakeRate = 15f;
        [SerializeField, Range(0f, 0.04f)] private float shakeStrength = 0.018f;
        [SerializeField, Range(0f, 24f)] private float colorSeparationPixels = 10f;
        [SerializeField, Range(0f, 1f)] private float colorSeparationJitter = 0.85f;
        [SerializeField, Min(0f)] private float deathFadeSeconds;
        [SerializeField, Min(0f)] private float respawnFadeSeconds = 1.2f;
        [SerializeField, Range(0f, 1f)] private float bossFearLevel = 0.4f;
        [SerializeField, Range(0f, 1f)] private float bossVignetteDarkness = 0.28f;
        [SerializeField, Range(1f, 40f)] private float bossShakeRate = 8f;
        [SerializeField, Range(0f, 0.04f)] private float bossShakeStrength = 0.0035f;
        [SerializeField, Range(0f, 24f)] private float bossColorSeparationPixels = 6f;
        [SerializeField, Range(0f, 1f)] private float bossColorSeparationJitter = 0.55f;

        private static RgbGlitchSceneDriver active;
        private PlayerController attachedPlayer;
        private PlayerController boundPlayer;
        private PlayerHealth boundHealth;
        private bool wasDead;
        private bool bossFearActive;
        private bool endingBossFear;
        private float appliedFear = -1f;
        private Vector4 appliedParameters;
        private float appliedSeparationJitter;
        private bool hasAppliedParameters;
        private float nextPlayerSearch;

        public static RgbGlitchSceneDriver Active => active;
        public bool OwnsEffect => active == this;
        public bool BossFearActive => bossFearActive;
        public float RespawnFadeSeconds =>
            float.IsFinite(respawnFadeSeconds)
                ? Mathf.Max(0f, respawnFadeSeconds)
                : 1.2f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwnership() => active = null;

        public void PlayDeath()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !OwnsEffect) return;
            wasDead = true;
            ApplyParameters();
            RgbGlitchEffect.PlayDeath(deathFadeSeconds);
        }

        public void PlayRespawn()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !OwnsEffect) return;
            wasDead = false;
            RgbGlitchEffect.PlayRespawn(respawnFadeSeconds);
        }

        /// <summary>Explicit Boss encounter entry; merely finding a Boss never enables fear.</summary>
        public void BeginBossFear(float seconds = 1f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !OwnsEffect) return;
            bossFearActive = true;
            endingBossFear = false;
            ApplyParameters();
            ApplyFear(seconds);
        }

        public void EndBossFear(float seconds = 1f)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !OwnsEffect) return;
            bossFearActive = false;
            endingBossFear = true;
            ApplyFear(seconds);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            attachedPlayer = GetComponent<PlayerController>();
            PlayerController.PlayerActivated += HandlePlayerActivated;
            appliedFear = -1f;
            hasAppliedParameters = false;
            nextPlayerSearch = 0f;
            BindPlayer();
            TryAcquireOwnership();
        }

        private void Update()
        {
            BindPlayer();
            TryAcquireOwnership();
            if (!OwnsEffect) return;
            if (boundPlayer != null && boundPlayer.IsDead && !wasDead) HandleDied();

            // IsDead becomes false beneath the loading cover. Only RespawnCompleted may start the fade.
            if (endingBossFear && RgbGlitchEffect.Intensity <= fearLevel + 0.001f)
                endingBossFear = false;
            ApplyParameters();
            ApplyFear(0f);
        }

        private void OnDisable()
        {
            PlayerController.PlayerActivated -= HandlePlayerActivated;
            UnbindPlayer();
            bossFearActive = false;
            endingBossFear = false;
            appliedFear = -1f;
            hasAppliedParameters = false;
            if (!OwnsEffect) return;
            active = null;
            RgbGlitchEffect.ResetEffect();
            RgbGlitchEffect.UseRendererDefaults();
        }

        private void TryAcquireOwnership()
        {
            if (OwnsEffect || !Application.isPlaying || !isActiveAndEnabled) return;
            if (attachedPlayer != null && !attachedPlayer.isActiveAndEnabled) return;
            if (active != null && active.isActiveAndEnabled &&
                (attachedPlayer == null || active.attachedPlayer != null)) return;

            // Optional old scene rigs stay passive. Disabling one must not clear the player's live effect.
            active = this;
            wasDead = boundPlayer != null && boundPlayer.IsDead;
            appliedFear = -1f;
            hasAppliedParameters = false;
            RgbGlitchEffect.ResetEffect();
            ApplyParameters();
            ApplyFear(0f);
            if (wasDead) RgbGlitchEffect.PlayDeath(0f);
        }

        private void ApplyParameters()
        {
            bool useBoss = (bossFearActive || endingBossFear) && !wasDead &&
                RgbGlitchEffect.DeathIntensity <= 0.001f;
            var parameters = useBoss
                ? new Vector4(bossVignetteDarkness, bossShakeRate, bossShakeStrength, bossColorSeparationPixels)
                : new Vector4(vignetteDarkness, shakeRate, shakeStrength, colorSeparationPixels);
            float jitter = useBoss ? bossColorSeparationJitter : colorSeparationJitter;
            if (hasAppliedParameters && parameters == appliedParameters &&
                Mathf.Approximately(jitter, appliedSeparationJitter)) return;
            RgbGlitchEffect.SetParameters(parameters.x, parameters.y, parameters.z, parameters.w, jitter);
            appliedParameters = parameters;
            appliedSeparationJitter = jitter;
            hasAppliedParameters = true;
        }

        private void ApplyFear(float seconds)
        {
            float next = bossFearActive ? bossFearLevel : fearLevel;
            if (Mathf.Approximately(next, appliedFear)) return;
            RgbGlitchEffect.SetFear(next, seconds);
            appliedFear = next;
        }

        private void BindPlayer()
        {
            PlayerController candidate = targetPlayer != null ? targetPlayer : attachedPlayer;
            if (candidate == null) candidate = boundPlayer;
            if (candidate == null && Time.unscaledTime >= nextPlayerSearch)
            {
                candidate = Project.ProjectDiscovery.FindFirst<PlayerController>();
                nextPlayerSearch = Time.unscaledTime + 0.25f;
            }
            if (candidate == boundPlayer) return;
            UnbindPlayer();
            boundPlayer = candidate;
            if (boundPlayer == null) return;
            boundPlayer.RespawnCompleted += HandleRespawn;
            boundPlayer.ControlLockChanged += HandleControlLockChanged;
            boundHealth = boundPlayer.GetComponent<PlayerHealth>();
            if (boundHealth != null) boundHealth.Died += HandleDied;
            if (OwnsEffect && boundPlayer.IsDead) HandleDied();
        }

        private void HandlePlayerActivated(PlayerController player)
        {
            if (boundPlayer != null) return;
            nextPlayerSearch = 0f;
            BindPlayer();
        }

        private void HandleDied()
        {
            if (!OwnsEffect || wasDead) return;
            PlayDeath();
        }

        private void HandleControlLockChanged(bool isLocked)
        {
            if (boundPlayer != null && boundPlayer.IsDead) HandleDied();
        }

        private void HandleRespawn() => PlayRespawn();

        private void UnbindPlayer()
        {
            if (boundHealth != null) boundHealth.Died -= HandleDied;
            boundHealth = null;
            if (boundPlayer != null)
            {
                boundPlayer.RespawnCompleted -= HandleRespawn;
                boundPlayer.ControlLockChanged -= HandleControlLockChanged;
            }
            boundPlayer = null;
        }
    }
}
