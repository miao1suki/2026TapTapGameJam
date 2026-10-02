using System.Collections.Generic;
using UnityEngine;

namespace Project.Achievements
{
    [CreateAssetMenu(
        menuName = "2026TapTap/Achievements/Achievement",
        fileName = "Achievement_")]
    public sealed class AchievementSO : ScriptableObject
    {
        [SerializeField]
        private int achievementId = 1001;

        [SerializeField]
        private string displayName = "新成就";

        [SerializeField, TextArea(2, 5)]
        private string description = string.Empty;

        [SerializeField]
        private bool hidden;

        [SerializeField]
        private string platformAchievementId = string.Empty;

        [SerializeField]
        private List<AchievementConditionDefinition> conditions =
            new List<AchievementConditionDefinition>();

        [SerializeField]
        private List<AchievementLogicNodeDefinition> logicNodes =
            new List<AchievementLogicNodeDefinition>();

        [SerializeField]
        private string rootNodeId = string.Empty;

        [SerializeField]
        private List<string> lockConditionIds =
            new List<string>();

        [SerializeField]
        private Vector2 rootEditorPosition;

        public int AchievementId => achievementId;
        public string DisplayName => displayName;
        public string Description => description;
        public bool Hidden => hidden;
        public string PlatformAchievementId =>
            platformAchievementId;
        public IReadOnlyList<AchievementConditionDefinition>
            Conditions => conditions;
        public IReadOnlyList<AchievementLogicNodeDefinition>
            LogicNodes => logicNodes;
        public string RootNodeId => rootNodeId;
        public IReadOnlyList<string> LockConditionIds =>
            lockConditionIds;
        public Vector2 RootEditorPosition => rootEditorPosition;

        public AchievementConditionDefinition FindCondition(
            string conditionId)
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

            return null;
        }

        public AchievementLogicNodeDefinition FindLogicNode(
            string nodeId)
        {
            for (int index = 0;
                 index < logicNodes.Count;
                 index++)
            {
                if (logicNodes[index].NodeId == nodeId)
                {
                    return logicNodes[index];
                }
            }

            return null;
        }

        public bool IsLockCondition(
            string conditionId)
        {
            return lockConditionIds.Contains(conditionId);
        }

        public string GetConditionText(
            string conditionId,
            AchievementConditionState state)
        {
            AchievementConditionDefinition condition =
                FindCondition(conditionId);
            return condition == null
                ? string.Empty
                : condition.BuildText(state);
        }
    }
}
