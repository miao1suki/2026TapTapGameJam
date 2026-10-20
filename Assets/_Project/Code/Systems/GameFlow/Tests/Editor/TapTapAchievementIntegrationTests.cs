using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Project.Achievements;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class TapTapAchievementIntegrationTests
    {
        [Test]
        public void SettingsAssetUsesVerifiedBackendAndLocalSecret()
        {
            TapTapAchievementSettings settings =
                Resources.Load<TapTapAchievementSettings>(
                    "TapTap/TapTapAchievementSettings");
            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.Enabled, Is.True);
            Assert.That(settings.ClientId,
                Is.EqualTo("hsfsh06prdz6broh95"));
            Assert.That(settings.HasCredentials, Is.True);
            Assert.That(settings.BackendAchievementIdsConfirmed, Is.True);
            Assert.That(settings.CanInitialize, Is.True);
            Assert.That(settings.CanReport, Is.True);
            Assert.That(settings.ClientToken, Is.Empty);
        }

        [Test]
        public void LocalClientTokenPathIsIgnored()
        {
            string ignore = File.ReadAllText(".gitignore");
            Assert.That(
                ignore,
                Does.Contain(
                    "TapTapAchievementSecrets.json"));
            Assert.That(
                ignore,
                Does.Contain(
                    "TapTapAchievementSecrets.json.meta"));
        }

        [Test]
        public void AndroidApplicationIdMatchesTapTapPackage()
        {
            Assert.That(
                PlayerSettings.GetApplicationIdentifier(
                    NamedBuildTarget.Android),
                Is.EqualTo(
                    "com.miao1suki.shapeofcolor"));
        }

        [Test]
        public void EveryAchievementHasAProvisionalPlatformId()
        {
            AchievementCatalogSO catalog =
                Resources.Load<AchievementCatalogSO>(
                    "AchievementCatalog");
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Achievements.Count, Is.EqualTo(14));

            var seen = new HashSet<string>();
            for (int index = 0;
                 index < catalog.Achievements.Count;
                 index++)
            {
                AchievementSO achievement =
                    catalog.Achievements[index];
                Assert.That(achievement, Is.Not.Null);
                Assert.That(
                    achievement.PlatformAchievementId,
                    Is.Not.Empty,
                    achievement.name);
                Assert.That(
                    seen.Add(
                        achievement.PlatformAchievementId),
                    Is.True,
                    achievement.PlatformAchievementId);
            }
        }

        [Test]
        public void PackageManifestPinsOnlyPublishedTapTapVersion()
        {
            string manifest =
                File.ReadAllText("Packages/manifest.json");
            foreach (string package in new[]
                     {
                         "com.taptap.sdk.core",
                         "com.taptap.sdk.login",
                         "com.taptap.sdk.achievement"
                     })
            {
                Assert.That(
                    manifest,
                    Does.Contain(
                        $"\"{package}\": \"4.10.9\""));
            }

            Assert.That(
                manifest,
                Does.Contain(
                    "\"com.google.external-dependency-manager\": \"1.2.179\""));
            Assert.That(
                manifest,
                Does.Contain(
                    "\"com.unity.nuget.newtonsoft-json\": \"3.2.1\""));
        }
    }
}
