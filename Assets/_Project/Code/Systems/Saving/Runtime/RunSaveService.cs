using System;
using System.IO;
using UnityEngine;

namespace Project.Saving
{
    public static class RunSaveService
    {
        public const int CurrentSchemaVersion = 1;
        private const string SaveDirectoryName = "Saves";
        private const string RunFileName = "run.json";

        public static string SaveDirectoryOverrideForTests
        {
            get;
            set;
        }

        public static string SaveDirectory =>
            !string.IsNullOrWhiteSpace(
                SaveDirectoryOverrideForTests)
                ? SaveDirectoryOverrideForTests
                : Path.Combine(
                Application.persistentDataPath,
                SaveDirectoryName);
        public static string RunFilePath =>
            Path.Combine(SaveDirectory, RunFileName);

        public static bool HasActiveRun =>
            File.Exists(RunFilePath);

        public static RunSaveData CreateNew(string scenePath)
        {
            string now = DateTime.UtcNow.ToString("o");
            return new RunSaveData
            {
                schemaVersion = CurrentSchemaVersion,
                runId = Guid.NewGuid().ToString("N"),
                createdAtUtc = now,
                updatedAtUtc = now,
                scenePath = scenePath ?? string.Empty,
                respawnAnchorSaveJson = "{}"
            };
        }

        public static bool TryLoad(
            out RunSaveData data,
            out string error)
        {
            data = null;
            error = string.Empty;
            if (!File.Exists(RunFilePath))
            {
                error = "没有当前运行存档。";
                return false;
            }

            try
            {
                string json = File.ReadAllText(RunFilePath);
                data = JsonUtility.FromJson<RunSaveData>(json);
                if (data == null)
                {
                    error = "运行存档无法解析。";
                    return false;
                }
                if (data.schemaVersion != CurrentSchemaVersion)
                {
                    error =
                        $"不支持的运行存档版本：{data.schemaVersion}。";
                    data = null;
                    return false;
                }

                data.collectedCollectibleIds ??=
                    new System.Collections.Generic.List<string>();
                data.consumedColorKeyIds ??=
                    new System.Collections.Generic.List<string>();
                data.unlockedColorIds ??=
                    new System.Collections.Generic.List<string>();
                data.unlockedAbilityIds ??=
                    new System.Collections.Generic.List<string>();
                if (string.IsNullOrWhiteSpace(
                        data.respawnAnchorSaveJson))
                {
                    data.respawnAnchorSaveJson = "{}";
                }
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                data = null;
                return false;
            }
        }

        public static bool Save(RunSaveData data)
        {
            if (data == null ||
                string.IsNullOrWhiteSpace(data.runId))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(SaveDirectory);
                data.schemaVersion = CurrentSchemaVersion;
                data.updatedAtUtc =
                    DateTime.UtcNow.ToString("o");
                string json = JsonUtility.ToJson(data, true);
                string tempPath = RunFilePath + ".tmp";
                File.WriteAllText(tempPath, json);
                if (File.Exists(RunFilePath))
                {
                    File.Copy(
                        RunFilePath,
                        RunFilePath + ".bak",
                        true);
                }
                File.Copy(tempPath, RunFilePath, true);
                File.Delete(tempPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[RunSaveService] 写入运行存档失败。\n" +
                    exception);
                return false;
            }
        }

        public static void Delete()
        {
            DeleteIfExists(RunFilePath);
            DeleteIfExists(RunFilePath + ".tmp");
            DeleteIfExists(RunFilePath + ".bak");
        }

        public static void QuarantineCorrupt()
        {
            if (!File.Exists(RunFilePath))
            {
                return;
            }

            try
            {
                string badPath = RunFilePath +
                    ".invalid-" +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                    ".json";
                File.Move(RunFilePath, badPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RunSaveService] 无法隔离损坏存档。\n" +
                    exception);
            }
        }

        private static void DeleteIfExists(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[RunSaveService] 删除运行存档失败。\n" +
                    exception);
            }
        }
    }
}
