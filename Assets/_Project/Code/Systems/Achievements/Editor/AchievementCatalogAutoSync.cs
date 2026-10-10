using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Achievements;

namespace Project.Achievements.Editor
{
    [InitializeOnLoad]
    public static class AchievementCatalogAutoSync
    {
        private const string ContentFolder =
            "Assets/_Project/Content/Data/Global/Achievements";
        private const string CatalogPath =
            "Assets/_Project/Code/Systems/Achievements/Runtime/Resources/AchievementCatalog.asset";

        private static bool syncQueued;

        static AchievementCatalogAutoSync()
        {
            EditorApplication.delayCall += SyncNow;
            EditorApplication.playModeStateChanged +=
                OnPlayModeStateChanged;
        }

        [MenuItem("Tools/2026TapTap/Achievements/同步固定目录")]
        public static void SyncNow()
        {
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                ScheduleSync();
                return;
            }

            if (!AssetDatabase.IsValidFolder(ContentFolder))
            {
                Debug.LogWarning(
                    "[AchievementCatalogAutoSync] 固定成就目录不存在：" +
                    ContentFolder);
                return;
            }

            List<AchievementSO> achievements = LoadAll();
            string version = ComputeVersion(achievements);
            AchievementCatalogSO catalog =
                AssetDatabase.LoadAssetAtPath<AchievementCatalogSO>(
                    CatalogPath);
            if (catalog == null)
            {
                EnsureFolder(
                    System.IO.Path.GetDirectoryName(CatalogPath)
                        ?.Replace('\\', '/'));
                catalog = ScriptableObject.CreateInstance<
                    AchievementCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            if (HasSameCatalog(catalog, achievements, version))
            {
                return;
            }

            Undo.RecordObject(catalog, "同步成就固定目录");
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty list =
                serialized.FindProperty("achievements");
            list.arraySize = achievements.Count;
            for (int index = 0; index < achievements.Count; index++)
            {
                list.GetArrayElementAtIndex(index)
                    .objectReferenceValue = achievements[index];
            }

            serialized.FindProperty("catalogVersion")
                .stringValue = version;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "[AchievementCatalogAutoSync] 已同步成就目录：" +
                achievements.Count +
                " 条");
        }

        public static void ScheduleSync()
        {
            if (syncQueued)
            {
                return;
            }

            syncQueued = true;
            EditorApplication.delayCall += () =>
            {
                syncQueued = false;
                SyncNow();
            };
        }

        private static void OnPlayModeStateChanged(
            PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                SyncNow();
            }
        }

        private static List<AchievementSO> LoadAll()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:AchievementSO",
                new[] { ContentFolder });
            List<AchievementSO> result =
                new List<AchievementSO>();
            for (int index = 0; index < guids.Length; index++)
            {
                AchievementSO achievement =
                    AssetDatabase.LoadAssetAtPath<AchievementSO>(
                        AssetDatabase.GUIDToAssetPath(guids[index]));
                if (achievement != null)
                {
                    result.Add(achievement);
                }
            }

            result.Sort((left, right) =>
                left.AchievementId.CompareTo(right.AchievementId));
            return result;
        }

        private static bool HasSameCatalog(
            AchievementCatalogSO catalog,
            List<AchievementSO> achievements,
            string version)
        {
            if (catalog.CatalogVersion != version ||
                catalog.Achievements.Count != achievements.Count)
            {
                return false;
            }

            for (int index = 0; index < achievements.Count; index++)
            {
                if (catalog.Achievements[index] != achievements[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeVersion(
            List<AchievementSO> achievements)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < achievements.Count; index++)
            {
                AchievementSO achievement = achievements[index];
                builder.Append(achievement.AchievementId)
                    .Append('|')
                    .Append(achievement.DisplayName)
                    .Append('|')
                    .Append(achievement.RootNodeId)
                    .Append('|');
                for (int conditionIndex = 0;
                     conditionIndex < achievement.Conditions.Count;
                     conditionIndex++)
                {
                    AchievementConditionDefinition condition =
                        achievement.Conditions[conditionIndex];
                    builder.Append(condition.ConditionId)
                        .Append(':')
                        .Append((int)condition.Mode)
                        .Append(':')
                        .Append(condition.TargetCount)
                        .Append(':')
                        .Append(condition.TargetProgress)
                        .Append(';');
                }

                builder.Append('\n');
            }

            return Hash128.Compute(builder.ToString()).ToString();
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }
    }

    internal sealed class AchievementCatalogAssetPostprocessor :
        AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsContentAsset(importedAssets) ||
                ContainsContentAsset(deletedAssets) ||
                ContainsContentAsset(movedAssets) ||
                ContainsContentAsset(movedFromAssetPaths))
            {
                AchievementCatalogAutoSync.ScheduleSync();
            }
        }

        private static bool ContainsContentAsset(
            string[] paths)
        {
            if (paths == null)
            {
                return false;
            }

            for (int index = 0; index < paths.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(paths[index]) &&
                    paths[index].Replace('\\', '/')
                        .StartsWith(
                            "Assets/_Project/Content/Data/Global/Achievements",
                            StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
