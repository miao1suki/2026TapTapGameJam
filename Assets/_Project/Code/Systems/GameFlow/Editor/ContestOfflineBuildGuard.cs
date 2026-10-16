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
    /// <summary>Competition builds are offline. TapTap SDK integration requires an explicit policy review.</summary>
    public sealed class ContestOfflineBuildGuard : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public const BuildOptions ForbiddenOptions = BuildOptions.Development | BuildOptions.ConnectWithProfiler |
            BuildOptions.AllowDebugging | BuildOptions.EnableDeepProfilingSupport;
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";
        public static readonly string[] NetworkPermissions =
        {
            "INTERNET", "ACCESS_NETWORK_STATE", "CHANGE_NETWORK_STATE", "ACCESS_WIFI_STATE",
            "CHANGE_WIFI_STATE", "CHANGE_WIFI_MULTICAST_STATE", "NEARBY_WIFI_DEVICES", "ACCESS_LOCAL_NETWORK",
            "BLUETOOTH", "BLUETOOTH_ADMIN", "BLUETOOTH_SCAN", "BLUETOOTH_CONNECT", "BLUETOOTH_ADVERTISE"
        };
        public int callbackOrder => 10010;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOptions(report.summary.options);
            if (report.summary.platform == BuildTarget.Android && PlayerSettings.Android.forceInternetPermission)
                throw new BuildFailedException("比赛离线版本不得设置 Android Force Internet Permission。");
            ValidateUnityServices();
        }

        public static void ValidateOptions(BuildOptions options)
        {
            if ((options & ForbiddenOptions) != 0)
                throw new BuildFailedException("比赛离线版本禁止 Development、Profiler 和 Script Debugging 构建。请关闭后重新构建。");
        }

        public static void ValidateUnityServices()
        {
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("UnityConnectSettings"));
            foreach (string property in new[] { "m_Enabled", "UnityAnalyticsSettings.m_Enabled", "UnityAnalyticsSettings.m_InitializeOnStartup",
                "UnityAdsSettings.m_Enabled", "UnityAdsSettings.m_InitializeOnStartup", "PerformanceReportingSettings.m_Enabled",
                "CrashReportingSettings.m_EnableCloudDiagnosticsReporting", "InsightsSettings.m_EngineDiagnosticsEnabled", "InsightsSettings.m_Enabled" })
                if (ReadFlag(settings, property))
                    throw new BuildFailedException("比赛离线版本禁止 Unity 联网服务：" + property);
            var player = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            if (ReadFlag(player, "submitAnalytics"))
                throw new BuildFailedException("比赛离线版本禁止 Unity submitAnalytics。");
        }

        private static bool ReadFlag(SerializedObject settings, string name)
        {
            var flag = settings.FindProperty(name);
            if (flag == null) throw new BuildFailedException("无法验证离线设置，停止构建：" + name);
            return flag.boolValue;
        }

        public static void ProtectManifest(XDocument document)
        {
            var root = document.Root ?? throw new BuildFailedException("Android Manifest 缺少根节点。");
            root.SetAttributeValue(XNamespace.Xmlns + "tools", Tools.NamespaceName);
            foreach (string permission in NetworkPermissions)
            {
                string name = "android.permission." + permission;
                foreach (var entry in root.Elements().Where(e => e.Name.LocalName.StartsWith("uses-permission", StringComparison.Ordinal) &&
                             (string)e.Attribute(Android + "name") == name).ToArray()) entry.Remove();
                root.AddFirst(new XElement("uses-permission", new XAttribute(Android + "name", name), new XAttribute(Tools + "node", "remove")));
            }
            var application = root.Element("application");
            if (application == null) { application = new XElement("application"); root.Add(application); }
            application.SetAttributeValue(Android + "usesCleartextTraffic", "false");
            application.SetAttributeValue(Android + "debuggable", "false");
            string[] replacements = ((string)application.Attribute(Tools + "replace") ?? "").Split(',');
            application.SetAttributeValue(Tools + "replace", string.Join(",", replacements.Select(s => s.Trim())
                .Where(s => s.Length > 0).Concat(new[] { "android:usesCleartextTraffic", "android:debuggable" }).Distinct()));
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // launcher is the highest-priority application manifest, above Unity and all Android libraries.
            string launcher = Path.Combine(Directory.GetParent(path)!.FullName, "launcher/src/main/AndroidManifest.xml");
            if (!File.Exists(launcher)) throw new BuildFailedException("缺少 launcher Manifest，无法保证离线权限合并。");
            var manifest = XDocument.Load(launcher); ProtectManifest(manifest); manifest.Save(launcher);
            UnityEngine.Debug.Log("[OfflineBuild] Release player; launcher removes Internet, LAN, Wi-Fi and Bluetooth permissions. No TapTap SDK is currently installed.");
        }
    }
}
