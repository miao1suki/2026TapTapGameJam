using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Audio
{
    public readonly struct MusicRequestHandle : IDisposable
    {
        private readonly AudioManager manager;
        private readonly object owner;
        private readonly long requestId;

        internal MusicRequestHandle(
            AudioManager manager,
            object owner,
            long requestId)
        {
            this.manager = manager;
            this.owner = owner;
            this.requestId = requestId;
        }

        public bool IsValid =>
            manager != null &&
            manager.IsMusicOverrideActive(owner, requestId);

        public void Release()
        {
            if (manager != null)
            {
                manager.ReleaseMusicOverride(
                    owner,
                    requestId);
            }
        }

        public void Dispose()
        {
            Release();
        }
    }

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
        private AudioClip baseMusicClip;
        private bool baseMusicLoop = true;
        private float baseMusicVolumeScale = 1f;
        private long nextMusicOverrideId;
        private readonly Dictionary<object, LoopPlayback> loopPlaybacks =
            new Dictionary<object, LoopPlayback>();
        private readonly Dictionary<object, MusicOverridePlayback>
            musicOverrides =
                new Dictionary<object, MusicOverridePlayback>();
        private readonly List<object> staleLoopOwners =
            new List<object>();
        private readonly List<object> staleMusicOverrideOwners =
            new List<object>();

        private sealed class LoopPlayback
        {
            public AudioSource Source;
            public AudioChannel Channel;
            public float VolumeScale;
        }

        private sealed class MusicOverridePlayback
        {
            public AudioClip Clip;
            public bool Loop;
            public float VolumeScale;
            public long RequestId;
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
        public bool HasActiveMusicOverride
        {
            get
            {
                return TryGetActiveMusicOverride(
                    out MusicOverridePlayback _);
            }
        }

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
            baseMusicClip = clip;
            baseMusicLoop = loop;
            baseMusicVolumeScale = Mathf.Clamp01(volume);
            if (!TryGetActiveMusicOverride(
                    out MusicOverridePlayback _))
            {
                ApplyMusicState(
                    baseMusicClip,
                    baseMusicLoop,
                    baseMusicVolumeScale);
            }
        }

        public void StopMusic()
        {
            baseMusicClip = null;
            if (!TryGetActiveMusicOverride(
                    out MusicOverridePlayback _))
            {
                ApplyMusicState(null, true, 1f);
            }
        }

        public MusicRequestHandle RequestMusicOverride(
            object owner,
            AudioClip clip,
            bool loop = true,
            float volume = 1f)
        {
            if (owner == null || clip == null)
            {
                return default;
            }

            EnsureSources();
            long requestId = ++nextMusicOverrideId;
            musicOverrides[owner] =
                new MusicOverridePlayback
                {
                    Clip = clip,
                    Loop = loop,
                    VolumeScale = Mathf.Clamp01(volume),
                    RequestId = requestId
                };
            ApplyActiveMusic();
            return new MusicRequestHandle(
                this,
                owner,
                requestId);
        }

        public void ReleaseMusicOverride(
            object owner,
            long requestId)
        {
            if (owner == null ||
                !musicOverrides.TryGetValue(
                    owner,
                    out MusicOverridePlayback playback) ||
                playback.RequestId != requestId)
            {
                return;
            }

            musicOverrides.Remove(owner);
            ApplyActiveMusic();
        }

        internal bool IsMusicOverrideActive(
            object owner,
            long requestId)
        {
            return owner != null &&
                   musicOverrides.TryGetValue(
                       owner,
                       out MusicOverridePlayback playback) &&
                   playback.RequestId == requestId;
        }

        private void ApplyActiveMusic()
        {
            if (TryGetActiveMusicOverride(
                    out MusicOverridePlayback playback))
            {
                ApplyMusicState(
                    playback.Clip,
                    playback.Loop,
                    playback.VolumeScale);
                return;
            }

            ApplyMusicState(
                baseMusicClip,
                baseMusicLoop,
                baseMusicVolumeScale);
        }

        private bool TryGetActiveMusicOverride(
            out MusicOverridePlayback active)
        {
            active = null;
            foreach (MusicOverridePlayback playback in
                     musicOverrides.Values)
            {
                if (playback == null)
                {
                    continue;
                }

                if (active == null ||
                    playback.RequestId > active.RequestId)
                {
                    active = playback;
                }
            }

            return active != null;
        }

        private void ApplyMusicState(
            AudioClip clip,
            bool loop,
            float volumeScale)
        {
            EnsureSources();
            musicVolumeScale = Mathf.Clamp01(volumeScale);
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
            if (loopPlaybacks.Count == 0 &&
                musicOverrides.Count == 0)
            {
                return;
            }

            staleLoopOwners.Clear();
            staleMusicOverrideOwners.Clear();
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

            foreach (KeyValuePair<object, MusicOverridePlayback> pair in
                     musicOverrides)
            {
                bool ownerAlive = pair.Key is UnityEngine.Object unityOwner
                    ? unityOwner != null
                    : pair.Key != null;
                if (!ownerAlive || pair.Value == null)
                {
                    staleMusicOverrideOwners.Add(pair.Key);
                }
            }

            for (int index = 0;
                 index < staleMusicOverrideOwners.Count;
                 index++)
            {
                object owner = staleMusicOverrideOwners[index];
                if (owner != null &&
                    musicOverrides.TryGetValue(
                        owner,
                        out MusicOverridePlayback playback))
                {
                    ReleaseMusicOverride(
                        owner,
                        playback.RequestId);
                }
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
