using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

namespace Project.GameFlow.Editor
{
    /// <summary>Keep Android releases off the confirmed Adreno Vulkan startup crash path.</summary>
    public sealed class AndroidGraphicsCompatibilityBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10000;

        public static void Configure()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { GraphicsDeviceType.OpenGLES3 });
        }

        public static void Validate()
        {
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            if (PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android) ||
                apis.Length != 1 || apis[0] != GraphicsDeviceType.OpenGLES3)
                throw new BuildFailedException(
                    "Android releases must use OpenGLES3 only. TapPlay vivo S19 / Android 16 crashed in Adreno Vulkan vkCmdDrawIndexed; do not silently restore Vulkan or automatic API selection.");
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android) Validate();
        }
    }
}
