using System.Collections.Generic;
using UnityEngine;

namespace Project.Audio
{
    public static class AudioCuePlayer
    {
        private static readonly HashSet<string> MissingCueWarnings =
            new HashSet<string>();
        private static int lastConfirmationFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetConfirmationFrame() => lastConfirmationFrame = -1;

        public static bool PlayUiConfirm(AudioCatalog catalog = null, float volumeScale = 1f)
        {
            // A UI action can pass through both a button and its flow router.
            if (lastConfirmationFrame == Time.frameCount) return true;
            bool played = Play(catalog != null ? catalog : AudioCatalog.LoadDefault(),
                AudioCueIds.NoticeConfirm, volumeScale);
            if (played) lastConfirmationFrame = Time.frameCount;
            return played;
        }

        public static bool Play(
            string cueId,
            float volumeScale = 1f)
        {
            return Play(
                AudioCatalog.LoadDefault(),
                cueId,
                volumeScale);
        }

        public static bool Play(
            AudioCatalog catalog,
            string cueId,
            float volumeScale = 1f)
        {
            if (!TryGetDefinition(
                    catalog,
                    cueId,
                    out AudioCueDefinition definition))
            {
                return false;
            }

            float volume = Mathf.Clamp01(
                definition.VolumeScale * Mathf.Clamp01(volumeScale));
            AudioManager manager = AudioManager.Instance;
            if (definition.Channel == AudioChannel.Music)
            {
                AudioClip clip = PickClip(definition);
                manager.PlayMusic(clip, definition.Loop, volume);
                return clip != null;
            }

            if (definition.Loop)
            {
                return PlayLoop(
                    catalog,
                    cueId,
                    cueId,
                    volumeScale);
            }

            manager.PlaySound(definition.Clips, volume);
            return definition.HasClips;
        }

        public static bool PlayLoop(
            string cueId,
            object owner,
            float volumeScale = 1f)
        {
            return PlayLoop(
                AudioCatalog.LoadDefault(),
                cueId,
                owner,
                volumeScale);
        }

        public static bool PlayLoop(
            AudioCatalog catalog,
            string cueId,
            object owner,
            float volumeScale = 1f)
        {
            if (!TryGetDefinition(
                    catalog,
                    cueId,
                    out AudioCueDefinition definition))
            {
                return false;
            }

            object resolvedOwner = owner ?? cueId;
            float volume = Mathf.Clamp01(
                definition.VolumeScale * Mathf.Clamp01(volumeScale));
            AudioManager manager = AudioManager.Instance;
            if (definition.Channel == AudioChannel.Music)
            {
                AudioClip clip = PickClip(definition);
                manager.PlayMusic(clip, true, volume);
                return clip != null;
            }

            AudioClip loopClip = PickClip(definition);
            return loopClip != null &&
                   manager.PlayLoop(
                       resolvedOwner,
                       loopClip,
                       volume,
                       false,
                       AudioChannel.SoundEffect) != null;
        }

        public static void StopLoop(object owner)
        {
            if (owner != null)
            {
                AudioManager.Instance.StopLoop(owner);
            }
        }

        private static bool TryGetDefinition(
            AudioCatalog catalog,
            string cueId,
            out AudioCueDefinition definition)
        {
            if (catalog != null &&
                catalog.TryGetCue(cueId, out definition) &&
                definition.HasClips)
            {
                return true;
            }

            definition = null;
            if (!string.IsNullOrWhiteSpace(cueId) &&
                MissingCueWarnings.Add(cueId))
            {
                Debug.LogWarning(
                    $"[Audio] 未找到音频 Cue 或素材为空：{cueId}");
            }

            return false;
        }

        private static AudioClip PickClip(
            AudioCueDefinition definition)
        {
            AudioClip[] clips = definition != null
                ? definition.Clips
                : null;
            if (clips == null || clips.Length == 0)
            {
                return null;
            }

            int start = Random.Range(0, clips.Length);
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
    }
}
