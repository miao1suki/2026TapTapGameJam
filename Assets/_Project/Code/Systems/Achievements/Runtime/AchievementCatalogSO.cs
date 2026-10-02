using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Project.Achievements
{
    [CreateAssetMenu(
        menuName = "2026TapTap/Achievements/Achievement Catalog",
        fileName = "AchievementCatalog")]
    public sealed class AchievementCatalogSO : ScriptableObject
    {
        [SerializeField]
        private List<AchievementSO> achievements =
            new List<AchievementSO>();

        [SerializeField]
        private string catalogVersion = string.Empty;

        [SerializeField]
        private string saveDirectoryOverride = string.Empty;

        [SerializeField]
        private string saveFileName = "achievements.json";

        public IReadOnlyList<AchievementSO> Achievements =>
            achievements;
        public string CatalogVersion => catalogVersion;
        public string SaveDirectoryOverride =>
            saveDirectoryOverride;
        public string SaveFileName => saveFileName;

        public string ResolveSaveFilePath()
        {
            string directory = string.IsNullOrWhiteSpace(
                    saveDirectoryOverride)
                ? Path.Combine(
                    Application.persistentDataPath,
                    "Achievements")
                : Path.IsPathRooted(saveDirectoryOverride)
                    ? saveDirectoryOverride
                    : Path.Combine(
                        Application.persistentDataPath,
                        saveDirectoryOverride);
            string fileName = string.IsNullOrWhiteSpace(
                    saveFileName)
                ? "achievements.json"
                : Path.GetFileName(saveFileName);
            return Path.Combine(directory, fileName);
        }
    }
}
