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
            string policy = File.ReadAllText("Assets/Plugins/Android/PrivacyGate.androidlib/assets/privacy-policy.html");
            foreach (string part in new[] { "形之彩隐私政策", "Unity3D SDK", "如何联系我们", "2565553717@qq.com" }) Assert.That(policy, Does.Contain(part));
            Assert.That(policy, Does.Not.Contain("<script"));
            Assert.That(policy, Does.Not.Contain("<iframe"));
        }
    }
}
