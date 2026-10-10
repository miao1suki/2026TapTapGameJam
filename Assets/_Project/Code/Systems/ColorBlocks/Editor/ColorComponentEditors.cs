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
                "管理基础外观变白、纯白交接与正式外观恢复，不修改物体功能状态。"));

            Foldout timing = ProjectInspectorUtility.CreateFoldout(
                "默认渐变",
                true);
            timing.Add(ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "defaultDuration",
                "默认时间",
                "未单独指定时长时的颜色渐变时间。"));
            root.Add(timing);
            timing.Add(ProjectInspectorUtility.CreateProperty(serializedObject,
                "durationMultiplier", "指定时长倍率", "钥匙等入口指定的渐变时长乘以此倍率；立即切换不受影响。"));

            Foldout pipeline = ProjectInspectorUtility.CreateFoldout("渲染状态", true);
            var pipelineLabel = new Label();
            var passLabel = new Label();
            var valuesLabel = new Label();
            pipeline.Add(pipelineLabel);
            pipeline.Add(passLabel);
            pipeline.Add(valuesLabel);
            root.Add(pipeline);
            var outline = new Toggle("像素描边");
            outline.RegisterValueChangedCallback(evt => Project.Pixelization.PixelizationControl.SetOutlineEnabled(evt.newValue));
            pipeline.Add(outline);
            void RefreshStatus()
            {
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                outline.SetEnabled(UnityEngine.Application.isPlaying);
                outline.SetValueWithoutNotify(Project.Pixelization.PixelizationControl.OutlineEnabled);
                pipelineLabel.text = "当前管线：" + (asset != null ? asset.name : "内置管线");
                if (!UnityEngine.Application.isPlaying)
                {
                    passLabel.text = "全屏通道：待运行";
                    valuesLabel.text = "";
                    return;
                }
                int frame = SelectiveHsvRendererFeature.LastExecutedFrame;
                passLabel.text = "全屏通道：" + (frame >= UnityEngine.Time.frameCount - 2 && frame >= 0
                    ? "运行中" : (UnityEditor.EditorApplication.isPaused ? "暂停" : "未执行"));
                var manager = target as HSVColorFadeManager;
                if (manager == null) return;
                valuesLabel.text = $"红  S {manager.GetSaturation("red"):F2} · 白 {manager.GetWhiteAmount("red"):F2}\n" +
                    $"绿  S {manager.GetSaturation("green"):F2} · 白 {manager.GetWhiteAmount("green"):F2}\n" +
                    $"蓝  S {manager.GetSaturation("blue"):F2} · 白 {manager.GetWhiteAmount("blue"):F2}";
            }
            RefreshStatus();
            root.schedule.Execute(RefreshStatus).Every(200);

            ProjectInspectorUtility.Bind(root, serializedObject);
            return root;
        }
    }
    [CustomEditor(typeof(ColorAppearanceManager))]
    public sealed class ColorAppearanceManagerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ProjectInspectorUtility.CreateRoot(serializedObject);
            root.Add(ProjectInspectorUtility.CreateTitle("颜色表现管理器"));
            root.Add(ProjectInspectorUtility.CreateScriptField(serializedObject));
            root.Add(ProjectInspectorUtility.CreateProperty(serializedObject, "vineRevealShader", "藤蔓透明着色器",
                "留空使用内置藤蔓透明表现资源，不修改 TA 源材质。"));
            var status = ProjectInspectorUtility.CreateFoldout("分类表现", true);
            var label = new Label();
            status.Add(label);
            root.Add(status);
            void Refresh()
            {
                var manager = target as ColorAppearanceManager;
                if (manager == null) return;
                label.text = $"共享材质：{manager.SharedMaterialCount} · 水体：{manager.WaterCount} · 藤蔓：{manager.VineCount}\n";
                foreach (string id in new[] { "red", "green", "blue" })
                {
                    string name = id == "red" ? "红" : id == "green" ? "绿" : "蓝";
                    label.text += $"{name} · 基础外观 {manager.GetBaseOpacity(id):F2} · 正式外观 {manager.GetRealOpacity(id):F2}\n";
                }
            }
            Refresh();
            root.schedule.Execute(Refresh).Every(200);
            return root;
        }
    }

}
