using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Project.Audio;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class GameplayAudioTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void BackgroundMusicKeepsARealVoiceAheadOfMechanismLoops()
        {
            var manager = AudioManager.EnsureInstance();
            typeof(AudioManager).GetMethod("EnsureSources", Private).Invoke(manager, null);
            Assert.That(manager.MusicSource.priority, Is.Zero);
            Assert.That(manager.SoundEffectSource.priority, Is.GreaterThan(manager.MusicSource.priority));
        }

        [Test]
        public void RandomMusicGapDefaultsToEighteenSeconds()
        {
            var root = new GameObject("Music gap configuration test");
            try
            {
                var service = root.AddComponent<GameAudioService>();
                Assert.That(typeof(GameAudioService).GetField("randomMusicGapSeconds", Private).GetValue(service), Is.EqualTo(18f));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(2205, 0d, false)]
        [TestCase(2205, .1d, false)]
        [TestCase(22050, .25d, false)]
        [TestCase(43000, .02d, true)]
        [TestCase(40000, .1d, true)]
        public void RandomMusicOnlyCompletesAtTheActualEnd(int lastSample, double dspElapsed, bool expected)
        {
            var clip = AudioClip.Create("Music completion test", 44100, 1, 44100, false);
            try
            {
                var method = typeof(GameAudioService).GetMethod("HasReachedMusicEnd", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(method.Invoke(null, new object[] { clip, lastSample, dspElapsed, 1f }), Is.EqualTo(expected));
            }
            finally { UnityEngine.Object.DestroyImmediate(clip); }
        }

        [Test]
        public void PausedRandomMusicDoesNotClearItsUnfinishedClip()
        {
            var root = new GameObject("Paused random music test");
            bool previousPause = AudioListener.pause;
            try
            {
                var service = root.AddComponent<GameAudioService>();
                typeof(GameAudioService).GetMethod("Awake", Private).Invoke(service, null);
                AudioListener.pause = true;
                var routine = (System.Collections.IEnumerator)typeof(GameAudioService)
                    .GetMethod("RunRandomGameplayMusic", Private).Invoke(service, null);
                Assert.That(routine.MoveNext(), Is.True);
                AudioClip started = service.MusicSource.clip;
                Assert.That(started, Is.Not.Null);
                for (int index = 0; index < 10; index++)
                {
                    Assert.That(routine.MoveNext(), Is.True);
                    Assert.That(service.MusicSource.clip, Is.SameAs(started), "Pausing is not song completion.");
                }
            }
            finally
            {
                AudioListener.pause = previousPause;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RandomMusicRotationCyclesWithoutImmediateRepeat()
        {
            string[] source =
            {
                "music.a",
                "music.b",
                "music.c",
                "music.d",
                "music.e"
            };
            var rotation = new AudioMusicRotation(
                source,
                new System.Random(20261021));
            string previous = null;
            for (int cycle = 0; cycle < 4; cycle++)
            {
                var seen = new HashSet<string>();
                for (int index = 0;
                     index < source.Length;
                     index++)
                {
                    string next = rotation.Next();
                    Assert.IsNotNull(next);
                    Assert.AreNotEqual(previous, next);
                    Assert.IsTrue(
                        seen.Add(next),
                        "Each cue must appear once before the bag refills.");
                    previous = next;
                }
            }
        }

        [Test]
        public void RandomGameplayMusicPoolExcludesMenuAndBoss()
        {
            string[] expected =
            {
                AudioCueIds.MusicAmbientLoop,
                AudioCueIds.MusicAtmosphereAMinor,
                AudioCueIds.MusicAtmosphereDSharpMinor,
                AudioCueIds.MusicAtmosphereEMinor,
                AudioCueIds.MusicAtmosphereFMinor,
                AudioCueIds.MusicBanjo,
                AudioCueIds.MusicKeyboard
            };
            CollectionAssert.AreEquivalent(
                expected,
                GameAudioService.RandomGameplayMusicCueIds);
            CollectionAssert.DoesNotContain(
                GameAudioService.RandomGameplayMusicCueIds,
                AudioCueIds.MusicMenuOpening);
            CollectionAssert.DoesNotContain(
                GameAudioService.RandomGameplayMusicCueIds,
                AudioCueIds.MusicBoss);

            var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(
                "Assets/_Project/Content/Audio/Resources/AudioCatalog.asset");
            Assert.NotNull(catalog);
            foreach (string cueId in expected)
            {
                Assert.IsTrue(
                    catalog.TryGetCue(
                        cueId,
                        out AudioCueDefinition definition),
                    cueId);
                Assert.AreEqual(
                    AudioChannel.Music,
                    definition.Channel,
                    cueId);
                Assert.IsTrue(definition.HasClips, cueId);
                Assert.AreEqual(
                    1,
                    definition.Clips.Length,
                    cueId);
            }
        }

        [TestCase("sfx.block.pickup", false)]
        [TestCase("sfx.block.drop", false)]
        [TestCase("sfx.platform.move_loop", true)]
        [TestCase("sfx.machine.start", false)]
        [TestCase("sfx.machine.stop", false)]
        [TestCase("sfx.bubble.loop", true)]
        [TestCase("sfx.player.buoy_enter", false)]
        [TestCase("sfx.player.buoy_exit", false)]
        [TestCase("sfx.player.impact", false)]
        [TestCase("sfx.plant.burn", false)]
        [TestCase("sfx.lava.cool", false)]
        [TestCase("sfx.notice.confirm", false)]
        public void GameplayCueHasValidClipAndLoopMode(string id, bool loop)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(
                "Assets/_Project/Content/Audio/Resources/AudioCatalog.asset");
            Assert.NotNull(asset);
            var cues = new SerializedObject(asset).FindProperty("cues");
            int matches = 0;
            for (int index = 0; index < cues.arraySize; index++)
            {
                var cue = cues.GetArrayElementAtIndex(index);
                if (cue.FindPropertyRelative("cueId").stringValue != id) continue;
                matches++;
                Assert.AreEqual(loop, cue.FindPropertyRelative("loop").boolValue);
                Assert.AreEqual(1, cue.FindPropertyRelative("channel").enumValueIndex);
                var clips = cue.FindPropertyRelative("clips");
                Assert.Greater(clips.arraySize, 0);
                for (int clip = 0; clip < clips.arraySize; clip++)
                    Assert.NotNull(clips.GetArrayElementAtIndex(clip).objectReferenceValue);
            }
            Assert.AreEqual(1, matches, id);
        }

        [Test]
        public void BossThemeUsesLoopingMusicChannel()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(
                "Assets/_Project/Content/Audio/Resources/AudioCatalog.asset");
            Assert.NotNull(asset);
            var cues = new SerializedObject(asset).FindProperty("cues");
            int matches = 0;
            for (int index = 0; index < cues.arraySize; index++)
            {
                var cue = cues.GetArrayElementAtIndex(index);
                if (cue.FindPropertyRelative("cueId").stringValue !=
                    AudioCueIds.MusicBoss)
                {
                    continue;
                }

                matches++;
                Assert.AreEqual(
                    0,
                    cue.FindPropertyRelative("channel")
                        .enumValueIndex);
                Assert.IsTrue(
                    cue.FindPropertyRelative("loop").boolValue);
                var clips = cue.FindPropertyRelative("clips");
                Assert.Greater(clips.arraySize, 0);
                Assert.NotNull(
                    clips.GetArrayElementAtIndex(0)
                        .objectReferenceValue);
            }

            Assert.AreEqual(1, matches);
        }

        [Test]
        public void MusicOverrideRestoresLatestBaseMusic()
        {
            AudioManager manager = AudioManager.EnsureInstance();
            // EditMode does not invoke MonoBehaviour.Awake for a new manager.
            typeof(AudioManager).GetMethod("EnsureSources", Private).Invoke(manager, null);
            AudioSource source = manager.MusicSource;
            AudioClip originalClip = source.clip;
            bool originalLoop = source.loop;
            AudioClip firstBase = null;
            AudioClip secondBase = null;
            AudioClip boss = null;
            object owner = new object();
            MusicRequestHandle handle = default;
            try
            {
                firstBase = AudioClip.Create(
                    "First Base",
                    64,
                    1,
                    22050,
                    false);
                secondBase = AudioClip.Create(
                    "Second Base",
                    64,
                    1,
                    22050,
                    false);
                boss = AudioClip.Create(
                    "Boss",
                    64,
                    1,
                    22050,
                    false);

                manager.PlayMusic(firstBase, true, .4f);
                Assert.AreSame(firstBase, source.clip);
                Assert.IsFalse(manager.HasActiveMusicOverride);
                handle = manager.RequestMusicOverride(
                    owner,
                    boss,
                    true,
                    .7f);
                Assert.IsTrue(handle.IsValid);
                Assert.IsTrue(manager.HasActiveMusicOverride);
                Assert.AreSame(boss, source.clip);

                manager.PlayMusic(secondBase, false, .6f);
                Assert.AreSame(
                    boss,
                    source.clip,
                    "A base music change must not replace an active override.");

                handle.Release();
                Assert.IsFalse(handle.IsValid);
                Assert.IsFalse(manager.HasActiveMusicOverride);
                Assert.AreSame(secondBase, source.clip);
                Assert.IsFalse(source.loop);
            }
            finally
            {
                handle.Release();
                if (originalClip != null)
                {
                    manager.PlayMusic(
                        originalClip,
                        originalLoop,
                        1f);
                }
                else
                {
                    manager.StopMusic();
                }

                DestroyClip(boss);
                DestroyClip(secondBase);
                DestroyClip(firstBase);
            }
        }

        [TestCase(5.9f, 1f, 5f, false, false, false)]
        [TestCase(6f, 1f, 5f, false, false, true)]
        [TestCase(12f, 2f, 5f, false, false, true)]
        [TestCase(11f, 2f, 5f, false, false, false)]
        [TestCase(8f, 1f, 0f, false, false, true)]
        [TestCase(8f, 1f, 5f, true, false, false)]
        [TestCase(8f, 1f, 5f, false, true, false)]
        public void HeavyLandingUsesSixGridCellsAndRejectsWaterOrClimb(
            float distance, float cellSize, float speed, bool water, bool climb, bool expected)
        {
            var actor = new GameObject("Heavy landing audio test");
            try
            {
                var type = Type.GetType("Project.Player.PlayerController, Assembly-CSharp", true);
                var player = actor.AddComponent(type);
                type.GetField("lastLandingFallDistance", Private).SetValue(player, distance);
                type.GetField("lastLandingDownwardSpeed", Private).SetValue(player, speed);
                type.GetField("gridCellWorldSize", Private).SetValue(player, cellSize);
                type.GetField("enteredWaterDuringFall", Private).SetValue(player, water);
                type.GetField("enteredClimbDuringFall", Private).SetValue(player, climb);
                Assert.AreEqual(expected, type.GetMethod("ShouldPlayLandingImpact", Private).Invoke(player, null));
            }
            finally { UnityEngine.Object.DestroyImmediate(actor); }
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null)
            {
                UnityEngine.Object.DestroyImmediate(clip);
            }
        }
    }
}
