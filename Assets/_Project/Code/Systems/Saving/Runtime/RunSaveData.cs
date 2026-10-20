using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Saving
{
    [Serializable]
    public sealed class RunSaveData
    {
        public int schemaVersion = 1;
        public string runId = string.Empty;
        public string createdAtUtc = string.Empty;
        public string updatedAtUtc = string.Empty;
        public string scenePath = string.Empty;
        public string planningRevision = string.Empty;
        public string currentRespawnAnchorId = string.Empty;
        public Vector3 initialPlayerPosition;
        public bool hasFallbackPlayerPosition;
        public Vector3 fallbackPlayerPosition;
        public string respawnAnchorSaveJson = "{}";
        public List<string> collectedCollectibleIds =
            new List<string>();
        public List<string> consumedColorKeyIds =
            new List<string>();
        public List<string> unlockedColorIds =
            new List<string>();
        public List<string> unlockedAbilityIds =
            new List<string>();
        public bool bossEntered;
        public bool bossFightActive;
        public bool bossDefeated;
        public bool trueEndingCompleted;
    }
}
