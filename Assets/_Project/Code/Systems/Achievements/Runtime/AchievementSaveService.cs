using System;
using System.IO;
using UnityEngine;

namespace Project.Achievements
{
    public sealed class AchievementSaveService
    {
        public string FilePath { get; }

        public AchievementSaveService(
            AchievementCatalogSO catalog)
        {
            FilePath = catalog != null
                ? catalog.ResolveSaveFilePath()
                : Path.Combine(
                    Application.persistentDataPath,
                    "Achievements",
                    "achievements.json");
        }

        public AchievementSaveFile Load()
        {
            if (!File.Exists(FilePath))
            {
                return new AchievementSaveFile();
            }

            try
            {
                string json = File.ReadAllText(FilePath);
                return string.IsNullOrWhiteSpace(json)
                    ? new AchievementSaveFile()
                    : JsonUtility.FromJson<AchievementSaveFile>(
                          json) ??
                      new AchievementSaveFile();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AchievementSaveService] 读取失败，将重建成就存档。\n" +
                    exception);
                return new AchievementSaveFile();
            }
        }

        public void Save(
            AchievementSaveFile saveFile)
        {
            if (saveFile == null)
            {
                return;
            }

            try
            {
                string directory =
                    Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonUtility.ToJson(saveFile, true);
                string tempPath = FilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(FilePath))
                {
                    File.Copy(
                        FilePath,
                        FilePath + ".bak",
                        true);
                }

                File.Copy(tempPath, FilePath, true);
                File.Delete(tempPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[AchievementSaveService] 写入失败。\n" +
                    exception);
            }
        }

        public void Clear()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            if (File.Exists(FilePath + ".bak"))
            {
                File.Delete(FilePath + ".bak");
            }
        }
    }
}
