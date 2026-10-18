using NUnit.Framework;
using Project.GameFlow.Editor;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowSceneTests
    {
        [Test]
        public void RedButtonDoesNotRegisterKeyboardInteraction()
        {
            var buttonType = System.Type.GetType("Project.BlockFeatures.ButtonLockFeature, Assembly-CSharp");
            Assert.NotNull(buttonType);
            foreach (var contract in buttonType.GetInterfaces())
                Assert.AreNotEqual("IInteractionTarget", contract.Name);
        }
        [TestCase(200f, 5)]
        [TestCase(-200f, 1)]
        public void PauseTouchSwipeUsesReversedVerticalDirection(float distance, int expected)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab");
            var root = Object.Instantiate(source);
            var events = new GameObject("Swipe test events");
            try
            {
                var controller = root.GetComponentInChildren<EscMenuController>(true);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(EscMenuController).GetField("isOpen", flags).SetValue(controller, true);
                typeof(EscMenuController).GetField("wheelInputReady", flags).SetValue(controller, true);
                typeof(EscMenuController).GetField("currentIndex", flags).SetValue(controller, 0);
                var touch = events.AddComponent<EscWheelTouchNavigation>(); touch.Configure(controller);
                var system = events.AddComponent<UnityEngine.EventSystems.EventSystem>();
                var data = new UnityEngine.EventSystems.PointerEventData(system) { pointerId = 5, position = Vector2.zero };
                touch.OnBeginDrag(data);
                data.position = new Vector2(0, distance); touch.OnDrag(data);
                Assert.AreEqual(expected, typeof(EscMenuController).GetField("currentIndex", flags).GetValue(controller));
                touch.OnEndDrag(data);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(events); }
        }
        [Test]
        public void PauseWheel_UsesSixSpacedSectorsWithExternalPivot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab");
            var controller = prefab.GetComponentInChildren<EscMenuController>(true);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EscMenuController);
            var pivot = (RectTransform)type.GetField("wheelPivot", flags).GetValue(controller);
            var buttons = (UnityEngine.UI.Button[])type.GetField("wheelButtons", flags).GetValue(controller);
            Assert.That(buttons.Length, Is.EqualTo(6));
            Assert.That(pivot.anchorMin.x, Is.Zero);
            Assert.That(((RectTransform)pivot.parent).sizeDelta.x, Is.GreaterThanOrEqualTo(640f));
            for (int index = 0; index < buttons.Length; index++)
            {
                var rect = (RectTransform)buttons[index].transform;
                Assert.That(rect.anchoredPosition.magnitude, Is.EqualTo(64f).Within(.01f));
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(288f, 342f)));
                float positionAngle = Mathf.Atan2(rect.anchoredPosition.y, rect.anchoredPosition.x) * Mathf.Rad2Deg;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(positionAngle, index * 60f)), Is.LessThan(.01f));
                Assert.That(rect.pivot.x, Is.LessThan(0f));
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rect.localEulerAngles.z, index * 60f)), Is.LessThan(.01f));
                var image = (UnityEngine.UI.Image)buttons[index].targetGraphic;
                Assert.That(image.sprite.texture.isReadable, Is.True);
            }
            var options = (System.Collections.Generic.List<EscWheelOption>)type.GetField("options", flags).GetValue(controller);
            Assert.That(options[5].action, Is.EqualTo(EscWheelAction.Credits));
            Assert.That(options[5].panel, Is.EqualTo(type.GetField("settingsPage", flags).GetValue(controller)));
        }

        [TestCase(false, true)]
        [TestCase(false, false)]
        [TestCase(true, false)]
        public void ImmediateUnpause_RestoresGlobalsEvenDuringPendingClose(bool paused, bool closing)
        {
            float previousScale = Time.timeScale;
            bool previousAudioPause = AudioListener.pause;
            var root = new GameObject("Pause cleanup regression");
            try
            {
                var router = root.AddComponent<GameUiRouter>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(GameUiRouter).GetField("isPaused", flags).SetValue(router, paused);
                typeof(GameUiRouter).GetField("pauseCloseInProgress", flags).SetValue(router, closing);
                Time.timeScale = 0f;
                AudioListener.pause = true;
                router.SetPaused(false, false, true);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(AudioListener.pause, Is.False);
                Assert.That(router.IsPaused, Is.False);
                Assert.That(typeof(GameUiRouter).GetField("pauseCloseInProgress", flags).GetValue(router), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Time.timeScale = previousScale;
                AudioListener.pause = previousAudioPause;
            }
        }

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
            Assert.That(menu.ScenePath, Is.EqualTo(GameFlowSceneScaffolder.GamingPath));
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
            Assert.That(System.Linq.Enumerable.Where(EditorBuildSettings.scenes, s => s.enabled)
                .Select(s => s.path), Is.EqualTo(new[] { GameFlowSceneScaffolder.GamingPath }));
        }

        [Test]
        public void WorldMenuPrefab_HasOnlyNewArtworkAndNoWorldDevices()
        {
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/UI/WorldMainMenu.prefab");
            Assert.That(menu, Is.Not.Null);
            Assert.That(menu.GetComponentInChildren<Project.StartMenu.StartMenuController>(true).Background, Is.Null);
            Assert.That(menu.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(menu.GetComponentsInChildren<AudioListener>(true), Is.Empty);
            Assert.That(menu.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true), Is.Empty);
            Assert.That(System.Linq.Enumerable.Any(menu.GetComponentsInChildren<UnityEngine.UI.Image>(true), x => x.name == "Animated Logo"), Is.True);
        }

        [Test]
        public void Validator_AcceptsIndependentScenes()
        {
            Assert.That(GameFlowProjectValidator.Validate(out string report), Is.True, report);
        }

        [TestCase("Assets/_Project/Content/UI/MainMenuUI.prefab")]
        [TestCase("Assets/_Project/Content/UI/SharedGameUI.prefab")]
        [TestCase("Assets/_Project/Content/Player/Player.prefab")]
        public void IndependentPrefabs_ArePreserved(string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Not.Null);
        }

        [Test]
        public void StandardPlayer_IsOneCellAndRetiredSourcesAreAbsent()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.CompareTag("Player"), Is.True);
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.localScale, Is.EqualTo(Vector3.one));
            var capsule = prefab.GetComponent<CapsuleCollider>();
            Assert.That(capsule.center, Is.EqualTo(Vector3.zero));
            Assert.That(capsule.height, Is.EqualTo(1f));
            Assert.That(capsule.radius, Is.EqualTo(.5f));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/RuntimePlayer.prefab"), Is.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player(暂定).prefab"), Is.Null);
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
