using NUnit.Framework;
using Project.GameFlow.Editor;
using UnityEditor;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowSceneTests
    {
        [Test]
        public void Catalog_NoLongerReferencesRetiredFlow()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(GameFlowSceneScaffolder.CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.PersistentScenePaths.Count, Is.Zero);
            Assert.That(catalog.FlowScenes.Count, Is.EqualTo(2));
            Assert.That(
                catalog.TryGet(GameFlowSceneId.MainMenu, out var menu),
                Is.True);
            Assert.That(menu.ScenePath, Is.EqualTo(GameFlowSceneScaffolder.MainMenuPath));
            Assert.That(
                catalog.TryGet(GameFlowSceneId.Gaming, out var gaming),
                Is.True);
            Assert.That(gaming.ScenePath, Is.EqualTo(GameFlowSceneScaffolder.GamingPath));
        }

        [Test]
        public void BuildSettings_ReferencesExistingScenesOnly()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.GreaterThan(0));
            foreach (var scene in EditorBuildSettings.scenes)
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path), Is.Not.Null, scene.path);
        }

        [Test]
        public void Validator_AcceptsIndependentScenes()
        {
            Assert.That(GameFlowProjectValidator.Validate(out string report), Is.True, report);
        }

        [TestCase("Assets/_Project/Content/UI/MainMenuUI.prefab")]
        [TestCase("Assets/_Project/Content/UI/SharedGameUI.prefab")]
        [TestCase("Assets/_Project/Content/Player/RuntimePlayer.prefab")]
        [TestCase("Assets/_Project/Content/Player/Player(暂定).prefab")]
        public void IndependentPrefabs_ArePreserved(string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null);
        }

        [TestCase(GameFlowSceneScaffolder.CorePath)]
        [TestCase(GameFlowSceneScaffolder.InputPath)]
        [TestCase(GameFlowSceneScaffolder.AudioPath)]
        [TestCase(GameFlowSceneScaffolder.CameraPath)]
        [TestCase(GameFlowSceneScaffolder.UiPath)]
        [TestCase(GameFlowSceneScaffolder.Level01Path)]
        [TestCase(GameFlowSceneScaffolder.Level02Path)]
        [TestCase(GameFlowSceneScaffolder.Level03Path)]
        [TestCase(GameFlowSceneScaffolder.EndingPath)]
        public void RetiredScenes_AreAbsent(string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Null);
        }

        [Test]
        public void RetiredGenerator_CannotRecreateScenes()
        {
            Assert.Throws<System.NotSupportedException>(() => GameFlowSceneScaffolder.Generate());
        }
    }
}
