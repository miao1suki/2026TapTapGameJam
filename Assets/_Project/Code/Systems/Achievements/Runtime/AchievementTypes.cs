using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Achievements
{
    public enum AchievementConditionMode
    {
        Trigger = 0,
        Count = 1,
        Progress = 2
    }

    public enum AchievementLogicType
    {
        And = 0,
        Or = 1,
        Xor = 2,
        Not = 3
    }

    [Serializable]
    public sealed class AchievementConditionDefinition
    {
        [SerializeField]
        private string conditionId = "0001";

        [SerializeField]
        private AchievementConditionMode mode =
            AchievementConditionMode.Trigger;

        [SerializeField, Min(1)]
        private int targetCount = 1;

        [SerializeField, Range(0f, 100f)]
        private float targetProgress = 100f;

        [SerializeField]
        private string textPrefix = string.Empty;

        [SerializeField]
        private string textSuffix = string.Empty;

        [SerializeField]
        private Vector2 editorPosition;

        public string ConditionId => conditionId;
        public AchievementConditionMode Mode => mode;
        public int TargetCount => targetCount;
        public float TargetProgress => targetProgress;
        public string TextPrefix => textPrefix;
        public string TextSuffix => textSuffix;
        public Vector2 EditorPosition => editorPosition;

        public string BuildText(
            AchievementConditionState state)
        {
            switch (mode)
            {
                case AchievementConditionMode.Count:
                    return textPrefix +
                           (state?.CurrentCount ?? 0) +
                           "/" +
                           targetCount +
                           textSuffix;
                case AchievementConditionMode.Progress:
                    return textPrefix +
                           (state?.CurrentProgress ?? 0f)
                           .ToString("0.#") +
                           "%" +
                           textSuffix;
                default:
                    return textPrefix + textSuffix;
            }
        }
    }

    [Serializable]
    public sealed class AchievementLogicNodeDefinition
    {
        [SerializeField]
        private string nodeId = "L0001";

        [SerializeField]
        private AchievementLogicType logicType =
            AchievementLogicType.And;

        [SerializeField]
        private List<string> inputNodeIds = new List<string>();

        [SerializeField]
        private Vector2 editorPosition;

        public string NodeId => nodeId;
        public AchievementLogicType LogicType => logicType;
        public IReadOnlyList<string> InputNodeIds => inputNodeIds;
        public Vector2 EditorPosition => editorPosition;
    }

    [Serializable]
    public sealed class AchievementConditionState
    {
        [SerializeField]
        private string conditionId = string.Empty;

        [SerializeField]
        private int currentCount;

        [SerializeField]
        private float currentProgress;

        [SerializeField]
        private bool isSatisfied;

        public string ConditionId => conditionId;
        public int CurrentCount => currentCount;
        public float CurrentProgress => currentProgress;
        public bool IsSatisfied => isSatisfied;

        public AchievementConditionState()
        {
        }

        internal AchievementConditionState(
            string id)
        {
            conditionId = id;
        }

        internal void SetCount(
            int value)
        {
            currentCount = Mathf.Max(0, value);
        }

        internal void SetProgress(
            float value)
        {
            currentProgress = Mathf.Clamp(value, 0f, 100f);
        }

        internal void SetSatisfied(
            bool value)
        {
            isSatisfied = value;
        }
    }

    [Serializable]
    public sealed class AchievementRuntimeState
    {
        [SerializeField]
        private int achievementId;

        [SerializeField]
        private bool isUnlocked;

        [SerializeField]
        private bool isLocked;

        [SerializeField]
        private List<AchievementConditionState> conditions =
            new List<AchievementConditionState>();

        public int AchievementId => achievementId;
        public bool IsUnlocked => isUnlocked;
        public bool IsLocked => isLocked;
        public IReadOnlyList<AchievementConditionState> Conditions =>
            conditions;

        public AchievementRuntimeState()
        {
        }

        internal AchievementRuntimeState(
            int id)
        {
            achievementId = id;
        }

        internal void SetUnlocked(
            bool value)
        {
            isUnlocked = value;
        }

        internal void SetLocked(
            bool value)
        {
            isLocked = value;
        }

        internal AchievementConditionState GetCondition(
            string conditionId,
            bool create)
        {
            for (int index = 0;
                 index < conditions.Count;
                 index++)
            {
                if (conditions[index].ConditionId ==
                    conditionId)
                {
                    return conditions[index];
                }
            }

            if (!create)
            {
                return null;
            }

            AchievementConditionState state =
                new AchievementConditionState(conditionId);
            conditions.Add(state);
            return state;
        }
    }

    [Serializable]
    public sealed class AchievementSaveFile
    {
        [SerializeField]
        private int schemaVersion = 1;

        [SerializeField]
        private string catalogVersion = string.Empty;

        [SerializeField]
        private List<AchievementRuntimeState> achievements =
            new List<AchievementRuntimeState>();

        public int SchemaVersion => schemaVersion;
        public string CatalogVersion => catalogVersion;
        public IReadOnlyList<AchievementRuntimeState> Achievements =>
            achievements;

        internal void SetCatalogVersion(
            string value)
        {
            catalogVersion = value ?? string.Empty;
        }

        internal void SetAchievements(
            List<AchievementRuntimeState> value)
        {
            achievements =
                value ?? new List<AchievementRuntimeState>();
        }
    }

    public readonly struct AchievementSignal
    {
        public AchievementSignal(
            string signalId,
            int sourceId = 0,
            int count = 0,
            float progress = 0f)
        {
            SignalId = signalId ?? string.Empty;
            SourceId = sourceId;
            Count = count;
            Progress = progress;
        }

        public string SignalId { get; }
        public int SourceId { get; }
        public int Count { get; }
        public float Progress { get; }
    }
}
