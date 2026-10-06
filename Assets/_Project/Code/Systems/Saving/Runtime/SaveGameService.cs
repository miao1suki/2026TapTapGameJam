using System;
using System.Collections.Generic;
using System.IO;
using Project.Settings;
using UnityEngine;

namespace Project.Saving
{
    public static class SaveGameService
    {
        private const string SaveDirectoryName = "Saves";
        private const string SaveExtension = ".json";
        private static readonly List<SaveGameData> previewSlots =
            new List<SaveGameData>();

        public static string SaveDirectory =>
            Path.Combine(Application.persistentDataPath, SaveDirectoryName);

        public static IReadOnlyList<SaveSlotInfo> GetSlots()
        {
            List<SaveSlotInfo> slots = new List<SaveSlotInfo>();
            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                for (int index = 0; index < previewSlots.Count; index++)
                {
                    slots.Add(ToSlotInfo(previewSlots[index]));
                }

                SortSlots(slots);
                return slots;
            }

            Directory.CreateDirectory(SaveDirectory);
            string[] paths = Directory.GetFiles(
                SaveDirectory,
                "*" + SaveExtension,
                SearchOption.TopDirectoryOnly);
            for (int index = 0; index < paths.Length; index++)
            {
                SaveGameData data = LoadFromPath(paths[index]);
                if (data == null)
                {
                    continue;
                }

                slots.Add(ToSlotInfo(data));
            }

            SortSlots(slots);
            return slots;
        }

        public static SaveSlotInfo GetMostRecentSlot()
        {
            IReadOnlyList<SaveSlotInfo> slots = GetSlots();
            return slots.Count > 0 ? slots[0] : null;
        }

        public static SaveGameData CreateEmptySlot()
        {
            string now = DateTime.UtcNow.ToString("o");
            SaveGameData data = new SaveGameData
            {
                schemaVersion = 1,
                slotId = Guid.NewGuid().ToString("N"),
                displayName = "未命名存档",
                savedAtUtc = now,
                levelLabel = "未开始",
                customDataJson = "{}",
            };
            Save(data);
            return data;
        }

        public static SaveGameData Load(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return null;
            }

            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                for (int index = 0; index < previewSlots.Count; index++)
                {
                    if (previewSlots[index].slotId == slotId)
                    {
                        return Clone(previewSlots[index]);
                    }
                }

                return null;
            }

            string path = GetSlotPath(slotId);
            return File.Exists(path) ? LoadFromPath(path) : null;
        }

        public static bool Save(SaveGameData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.slotId))
            {
                return false;
            }

            Directory.CreateDirectory(SaveDirectory);
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            data.displayName = BuildDisplayName(
                data.levelLabel,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                UpsertPreview(data);
                return true;
            }

            File.WriteAllText(
                GetSlotPath(data.slotId),
                JsonUtility.ToJson(data, true));
            return true;
        }

        public static bool Delete(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                return false;
            }

            if (RuntimeSettingsPolicy.IsPreviewOnly)
            {
                for (int index = previewSlots.Count - 1; index >= 0; index--)
                {
                    if (previewSlots[index].slotId == slotId)
                    {
                        previewSlots.RemoveAt(index);
                        return true;
                    }
                }

                return false;
            }

            string path = GetSlotPath(slotId);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        private static SaveGameData LoadFromPath(string path)
        {
            try
            {
                return JsonUtility.FromJson<SaveGameData>(
                    File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        private static string GetSlotPath(string slotId)
        {
            return Path.Combine(
                SaveDirectory,
                slotId + SaveExtension);
        }

        private static string BuildDisplayName(
            string chapterLabel,
            string timestamp)
        {
            string chapter = string.IsNullOrWhiteSpace(chapterLabel)
                ? "未开始"
                : chapterLabel.Trim();
            return $"{chapter} {timestamp}";
        }

        private static string FormatLocalTimestamp(string utcValue)
        {
            if (!DateTime.TryParse(
                    utcValue,
                    null,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTime parsed))
            {
                return "未记录时间";
            }

            return parsed
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static SaveSlotInfo ToSlotInfo(SaveGameData data)
        {
            string timestamp = FormatLocalTimestamp(data.savedAtUtc);
            return new SaveSlotInfo
            {
                slotId = data.slotId,
                displayName = BuildDisplayName(
                    data.levelLabel,
                    timestamp),
                displayTimestamp = timestamp,
                levelLabel = data.levelLabel,
                hasData = true,
            };
        }

        private static void SortSlots(List<SaveSlotInfo> slots)
        {
            slots.Sort((left, right) =>
                string.CompareOrdinal(
                    right.displayTimestamp,
                    left.displayTimestamp));
        }

        private static void UpsertPreview(SaveGameData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.slotId))
            {
                return;
            }

            for (int index = 0; index < previewSlots.Count; index++)
            {
                if (previewSlots[index].slotId == data.slotId)
                {
                    previewSlots[index] = Clone(data);
                    return;
                }
            }

            previewSlots.Add(Clone(data));
        }

        private static SaveGameData Clone(SaveGameData data)
        {
            return data == null
                ? null
                : new SaveGameData
                {
                    schemaVersion = data.schemaVersion,
                    slotId = data.slotId,
                    displayName = data.displayName,
                    savedAtUtc = data.savedAtUtc,
                    levelLabel = data.levelLabel,
                    customDataJson = data.customDataJson,
                };
        }
    }
}
