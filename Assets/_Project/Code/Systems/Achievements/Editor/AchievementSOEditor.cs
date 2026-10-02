using UnityEditor;
using UnityEngine;

namespace Project.Achievements.Editor
{
    [CustomEditor(typeof(AchievementSO))]
    public sealed class AchievementSOEditor : UnityEditor.Editor
    {
        private SerializedProperty achievementId;
        private SerializedProperty displayName;
        private SerializedProperty description;
        private SerializedProperty hidden;
        private SerializedProperty platformAchievementId;
        private bool showBasicInfo = true;
        private bool showConditions = true;
        private bool showLogicNodes = true;
        private bool showResult = true;

        private void OnEnable()
        {
            achievementId =
                serializedObject.FindProperty("achievementId");
            displayName =
                serializedObject.FindProperty("displayName");
            description =
                serializedObject.FindProperty("description");
            hidden =
                serializedObject.FindProperty("hidden");
            platformAchievementId =
                serializedObject.FindProperty(
                    "platformAchievementId");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AchievementSO achievement =
                (AchievementSO)target;

            EditorGUILayout.HelpBox(
                "该数据盒只读。请通过成就工具窗口修改字段和条件。",
                MessageType.Info);

            DrawBasicInfo(achievement);
            DrawConditions(achievement);
            DrawLogicNodes(achievement);
            DrawResult(achievement);

            if (GUILayout.Button("在成就工具窗口中打开"))
            {
                AchievementToolWindow.OpenWith(achievement);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBasicInfo(
            AchievementSO achievement)
        {
            showBasicInfo = EditorGUILayout.Foldout(
                showBasicInfo,
                "基础信息",
                true);
            if (!showBasicInfo)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.PropertyField(
                    achievementId,
                    new GUIContent("成就编号"));
                EditorGUILayout.PropertyField(
                    displayName,
                    new GUIContent("成就名字"));
                EditorGUILayout.PropertyField(
                    description,
                    new GUIContent("成就简介"));
                EditorGUILayout.PropertyField(
                    hidden,
                    new GUIContent("隐藏成就"));
                EditorGUILayout.PropertyField(
                    platformAchievementId,
                    new GUIContent("平台成就 ID"));
                EditorGUILayout.LabelField(
                    "条件数量",
                    achievement.Conditions.Count.ToString());
                EditorGUILayout.LabelField(
                    "条件关系数量",
                    achievement.LogicNodes.Count.ToString());
                EditorGUILayout.LabelField(
                    "失败条件数量",
                    achievement.LockConditionIds.Count.ToString());
            }
        }

        private void DrawConditions(
            AchievementSO achievement)
        {
            showConditions = EditorGUILayout.Foldout(
                showConditions,
                "条件列表（" +
                achievement.Conditions.Count +
                "）",
                true);
            if (!showConditions)
            {
                return;
            }

            if (achievement.Conditions.Count == 0)
            {
                EditorGUILayout.LabelField("暂无条件。");
                return;
            }

            for (int index = 0;
                 index < achievement.Conditions.Count;
                 index++)
            {
                AchievementConditionDefinition condition =
                    achievement.Conditions[index];
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField(
                        "条件 " + condition.ConditionId,
                        ModeLabel(condition.Mode) +
                        (achievement.IsLockCondition(
                            condition.ConditionId)
                                ? " · 失败条件"
                                : string.Empty),
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "显示文字",
                        BuildConditionPreview(condition));
                    EditorGUILayout.LabelField(
                        "条件编号",
                        condition.ConditionId);
                    EditorGUILayout.LabelField(
                        "达成方式",
                        ModeLabel(condition.Mode));

                    if (condition.Mode ==
                        AchievementConditionMode.Count)
                    {
                        EditorGUILayout.LabelField(
                            "目标数量",
                            condition.TargetCount.ToString());
                    }
                    else if (condition.Mode ==
                             AchievementConditionMode.Progress)
                    {
                        EditorGUILayout.LabelField(
                            "目标进度",
                            condition.TargetProgress
                                .ToString("0.#") +
                            "%");
                    }

                    EditorGUILayout.LabelField(
                        "是否失败条件",
                        achievement.IsLockCondition(
                            condition.ConditionId)
                                ? "是"
                                : "否");
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawLogicNodes(
            AchievementSO achievement)
        {
            showLogicNodes = EditorGUILayout.Foldout(
                showLogicNodes,
                "条件关系（" +
                achievement.LogicNodes.Count +
                "）",
                true);
            if (!showLogicNodes)
            {
                return;
            }

            if (achievement.LogicNodes.Count == 0)
            {
                EditorGUILayout.LabelField(
                    "暂无条件关系，完成条件直接使用条件编号。");
                return;
            }

            for (int index = 0;
                 index < achievement.LogicNodes.Count;
                 index++)
            {
                AchievementLogicNodeDefinition logic =
                    achievement.LogicNodes[index];
                EditorGUILayout.BeginVertical(
                    EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField(
                        "关系 " + logic.NodeId,
                        LogicLabel(logic.LogicType),
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "组合方式",
                        LogicLabel(logic.LogicType));
                    EditorGUILayout.LabelField(
                        "接入内容",
                        logic.InputNodeIds.Count == 0
                            ? "无"
                            : string.Join(
                                "、",
                                logic.InputNodeIds));
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawResult(
            AchievementSO achievement)
        {
            showResult = EditorGUILayout.Foldout(
                showResult,
                "最终结果",
                true);
            if (!showResult)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.LabelField(
                    "完成条件",
                    string.IsNullOrWhiteSpace(
                        achievement.RootNodeId)
                            ? "未设置"
                            : achievement.RootNodeId);
                EditorGUILayout.LabelField(
                    "失败条件",
                    achievement.LockConditionIds.Count == 0
                        ? "无"
                        : string.Join(
                            "、",
                            achievement.LockConditionIds));
            }
        }

        private static string BuildConditionPreview(
            AchievementConditionDefinition condition)
        {
            switch (condition.Mode)
            {
                case AchievementConditionMode.Count:
                    return condition.TextPrefix +
                           " 0/" +
                           condition.TargetCount +
                           condition.TextSuffix;
                case AchievementConditionMode.Progress:
                    return condition.TextPrefix +
                           " 0/" +
                           condition.TargetProgress
                               .ToString("0.#") +
                           "%" +
                           condition.TextSuffix;
                default:
                    return condition.TextPrefix +
                           condition.TextSuffix;
            }
        }

        private static string ModeLabel(
            AchievementConditionMode mode)
        {
            switch (mode)
            {
                case AchievementConditionMode.Count:
                    return "累计数量";
                case AchievementConditionMode.Progress:
                    return "完成百分比";
                default:
                    return "触发一次";
            }
        }

        private static string LogicLabel(
            AchievementLogicType logicType)
        {
            switch (logicType)
            {
                case AchievementLogicType.And:
                    return "全部达成";
                case AchievementLogicType.Or:
                    return "任意一个达成";
                case AchievementLogicType.Xor:
                    return "只能有一个达成";
                case AchievementLogicType.Not:
                    return "必须未发生";
                default:
                    return "未设置";
            }
        }
    }
}
