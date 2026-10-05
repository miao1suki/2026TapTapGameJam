using System;
using System.Collections.Generic;
using Project.Editor;
using Project.Interactions.Editor;
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
            color.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "interactionDefinition",
                "物体交互定义",
                "同色不同物体可以拥有不同定义；颜色只作为属性。"));
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
                "基础颜色是物体属性；运行时颜色仍由颜色系统维护，交互逻辑由物体交互管理器维护。"));
            root.Add(new Button(() =>
            {
                InteractionManagerWindow.OpenForObject(block.gameObject);
            })
            {
                text = "打开物体交互管理器"
            });
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
                "HSV 颜色渐变服务"));
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
