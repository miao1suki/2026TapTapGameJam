using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Project.ObjectArtwork.Editor
{
    /// <summary>Never ship a missing sequence and silently substitute the legacy live simulation.</summary>
    public sealed class VineSequenceBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;
        public void OnPreprocessBuild(BuildReport report) => Validate();
        public static void Validate()
        {
            var library = ObjectArtworkLibrary.Load();
            if (library == null || library.vineGrowth == null || library.vineGrowth.Length < 192 ||
                library.colorSpriteMaterial == null || library.colorSpriteMaterial.shader == null)
                throw new BuildFailedException("藤蔓序列帧资源不完整，停止构建。请检查 ObjectArtwork/Objects 与序列帧材质。");
            for (int i = 0; i < library.vineGrowth.Length; i++)
                if (library.vineGrowth[i] == null || library.vineGrowth[i].texture == null)
                    throw new BuildFailedException($"藤蔓序列帧第 {i + 1} 帧缺失，停止构建。");
        }
    }
}
