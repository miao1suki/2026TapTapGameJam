using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Project.GameFlow.Editor;
using Project.Privacy;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class ContestOfflineTests
    {
        private static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        private static readonly XNamespace Tools = "http://schemas.android.com/tools";

        [TestCase(BuildOptions.Development)]
        [TestCase(BuildOptions.ConnectWithProfiler)]
        [TestCase(BuildOptions.AllowDebugging)]
        [TestCase(BuildOptions.EnableDeepProfilingSupport)]
        public void NetworkCapableDebugOptionsAreRejected(BuildOptions options)
        { Assert.Throws<BuildFailedException>(() => ContestOfflineBuildGuard.ValidateOptions(options)); }

        [Test] public void ReleaseOptionsAndCurrentServicesAreOffline()
        {
            Assert.DoesNotThrow(() => ContestOfflineBuildGuard.ValidateOptions(BuildOptions.None));
            Assert.DoesNotThrow(ContestOfflineBuildGuard.ValidateUnityServices);
            Assert.IsFalse(PlayerSettings.Android.forceInternetPermission);
        }

        [Test] public void ManifestAllowsOnlyTapTapNetworkPermissionsAndPreservesUnrelatedEntries()
        {
            var manifest = XDocument.Parse("<manifest xmlns:android='http://schemas.android.com/apk/res/android' xmlns:tools='http://schemas.android.com/tools'><uses-permission android:name='android.permission.INTERNET'/><uses-permission android:name='android.permission.ACCESS_NETWORK_STATE'/><uses-permission android:name='android.permission.CHANGE_NETWORK_STATE'/><uses-permission-sdk-23 android:name='android.permission.NEARBY_WIFI_DEVICES'/><uses-permission android:name='android.permission.VIBRATE'/><application tools:replace='android:theme' android:theme='@style/Theme'><meta-data android:name='example' android:value='1'/></application></manifest>");
            ContestOfflineBuildGuard.ProtectManifest(manifest);
            ContestOfflineBuildGuard.ProtectManifest(manifest);
            foreach (string permission in ContestOfflineBuildGuard.TapTapNetworkPermissions)
            {
                var entries = manifest.Root.Elements().Where(e => (string)e.Attribute(Android + "name") == "android.permission." + permission).ToArray();
                Assert.That(entries.Length, Is.EqualTo(1), permission);
                Assert.That(entries[0].Attribute(Tools + "node"), Is.Null, permission);
            }
            foreach (string permission in ContestOfflineBuildGuard.ForbiddenNetworkPermissions)
            {
                var entries = manifest.Root.Elements().Where(e => (string)e.Attribute(Android + "name") == "android.permission." + permission).ToArray();
                Assert.That(entries.Length, Is.EqualTo(1), permission);
                Assert.That((string)entries[0].Attribute(Tools + "node"), Is.EqualTo("remove"), permission);
            }
            Assert.That(manifest.Root.Elements("uses-permission").Single(e => (string)e.Attribute(Android + "name") == "android.permission.VIBRATE").Attribute(Tools + "node"), Is.Null);
            var app = manifest.Root.Element("application");
            Assert.That((string)app.Attribute(Android + "debuggable"), Is.EqualTo("false"));
            Assert.That((string)app.Attribute(Android + "usesCleartextTraffic"), Is.EqualTo("false"));
            Assert.That((string)app.Attribute(Tools + "replace"), Does.Contain("android:theme"));
            Assert.That(app.Elements("meta-data").Count(), Is.EqualTo(1));
        }

        [Test] public void LocalPolicyRetainsFullTextAndOnlyExplicitFixedWebsiteIsAllowed()
        {
            var policy = Resources.Load<TextAsset>("Privacy/privacy-policy");
            Assert.IsNotNull(policy);
            string plain = AndroidPrivacyPolicy.PlainText(policy.text);
            Assert.That(plain, Does.Contain("形之彩隐私政策"));
            Assert.That(plain, Does.Contain("如何联系我们"));
            Assert.That(plain, Does.Contain("2565553717@qq.com"));
            Assert.That(plain, Does.Not.Contain("<a"));
            Assert.That(plain, Does.Not.Contain("<script"));
            string native = File.ReadAllText("Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/PrivacyActivity.java");
            Assert.That(native, Does.Not.Contain("ACTION_VIEW"));
            Assert.That(native, Does.Not.Contain("LinkMovementMethod"));
            Assert.That(native, Does.Contain("text.removeSpan(link)"));
            Assert.That(File.ReadAllText("Assets/_Project/Code/Shared/Runtime/AndroidPrivacyPolicy.cs"), Does.Not.Contain("Application.OpenURL"));
            string consent = File.ReadAllText("Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/PrivacyConsent.java");
            Assert.That(consent, Does.Contain("Intent.ACTION_VIEW, Uri.parse(POLICY_URL)"));
            Assert.That(consent, Does.Contain("POLICY_URL = \"https://meowgame.cloud/shape-of-color/privacy/\""));
            Assert.That(consent, Does.Not.Contain("android.webkit"));
            Assert.That(consent, Does.Not.Contain("java.net"));
        }
    }
}
