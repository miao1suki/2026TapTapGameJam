using System;
using System.Collections.Generic;
using System.Reflection;
using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.BlockFeatures.Editor
{
    [CustomEditor(typeof(BlockFeature), true)]
    public sealed class BlockFeatureEditor : UnityEditor.Editor
    {
        private sealed class FieldEntry
        {
            public FieldInfo Field;
            public BlockParameterAttribute Parameter;
        }

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            BlockFeature feature = (BlockFeature)target;
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);

            root.Add(ProjectInspectorUtility.CreateTitle(
                feature.Metadata.DisplayName));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "本组件遵循颜色物功能组件契约，只处理其声明的能力。" +
                "启用状态由颜色物功能控制在运行时按固定颜色组切换；" +
                "编辑器中不会保留手动启用状态。"));

            Foldout contract =
                ProjectInspectorUtility.CreateFoldout(
                    "组件契约",
                    true);
            contract.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "固定颜色组",
                () => string.IsNullOrWhiteSpace(
                    feature.Metadata.DefaultColorId)
                    ? "无"
                    : feature.Metadata.DefaultColorId));
            contract.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "功能类别",
                () => FormatCategory(
                    feature.Metadata.Category)));
            contract.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "交互类型",
                () => FormatInteractions(
                    feature.Metadata.Interactions)));
            root.Add(contract);

            AddParameterGroups(root, feature);
            AddRuntimeDebug(root, feature);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }

        private static string FormatCategory(
            BlockFeatureCategory category)
        {
            switch (category)
            {
                case BlockFeatureCategory.Mechanism:
                    return "机关";
                case BlockFeatureCategory.Hazard:
                    return "危险物";
                case BlockFeatureCategory.Water:
                    return "水域";
                case BlockFeatureCategory.Plant:
                    return "植物";
                case BlockFeatureCategory.Movement:
                    return "移动";
                case BlockFeatureCategory.Reaction:
                    return "反应";
                default:
                    return "通用";
            }
        }

        private static string FormatInteractions(
            BlockFeatureInteraction interactions)
        {
            if (interactions == BlockFeatureInteraction.None)
            {
                return "无";
            }

            var builder = new System.Text.StringBuilder();
            AppendInteraction(
                builder,
                interactions,
                BlockFeatureInteraction.PlayerContact,
                "玩家接触");
            AppendInteraction(
                builder,
                interactions,
                BlockFeatureInteraction.ObjectContact,
                "物体接触");
            AppendInteraction(
                builder,
                interactions,
                BlockFeatureInteraction.AppliedColor,
                "接收颜色");
            AppendInteraction(
                builder,
                interactions,
                BlockFeatureInteraction.RoomReset,
                "房间重置");
            return builder.ToString();
        }

        private static void AppendInteraction(
            System.Text.StringBuilder builder,
            BlockFeatureInteraction interactions,
            BlockFeatureInteraction flag,
            string label)
        {
            if ((interactions & flag) == 0)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.Append("、");
            }

            builder.Append(label);
        }

        private void AddParameterGroups(
            VisualElement root,
            BlockFeature feature)
        {
            var groups = new Dictionary<string, List<FieldEntry>>(
                StringComparer.Ordinal);
            var groupOrder = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (FieldInfo field in EnumerateParameterFields(
                         feature.GetType()))
            {
                BlockParameterAttribute parameter =
                    field.GetCustomAttribute<BlockParameterAttribute>();
                if (parameter == null ||
                    serializedObject.FindProperty(field.Name) == null)
                {
                    continue;
                }

                string group = string.IsNullOrWhiteSpace(
                    parameter.Group)
                    ? "参数"
                    : parameter.Group.Trim();
                if (!groups.TryGetValue(
                        group,
                        out List<FieldEntry> entries))
                {
                    entries = new List<FieldEntry>();
                    groups.Add(group, entries);
                    groupOrder.Add(group, parameter.Order);
                }
                else if (parameter.Order < groupOrder[group])
                {
                    groupOrder[group] = parameter.Order;
                }

                entries.Add(new FieldEntry
                {
                    Field = field,
                    Parameter = parameter
                });
            }

            var orderedGroups = new List<string>(groups.Keys);
            orderedGroups.Sort((left, right) =>
            {
                int order = groupOrder[left].CompareTo(
                    groupOrder[right]);
                return order != 0
                    ? order
                    : string.CompareOrdinal(left, right);
            });

            for (int groupIndex = 0;
                 groupIndex < orderedGroups.Count;
                 groupIndex++)
            {
                string groupName = orderedGroups[groupIndex];
                List<FieldEntry> entries = groups[groupName];
                entries.Sort((left, right) =>
                {
                    int order = left.Parameter.Order.CompareTo(
                        right.Parameter.Order);
                    return order != 0
                        ? order
                        : string.CompareOrdinal(
                            left.Field.Name,
                            right.Field.Name);
                });

                Foldout foldout =
                    ProjectInspectorUtility.CreateFoldout(
                        groupName,
                        ShouldExpandGroup(groupName));
                for (int fieldIndex = 0;
                     fieldIndex < entries.Count;
                     fieldIndex++)
                {
                    AddParameterField(
                        foldout,
                        entries[fieldIndex]);
                }

                root.Add(foldout);
            }
        }

        private static IEnumerable<FieldInfo> EnumerateParameterFields(
            Type featureType)
        {
            var hierarchy = new List<Type>();
            for (Type current = featureType;
                 current != null &&
                 current != typeof(MonoBehaviour);
                 current = current.BaseType)
            {
                hierarchy.Add(current);
            }

            for (int typeIndex = hierarchy.Count - 1;
                 typeIndex >= 0;
                 typeIndex--)
            {
                FieldInfo[] fields = hierarchy[typeIndex].GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.DeclaredOnly);
                for (int fieldIndex = 0;
                     fieldIndex < fields.Length;
                     fieldIndex++)
                {
                    yield return fields[fieldIndex];
                }
            }
        }

        private void AddParameterField(
            VisualElement parent,
            FieldEntry entry)
        {
            string label = string.IsNullOrWhiteSpace(
                entry.Parameter.Label)
                ? entry.Field.Name
                : entry.Parameter.Label.Trim();
            SerializedProperty property =
                serializedObject.FindProperty(entry.Field.Name);
            if (entry.Field.FieldType == typeof(string) &&
                entry.Field.Name.EndsWith(
                    "Tag",
                    StringComparison.OrdinalIgnoreCase))
            {
                TagField tag = new TagField(label)
                {
                    value = property.stringValue,
                    tooltip = entry.Parameter.Tooltip
                };
                tag.RegisterValueChangedCallback(evt =>
                {
                    property.stringValue = evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                });
                parent.Add(tag);
                return;
            }

            PropertyField field = ProjectInspectorUtility.CreateProperty(
                serializedObject,
                entry.Field.Name,
                label,
                entry.Parameter.Tooltip);
            parent.Add(field);

            string visibilityField =
                entry.Parameter.VisibleWhenField;
            SerializedProperty condition =
                string.IsNullOrWhiteSpace(visibilityField)
                    ? null
                    : serializedObject.FindProperty(
                        visibilityField);
            if (condition == null)
            {
                return;
            }

            Action refresh = () =>
            {
                field.style.display =
                    MatchesVisibility(
                        condition,
                        entry.Parameter.VisibleWhenValue)
                        ? DisplayStyle.Flex
                        : DisplayStyle.None;
            };
            refresh();
            field.schedule.Execute(refresh).Every(100);
        }

        private static bool ShouldExpandGroup(string groupName)
        {
            return !string.Equals(
                groupName,
                "表现接口",
                StringComparison.Ordinal);
        }

        private static bool MatchesVisibility(
            SerializedProperty condition,
            int expected)
        {
            if (condition == null)
            {
                return true;
            }

            if (condition.propertyType ==
                SerializedPropertyType.Boolean)
            {
                return (condition.boolValue ? 1 : 0) == expected;
            }

            return condition.intValue == expected;
        }

        private static void AddRuntimeDebug(
            VisualElement root,
            BlockFeature feature)
        {
            Foldout debug = ProjectInspectorUtility.CreateFoldout(
                "运行时调试",
                false);
            root.Add(debug);

            var values = new List<BlockDebugValue>();
            feature.CollectDebugValues(values);
            for (int index = 0; index < values.Count; index++)
            {
                BlockDebugValue value = values[index];
                DebugValueEntry entry = new DebugValueEntry(
                    feature,
                    index,
                    value?.Label ?? "状态");
                debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                    entry.Label,
                    entry.Read));
            }

            var actions = new List<BlockDebugAction>();
            feature.CollectDebugActions(actions);
            if (actions.Count == 0)
            {
                debug.Add(new Label("暂无可执行的调试操作。"));
                return;
            }

            debug.Add(ProjectInspectorUtility.CreateDivider());
            for (int index = 0; index < actions.Count; index++)
            {
                BlockDebugAction action = actions[index];
                if (action == null)
                {
                    continue;
                }

                var button = new Button(action.Action)
                {
                    text = action.Label
                };
                button.SetEnabled(action.Enabled);
                button.style.marginBottom = 3f;
                debug.Add(button);
            }
        }

        private sealed class DebugValueEntry
        {
            private readonly BlockFeature feature;
            private readonly int index;

            public DebugValueEntry(
                BlockFeature owner,
                int valueIndex,
                string label)
            {
                feature = owner;
                index = valueIndex;
                Label = label;
            }

            public string Label { get; }

            public string Read()
            {
                if (feature == null || !feature)
                {
                    return "组件已销毁";
                }

                var values = new List<BlockDebugValue>();
                feature.CollectDebugValues(values);
                return index >= 0 && index < values.Count
                    ? values[index]?.Value?.ToString() ?? "无"
                    : "无";
            }
        }
    }
}
