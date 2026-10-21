using NUnit.Framework;
using Project.GameFlow.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace Project.GameFlow.Tests
{
    public sealed class AndroidGraphicsCompatibilityTests
    {
        [Test]
        public void ReleaseConfiguration_UsesOnlyOpenGLES3()
        {
            Assert.DoesNotThrow(AndroidGraphicsCompatibilityBuildGuard.Validate);
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android),
                Is.EqualTo(new[] { GraphicsDeviceType.OpenGLES3 }));
        }

        [Test]
        public void Configure_IsIdempotentAndDoesNotChangeDesktopApi()
        {
            var saved = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool automatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var desktop = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
            bool desktopAutomatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
            try
            {
                AndroidGraphicsCompatibilityBuildGuard.Configure();
                AndroidGraphicsCompatibilityBuildGuard.Configure();
                Assert.DoesNotThrow(AndroidGraphicsCompatibilityBuildGuard.Validate);
                Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64), Is.EqualTo(desktop));
                Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64), Is.EqualTo(desktopAutomatic));
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, saved);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, automatic);
            }
        }

        [Test]
        public void Guard_RejectsVulkanFallbackAndAutomaticSelection()
        {
            var saved = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            bool automatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                    new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
                Assert.Throws<BuildFailedException>(AndroidGraphicsCompatibilityBuildGuard.Validate);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, true);
                Assert.Throws<BuildFailedException>(AndroidGraphicsCompatibilityBuildGuard.Validate);
            }
            finally
            {
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, saved);
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, automatic);
            }
        }
    }
}
