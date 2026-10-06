using System;
using Project.Settings;
using UnityEngine;

namespace Project.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private const string MasterVolumePreference = "2026TapTap.Audio.MasterVolume";
        private const string MusicVolumePreference = "2026TapTap.Audio.MusicVolume";
        private const string EffectsVolumePreference = "2026TapTap.Audio.EffectsVolume";

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource soundEffectSource;
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float effectsVolume = 1f;
        private float previewMasterVolume = 1f;
        private float previewMusicVolume = 1f;
        private float previewEffectsVolume = 1f;

        private static AudioManager instance;

        public static AudioManager Instance => EnsureInstance();
        public static AudioManager Existing => instance;
        public float MasterVolume => RuntimeSettingsPolicy.IsPreviewOnly
            ? previewMasterVolume
            : masterVolume;
        public float MusicVolume => RuntimeSettingsPolicy.IsPreviewOnly
            ? previewMusicVolume
            : musicVolume;
        public float EffectsVolume => RuntimeSettingsPolicy.IsPreviewOnly
            ? previewEffectsVolume
            : effectsVolume;
        public float EffectiveMusicVolume =>
            RuntimeSettingsPolicy.IsPreviewOnly
                ? previewMasterVolume * previewMusicVolume
                : masterVolume * musicVolume;
        public float EffectiveEffectsVolume =>
            RuntimeSettingsPolicy.IsPreviewOnly
                ? previewMasterVolume * previewEffectsVolume
                : masterVolume * effectsVolume;
        public AudioSource MusicSource => musicSource;
        public AudioSource SoundEffectSource => soundEffectSource;

        public event Action VolumesChanged;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        public static AudioManager EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            AudioManager existing = ProjectDiscovery.FindFirst<AudioManager>();
            if (existing != null)
            {
                return existing;
            }

            GameObject root = new GameObject("Audio Manager");
            instance = root.AddComponent<AudioManager>();
            return instance;
        }

        public void SetMasterVolume(float value)
        {
            float next = Mathf.Clamp01(value);
            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                previewMasterVolume = next;
                return;
            }

            if (Mathf.Approximately(masterVolume, next))
            {
                return;
            }

            masterVolume = next;
            PlayerPrefs.SetFloat(MasterVolumePreference, masterVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
            VolumesChanged?.Invoke();
        }

        public void SetMusicVolume(float value)
        {
            float next = Mathf.Clamp01(value);
            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                previewMusicVolume = next;
                return;
            }

            if (Mathf.Approximately(musicVolume, next))
            {
                return;
            }

            musicVolume = next;
            PlayerPrefs.SetFloat(MusicVolumePreference, musicVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
            VolumesChanged?.Invoke();
        }

        public void SetEffectsVolume(float value)
        {
            float next = Mathf.Clamp01(value);
            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                previewEffectsVolume = next;
                return;
            }

            if (Mathf.Approximately(effectsVolume, next))
            {
                return;
            }

            effectsVolume = next;
            PlayerPrefs.SetFloat(EffectsVolumePreference, effectsVolume);
            PlayerPrefs.Save();
            ApplyVolumes();
            VolumesChanged?.Invoke();
        }

        public void PlayMusic(AudioClip clip, bool loop = true, float volume = 1f)
        {
            EnsureSources();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = Mathf.Clamp01(
                volume * (masterVolume * musicVolume));
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

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            masterVolume = PlayerPrefs.GetFloat(
                MasterVolumePreference,
                masterVolume);
            musicVolume = PlayerPrefs.GetFloat(
                MusicVolumePreference,
                musicVolume);
            effectsVolume = PlayerPrefs.GetFloat(
                EffectsVolumePreference,
                effectsVolume);
            previewMasterVolume = masterVolume;
            previewMusicVolume = musicVolume;
            previewEffectsVolume = effectsVolume;
            EnsureSources();
            ApplyVolumes();
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
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
                GameObject soundEffects = new GameObject("Sound Effects");
                soundEffects.transform.SetParent(transform, false);
                soundEffectSource = soundEffects.AddComponent<AudioSource>();
                soundEffectSource.playOnAwake = false;
            }

            soundEffectSource.ignoreListenerPause = true;
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            if (musicSource != null)
            {
                musicSource.volume = musicVolume;
            }

            if (soundEffectSource != null)
            {
                soundEffectSource.volume = effectsVolume;
            }
        }
    }
}
