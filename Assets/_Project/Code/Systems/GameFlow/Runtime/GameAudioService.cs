using System;
using System.Collections;
using System.Collections.Generic;
using Project.Audio;
using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameAudioService : MonoBehaviour, IGameSystemService
    {
        private static readonly IReadOnlyList<string> RandomGameplayMusicCues =
            Array.AsReadOnly(
                new[]
                {
                    AudioCueIds.MusicAmbientLoop,
                    AudioCueIds.MusicAtmosphereAMinor,
                    AudioCueIds.MusicAtmosphereDSharpMinor,
                    AudioCueIds.MusicAtmosphereEMinor,
                    AudioCueIds.MusicAtmosphereFMinor,
                    AudioCueIds.MusicBanjo,
                    AudioCueIds.MusicKeyboard
                });

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource soundEffectSource;
        [SerializeField] private AudioCatalog audioCatalog;
        [SerializeField, Min(0f)] private float randomMusicGapSeconds = 18f;

        private static GameAudioService instance;
        private AudioClip uiClickClip;
        private AudioClip pauseClip;
        private AudioClip levelCompleteClip;
        private AudioManager audioManager;
        private GameFlowController flowController;
        private AudioMusicRotation randomMusicRotation;
        private Coroutine randomMusicRoutine;
        private bool applicationPaused;

        public static GameAudioService Instance => instance;
        public static IReadOnlyList<string> RandomGameplayMusicCueIds =>
            RandomGameplayMusicCues;
        public int InitializationOrder => 0;
        public bool IsInitialized { get; private set; }
        public AudioCatalog Catalog
        {
            get => ResolveCatalog();
            set => audioCatalog = value;
        }

        public AudioSource MusicSource =>
            EnsureAudioManager()?.MusicSource;
        public AudioSource SoundEffectSource =>
            EnsureAudioManager()?.SoundEffectSource;

        public void Configure(
            AudioSource music,
            AudioSource soundEffects)
        {
            // Kept for existing scene/prefab serialization. Playback now
            // funnels through AudioManager so both volume buses are shared.
            musicSource = music;
            soundEffectSource = soundEffects;
        }

        public IEnumerator Initialize()
        {
            audioManager = EnsureAudioManager();
            flowController = GameFlowController.Instance;
            if (flowController != null)
            {
                flowController.ActiveSceneChanged -=
                    HandleActiveSceneChanged;
                flowController.ActiveSceneChanged +=
                    HandleActiveSceneChanged;
                if (flowController.ActiveSceneId.HasValue)
                {
                    HandleActiveSceneChanged(
                        flowController.ActiveSceneId.Value);
                }
            }

            IsInitialized = true;
            yield break;
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            EnsureAudioManager()?.PlayMusic(clip, loop);
        }

        public void StopMusic()
        {
            EnsureAudioManager()?.StopMusic();
        }

        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            EnsureAudioManager()?.PlaySound(clip, volume);
        }

        public void PlaySound(AudioClip[] clips, float volume = 1f)
        {
            EnsureAudioManager()?.PlaySound(clips, volume);
        }

        public bool PlayCue(
            string cueId,
            float volumeScale = 1f)
        {
            return AudioCuePlayer.Play(
                ResolveCatalog(),
                cueId,
                volumeScale);
        }

        public bool PlayCueLoop(
            string cueId,
            object owner,
            float volumeScale = 1f)
        {
            return AudioCuePlayer.PlayLoop(
                ResolveCatalog(),
                cueId,
                owner,
                volumeScale);
        }

        public MusicRequestHandle RequestMusic(
            string cueId,
            object owner,
            float volumeScale = 1f)
        {
            return AudioCuePlayer.RequestMusic(
                ResolveCatalog(),
                cueId,
                owner,
                volumeScale);
        }

        public AudioSource PlayLoop(
            object owner,
            AudioClip clip,
            float volume = 1f,
            bool ignoreListenerPause = false,
            AudioChannel channel = AudioChannel.SoundEffect)
        {
            return EnsureAudioManager()?.PlayLoop(
                owner,
                clip,
                volume,
                ignoreListenerPause,
                channel);
        }

        public bool IsLoopPlaying(object owner)
        {
            return EnsureAudioManager()?.IsLoopPlaying(owner) ?? false;
        }

        public AudioSource GetLoop(object owner)
        {
            return EnsureAudioManager()?.GetLoop(owner);
        }

        public void StopLoop(object owner)
        {
            EnsureAudioManager()?.StopLoop(owner);
        }

        public void StopAllLoops()
        {
            EnsureAudioManager()?.StopAllLoops();
        }

        public void PlayUiClick()
        {
            if (AudioCuePlayer.PlayUiConfirm(ResolveCatalog(), .7f)) return;
            uiClickClip ??= CreateTone(
                "UI_Click",
                620f,
                0.055f,
                0.22f);
            PlaySound(uiClickClip, 0.7f);
        }

        public void PlayPause()
        {
            if (AudioCuePlayer.PlayUiConfirm(ResolveCatalog(), .65f)) return;
            pauseClip ??= CreateTone(
                "UI_Pause",
                360f,
                0.09f,
                0.2f);
            PlaySound(pauseClip, 0.65f);
        }

        public void PlayLevelComplete()
        {
            levelCompleteClip ??=
                CreateTone(
                    "Level_Complete",
                    840f,
                    0.22f,
                    0.24f,
                    1.45f);
            PlaySound(levelCompleteClip, 0.85f);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            randomMusicRotation =
                new AudioMusicRotation(RandomGameplayMusicCues);
            EnsureAudioManager();
        }

        private void OnDisable()
        {
            StopRandomGameplayMusic(true);
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
        }

        private void OnDestroy()
        {
            StopRandomGameplayMusic(false);
            if (flowController != null)
            {
                flowController.ActiveSceneChanged -=
                    HandleActiveSceneChanged;
                flowController = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private AudioCatalog ResolveCatalog()
        {
            return audioCatalog != null
                ? audioCatalog
                : AudioCatalog.LoadDefault();
        }

        private AudioManager EnsureAudioManager()
        {
            if (audioManager == null)
            {
                audioManager = AudioManager.EnsureInstance();
            }

            return audioManager;
        }

        private void HandleActiveSceneChanged(GameFlowSceneId sceneId)
        {
            if (sceneId == GameFlowSceneId.MainMenu)
            {
                // MainMenu stays loaded over the live level; OnEnable is no longer a
                // reliable menu-entry signal. Route music from the logical flow state.
                StopRandomGameplayMusic(false);
                AudioCuePlayer.Play(ResolveCatalog(), AudioCueIds.MusicMenuOpening);
                return;
            }

            StartRandomGameplayMusic();
        }

        private void StartRandomGameplayMusic()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            StopRandomGameplayMusic(false);
            randomMusicRotation ??=
                new AudioMusicRotation(RandomGameplayMusicCues);
            randomMusicRoutine =
                StartCoroutine(RunRandomGameplayMusic());
        }

        private void StopRandomGameplayMusic(bool stopBaseMusic)
        {
            if (randomMusicRoutine != null)
            {
                StopCoroutine(randomMusicRoutine);
                randomMusicRoutine = null;
            }

            if (stopBaseMusic)
            {
                EnsureAudioManager()?.StopMusic();
            }
        }

        private IEnumerator RunRandomGameplayMusic()
        {
            while (true)
            {
                AudioManager manager =
                    EnsureAudioManager();
                if (manager == null)
                {
                    yield return null;
                    continue;
                }

                if (manager.HasActiveMusicOverride)
                {
                    yield return null;
                    continue;
                }

                string cueId =
                    randomMusicRotation?.Next();
                AudioClip clip =
                    AudioCuePlayer.PlayMusicOnce(
                        ResolveCatalog(),
                        cueId);
                if (clip == null)
                {
                    yield return WaitForRandomMusicGap();
                    continue;
                }

                int lastSample = 0;
                double lastPlaybackDspTime = AudioSettings.dspTime;
                bool observedPlayback = false;
                float startupWait = 0f;
                // Play/streaming initialization may not be reflected by isPlaying in the same frame.
                yield return null;

                while (true)
                {
                    manager = EnsureAudioManager();
                    if (manager == null)
                    {
                        break;
                    }

                    if (manager.HasActiveMusicOverride ||
                        AudioListener.pause || applicationPaused)
                    {
                        lastPlaybackDspTime = AudioSettings.dspTime;
                        yield return null;
                        continue;
                    }

                    AudioSource source = manager.MusicSource;
                    if (source == null ||
                        source.clip != clip)
                    {
                        break;
                    }

                    if (clip.loadState == AudioDataLoadState.Loading)
                    {
                        yield return null;
                        continue;
                    }

                    if (source.isPlaying)
                    {
                        observedPlayback = true;
                        lastSample = source.timeSamples;
                        lastPlaybackDspTime = AudioSettings.dspTime;
                    }
                    else if (observedPlayback)
                    {
                        if (HasReachedMusicEnd(clip, lastSample,
                                AudioSettings.dspTime - lastPlaybackDspTime, source.pitch))
                            break;
                        // Audio device / application suspension can stop a source without finishing it.
                        // Resume the interrupted track rather than silently consuming it as a completed song.
                        source.Play();
                        source.timeSamples = Mathf.Clamp(lastSample, 0, Mathf.Max(0, clip.samples - 1));
                        lastPlaybackDspTime = AudioSettings.dspTime;
                    }
                    else
                    {
                        startupWait += Time.unscaledDeltaTime;
                        if (clip.loadState == AudioDataLoadState.Failed || startupWait >= 5f)
                        {
                            Debug.LogWarning("Background music could not start: " + cueId, this);
                            break;
                        }
                    }

                    yield return null;
                }

                EnsureAudioManager()?.StopMusic();
                yield return WaitForRandomMusicGap();
            }
        }

        private static bool HasReachedMusicEnd(AudioClip clip, int lastSample, double elapsedDspSeconds, float pitch)
        {
            if (clip == null) return true;
            double estimatedSample = lastSample + Math.Max(0d, elapsedDspSeconds) *
                clip.frequency * Math.Abs(pitch);
            // The final audio buffer can finish between Update calls; tolerate only one small output buffer.
            return estimatedSample >= clip.samples - Math.Max(2048, clip.frequency * .05f);
        }

        private IEnumerator WaitForRandomMusicGap()
        {
            float remaining =
                Mathf.Max(0f, randomMusicGapSeconds);
            while (remaining > 0f)
            {
                AudioManager manager =
                    EnsureAudioManager();
                if (AudioListener.pause || applicationPaused || Time.timeScale <= 0f ||
                    manager != null && manager.HasActiveMusicOverride)
                {
                    yield return null;
                    continue;
                }

                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static AudioClip CreateTone(
            string clipName,
            float frequency,
            float duration,
            float amplitude,
            float frequencyEndMultiplier = 1f)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] samples = new float[sampleCount];
            float phase = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float normalized = index / (float)sampleCount;
                float currentFrequency = Mathf.Lerp(
                    frequency,
                    frequency * frequencyEndMultiplier,
                    normalized);
                phase += 2f * Mathf.PI * currentFrequency / sampleRate;
                float envelope = 1f - normalized;
                samples[index] = Mathf.Sin(phase) * amplitude * envelope * envelope;
            }

            AudioClip clip = AudioClip.Create(
                clipName,
                sampleCount,
                1,
                sampleRate,
                false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
