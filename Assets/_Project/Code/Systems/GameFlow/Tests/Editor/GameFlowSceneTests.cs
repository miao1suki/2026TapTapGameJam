using NUnit.Framework;
using Project.GameFlow.Editor;
using UnityEditor;

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowSceneTests
    {
        [Test]
        public void Catalog_ContainsCanonicalScenes()
        {
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.BootstrapScenePath,
                Is.EqualTo(GameFlowSceneScaffolder.BootstrapPath));
            Assert.That(catalog.PersistentScenePaths.Count, Is.EqualTo(5));
            Assert.That(catalog.FlowScenes.Count, Is.EqualTo(5));
            Assert.That(catalog.TryGet(GameFlowSceneId.MainMenu, out _), Is.True);
            Assert.That(catalog.TryGet(GameFlowSceneId.Level01, out _), Is.True);
            Assert.That(catalog.TryGet(GameFlowSceneId.Level02, out _), Is.True);
            Assert.That(catalog.TryGet(GameFlowSceneId.Level03, out _), Is.True);
            Assert.That(catalog.TryGet(GameFlowSceneId.Ending, out _), Is.True);
        }

        [Test]
        public void BuildSettings_ContainsOnlyCanonicalScenesInOrder()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            Assert.That(
                scenes.Length,
                Is.EqualTo(GameFlowSceneScaffolder.CanonicalBuildScenePaths.Length));
            for (int index = 0; index < scenes.Length; index++)
            {
                Assert.That(scenes[index].enabled, Is.True);
                Assert.That(
                    scenes[index].path,
                    Is.EqualTo(
                        GameFlowSceneScaffolder.CanonicalBuildScenePaths[index]));
            }
        }

        [Test]
        public void Validator_AcceptsGeneratedProject()
        {
            bool valid = GameFlowProjectValidator.Validate(out string report);
            Assert.That(valid, Is.True, report);
        }

        [Test]
        public void PlayablePrototype_HasPlayerPrefabAndLevelScenes()
        {
            Assert.That(
                AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(
                    "Assets/_Project/Content/Player/RuntimePlayer.prefab"),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GameFlowSceneScaffolder.Level01Path),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GameFlowSceneScaffolder.Level02Path),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GameFlowSceneScaffolder.Level03Path),
                Is.Not.Null);
        }

        [TestCase(GameFlowSceneId.Level01, GameFlowSceneScaffolder.Level01Path)]
        [TestCase(GameFlowSceneId.Level02, GameFlowSceneScaffolder.Level02Path)]
        [TestCase(GameFlowSceneId.Level03, GameFlowSceneScaffolder.Level03Path)]
        public void Catalog_MapsLevelPathBackToId(
            GameFlowSceneId expected,
            string path)
        {
            GameSceneCatalog catalog =
                AssetDatabase.LoadAssetAtPath<GameSceneCatalog>(
                    GameFlowSceneScaffolder.CatalogPath);
            Assert.That(catalog.TryGetIdForPath(path, out GameFlowSceneId actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
