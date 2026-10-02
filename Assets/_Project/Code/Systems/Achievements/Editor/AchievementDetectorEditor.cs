using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Project.Achievements.Editor
{
    [CustomEditor(typeof(AchievementDetector))]
    public sealed class AchievementDetectorEditor :
        UnityEditor.Editor
    {
        private SerializedProperty bindings;

        private void OnEnable()
        {
            bindings = serializedObject.FindProperty("bindings");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            AchievementEditorHelp.DrawDetectorHelp();

            EditorGUILayout.HelpBox(
                "检测组件只转发信号，不判断成就是否完成。\n" +
                "玩法信号通常先经过 AchievementSignalBridge；" +
                "官方 UI 组件也可以直接监听。",
                MessageType.Info);

            for (int index = 0;
                 index < bindings.arraySize;
                 index++)
            {
                DrawBinding(index);
            }

            if (GUILayout.Button("＋ 添加检测项"))
            {
                bindings.InsertArrayElementAtIndex(
                    bindings.arraySize);
                SerializedProperty item =
                    bindings.GetArrayElementAtIndex(
                        bindings.arraySize - 1);
                item.FindPropertyRelative("enabled")
                    .boolValue = true;
                item.FindPropertyRelative("conditionId")
                    .stringValue = "0001";
                item.FindPropertyRelative("count")
                    .intValue = 1;
                item.FindPropertyRelative("progress")
                    .floatValue = 1f;
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawBinding(int index)
        {
            SerializedProperty binding =
                bindings.GetArrayElementAtIndex(index);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(
                binding.FindPropertyRelative("enabled"),
                GUILayout.Width(42f));
            EditorGUILayout.LabelField(
                "检测项 " + (index + 1),
                EditorStyles.boldLabel);
            if (GUILayout.Button("×", GUILayout.Width(24f)))
            {
                bindings.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(
                binding.FindPropertyRelative("source"),
                new GUIContent("源组件"));
            Component source =
                binding.FindPropertyRelative("source")
                    .objectReferenceValue as Component;
            DrawSignalField(
                binding,
                source);
            SerializedProperty lookupMode =
                binding.FindPropertyRelative("lookupMode");
            string[] lookupOptions =
            {
                "按成就编号",
                "按成就名字"
            };
            int selectedLookup = EditorGUILayout.Popup(
                "查找方式",
                Mathf.Clamp(
                    lookupMode.enumValueIndex,
                    0,
                    lookupOptions.Length - 1),
                lookupOptions);
            lookupMode.enumValueIndex = selectedLookup;
            if (lookupMode.enumValueIndex ==
                (int)AchievementLookupMode.Id)
            {
                DrawAchievementField(binding);
            }
            else
            {
                EditorGUILayout.PropertyField(
                    binding.FindPropertyRelative("achievementName"),
                    new GUIContent("成就名字"));
            }

            DrawConditionField(binding);
            EditorGUILayout.PropertyField(
                binding.FindPropertyRelative("count"),
                new GUIContent("增加计数"));
            EditorGUILayout.PropertyField(
                binding.FindPropertyRelative("progress"),
                new GUIContent(
                    "增加进度（%）",
                    "1 表示增加 1%"));

            if (source == null)
            {
                EditorGUILayout.HelpBox(
                    "源组件为空。",
                    MessageType.Warning);
            }
            else if (!(source is IAchievementSignalSource) &&
                     !(source is UnityEngine.UI.Button) &&
                     !(source is UnityEngine.UI.Slider) &&
                     !(source is UnityEngine.UI.Toggle) &&
                     !(source is UnityEngine.UI.Dropdown) &&
                     !(source is UnityEngine.UI.InputField) &&
                     !(source is UnityEngine.UI.ScrollRect))
            {
                EditorGUILayout.HelpBox(
                    "该组件没有可识别的成就信号接口，也不会被检测器订阅。",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawSignalField(
            SerializedProperty binding,
            Component source)
        {
            SerializedProperty signalName =
                binding.FindPropertyRelative("signalName");
            AchievementSignalBridge bridge =
                source as AchievementSignalBridge;
            IReadOnlyList<string> signalIds =
                bridge?.GetAvailableSignalIds();
            if (signalIds == null || signalIds.Count == 0)
            {
                EditorGUILayout.PropertyField(
                    signalName,
                    new GUIContent(
                        "信号名",
                        "自定义信号源使用；没有信号列表时可手动填写"));
                return;
            }

            List<string> options = new List<string>
            {
                "任意信号"
            };
            options.AddRange(signalIds);
            int current = string.IsNullOrWhiteSpace(
                signalName.stringValue)
                ? 0
                : options.IndexOf(signalName.stringValue);
            current = Mathf.Clamp(
                current,
                0,
                options.Count - 1);
            int selected = EditorGUILayout.Popup(
                "信号",
                current,
                options.ToArray());
            if (selected != current)
            {
                signalName.stringValue =
                    selected == 0
                        ? string.Empty
                        : options[selected];
            }
        }

        private static void DrawAchievementField(
            SerializedProperty binding)
        {
            List<AchievementSO> achievements =
                FindAchievements();
            SerializedProperty achievementId =
                binding.FindPropertyRelative("achievementId");
            if (achievements.Count == 0)
            {
                EditorGUILayout.PropertyField(
                    achievementId,
                    new GUIContent("成就编号"));
                return;
            }

            List<string> options = new List<string>();
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                options.Add(
                    achievements[index].AchievementId +
                    " · " +
                    achievements[index].DisplayName);
            }
            List<AchievementSO> optionAssets =
                new List<AchievementSO>(achievements);

            int current = -1;
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                if (achievements[index].AchievementId ==
                    achievementId.intValue)
                {
                    current = index;
                    break;
                }
            }

            if (current < 0)
            {
                options.Insert(
                    0,
                    "当前编号 " +
                    achievementId.intValue +
                    "（数据盒不存在）");
                optionAssets.Insert(0, null);
                current = 0;
            }

            int selected = EditorGUILayout.Popup(
                "成就",
                current,
                options.ToArray());
            if (selected != current)
            {
                AchievementSO selectedAsset =
                    optionAssets[selected];
                if (selectedAsset != null)
                {
                    achievementId.intValue =
                        selectedAsset.AchievementId;
                }
            }
        }

        private static void DrawConditionField(
            SerializedProperty binding)
        {
            SerializedProperty achievementId =
                binding.FindPropertyRelative("achievementId");
            SerializedProperty conditionId =
                binding.FindPropertyRelative("conditionId");
            AchievementSO achievement =
                FindAchievement(achievementId.intValue);
            if (achievement == null ||
                achievement.Conditions.Count == 0)
            {
                EditorGUILayout.PropertyField(
                    conditionId,
                    new GUIContent("条件编号"));
                return;
            }

            List<string> options = new List<string>();
            int current = -1;
            for (int index = 0;
                 index < achievement.Conditions.Count;
                 index++)
            {
                AchievementConditionDefinition condition =
                    achievement.Conditions[index];
                options.Add(
                    condition.ConditionId +
                    " · " +
                    BuildConditionLabel(condition));
                if (condition.ConditionId ==
                    conditionId.stringValue)
                {
                    current = index;
                }
            }

            if (current < 0)
            {
                current = 0;
                conditionId.stringValue =
                    achievement.Conditions[0].ConditionId;
            }

            int selected = EditorGUILayout.Popup(
                "条件",
                current,
                options.ToArray());
            if (selected != current)
            {
                conditionId.stringValue =
                    achievement.Conditions[selected].ConditionId;
            }
        }

        private static string BuildConditionLabel(
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

        private static List<AchievementSO> FindAchievements()
        {
            List<AchievementSO> result =
                new List<AchievementSO>();
            string[] guids =
                AssetDatabase.FindAssets("t:AchievementSO");
            for (int index = 0;
                 index < guids.Length;
                 index++)
            {
                AchievementSO achievement =
                    AssetDatabase.LoadAssetAtPath<AchievementSO>(
                        AssetDatabase.GUIDToAssetPath(
                            guids[index]));
                if (achievement != null)
                {
                    result.Add(achievement);
                }
            }

            result.Sort(
                (left, right) =>
                    left.AchievementId.CompareTo(
                        right.AchievementId));
            return result;
        }

        private static AchievementSO FindAchievement(
            int achievementId)
        {
            List<AchievementSO> achievements =
                FindAchievements();
            for (int index = 0;
                 index < achievements.Count;
                 index++)
            {
                if (achievements[index].AchievementId ==
                    achievementId)
                {
                    return achievements[index];
                }
            }

            return null;
        }
    }
}
