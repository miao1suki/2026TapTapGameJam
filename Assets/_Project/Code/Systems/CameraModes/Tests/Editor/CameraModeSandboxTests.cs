using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Project.CameraModes.Tests
{
    public sealed class CameraModeSandboxTests
    {
        private const string ScenePath =
            "Assets/_Project/Scenes/Systems/Systems_Camera.unity";

        [Test]
        public void SandboxScene_HasConfiguredControllerAndIsNotInBuildSettings()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            CameraModeController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CameraModeController>(true))
                .Single();

            Assert.That(controller.ControlledCamera, Is.Not.Null);
            Assert.That(controller.Manager, Is.Not.Null);
            Assert.That(controller.Manager.OutputCamera, Is.SameAs(controller.ControlledCamera));
            Assert.That(
                EditorBuildSettings.scenes.Any(item =>
                    string.Equals(item.path, ScenePath, StringComparison.OrdinalIgnoreCase)),
                Is.True);
        }
    }
}
