using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Project.GameFlow.Editor
{
    /// <summary>Installs the native launcher into generated Gradle output, never into user scenes.</summary>
    public sealed class AndroidPrivacyBuildGuard : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public const string Gate = "com.miao1suki.shapeofcolor.privacy.PrivacyActivity";
        public const string App = "com.miao1suki.shapeofcolor.privacy.PrivacyApplication";
        public const string UnityActivity = "com.unity3d.player.UnityPlayerActivity";
        public const string TapTapInitProvider =
            "com.taptap.sdk.kit.internal.TapTapKitInitProvider";
        public const string TapTapInitAuthority =
            "${applicationId}.TapTapKitInitProvider";
        public const string PolicyAsset = "Assets/_Project/Resources/Privacy/privacy-policy.txt";
        static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 10000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity)
                throw new BuildFailedException("原生隐私入口要求 Android Application Entry 为 Activity，不能直接启动 GameActivity。");
            var policy = File.ReadAllText(PolicyAsset);
            if (!policy.Contains("形之彩隐私政策") || !policy.Contains("Unity3D SDK") || !policy.Contains("如何联系我们"))
                throw new BuildFailedException("隐私政策正文不完整，停止安卓构建。");
        }

        public static void ProtectManifest(XDocument document)
        {
            var application = document.Root?.Element("application") ?? throw new BuildFailedException("缺少 Android application。");
            ValidateProviders(application.Elements("provider"));
            if (!application.Elements("activity").Any(e => (string)e.Attribute(Android + "name") == UnityActivity))
                throw new BuildFailedException("缺少 UnityPlayerActivity，停止构建以避免绕过隐私入口。");
            foreach (var activity in application.Elements("activity"))
            {
                string name = (string)activity.Attribute(Android + "name");
                if (name != UnityActivity) continue;
                activity.SetAttributeValue(Android + "exported", "false");
                foreach (var filter in activity.Elements("intent-filter").ToArray())
                    if (filter.Elements("category").Any(e => (string)e.Attribute(Android + "name") == "android.intent.category.LAUNCHER")) filter.Remove();
            }
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            var document = XDocument.Load(manifest); ProtectManifest(document); document.Save(manifest);
            string root = Directory.GetParent(path)!.FullName;
            foreach (string file in Directory.GetFiles(root, "AndroidManifest.xml", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "build" + Path.DirectorySeparatorChar)) continue;
                var source = XDocument.Load(file);
                ValidateProviders(source.Descendants("provider"), file);
            }
            var module = Directory.GetDirectories(root, "PrivacyGate.androidlib", SearchOption.AllDirectories).FirstOrDefault();
            if (module == null) throw new BuildFailedException("PrivacyGate.androidlib 未纳入 Gradle 工程。");
            var font = AssetDatabase.FindAssets("Cubic_11 t:Font").Select(AssetDatabase.GUIDToAssetPath)
                .SingleOrDefault(file => file.EndsWith("/Cubic_11.ttf", StringComparison.OrdinalIgnoreCase));
            if (font == null) throw new BuildFailedException("缺少项目统一字体 Cubic_11.ttf。");
            Directory.CreateDirectory(Path.Combine(module, "assets"));
            File.Copy(PolicyAsset, Path.Combine(module, "assets/privacy-policy.html"), true);
            File.Copy(font, Path.Combine(module, "assets/privacy-font.ttf"), true);
            UnityEngine.Debug.Log("[PrivacyGate] Native consent launcher installed; Unity is not exported. Policy/font are offline assets.");
        }

        private static void ValidateProviders(
            System.Collections.Generic.IEnumerable<XElement> providers,
            string source = "")
        {
            foreach (var provider in providers)
            {
                string name = (string)provider.Attribute(Android + "name");
                string authority = (string)provider.Attribute(Android + "authorities");
                string exported = (string)provider.Attribute(Android + "exported");
                if (name == TapTapInitProvider &&
                    authority == TapTapInitAuthority &&
                    exported == "false")
                {
                    continue;
                }

                throw new BuildFailedException(
                    "发现未经审计的自动初始化 Provider：" +
                    name +
                    (string.IsNullOrEmpty(source) ? "" : " @ " + source));
            }
        }
    }
}
