using System.IO;
using NUnit.Framework;
using Project.StartMenu;
using Project.Saving;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

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

        [Test]
        public void DeleteCurrentSaveButtonUsesExistingArtworkAndRaisesRequest()
        {
            Assert.IsTrue(
                RunSaveService.Save(
                    RunSaveService.CreateNew(
                        "Assets/Test/Gaming.unity")));
            GameObject root = new GameObject(
                "Save Panel",
                typeof(RectTransform));
            root.SetActive(false);
            var rootRect =
                (RectTransform)root.transform;
            rootRect.sizeDelta = new Vector2(830f, 360f);
            SavePanelController panel =
                root.AddComponent<SavePanelController>();
            TMP_FontAsset font =
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset");
            Assert.IsNotNull(font);
            var serialized = new SerializedObject(panel);
            serialized.FindProperty("uiSdfFont")
                .objectReferenceValue = font;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            panel.BuildStructure();
            panel.ConfigureCurrentRunDeletion(true);
            root.SetActive(true);
            try
            {
                var buttonRect =
                    (RectTransform)root.transform.Find(
                        "Delete Current Save");
                Assert.IsNotNull(buttonRect);
                Assert.IsTrue(buttonRect.gameObject.activeSelf);
                Button button =
                    buttonRect.GetComponent<Button>();
                Image background =
                    button.targetGraphic as Image;
                Assert.AreSame(
                    GameUiArtwork.Load()?.settingButton,
                    background?.sprite);
                TextMeshProUGUI label =
                    buttonRect.GetComponentInChildren<
                        TextMeshProUGUI>(true);
                Assert.AreEqual("删除当前存档", label.text);
                Assert.AreSame(font, label.font);

                int requests = 0;
                System.Func<bool> request = () =>
                {
                    requests++;
                    return true;
                };
                SavePanelController.DeleteCurrentSaveRequested +=
                    request;
                try
                {
                    button.onClick.Invoke();
                }
                finally
                {
                    SavePanelController.DeleteCurrentSaveRequested -=
                        request;
                }

                Assert.AreEqual(1, requests);
                Assert.IsFalse(button.interactable);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
