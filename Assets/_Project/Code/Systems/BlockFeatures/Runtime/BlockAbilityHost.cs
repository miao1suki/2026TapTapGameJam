using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>
    /// Thin fixed-color feature host for existing level block prefabs.
    /// It never discovers or adds features; it only enables the authored
    /// components while the object's color group is active.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockRuntime))]
    public sealed class BlockAbilityHost : MonoBehaviour
    {
        [SerializeField] private bool debugLog;

        private readonly List<BlockFeature> features =
            new List<BlockFeature>();
        private readonly List<BlockFeature> activeFeatures =
            new List<BlockFeature>();
        private BlockRuntime runtime;
        private IColorObject colorObject;
        private bool applying;

        public string BaseColorId =>
            colorObject?.BaseColorTypeId ?? string.Empty;

        public IReadOnlyList<BlockFeature> Features => features;

        public void CollectFeatureStates(
            List<BlockFeature> enabledFeatures,
            List<BlockFeature> disabledFeatures)
        {
            enabledFeatures?.Clear();
            disabledFeatures?.Clear();
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
            colorObject = GetComponent<IColorObject>();
            RefreshFeatureBuffer();
        }

        private void OnEnable()
        {
            if (colorObject == null)
            {
                colorObject = GetComponent<IColorObject>();
            }

            if (colorObject != null)
            {
                colorObject.ActiveStateChanged +=
                    OnActiveStateChanged;
            }

            EventMgr.OnRoomColorReset += OnRoomColorReset;
            RefreshActivation();
        }

        private void OnDisable()
        {
            if (colorObject != null)
            {
                colorObject.ActiveStateChanged -=
                    OnActiveStateChanged;
            }

            EventMgr.OnRoomColorReset -= OnRoomColorReset;
            DisableAllFeatures();
        }

        /// <summary>
        /// Rebuilds feature activation from the fixed color state.
        /// </summary>
        public void RefreshActivation()
        {
            if (applying || runtime == null || colorObject == null)
            {
                return;
            }

            applying = true;
            try
            {
                RefreshFeatureBuffer();
                bool active = colorObject.IsActive;
                activeFeatures.Clear();
                for (int index = 0; index < features.Count; index++)
                {
                    BlockFeature feature = features[index];
                    if (feature == null)
                    {
                        continue;
                    }

                    string requiredColor =
                        feature.Metadata.DefaultColorId;
                    bool belongsToColor =
                        string.IsNullOrWhiteSpace(requiredColor) ||
                        string.Equals(
                            requiredColor,
                            colorObject.BaseColorTypeId,
                            StringComparison.OrdinalIgnoreCase);
                    feature.enabled = active && belongsToColor;
                    if (feature.enabled)
                    {
                        activeFeatures.Add(feature);
                    }
                }

                if (!ValidateProfile(
                        activeFeatures,
                        out string validationMessage))
                {
                    Debug.LogError(
                        $"[ColorObject] {validationMessage}",
                        this);
                    DisableAllFeatures();
                    runtime.RefreshFeatureSet(true);
                    return;
                }

                runtime.RefreshFeatureSet(true);
                if (debugLog)
                {
                    Debug.Log(
                        $"[ColorObject] {name} -> " +
                        $"{(active ? colorObject.BaseColorTypeId : "失效")}",
                        this);
                }
            }
            finally
            {
                applying = false;
            }
        }

        private void RefreshFeatureBuffer()
        {
            features.Clear();
            GetComponents(features);
        }

        private void DisableAllFeatures()
        {
            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] != null)
                {
                    features[index].enabled = false;
                }
            }
        }

        private void OnActiveStateChanged(
            IColorObject source,
            bool active)
        {
            if (ReferenceEquals(source, colorObject))
            {
                RefreshActivation();
            }
        }

        private void OnRoomColorReset()
        {
            for (int index = 0; index < features.Count; index++)
            {
                if (features[index] is IRoomColorResettable resettable)
                {
                    resettable.ResetForRoom();
                }
            }

            DisableAllFeatures();
            if (runtime != null)
            {
                runtime.RefreshFeatureSet(true);
            }
        }

        private static bool ValidateProfile(
            List<BlockFeature> profileFeatures,
            out string message)
        {
            var counts = new Dictionary<Type, int>();
            for (int index = 0; index < profileFeatures.Count; index++)
            {
                BlockFeature feature = profileFeatures[index];
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
                    if (!HasCapability(profileFeatures, required))
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
                    if (HasCapability(profileFeatures, conflict))
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
            List<BlockFeature> profileFeatures,
            Type capability)
        {
            if (capability == null)
            {
                return false;
            }

            for (int index = 0;
                 index < profileFeatures.Count;
                 index++)
            {
                if (profileFeatures[index] != null &&
                    capability.IsInstanceOfType(
                        profileFeatures[index]))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
