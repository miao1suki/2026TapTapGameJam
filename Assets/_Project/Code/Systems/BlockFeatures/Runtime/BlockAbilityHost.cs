using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.BlockFeatures
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockRuntime))]
    public sealed class BlockAbilityHost : MonoBehaviour
    {
        [SerializeField] private bool debugLog;

        private BlockRuntime runtime;
        private ColorBlock colorBlock;
        private bool applying;

        public string CurrentColorId => ResolveColorId();

        public void CollectFeatureStates(
            List<BlockFeature> enabledFeatures,
            List<BlockFeature> disabledFeatures)
        {
            enabledFeatures?.Clear();
            disabledFeatures?.Clear();
            IReadOnlyList<BlockFeature> features =
                ProjectDiscovery.GetComponents<BlockFeature>(
                    gameObject);
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                if (feature == null)
                {
                    continue;
                }

                if (feature.enabled)
                {
                    enabledFeatures?.Add(feature);
                }
                else
                {
                    disabledFeatures?.Add(feature);
                }
            }
        }

        private void Awake()
        {
            runtime = GetComponent<BlockRuntime>();
            colorBlock = GetComponent<ColorBlock>();
        }

        private void OnEnable()
        {
            EventMgr.OnColorBlockChanged += OnColorBlockChanged;
            EnsureAllFeatures();
            ApplyCurrentProfile();
        }

        private void OnDisable()
        {
            EventMgr.OnColorBlockChanged -= OnColorBlockChanged;
        }

        public void ApplyCurrentProfile()
        {
            if (applying || runtime == null)
            {
                return;
            }

            BlockAbilityCatalog catalog =
                BlockAbilityCatalogService.Load();
            if (catalog == null)
            {
                return;
            }

            applying = true;
            try
            {
                string colorId = ResolveColorId();
                var activeTypes = new HashSet<Type>();
                for (int index = 0;
                     index < catalog.Features.Count;
                     index++)
                {
                    BlockAbilityDefinition definition =
                        catalog.Features[index];
                    Type type = ResolveFeatureType(
                        definition != null
                            ? definition.featureTypeName
                            : string.Empty);
                    if (type == null ||
                        !MatchesProfile(definition, colorId))
                    {
                        continue;
                    }

                    activeTypes.Add(type);
                }

                IReadOnlyList<BlockFeature> features =
                    ProjectDiscovery.GetComponents<BlockFeature>(
                        gameObject);
                var activeFeatures = new List<BlockFeature>();
                for (int index = 0; index < features.Count; index++)
                {
                    BlockFeature feature = features[index];
                    if (feature == null)
                    {
                        continue;
                    }

                    BlockAbilityDefinition definition =
                        catalog.Find(feature.GetType().FullName);
                    if (definition == null)
                    {
                        continue;
                    }

                    bool shouldEnable =
                        activeTypes.Contains(feature.GetType());
                    feature.enabled = shouldEnable;
                    if (shouldEnable)
                    {
                        activeFeatures.Add(feature);
                    }
                }

                if (!ValidateProfile(
                        activeFeatures,
                        out string validationMessage))
                {
                    Debug.LogError(
                        $"[BlockAbilityHost] {validationMessage}",
                        this);
                    return;
                }

                runtime.RefreshFeatureSet(true);
                if (debugLog)
                {
                    Debug.Log(
                        $"[BlockAbilityHost] {name} -> {colorId} " +
                        $"({activeTypes.Count} features)",
                        this);
                }
            }
            finally
            {
                applying = false;
            }
        }

        private void EnsureAllFeatures()
        {
            BlockAbilityCatalog catalog =
                BlockAbilityCatalogService.Load();
            if (catalog == null)
            {
                return;
            }

            for (int index = 0; index < catalog.Features.Count; index++)
            {
                BlockAbilityDefinition definition =
                    catalog.Features[index];
                Type type = ResolveFeatureType(
                    definition != null
                        ? definition.featureTypeName
                        : string.Empty);
                if (type == null ||
                    !typeof(BlockFeature).IsAssignableFrom(type) ||
                    GetComponent(type) != null)
                {
                    continue;
                }

                BlockFeature feature =
                    (BlockFeature)gameObject.AddComponent(type);
                if (feature != null)
                {
                    feature.enabled = false;
                }
            }
        }

        private bool MatchesProfile(
            BlockAbilityDefinition definition,
            string colorId)
        {
            if (definition == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(
                       definition.defaultColorId) &&
                   string.Equals(
                       definition.defaultColorId,
                       colorId,
                       StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveColorId()
        {
            if (colorBlock == null)
            {
                colorBlock = GetComponent<ColorBlock>();
            }

            if (colorBlock != null)
            {
                if (!string.IsNullOrEmpty(
                        colorBlock.CurrentColorTypeId))
                {
                    return colorBlock.CurrentColorTypeId;
                }

                return string.Empty;
            }

            return string.Empty;
        }

        private void OnColorBlockChanged(
            ColorBlock block,
            string previous,
            string current)
        {
            if (block != colorBlock)
            {
                return;
            }

            ApplyCurrentProfile();
        }

        private static Type ResolveFeatureType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            Type type = typeof(BlockFeature).Assembly.GetType(
                typeName);
            return type ?? Type.GetType(typeName);
        }

        private static bool ValidateProfile(
            List<BlockFeature> features,
            out string message)
        {
            var counts = new Dictionary<Type, int>();
            for (int index = 0; index < features.Count; index++)
            {
                BlockFeature feature = features[index];
                Type type = feature.GetType();
                BlockFeatureMetadata metadata =
                    BlockFeatureMetadataCache.Get(type);
                counts.TryGetValue(type, out int count);
                counts[type] = count + 1;
                if (counts[type] > metadata.MaxPerBlock)
                {
                    message =
                        $"{metadata.DisplayName} 超过允许数量。";
                    return false;
                }

                for (int requirementIndex = 0;
                     requirementIndex < metadata.Requires.Count;
                     requirementIndex++)
                {
                    Type required =
                        metadata.Requires[requirementIndex];
                    if (!HasCapability(features, required))
                    {
                        message =
                            $"{metadata.DisplayName} 缺少依赖 " +
                            $"{required.Name}。";
                        return false;
                    }
                }

                for (int conflictIndex = 0;
                     conflictIndex < metadata.Conflicts.Count;
                     conflictIndex++)
                {
                    Type conflict =
                        metadata.Conflicts[conflictIndex];
                    if (HasCapability(features, conflict))
                    {
                        message =
                            $"{metadata.DisplayName} 与 " +
                            $"{conflict.Name} 冲突。";
                        return false;
                    }
                }
            }

            message = string.Empty;
            return true;
        }

        private static bool HasCapability(
            List<BlockFeature> features,
            Type capability)
        {
            if (capability == null)
            {
                return false;
            }

            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] != null &&
                    capability.IsInstanceOfType(features[index]))
                {
                    return true;
                }
            }

            return false;
        }

        public static BlockAbilityHost EnsureOn(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            BlockAbilityHost host =
                target.GetComponent<BlockAbilityHost>();
            if (host != null)
            {
                return host;
            }

#if UNITY_EDITOR
            return UnityEditor.Undo.AddComponent<BlockAbilityHost>(
                target);
#else
            return target.AddComponent<BlockAbilityHost>();
#endif
        }
    }
}
