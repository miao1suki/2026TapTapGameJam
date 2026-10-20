using System.IO;
using NUnit.Framework;
using Project.Saving;
using UnityEngine;

namespace Project.GameFlow.Tests
{
    public sealed class RunSaveServiceTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(
                Path.GetTempPath(),
                "2026TapTap-RunSave-" +
                System.Guid.NewGuid().ToString("N"));
            RunSaveService.SaveDirectoryOverrideForTests =
                directory;
        }

        [TearDown]
        public void TearDown()
        {
            RunSaveService.SaveDirectoryOverrideForTests =
                null;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void RunSaveRoundTripKeepsProgressAndBackup()
        {
            RunSaveData data =
                RunSaveService.CreateNew(
                    "Assets/Test/Gaming.unity");
            data.currentRespawnAnchorId = "anchor-2";
            data.initialPlayerPosition =
                new Vector3(1f, 2f, 0f);
            data.fallbackPlayerPosition =
                new Vector3(3f, 4f, 0f);
            data.hasFallbackPlayerPosition = true;
            data.collectedCollectibleIds.Add("collectible-a");
            data.consumedColorKeyIds.Add("key-red-1");
            data.unlockedColorIds.Add("red");
            data.unlockedAbilityIds.Add("red");
            Assert.IsTrue(RunSaveService.Save(data));
            Assert.IsTrue(RunSaveService.Save(data));
            Assert.IsTrue(
                File.Exists(
                    RunSaveService.RunFilePath +
                    ".bak"));

            Assert.IsTrue(
                RunSaveService.TryLoad(
                    out RunSaveData loaded,
                    out string error),
                error);
            Assert.AreEqual(
                "anchor-2",
                loaded.currentRespawnAnchorId);
            Assert.AreEqual(
                new Vector3(1f, 2f, 0f),
                loaded.initialPlayerPosition);
            CollectionAssert.Contains(
                loaded.collectedCollectibleIds,
                "collectible-a");
            CollectionAssert.Contains(
                loaded.consumedColorKeyIds,
                "key-red-1");
            CollectionAssert.Contains(
                loaded.unlockedAbilityIds,
                "red");
        }

        [Test]
        public void CorruptRunSaveIsQuarantined()
        {
            Directory.CreateDirectory(
                RunSaveService.SaveDirectory);
            File.WriteAllText(
                RunSaveService.RunFilePath,
                "{ broken json");

            Assert.IsFalse(
                RunSaveService.TryLoad(
                    out _,
                    out _));
            Assert.IsTrue(RunSaveService.HasActiveRun);
            RunSaveService.QuarantineCorrupt();
            Assert.IsFalse(RunSaveService.HasActiveRun);
            Assert.IsNotEmpty(
                Directory.GetFiles(
                    RunSaveService.SaveDirectory,
                    "run.json.invalid-*.json"));
        }
    }
}
