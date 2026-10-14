using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowRoundTripTests
    {
        private GameObject audioRoot;

        [UnityTest]
        public IEnumerator Menu_Gameplay_PausedReturn_Repeat_RestoresMusicAndSimulation()
        {
            yield return LoadForTest("Assets/_Project/Scenes/Start/MainMenu.unity");
            yield return WaitForScene(GameFlowSceneId.MainMenu);
            var audio = GameAudioService.Instance;
            Assert.That(audio, Is.Not.Null, "Flow audio service must not depend on a retired system scene.");
            audioRoot = audio.MusicSource.transform.root.gameObject;

            for (int entry = 0; entry < 2; entry++)
            {
                yield return LoadForTest("Assets/_Project/Scenes/Playing/Gaming.unity");
                Assert.That(GameFlowController.Instance.RequestTransition(GameFlowSceneId.Gaming), Is.True);
                yield return WaitForScene(GameFlowSceneId.Gaming);
                Assert.That(GameAudioService.Instance, Is.SameAs(audio));
                Assert.That(audio.MusicSource.clip, Is.Not.Null);
                StringAssert.Contains("atmosphere_emin", audio.MusicSource.clip.name);
                Assert.That(audio.MusicSource.loop, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(AudioListener.pause, Is.False);

                var ui = Object.FindFirstObjectByType<GameUiRouter>();
                Assert.That(ui, Is.Not.Null);
                yield return LoadForTest("Assets/_Project/Scenes/Start/MainMenu.unity");
                ui.SetPaused(true, false);
                Assert.That(Time.timeScale, Is.Zero);
                if (entry == 1) ui.ResumeGame(); // Leave while the normal close animation is pending.
                ui.OpenMainMenu();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(AudioListener.pause, Is.False);
                yield return WaitForScene(GameFlowSceneId.MainMenu);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(AudioListener.pause, Is.False);
            }
        }

        private static AsyncOperation LoadForTest(string path)
        {
#if UNITY_EDITOR
            // The editor PlayMode runner replaces the build scene list. Load assets explicitly;
            // the separate real-entry harness covers GameSceneLoader's normal build-list loading.
            return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
#else
            return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
#endif
        }

        private static IEnumerator WaitForScene(GameFlowSceneId id)
        {
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var flow = GameFlowController.Instance;
                if (flow != null && flow.IsInitialized && !flow.IsTransitioning && flow.ActiveSceneId == id)
                {
                    yield return null;
                    yield break;
                }
                yield return null;
            }
            Assert.Fail("Scene transition timed out: " + id);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (GameFlowController.Instance != null) Object.Destroy(GameFlowController.Instance.gameObject);
            if (audioRoot != null) Object.Destroy(audioRoot);
            yield return null;
            foreach (var path in new[] {
                "Assets/_Project/Scenes/Start/MainMenu.unity",
                "Assets/_Project/Scenes/Playing/Gaming.unity" })
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
