using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.LevelEditor
{
    [Serializable]
    public sealed class LevelEditorPropEntry
    {
        [SerializeField] private string entryId;
        [SerializeField] private string displayName = "新道具";
        [SerializeField] private GameObject prefab;
        [SerializeField]
        private List<LevelEditorComponentValueOverride>
            componentValueOverrides =
                new List<LevelEditorComponentValueOverride>();

        public string EntryId => entryId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public IReadOnlyList<LevelEditorComponentValueOverride>
            ComponentValueOverrides => componentValueOverrides;

        public void EnsureEntryId()
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                entryId = Guid.NewGuid().ToString("N");
            }
        }

        public void Configure(
            string valueDisplayName,
            GameObject valuePrefab)
        {
            EnsureEntryId();
            displayName = string.IsNullOrWhiteSpace(valueDisplayName)
                ? "新道具"
                : valueDisplayName;
            prefab = valuePrefab;
        }

        public void ReplaceComponentValueOverrides(
            IReadOnlyList<LevelEditorComponentValueOverride> values)
        {
            componentValueOverrides.Clear();
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] != null)
                {
                    componentValueOverrides.Add(values[index].Clone());
                }
            }
        }

        public void ClearComponentValueOverrides()
        {
            componentValueOverrides.Clear();
        }
    }
}
