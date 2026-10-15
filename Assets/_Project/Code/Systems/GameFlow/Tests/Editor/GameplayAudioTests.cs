using System;
using System.Reflection;
using NUnit.Framework;
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
    }
}
