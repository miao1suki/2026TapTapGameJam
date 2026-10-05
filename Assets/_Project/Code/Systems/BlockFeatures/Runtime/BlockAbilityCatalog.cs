using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    [Serializable]
    public sealed class BlockAbilityDefinition
    {
        public string featureTypeName;
        public string displayName;
        public string defaultColorId = string.Empty;
    }

    [CreateAssetMenu(
        menuName = "2026TapTap/方块功能/能力目录",
        fileName = "BlockAbilityCatalog")]
    public sealed class BlockAbilityCatalog : ScriptableObject
    {
        [SerializeField]
        private List<BlockAbilityDefinition> features =
            new List<BlockAbilityDefinition>();

        public IReadOnlyList<BlockAbilityDefinition> Features =>
            features;

        public BlockAbilityDefinition Find(string featureTypeName)
        {
            if (string.IsNullOrEmpty(featureTypeName))
            {
                return null;
            }

            for (int index = 0; index < features.Count; index++)
            {
                BlockAbilityDefinition definition = features[index];
                if (definition != null &&
                    definition.featureTypeName == featureTypeName)
                {
                    return definition;
                }
            }

            return null;
        }

        public void ReplaceFeatures(
            List<BlockAbilityDefinition> definitions)
        {
            features.Clear();
            if (definitions == null)
            {
                return;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                {
                    features.Add(definitions[index]);
                }
            }
        }
    }

    public static class BlockAbilityCatalogService
    {
        private const string CatalogResourcesPath =
            "BlockFeatures/BlockAbilityCatalog";
        private static BlockAbilityCatalog cached;

        public static BlockAbilityCatalog Load()
        {
            if (cached != null)
            {
                return cached;
            }

            cached = Resources.Load<BlockAbilityCatalog>(
                CatalogResourcesPath);
            return cached;
        }

        public static void ClearCache()
        {
            cached = null;
        }
    }
}
