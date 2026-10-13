using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Project.GameFlow.Tests
{
    public sealed class GameFlowMenuEntryTests
    {
        [UnityTest]
        public IEnumerator MainMenu_LoadsOnlyCurrentFlow()
        {
            const string path = "Assets/_Project/Scenes/Start/MainMenu.unity";
            yield return SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
            yield return null;
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
            for (int index = 0; index < SceneManager.sceneCount; index++)
                Assert.That(SceneManager.GetSceneAt(index).name.StartsWith("Systems_"), Is.False);
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
