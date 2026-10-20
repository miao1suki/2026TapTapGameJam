using System;
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
                handle = manager.RequestMusicOverride(
                    owner,
                    boss,
                    true,
                    .7f);
                Assert.IsTrue(handle.IsValid);
                Assert.AreSame(boss, source.clip);

                manager.PlayMusic(secondBase, false, .6f);
                Assert.AreSame(
                    boss,
                    source.clip,
                    "A base music change must not replace an active override.");

                handle.Release();
                Assert.IsFalse(handle.IsValid);
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
