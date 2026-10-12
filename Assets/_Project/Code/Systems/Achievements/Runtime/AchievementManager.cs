using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Achievements
{
    [DisallowMultipleComponent]
    public sealed class AchievementManager : MonoBehaviour
    {
        private const string CatalogResourcePath =
            "AchievementCatalog";

        private static AchievementManager instance;

        private readonly Dictionary<int, AchievementSO>
            achievementsById =
                new Dictionary<int, AchievementSO>();
        private readonly Dictionary<string, AchievementSO>
            achievementsByName =
                new Dictionary<string, AchievementSO>(
                    StringComparer.Ordinal);
        private readonly Dictionary<int, AchievementRuntimeState>
            runtimeStates =
                new Dictionary<int, AchievementRuntimeState>();
        private readonly List<IAchievementUnlockReceiver>
            unlockReceivers =
                new List<IAchievementUnlockReceiver>();

        private AchievementCatalogSO catalog;
        private AchievementSaveService saveService;

        public static AchievementManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                GameObject managerObject =
                    new GameObject("[AchievementManager]");
                instance =
                    managerObject.AddComponent<AchievementManager>();
                return instance;
            }
        }

        public bool IsReady { get; private set; }

        public event Action<AchievementSO> AchievementUnlocked;
        public event Action<AchievementSO> AchievementLocked;
        public event Action<AchievementSO> AchievementProgressChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        public void Reload()
        {
            Initialize();
        }

        public bool TriggerById(
            int achievementId,
            string conditionId,
            int count = 1,
            float progress = 1f)
        {
            return achievementsById.TryGetValue(
                       achievementId,
                       out AchievementSO achievement) &&
                   Trigger(
                       achievement,
                       conditionId,
                       count,
                       progress);
        }

        public bool Trigger(
            int achievementId,
            string conditionId,
            int count = 1,
            float progress = 1f)
        {
            return TriggerById(
                achievementId,
                conditionId,
                count,
                progress);
        }

        public bool TriggerByName(
            string achievementName,
            string conditionId,
            int count = 1,
            float progress = 1f)
        {
            return !string.IsNullOrWhiteSpace(
                       achievementName) &&
                   achievementsByName.TryGetValue(
                       achievementName,
                       out AchievementSO achievement) &&
                   Trigger(
                       achievement,
                       conditionId,
                       count,
                       progress);
        }

        public bool Trigger(
            string achievementName,
            string conditionId,
            int count = 1,
            float progress = 1f)
        {
            return TriggerByName(
                achievementName,
                conditionId,
                count,
                progress);
        }

        public string GetDisplayName(
            int achievementId)
        {
            return achievementsById.TryGetValue(
                achievementId,
                out AchievementSO achievement)
                ? achievement.DisplayName
                : string.Empty;
        }

        public string GetDescription(
            int achievementId)
        {
            return achievementsById.TryGetValue(
                achievementId,
                out AchievementSO achievement)
                ? achievement.Description
                : string.Empty;
        }

        public string GetConditionText(
            int achievementId,
            string conditionId)
        {
            if (!achievementsById.TryGetValue(
                    achievementId,
                    out AchievementSO achievement))
            {
                return string.Empty;
            }

            AchievementRuntimeState state =
                GetRuntimeState(achievement, false);
            AchievementConditionState conditionState =
                state?.GetCondition(conditionId, false);
            return achievement.GetConditionText(
                conditionId,
                conditionState);
        }

        public int GetConditionCount(
            int achievementId,
            string conditionId)
        {
            AchievementConditionState state =
                GetConditionState(
                    achievementId,
                    conditionId);
            return state?.CurrentCount ?? 0;
        }

        public float GetConditionProgress(
            int achievementId,
            string conditionId)
        {
            AchievementConditionState state =
                GetConditionState(
                    achievementId,
                    conditionId);
            return state?.CurrentProgress ?? 0f;
        }

        public bool IsConditionSatisfied(
            int achievementId,
            string conditionId)
        {
            return GetConditionState(
                achievementId,
                conditionId)?.IsSatisfied ?? false;
        }

        public bool IsUnlocked(
            int achievementId)
        {
            return achievementsById.ContainsKey(
                       achievementId) &&
                   runtimeStates.TryGetValue(
                       achievementId,
                       out AchievementRuntimeState state) &&
                   state.IsUnlocked;
        }

        public bool IsLocked(
            int achievementId)
        {
            return achievementsById.ContainsKey(
                       achievementId) &&
                   runtimeStates.TryGetValue(
                       achievementId,
                       out AchievementRuntimeState state) &&
                   state.IsLocked;
        }

        public IReadOnlyList<AchievementSO> GetAllAchievements()
        {
            return catalog != null
                ? catalog.Achievements
                : Array.Empty<AchievementSO>();
        }

        public void RegisterReceiver(
            IAchievementUnlockReceiver receiver)
        {
            if (receiver == null ||
                unlockReceivers.Contains(receiver))
            {
                return;
            }

            unlockReceivers.Add(receiver);
        }

        public void UnregisterReceiver(
            IAchievementUnlockReceiver receiver)
        {
            if (receiver != null)
            {
                unlockReceivers.Remove(receiver);
            }
        }

        public void SaveNow()
        {
            if (saveService == null)
            {
                return;
            }

            AchievementSaveFile saveFile =
                new AchievementSaveFile();
            saveFile.SetCatalogVersion(
                catalog != null
                    ? catalog.CatalogVersion
                    : string.Empty);
            saveFile.SetAchievements(
                new List<AchievementRuntimeState>(
                    runtimeStates.Values));
            saveService.Save(saveFile);
        }

        public void ResetAllProgress()
        {
            runtimeStates.Clear();
            if (catalog != null)
            {
                for (int index = 0;
                     index < catalog.Achievements.Count;
                     index++)
                {
                    AchievementSO achievement =
                        catalog.Achievements[index];
                    if (achievement != null)
                    {
                        runtimeStates[achievement.AchievementId] =
                            new AchievementRuntimeState(
                                achievement.AchievementId);
                    }
                }
            }

            saveService?.Clear();
            SaveNow();
        }

        private void Initialize()
        {
            catalog =
                Resources.Load<AchievementCatalogSO>(
                    CatalogResourcePath);
            saveService =
                new AchievementSaveService(catalog);
            BuildIndexes();
            LoadRuntimeState();
            IsReady = catalog != null;

            if (!IsReady)
            {
                Debug.LogWarning(
                    "[AchievementManager] 未找到 Resources/" +
                    CatalogResourcePath +
                    ".asset，成就系统保持待机。");
            }
        }

        private void BuildIndexes()
        {
            achievementsById.Clear();
            achievementsByName.Clear();
            if (catalog == null)
            {
                return;
            }

            for (int index = 0;
                 index < catalog.Achievements.Count;
                 index++)
            {
                AchievementSO achievement =
                    catalog.Achievements[index];
                if (achievement == null)
                {
                    continue;
                }

                if (achievementsById.ContainsKey(
                        achievement.AchievementId))
                {
                    Debug.LogError(
                        "[AchievementManager] 成就编号重复：" +
                        achievement.AchievementId,
                        achievement);
                    continue;
                }

                achievementsById.Add(
                    achievement.AchievementId,
                    achievement);
                if (!string.IsNullOrWhiteSpace(
                        achievement.DisplayName) &&
                    !achievementsByName.ContainsKey(
                        achievement.DisplayName))
                {
                    achievementsByName.Add(
                        achievement.DisplayName,
                        achievement);
                }
                else
                {
                    Debug.LogError(
                        "[AchievementManager] 成就名字重复或为空：" +
                        achievement.DisplayName,
                        achievement);
                }
            }
        }

        private void LoadRuntimeState()
        {
            runtimeStates.Clear();
            AchievementSaveFile saveFile =
                saveService.Load();
            bool versionMismatch =
                catalog != null &&
                saveFile.CatalogVersion !=
                catalog.CatalogVersion;
            if (versionMismatch)
            {
                saveService.Clear();
                saveFile = new AchievementSaveFile();
            }

            for (int index = 0;
                 index < saveFile.Achievements.Count;
                 index++)
            {
                AchievementRuntimeState state =
                    saveFile.Achievements[index];
                if (state != null &&
                    achievementsById.ContainsKey(
                        state.AchievementId))
                {
                    runtimeStates[state.AchievementId] =
                        state;
                }
            }

            foreach (KeyValuePair<int, AchievementSO> pair in
                     achievementsById)
            {
                if (!runtimeStates.ContainsKey(pair.Key))
                {
                    runtimeStates[pair.Key] =
                        new AchievementRuntimeState(pair.Key);
                }
            }

            if (versionMismatch)
            {
                SaveNow();
            }
        }

        private bool Trigger(
            AchievementSO achievement,
            string conditionId,
            int count,
            float progress)
        {
            if (!IsReady ||
                achievement == null ||
                string.IsNullOrWhiteSpace(conditionId))
            {
                return false;
            }

            AchievementRuntimeState state =
                GetRuntimeState(achievement, true);
            if (state.IsUnlocked || state.IsLocked)
            {
                return false;
            }

            AchievementConditionDefinition condition =
                achievement.FindCondition(conditionId);
            if (condition == null)
            {
                Debug.LogWarning(
                    "[AchievementManager] 找不到条件：" +
                    achievement.DisplayName +
                    "/" +
                    conditionId);
                return false;
            }

            AchievementConditionState conditionState =
                state.GetCondition(conditionId, true);
            if (!ApplyCondition(
                    condition,
                    conditionState,
                    count,
                    progress))
            {
                return false;
            }

            AchievementProgressChanged?.Invoke(achievement);

            if (condition.Mode !=
                AchievementConditionMode.Trigger)
            {
                string value =
                    condition.Mode ==
                    AchievementConditionMode.Count
                        ? conditionState.CurrentCount +
                          "/" +
                          condition.TargetCount
                        : conditionState.CurrentProgress
                              .ToString("0.#") +
                          "/" +
                          condition.TargetProgress
                              .ToString("0.#") +
                          "%";
                Debug.Log(
                    $"[AchievementManager] 条件进度：" +
                    $"{achievement.DisplayName}（{achievement.AchievementId}）" +
                    $" 条件 {conditionId} = {value}",
                    achievement);
            }

            if (achievement.IsLockCondition(conditionId) &&
                conditionState.IsSatisfied)
            {
                state.SetLocked(true);
                SaveNow();
                AchievementLocked?.Invoke(achievement);
                return true;
            }

            Dictionary<string, bool> memo =
                new Dictionary<string, bool>(
                    StringComparer.Ordinal);
            HashSet<string> visiting =
                new HashSet<string>(
                    StringComparer.Ordinal);
            if (EvaluateRoot(
                    achievement,
                    state,
                    memo,
                    visiting))
            {
                state.SetUnlocked(true);
                SaveNow();
                Debug.Log(
                    $"[AchievementManager] 达成成就：" +
                    $"{achievement.DisplayName}（{achievement.AchievementId}）",
                    achievement);
                AchievementUnlocked?.Invoke(achievement);
                NotifyAchievementUnlocked(
                    achievement.DisplayName);
            }
            else
            {
                SaveNow();
            }

            return true;
        }

        private static bool ApplyCondition(
            AchievementConditionDefinition condition,
            AchievementConditionState state,
            int count,
            float progress)
        {
            switch (condition.Mode)
            {
                case AchievementConditionMode.Count:
                    if (count <= 0)
                    {
                        return false;
                    }

                    state.SetCount(
                        Mathf.Min(
                            condition.TargetCount,
                            state.CurrentCount + count));
                    state.SetSatisfied(
                        state.CurrentCount >=
                        condition.TargetCount);
                    return true;
                case AchievementConditionMode.Progress:
                    if (progress <= 0f)
                    {
                        return false;
                    }

                    state.SetProgress(
                        Mathf.Min(
                            condition.TargetProgress,
                            state.CurrentProgress + progress));
                    state.SetSatisfied(
                        state.CurrentProgress >=
                        condition.TargetProgress);
                    return true;
                default:
                    if (state.IsSatisfied)
                    {
                        return false;
                    }

                    state.SetSatisfied(true);
                    return true;
            }
        }

        private AchievementConditionState GetConditionState(
            int achievementId,
            string conditionId)
        {
            if (!achievementsById.TryGetValue(
                    achievementId,
                    out AchievementSO achievement))
            {
                return null;
            }

            return GetRuntimeState(
                    achievement,
                    false)
                ?.GetCondition(conditionId, false);
        }

        public string GetCurrentRequirementText(
            int achievementId)
        {
            if (!achievementsById.TryGetValue(
                    achievementId,
                    out AchievementSO achievement))
            {
                return string.Empty;
            }

            AchievementRuntimeState state =
                GetRuntimeState(achievement, false);
            if (state == null || state.IsUnlocked)
            {
                return string.Empty;
            }

            string rootNodeId = achievement.RootNodeId;
            if (string.IsNullOrWhiteSpace(rootNodeId) &&
                achievement.Conditions.Count > 0)
            {
                rootNodeId =
                    achievement.Conditions[0].ConditionId;
            }

            AchievementConditionDefinition condition =
                FindCurrentCondition(
                    achievement,
                    state,
                    rootNodeId,
                    new Dictionary<string, bool>(
                        StringComparer.Ordinal),
                    new HashSet<string>(
                        StringComparer.Ordinal));
            if (condition == null)
            {
                return string.Empty;
            }

            return achievement.GetConditionText(
                condition.ConditionId,
                state.GetCondition(
                    condition.ConditionId,
                    false));
        }

        private static AchievementConditionDefinition
            FindCurrentCondition(
                AchievementSO achievement,
                AchievementRuntimeState state,
                string nodeId,
                Dictionary<string, bool> memo,
                HashSet<string> visiting)
        {
            if (string.IsNullOrWhiteSpace(nodeId) ||
                !visiting.Add(nodeId))
            {
                return null;
            }

            AchievementConditionDefinition condition =
                achievement.FindCondition(nodeId);
            if (condition != null)
            {
                return condition;
            }

            AchievementLogicNodeDefinition logicNode =
                achievement.FindLogicNode(nodeId);
            if (logicNode == null)
            {
                return null;
            }

            IReadOnlyList<string> inputs =
                logicNode.InputNodeIds;
            AchievementConditionDefinition firstLeaf = null;
            for (int index = 0;
                 index < inputs.Count;
                 index++)
            {
                string inputId = inputs[index];
                bool satisfied = EvaluateNode(
                    achievement,
                    state,
                    inputId,
                    memo,
                    new HashSet<string>(
                        StringComparer.Ordinal));
                AchievementConditionDefinition child =
                    FindCurrentCondition(
                        achievement,
                        state,
                        inputId,
                        memo,
                        visiting);
                if (firstLeaf == null && child != null)
                {
                    firstLeaf = child;
                }

                if (!satisfied && child != null)
                {
                    visiting.Remove(nodeId);
                    return child;
                }
            }

            visiting.Remove(nodeId);
            return firstLeaf;
        }

        private AchievementRuntimeState GetRuntimeState(
            AchievementSO achievement,
            bool create)
        {
            if (achievement == null ||
                !achievementsById.ContainsKey(
                    achievement.AchievementId))
            {
                return null;
            }

            if (runtimeStates.TryGetValue(
                    achievement.AchievementId,
                    out AchievementRuntimeState state) ||
                !create)
            {
                return state;
            }

            state = new AchievementRuntimeState(
                achievement.AchievementId);
            runtimeStates.Add(
                achievement.AchievementId,
                state);
            return state;
        }

        private static bool EvaluateRoot(
            AchievementSO achievement,
            AchievementRuntimeState state,
            Dictionary<string, bool> memo,
            HashSet<string> visiting)
        {
            string rootNodeId = achievement.RootNodeId;
            if (string.IsNullOrWhiteSpace(rootNodeId) &&
                achievement.Conditions.Count > 0)
            {
                rootNodeId =
                    achievement.Conditions[0].ConditionId;
            }

            return EvaluateNode(
                achievement,
                state,
                rootNodeId,
                memo,
                visiting);
        }

        private static bool EvaluateNode(
            AchievementSO achievement,
            AchievementRuntimeState state,
            string nodeId,
            Dictionary<string, bool> memo,
            HashSet<string> visiting)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return false;
            }

            if (memo.TryGetValue(nodeId, out bool cached))
            {
                return cached;
            }

            if (!visiting.Add(nodeId))
            {
                return false;
            }

            bool result;
            AchievementConditionDefinition condition =
                achievement.FindCondition(nodeId);
            if (condition != null)
            {
                result =
                    state.GetCondition(nodeId, false)
                        ?.IsSatisfied ?? false;
            }
            else
            {
                AchievementLogicNodeDefinition logicNode =
                    achievement.FindLogicNode(nodeId);
                result = logicNode != null &&
                         EvaluateLogicNode(
                             achievement,
                             state,
                             logicNode,
                             memo,
                             visiting);
            }

            visiting.Remove(nodeId);
            memo[nodeId] = result;
            return result;
        }

        private static bool EvaluateLogicNode(
            AchievementSO achievement,
            AchievementRuntimeState state,
            AchievementLogicNodeDefinition logicNode,
            Dictionary<string, bool> memo,
            HashSet<string> visiting)
        {
            IReadOnlyList<string> inputs =
                logicNode.InputNodeIds;
            if (inputs.Count == 0)
            {
                return false;
            }

            int trueCount = 0;
            for (int index = 0;
                 index < inputs.Count;
                 index++)
            {
                if (EvaluateNode(
                        achievement,
                        state,
                        inputs[index],
                        memo,
                        visiting))
                {
                    trueCount++;
                }
            }

            switch (logicNode.LogicType)
            {
                case AchievementLogicType.And:
                    return trueCount == inputs.Count;
                case AchievementLogicType.Or:
                    return trueCount > 0;
                case AchievementLogicType.Xor:
                    return trueCount == 1;
                case AchievementLogicType.Not:
                    return trueCount == 0;
                default:
                    return false;
            }
        }

        private void NotifyAchievementUnlocked(
            string displayName)
        {
            for (int index = unlockReceivers.Count - 1;
                 index >= 0;
                 index--)
            {
                if (unlockReceivers[index] == null)
                {
                    unlockReceivers.RemoveAt(index);
                    continue;
                }

                unlockReceivers[index].OnAchievementUnlocked(
                    displayName);
            }
        }

        private void OnApplicationPause(
            bool pause)
        {
            if (pause)
            {
                SaveNow();
            }
        }

        private void OnApplicationQuit()
        {
            SaveNow();
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                SaveNow();
                unlockReceivers.Clear();
                instance = null;
            }
        }
    }
}
