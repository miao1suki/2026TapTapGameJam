using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowMenuEntryTests
    {
        [UnityTest]
        public IEnumerator Gaming_SingleSceneEntry_HasLiveWorldAndMenu()
        {
            const string path = GameFlowController.GameplayScenePath;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
#endif
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline && (GameFlowController.Instance == null ||
                !GameFlowController.Instance.IsInitialized || GameFlowController.Instance.IsTransitioning ||
                GameFlowController.Instance.ActiveSceneId != GameFlowSceneId.MainMenu)) yield return null;
            Assert.That(GameFlowController.Instance.ActiveSceneId, Is.EqualTo(GameFlowSceneId.MainMenu));
            var scene = SceneManager.GetSceneByPath(path);
            Assert.That(scene.isLoaded, Is.True);
            Assert.That(GameFlowController.Instance, Is.Not.Null);
            int cameras = 0, events = 0, listeners = 0, canvases = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                cameras += root.GetComponentsInChildren<Camera>(true).Length;
                events += root.GetComponentsInChildren<EventSystem>(true).Length;
                listeners += root.GetComponentsInChildren<AudioListener>(true).Length;
                canvases += root.GetComponentsInChildren<Canvas>(true).Length;
            }
            Assert.That(cameras, Is.EqualTo(1));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(listeners, Is.EqualTo(1));
            Assert.That(canvases, Is.GreaterThan(0));
            Assert.That(SceneManager.GetSceneByPath("Assets/_Project/Scenes/Playing/Gaming.unity").isLoaded, Is.True);
            Assert.That(SceneManager.GetSceneByPath(GameFlowController.CompatibilityMenuScenePath).isLoaded, Is.False);
            Assert.That(GameplayPresentationGate.IsSuppressed, Is.True);
            Assert.That(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x => x.isActiveAndEnabled), Is.EqualTo(1));
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Assert.That(SceneManager.GetSceneAt(index).name.StartsWith("Systems_"), Is.False);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var audio = GameAudioService.Instance;
            if (GameFlowController.Instance != null) Object.Destroy(GameFlowController.Instance.gameObject);
            if (GameSceneLoader.Existing != null) Object.Destroy(GameSceneLoader.Existing.gameObject);
            if (audio != null && audio.MusicSource != null) Object.Destroy(audio.MusicSource.transform.root.gameObject);
            yield return null;
            foreach (string path in new[] { "Assets/_Project/Scenes/Start/MainMenu.unity", "Assets/_Project/Scenes/Playing/Gaming.unity" })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }
    }
}
