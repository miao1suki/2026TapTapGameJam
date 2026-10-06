using System.Collections;
using Project.Audio;
using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameAudioService : MonoBehaviour, IGameSystemService
    {
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource soundEffectSource;

        private static GameAudioService instance;
        private AudioClip uiClickClip;
        private AudioClip pauseClip;
        private AudioClip levelCompleteClip;
        private AudioManager audioManager;

        public static GameAudioService Instance => instance;
        public int InitializationOrder => 0;
        public bool IsInitialized { get; private set; }
        public AudioSource MusicSource => musicSource;
        public AudioSource SoundEffectSource => soundEffectSource;

        public void Configure(AudioSource music, AudioSource soundEffects)
        {
            musicSource = music;
            soundEffectSource = soundEffects;
        }

        public IEnumerator Initialize()
        {
            EnsureSources();
            audioManager = AudioManager.EnsureInstance();
            if (audioManager != null)
            {
                audioManager.VolumesChanged -= ApplyVolumes;
                audioManager.VolumesChanged += ApplyVolumes;
                ApplyVolumes();
            }
            IsInitialized = true;
            yield break;
        }

        public void PlayMusic(AudioClip clip, bool loop = true)
        {
            EnsureSources();
            musicSource.clip = clip;
            musicSource.loop = loop;
            if (clip != null)
            {
                musicSource.Play();
            }
        }

        public void StopMusic()
        {
            musicSource?.Stop();
        }

        public void PlaySound(AudioClip clip, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            EnsureSources();
            soundEffectSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        public void PlayUiClick()
        {
            uiClickClip ??= CreateTone("UI_Click", 620f, 0.055f, 0.22f);
            PlaySound(uiClickClip, 0.7f);
        }

        public void PlayPause()
        {
            pauseClip ??= CreateTone("UI_Pause", 360f, 0.09f, 0.2f);
            PlaySound(pauseClip, 0.65f);
        }

        public void PlayLevelComplete()
        {
            levelCompleteClip ??=
                CreateTone("Level_Complete", 840f, 0.22f, 0.24f, 1.45f);
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
            EnsureSources();
        }

        private void OnDestroy()
        {
            if (audioManager != null)
            {
                audioManager.VolumesChanged -= ApplyVolumes;
                audioManager = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void ApplyVolumes()
        {
            if (audioManager == null)
            {
                return;
            }

            AudioListener.volume = audioManager.MasterVolume;
            if (musicSource != null)
            {
                musicSource.volume = audioManager.MusicVolume;
            }

            if (soundEffectSource != null)
            {
                soundEffectSource.volume = audioManager.EffectsVolume;
            }
        }

        private void EnsureSources()
        {
            if (musicSource == null)
            {
                GameObject music = new GameObject("Music");
                music.transform.SetParent(transform, false);
                musicSource = music.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }

            if (soundEffectSource == null)
            {
                GameObject soundEffects = new GameObject("SoundEffects");
                soundEffects.transform.SetParent(transform, false);
                soundEffectSource = soundEffects.AddComponent<AudioSource>();
                soundEffectSource.playOnAwake = false;
            }

            soundEffectSource.ignoreListenerPause = true;
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
