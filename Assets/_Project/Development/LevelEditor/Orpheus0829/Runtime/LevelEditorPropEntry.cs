using System;
using UnityEngine;

namespace Project.LevelEditor
{
    [Serializable]
    public sealed class LevelEditorPropEntry
    {
        [SerializeField] private string entryId;
        [SerializeField] private string displayName = "新道具";
        [SerializeField] private GameObject prefab;

        public string EntryId => entryId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;

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
    }
}
