using System;
using System.Collections.Generic;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.ColorBlocks.Editor
{
    internal static class ColorInspectorChoices
    {
        public static List<string> Build()
        {
            var result = new List<string>();
            ColorCatalog catalog =
                AssetDatabase.LoadAssetAtPath<ColorCatalog>(
                    ColorProjectSetup.CatalogPath);
            if (catalog != null)
            {
                for (int index = 0;
                     index < catalog.Colors.Count;
                     index++)
                {
                    ColorTypeDefinition definition =
                        catalog.Colors[index];
                    if (definition != null &&
                        !string.IsNullOrWhiteSpace(definition.id) &&
                        !result.Contains(definition.id))
                    {
                        result.Add(definition.id);
                    }
                }
            }

            if (result.Count == 0)
            {
                result.AddRange(new[] { "red", "green", "blue" });
            }

            return result;
        }

        public static DropdownField Create(
            SerializedObject serializedObject,
            string propertyName,
            string label)
        {
            SerializedProperty property =
                serializedObject.FindProperty(propertyName);
            List<string> choices = Build();
            string current = property?.stringValue ?? string.Empty;
            if (!choices.Contains(current))
            {
                choices.Insert(0, current);
            }

            var field = new DropdownField(
                label,
                choices,
                Mathf.Max(0, choices.IndexOf(current)));
            field.RegisterValueChangedCallback(evt =>
            {
                property.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            return field;
        }
    }

    [CustomEditor(typeof(ColorBlock))]
    public sealed class ColorBlockEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "颜色方块"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));

            Foldout color = ProjectInspectorUtility.CreateFoldout(
                "颜色配置",
                true);
            color.Add(ColorInspectorChoices.Create(
                serializedObject,
                "baseColorTypeId",
                "基础颜色"));
            color.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "targetRenderer",
                "颜色渲染器",
                "留空时运行时自动寻找第一个子渲染器。"));
            root.Add(color);

            ColorBlock block = (ColorBlock)target;
            Foldout status = ProjectInspectorUtility.CreateFoldout(
                "当前状态",
                true);
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "当前颜色",
                () => block != null
                    ? block.CurrentColorTypeId
                    : "无"));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "水体状态",
                () => block != null && block.IsActiveWater
                    ? "水体生效"
                    : "非水体"));
            root.Add(status);

            root.Add(ProjectInspectorUtility.CreateHelp(
                "基础颜色用于身份和图层匹配；运行时颜色由颜色管理器控制。"));
            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(ColorKeyPickup))]
    public sealed class ColorKeyPickupEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "颜色钥匙"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));

            Foldout pickup = ProjectInspectorUtility.CreateFoldout(
                "拾取配置",
                true);
            pickup.Add(ColorInspectorChoices.Create(
                serializedObject,
                "colorTypeId",
                "解锁颜色"));
            pickup.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cameraDirector",
                "相机演出",
                "播放拉远与恢复镜头的 Timeline 导演。"));
            pickup.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "hideOnCollect",
                "拾取后隐藏"));
            root.Add(pickup);

            Foldout timing = ProjectInspectorUtility.CreateFoldout(
                "演出时序",
                true);
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "revealAtTimelineProgress",
                "解锁进度",
                "Timeline 播放到该进度时解锁颜色。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "revealDuration",
                "褪色恢复时间",
                "颜色从褪色状态恢复的时长。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "orthographicZoomOut",
                "镜头拉远倍率",
                "2D 正交镜头在演出中的拉远倍率。"));
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "cameraReturnDuration",
                "镜头返回时间"));
            root.Add(timing);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(ColorWorldManager))]
    public sealed class ColorWorldManagerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "颜色世界管理器"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "管理颜色解锁、变色能力与水体聚合；材质渐变由 HSV 管理器负责。"));

            Foldout status = ProjectInspectorUtility.CreateFoldout(
                "运行时状态",
                true);
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "实例状态",
                () => ColorWorldManager.Existing != null
                    ? "已创建"
                    : "未创建"));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "变色能力",
                () => ColorWorldManager.Existing != null &&
                       ColorWorldManager.Existing.HasRecolorAbility
                    ? "已解锁"
                    : "未解锁"));
            status.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "颜色目录",
                () =>
                {
                    ColorCatalog catalog =
                        Resources.Load<ColorCatalog>(
                            "ColorBlocks/ColorCatalog");
                    return catalog != null ? "已加载" : "缺失";
                }));
            root.Add(status);

            Foldout test = ProjectInspectorUtility.CreateFoldout(
                "本次 Play 测试",
                true);
            test.Add(ProjectInspectorUtility.CreateHelp(
                "仅修改当前 Play 运行状态，不写入场景、颜色目录或存档。" +
                "全局检测颜色目录中的所有颜色，后续新增颜色会自动出现；" +
                "正常颜色钥匙和交互图解锁流程保持不变。"));

            ColorCatalog catalog =
                Resources.Load<ColorCatalog>(
                    "ColorBlocks/ColorCatalog");
            if (catalog == null || catalog.Colors.Count == 0)
            {
                test.Add(new Label("颜色目录为空，无法创建测试开关。"));
                root.Add(test);
                ProjectInspectorUtility.Bind(root, serializedObject);
                return root;
            }

            var entries = new List<(string Id, Toggle Toggle)>();
            for (int index = 0;
                 index < catalog.Colors.Count;
                 index++)
            {
                ColorTypeDefinition definition =
                    catalog.Colors[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.id))
                {
                    continue;
                }

                string colorId = definition.id;
                string label = string.IsNullOrWhiteSpace(
                    definition.displayName)
                    ? colorId
                    : $"{definition.displayName} ({colorId})";
                var toggle = new Toggle(label);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (!Application.isPlaying)
                    {
                        return;
                    }

                    ColorWorldManager manager =
                        ColorWorldManager.Existing;
                    manager?.SetUnlockedForCurrentSession(
                        colorId,
                        evt.newValue);
                });
                entries.Add((colorId, toggle));
                test.Add(toggle);
            }

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 6f;
            var unlockAll = new Button(() =>
            {
                ColorWorldManager.Existing?
                    .SetAllUnlockedForCurrentSession(true);
            })
            {
                text = "全部解锁"
            };
            var lockAll = new Button(() =>
            {
                ColorWorldManager.Existing?
                    .SetAllUnlockedForCurrentSession(false);
            })
            {
                text = "全部锁上"
            };
            unlockAll.style.flexGrow = 1f;
            lockAll.style.flexGrow = 1f;
            actions.Add(unlockAll);
            actions.Add(lockAll);
            test.Add(actions);

            var availability = new Label();
            availability.style.marginTop = 4f;
            availability.style.opacity = .75f;
            test.Add(availability);
            test.Add(ProjectInspectorUtility.CreateReadOnlyRow(
                "检测范围",
                () => $"颜色目录全部 {entries.Count} 种颜色"));
            Action refresh = () =>
            {
                ColorWorldManager manager =
                    ColorWorldManager.Existing;
                bool available = Application.isPlaying &&
                                 manager != null;
                availability.text = available
                    ? "当前 Play 中可直接切换"
                    : "进入 Play Mode 后可用";
                unlockAll.SetEnabled(available);
                lockAll.SetEnabled(available);
                for (int index = 0;
                     index < entries.Count;
                     index++)
                {
                    (string id, Toggle toggle) = entries[index];
                    toggle.SetEnabled(available);
                    toggle.SetValueWithoutNotify(
                        available && manager.IsUnlocked(id));
                }
            };
            refresh();
            test.schedule.Execute(refresh).Every(100);
            root.Add(test);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }

    [CustomEditor(typeof(HSVColorFadeManager))]
    public sealed class HSVColorFadeManagerEditor :
        UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            VisualElement root = ProjectInspectorUtility.CreateRoot(
                serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle(
                "HSV 颜色渐变管理器"));
            root.Add(ProjectInspectorUtility.CreateScriptField(
                serializedObject));
            root.Add(ProjectInspectorUtility.CreateHelp(
                "只负责屏幕 HSV 饱和度，不修改方块属性或材质。"));

            Foldout timing = ProjectInspectorUtility.CreateFoldout(
                "默认渐变",
                true);
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultDuration",
                "默认时间",
                "未单独指定时长时的颜色渐变时间。"));
            root.Add(timing);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
}
