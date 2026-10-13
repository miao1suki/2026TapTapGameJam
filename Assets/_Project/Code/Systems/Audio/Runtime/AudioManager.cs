using System;
using System.Collections.Generic;
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
        private float musicVolumeScale = 1f;
        private readonly Dictionary<object, LoopPlayback> loopPlaybacks =
            new Dictionary<object, LoopPlayback>();
        private readonly List<object> staleLoopOwners =
            new List<object>();

        private sealed class LoopPlayback
        {
            public AudioSource Source;
            public AudioChannel Channel;
            public float VolumeScale;
        }

        private static AudioManager instance;

        public static AudioManager Instance => EnsureInstance();
        public static AudioManager Existing => instance;
        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float EffectsVolume => effectsVolume;
        public float EffectiveMusicVolume => masterVolume * musicVolume;
        public float EffectiveEffectsVolume => masterVolume * effectsVolume;
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
            musicVolumeScale = Mathf.Clamp01(volume);
            if (clip != null &&
                musicSource.clip == clip &&
                musicSource.isPlaying)
            {
                musicSource.loop = loop;
                ApplyMusicVolume();
                return;
            }

            musicSource.clip = clip;
            musicSource.loop = loop;
            ApplyMusicVolume();
            if (clip != null)
            {
                musicSource.Play();
            }
            else
            {
                musicSource.Stop();
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

        public void PlaySound(AudioClip[] clips, float volume = 1f)
        {
            PlaySound(PickRandomClip(clips), volume);
        }

        public AudioSource PlayLoop(
            object owner,
            AudioClip clip,
            float volume = 1f,
            bool ignoreListenerPause = false,
            AudioChannel channel = AudioChannel.SoundEffect)
        {
            if (owner == null || clip == null)
            {
                return null;
            }

            StopLoop(owner);
            AudioSource source = CreateLoopSource();
            source.clip = clip;
            source.loop = true;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = ignoreListenerPause;
            var playback = new LoopPlayback
            {
                Source = source,
                Channel = channel,
                VolumeScale = Mathf.Clamp01(volume)
            };
            loopPlaybacks[owner] = playback;
            ApplyLoopVolume(playback);
            source.Play();
            return source;
        }

        public bool IsLoopPlaying(object owner)
        {
            return owner != null &&
                   loopPlaybacks.TryGetValue(
                       owner,
                       out LoopPlayback playback) &&
                   playback.Source != null &&
                   playback.Source.isPlaying;
        }

        public AudioSource GetLoop(object owner)
        {
            if (owner != null &&
                loopPlaybacks.TryGetValue(
                    owner,
                    out LoopPlayback playback))
            {
                return playback.Source;
            }

            return null;
        }

        public void StopLoop(object owner)
        {
            if (owner == null ||
                !loopPlaybacks.TryGetValue(
                    owner,
                    out LoopPlayback playback))
            {
                return;
            }

            loopPlaybacks.Remove(owner);
            DestroyLoop(playback);
        }

        public void StopAllLoops()
        {
            foreach (LoopPlayback playback in loopPlaybacks.Values)
            {
                DestroyLoop(playback);
            }

            loopPlaybacks.Clear();
        }

        private void Update()
        {
            if (loopPlaybacks.Count == 0)
            {
                return;
            }

            staleLoopOwners.Clear();
            foreach (KeyValuePair<object, LoopPlayback> pair in
                     loopPlaybacks)
            {
                LoopPlayback playback = pair.Value;
                bool ownerAlive = pair.Key is UnityEngine.Object unityOwner
                    ? unityOwner != null
                    : pair.Key != null;
                if (!ownerAlive ||
                    playback == null ||
                    playback.Source == null)
                {
                    staleLoopOwners.Add(pair.Key);
                }
            }

            for (int index = 0;
                 index < staleLoopOwners.Count;
                 index++)
            {
                StopLoop(staleLoopOwners[index]);
            }
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

        private AudioSource CreateLoopSource()
        {
            GameObject loop = new GameObject("Loop Source");
            loop.transform.SetParent(transform, false);
            AudioSource source = loop.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private void ApplyLoopVolume(LoopPlayback playback)
        {
            if (playback == null || playback.Source == null)
            {
                return;
            }

            playback.Source.volume = Mathf.Clamp01(
                playback.VolumeScale *
                (playback.Channel == AudioChannel.Music
                    ? musicVolume
                    : effectsVolume));
        }

        private static AudioClip PickRandomClip(AudioClip[] clips)
        {
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int start = UnityEngine.Random.Range(0, clips.Length);
            for (int offset = 0; offset < clips.Length; offset++)
            {
                AudioClip clip = clips[
                    (start + offset) % clips.Length];
                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private static void DestroyLoop(LoopPlayback playback)
        {
            if (playback?.Source == null)
            {
                return;
            }

            playback.Source.Stop();
            if (Application.isPlaying)
            {
                Destroy(playback.Source.gameObject);
            }
            else
            {
                DestroyImmediate(playback.Source.gameObject);
            }
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = masterVolume;
            ApplyMusicVolume();

            if (soundEffectSource != null)
            {
                soundEffectSource.volume = effectsVolume;
            }

            foreach (LoopPlayback playback in loopPlaybacks.Values)
            {
                ApplyLoopVolume(playback);
            }
        }

        private void ApplyMusicVolume()
        {
            if (musicSource != null)
            {
                musicSource.volume = Mathf.Clamp01(
                    musicVolume * musicVolumeScale);
            }
        }
    }
}
