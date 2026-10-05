using System;
using System.Collections.Generic;
using Project;
using UnityEditor;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    internal static class BlockAbilityCatalogBuilder
    {
        private const string Folder =
            "Assets/_Project/Resources/BlockFeatures";
        private const string CatalogPath =
            Folder + "/BlockAbilityCatalog.asset";

        [InitializeOnLoadMethod]
        private static void QueueSync()
        {
            EditorApplication.delayCall += Sync;
        }

        internal static void Sync()
        {
            EnsureFolder();
            BlockAbilityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<BlockAbilityCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    BlockAbilityCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            List<BlockAbilityDefinition> definitions =
                BuildDefinitions();
            string before = JsonUtility.ToJson(catalog, false);
            catalog.ReplaceFeatures(definitions);
            string after = JsonUtility.ToJson(catalog, false);
            if (before == after)
            {
                return;
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            BlockAbilityCatalogService.ClearCache();
            Debug.Log(
                $"[BlockFeatures] 已同步能力目录：" +
                $"{definitions.Count} 个功能组件。",
                catalog);
        }

        private static List<BlockAbilityDefinition> BuildDefinitions()
        {
            var result = new List<BlockAbilityDefinition>();
            foreach (Type type in ProjectDiscovery
                         .FindImplementations<BlockFeature>())
            {
                BlockFeatureMetadata metadata =
                    BlockFeatureMetadataCache.Get(type);
                result.Add(new BlockAbilityDefinition
                {
                    featureTypeName = type.FullName,
                    displayName = metadata.DisplayName,
                    defaultColorId = metadata.DefaultColorId
                });
            }

            return result;
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(Folder)
                ?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(Folder);
            if (!string.IsNullOrEmpty(parent) &&
                !AssetDatabase.IsValidFolder(parent))
            {
                AssetDatabase.CreateFolder(
                    System.IO.Path.GetDirectoryName(parent)
                        ?.Replace('\\', '/'),
                    System.IO.Path.GetFileName(parent));
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
