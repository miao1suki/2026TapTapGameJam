using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using Project.GameFlow.Editor;
using UnityEditor.Build;

namespace Project.GameFlow.Tests
{
    public sealed class AndroidPrivacyTests
    {
        static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
        [Test] public void UnityCannotBeAnExportedLauncher()
        {
            var manifest = XDocument.Parse("<manifest xmlns:android='http://schemas.android.com/apk/res/android'><application><activity android:name='com.unity3d.player.UnityPlayerActivity' android:exported='true'><intent-filter><action android:name='android.intent.action.MAIN'/><category android:name='android.intent.category.LAUNCHER'/></intent-filter><meta-data android:name='unityplayer.UnityActivity' android:value='true'/></activity></application></manifest>");
            AndroidPrivacyBuildGuard.ProtectManifest(manifest);
            AndroidPrivacyBuildGuard.ProtectManifest(manifest);
            var activity = manifest.Descendants("activity").Single();
            Assert.AreEqual("false", (string)activity.Attribute(Android + "exported"));
            Assert.IsFalse(activity.Elements("intent-filter").Any());
            Assert.IsTrue(activity.Elements("meta-data").Any());
        }
        [Test] public void UnreviewedAutoInitializingProvidersBlockBuild()
        {
            var manifest = XDocument.Parse("<manifest><application><provider/></application></manifest>");
            Assert.Throws<BuildFailedException>(() => AndroidPrivacyBuildGuard.ProtectManifest(manifest));
        }
        [Test] public void MissingUnityActivityBlocksBuild()
        {
            Assert.Throws<BuildFailedException>(() => AndroidPrivacyBuildGuard.ProtectManifest(
                XDocument.Parse("<manifest><application/></manifest>")));
        }
        [Test] public void GateHasItsOwnProcessAndIsOnlySourceLauncher()
        {
            var manifest = XDocument.Load("Assets/Plugins/Android/PrivacyGate.androidlib/AndroidManifest.xml");
            var activity = manifest.Descendants("activity").Single();
            Assert.AreEqual(AndroidPrivacyBuildGuard.Gate, (string)activity.Attribute(Android + "name"));
            Assert.AreEqual(":privacy", (string)activity.Attribute(Android + "process"));
            Assert.AreEqual(AndroidPrivacyBuildGuard.App, (string)manifest.Descendants("application").Single().Attribute(Android + "name"));
            string policy = File.ReadAllText(AndroidPrivacyBuildGuard.PolicyAsset);
            foreach (string part in new[] { "形之彩隐私政策", "Unity3D SDK", "如何联系我们", "2565553717@qq.com" }) Assert.That(policy, Does.Contain(part));
            Assert.That(policy, Does.Not.Contain("<script"));
            Assert.That(policy, Does.Not.Contain("<iframe"));
        }

        [Test] public void ConsentChoicesHaveOnlyTheRequestedLabels()
        {
            string native = File.ReadAllText("Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/PrivacyActivity.java");
            Assert.That(native, Does.Contain("Button decline = button(\"不同意\")"));
            Assert.That(native, Does.Contain("Button agree = button(\"同意\")"));
            Assert.That(native, Does.Not.Contain("button(\"不同意并退出\")"));
            Assert.That(native, Does.Not.Contain("button(\"同意并进入游戏\")"));
            Assert.That(native, Does.Contain("agree.setEnabled(loaded)"));
            Assert.That(native, Does.Contain("if (PrivacyConsent.accept(this)) enterGame()"));
        }

        [Test] public void PolicyWebsiteIsFixedHttpsAndOnlyUsesExternalBrowser()
        {
            string source = File.ReadAllText("Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/PrivacyConsent.java");
            Assert.That(source, Does.Contain("POLICY_URL = \"https://meowgame.cloud/shape-of-color/privacy/\""));
            Assert.That(source, Does.Contain("new Intent(Intent.ACTION_VIEW, Uri.parse(POLICY_URL))"));
            Assert.That(source, Does.Contain("Intent.CATEGORY_BROWSABLE"));
            Assert.That(source, Does.Not.Contain("android.webkit"));
            Assert.That(source, Does.Not.Contain("java.net"));
            Assert.That(source, Does.Not.Contain("getStringExtra"));
            Assert.That(source, Does.Contain("ActivityNotFoundException | SecurityException"));
            var manifest = XDocument.Load("Assets/Plugins/Android/PrivacyGate.androidlib/AndroidManifest.xml");
            Assert.That(manifest.Descendants("uses-permission").Any(), Is.False);
        }

        [Test] public void BrowsingIsExplicitDoesNotGrantConsentAndRestorationDoesNotReopen()
        {
            string root = "Assets/Plugins/Android/PrivacyGate.androidlib/src/com/miao1suki/shapeofcolor/privacy/";
            string native = File.ReadAllText(root + "PrivacyActivity.java");
            Assert.That(native, Does.Contain("website.setOnClickListener(view -> PrivacyConsent.openPolicyWebsite(this))"));
            Assert.That(native, Does.Contain("review && state == null && getIntent().getBooleanExtra(PrivacyConsent.OPEN_POLICY_WEBSITE, false)"));
            string consent = File.ReadAllText(root + "PrivacyConsent.java");
            string browser = consent.Substring(consent.IndexOf("static void openPolicyWebsite("));
            browser = browser.Substring(0, browser.IndexOf("public static void withdrawConsentAndExit"));
            Assert.That(browser, Does.Not.Contain("accept("));
            Assert.That(browser, Does.Not.Contain("enterGame("));
            Assert.That(consent, Does.Contain("gate(activity, true).putExtra(OPEN_POLICY_WEBSITE, true)"));
        }
    }
}
