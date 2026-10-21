using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Project.GameFlow.Tests
{
    public sealed class RandomMusicContinuityTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator PauseResumeAndInterruptedSourceKeepSongUntilItsRealEnd()
        {
            float previousScale = Time.timeScale, previousVolume = AudioListener.volume;
            bool previousPause = AudioListener.pause;
            var root = new GameObject("Music continuity test");
            var listener = new GameObject("Music test listener");
            AudioClip clip = null;
            ScriptableObject catalog = null;
            GameAudioService service = null;
            try
            {
                listener.AddComponent<AudioListener>();
                Time.timeScale = 1f;
                AudioListener.pause = false;
                AudioListener.volume = 0f;
                clip = AudioClip.Create("Continuity one second", 44100, 1, 44100, false);
                var catalogType = Type.GetType("Project.Audio.AudioCatalog, Project.Audio.Runtime", true);
                var cueType = Type.GetType("Project.Audio.AudioCueDefinition, Project.Audio.Runtime", true);
                catalog = ScriptableObject.CreateInstance(catalogType);
                var entries = Array.CreateInstance(cueType, GameAudioService.RandomGameplayMusicCueIds.Count);
                for (int index = 0; index < entries.Length; index++)
                {
                    object cue = Activator.CreateInstance(cueType);
                    cueType.GetField("cueId", Private).SetValue(cue, GameAudioService.RandomGameplayMusicCueIds[index]);
                    cueType.GetField("channel", Private).SetValue(cue, Enum.ToObject(cueType.GetField("channel", Private).FieldType, 0));
                    cueType.GetField("clips", Private).SetValue(cue, new[] { clip });
                    entries.SetValue(cue, index);
                }
                catalogType.GetField("cues", Private).SetValue(catalog, entries);
                service = root.AddComponent<GameAudioService>();
                typeof(GameAudioService).GetProperty("Catalog").SetValue(service, catalog);
                typeof(GameAudioService).GetField("randomMusicGapSeconds", Private).SetValue(service, .2f);
                var routine = (IEnumerator)typeof(GameAudioService).GetMethod("RunRandomGameplayMusic", Private).Invoke(service, null);
                service.StartCoroutine(routine);
                yield return new WaitForSecondsRealtime(.25f);
                AudioSource source = service.MusicSource;
                Assert.That(source.isPlaying, Is.True, "Run without -noaudio so Unity exercises real playback.");
                int pausedSample = source.timeSamples;
                AudioListener.pause = true;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(source.clip, Is.SameAs(clip), "Global pause must not clear the song.");
                Assert.That(source.timeSamples, Is.EqualTo(pausedSample).Within(2048));
                Time.timeScale = 1f;
                AudioListener.pause = false;
                yield return new WaitForSecondsRealtime(.12f);
                Assert.That(source.clip, Is.SameAs(clip));
                Assert.That(source.isPlaying, Is.True);
                int resumeSample = source.timeSamples;
                source.Stop();
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(source.clip, Is.SameAs(clip));
                Assert.That(source.isPlaying, Is.True, "An early device/source stop must resume, not enter the gap.");
                Assert.That(source.timeSamples, Is.GreaterThanOrEqualTo(resumeSample - 4096));
                float deadline = Time.realtimeSinceStartup + 2f;
                while (source.clip != null && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(source.clip, Is.Null, "Only natural song completion enters the silent gap.");
                AudioListener.pause = true;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(source.clip, Is.Null, "The gap must not elapse while global audio is paused.");
                AudioListener.pause = false;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(source.clip, Is.SameAs(clip), "After the short gap another pool entry must play.");
                Assert.That(source.isPlaying, Is.True);
            }
            finally
            {
                if (service != null)
                {
                    service.StopAllCoroutines();
                    var source = service.MusicSource;
                    if (source != null) UnityEngine.Object.DestroyImmediate(source.transform.root.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(listener);
                if (catalog != null) UnityEngine.Object.DestroyImmediate(catalog);
                if (clip != null) UnityEngine.Object.DestroyImmediate(clip);
                Time.timeScale = previousScale;
                AudioListener.pause = previousPause;
                AudioListener.volume = previousVolume;
            }
        }
    }
}
