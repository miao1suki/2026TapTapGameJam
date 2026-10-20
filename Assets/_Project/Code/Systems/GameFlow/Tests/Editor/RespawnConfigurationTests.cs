using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class RespawnConfigurationTests
    {
        private static Type PlayerType => Type.GetType("Project.Player.PlayerController, Assembly-CSharp");
        private static Type ModeType => Type.GetType("Project.Player.PlayerRespawnMode, Assembly-CSharp");
        private static Type AnchorType => Type.GetType("Project.BlockFeatures.RespawnAnchorFeature, Assembly-CSharp");
        private static Type ChainType => Type.GetType("Project.BlockFeatures.RespawnAnchorChainRuntime, Assembly-CSharp");

        [Test]
        public void StandardPlayer_DefaultsToOrdinaryRespawn()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
            var player = source.GetComponent(PlayerType);
            var data = new SerializedObject(player);
            Assert.That(data.FindProperty("respawnMode").intValue, Is.Zero);
            Assert.That(data.FindProperty("respawnLoadingSeconds").floatValue, Is.EqualTo(2f));
            Assert.That(Enum.GetNames(ModeType), Is.EquivalentTo(new[] { "Standard", "LoadingTransition" }));
        }

        [TestCase(-5f, 0f)]
        [TestCase(0f, 0f)]
        [TestCase(3.5f, 3.5f)]
        [TestCase(float.NaN, 2f)]
        [TestCase(float.PositiveInfinity, 2f)]
        public void DeveloperConfiguration_SanitizesHoldTime(float input, float expected)
        {
            var root = new GameObject("Respawn configuration test");
            try
            {
                var player = root.AddComponent(PlayerType);
                var mode = Enum.ToObject(ModeType, 1);
                PlayerType.GetMethod("ConfigureRespawn").Invoke(player, new[] { mode, (object)input });
                Assert.That(PlayerType.GetProperty("RespawnMode").GetValue(player), Is.EqualTo(mode));
                Assert.That(PlayerType.GetProperty("RespawnLoadingSeconds").GetValue(player), Is.EqualTo(expected));
                Assert.That(PlayerType.GetProperty("IsRespawning").GetValue(player), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DeveloperConfiguration_RejectsUnknownMode()
        {
            var root = new GameObject("Invalid respawn mode test");
            try
            {
                var player = root.AddComponent(PlayerType);
                var exception = Assert.Throws<TargetInvocationException>(() =>
                    PlayerType.GetMethod("ConfigureRespawn").Invoke(player, new[] { Enum.ToObject(ModeType, 99), (object)2f }));
                Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void TransitionResources_KeepExplicitFontAndShaderReferences()
        {
            var settings = Resources.Load<GameTransitionSettings>("GameTransitionSettings");
            Assert.That(settings, Is.Not.Null);
            var serialized = new SerializedObject(settings);
            var font = serialized.FindProperty("font").objectReferenceValue;
            Assert.That(font, Is.Not.Null, EditorJsonUtility.ToJson(settings));
            StringAssert.Contains("Cubic_11 SDF.asset", AssetDatabase.GetAssetPath(font));
            Assert.That(settings.RevealShader, Is.Not.Null);
        }

        [Test]
        public void BossEntryFallback_UsesActivatedMarkedAnchorBeforeBoss()
        {
            var root = new GameObject("Boss entry fallback test");
            try
            {
                var earlyEntry = CreateAnchor(root.transform, "Earlier Boss entry", 1, true, true);
                var entry = CreateAnchor(root.transform, "Boss entry", 2, true, true);
                var boss = CreateAnchor(root.transform, "Boss checkpoint", 3, false, true);
                AnchorType.GetMethod("SetBossArenaRespawnPoint").Invoke(boss, new object[] { true });

                object[] arguments = { boss, null };
                Assert.That(
                    ChainType.GetMethod("TryFindBossEntryFallback")
                        .Invoke(null, arguments),
                    Is.True);
                Assert.That(arguments[1], Is.SameAs(entry));
                Assert.That(earlyEntry, Is.Not.SameAs(arguments[1]));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void BossEntryFallback_RequiresActivatedMarkerAndBossCurrentPoint()
        {
            var root = new GameObject("Boss entry fallback invalid test");
            try
            {
                var entry = CreateAnchor(root.transform, "Inactive Boss entry", 1, true, false);
                var boss = CreateAnchor(root.transform, "Boss checkpoint", 2, false, true);
                AnchorType.GetMethod("SetBossArenaRespawnPoint").Invoke(boss, new object[] { true });

                Assert.That(TryFindFallback(boss), Is.False);
                AnchorType.GetMethod("Activate").Invoke(entry, null);
                Assert.That(TryFindFallback(entry), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Component CreateAnchor(
            Transform parent,
            string name,
            int chainIndex,
            bool isBossEntry,
            bool activate)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            Component anchor = gameObject.AddComponent(AnchorType);
            AnchorType.GetMethod("SetChainData")
                .Invoke(anchor, new object[] { string.Empty, chainIndex, false, null });
            AnchorType.GetMethod("SetBossEntryRespawnPoint")
                .Invoke(anchor, new object[] { isBossEntry });
            if (activate)
            {
                AnchorType.GetMethod("Activate").Invoke(anchor, null);
            }
            return anchor;
        }

        private static bool TryFindFallback(Component boss)
        {
            object[] arguments = { boss, null };
            return (bool)ChainType.GetMethod("TryFindBossEntryFallback")
                .Invoke(null, arguments);
        }

        [Test]
        public void Overlay_ResetClearsEveryVisibleLayerAndInputBlocker()
        {
            var parent = new GameObject("Respawn overlay test");
            try
            {
                var overlay = GameTransitionOverlay.Ensure(parent.transform,
                    Resources.Load<GameTransitionSettings>("GameTransitionSettings"));
                overlay.ShowBlackImmediate();
                overlay.SetBackgroundVisible(true);
                overlay.SetLoadingTextVisible(true);
                var background = new SerializedObject(overlay.transform.Find("Background Copy").GetComponent("RawImage"));
                Assert.That(background.FindProperty("m_Texture").objectReferenceValue, Is.Not.Null);
                overlay.ResetPresentation();
                Assert.That(overlay.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
                foreach (Transform child in overlay.transform) Assert.That(child.gameObject.activeSelf, Is.False, child.name);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
    }
}
