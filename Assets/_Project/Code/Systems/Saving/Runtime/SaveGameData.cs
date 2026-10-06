using System;

namespace Project.Saving
{
    [Serializable]
    public sealed class SaveGameData
    {
        public int schemaVersion = 1;
        public string slotId = string.Empty;
        public string displayName = "未命名存档";
        public string savedAtUtc = string.Empty;
        public string levelLabel = string.Empty;
        public string customDataJson = "{}";
    }

    [Serializable]
    public sealed class SaveSlotInfo
    {
        public string slotId = string.Empty;
        public string displayName = "未命名存档";
        public string displayTimestamp = "未记录时间";
        public string levelLabel = string.Empty;
        public bool hasData;
    }
}
