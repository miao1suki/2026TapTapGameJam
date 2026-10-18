using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.FlowMagma.Editor
{
    [CustomEditor(typeof(FlowMagmaVisual))]
    [CanEditMultipleObjects]
    public sealed class FlowMagmaVisualEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;
            root.Add(new Label("流动岩浆与喷溅")
            {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
            });
            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("编辑态显示静态波面与实心颗粒；运行时持续随机间隔喷溅，偶尔连续喷出两三颗。每格16美术像素，颗粒尺寸和边缘均对齐世界像素网格。只改变视觉，不处理碰撞或伤害。", HelpBoxMessageType.Info));

            var flow = new Foldout { text = "流动与外形", value = true };
            Add(flow, "magmaMaterial", "岩浆材质");
            Add(flow, "width", "宽度（世界单位）");
            Add(flow, "height", "高度（世界单位）");
            Add(flow, "flowSpeed", "横向流动速度（循环/秒）");
            Add(flow, "waveAmplitude", "表面上下波幅（格）");
            Add(flow, "verticalWarpStrength", "上下纹理扭曲（0～0.03）");
            Add(flow, "waveFrequency", "表面波数");
            root.Add(flow);

            var eruption = new Foldout { text = "喷发", value = true };
            Add(eruption, "particlesPerSecond", "平均喷出速率（次/秒/表面格）");
            Add(eruption, "emissionIntervalRandomness", "喷出间隔随机幅度");
            Add(eruption, "clusterChance", "连喷多颗概率");
            Add(eruption, "launchSpeed", "喷出初速（单位/秒）");
            Add(eruption, "fallSpeed", "下坠加速度（单位/秒²）");
            Add(eruption, "shrinkingParticleChance", "逐渐缩小颗粒占比");
            Add(eruption, "largeParticleChance", "大颗粒占比");
            root.Add(eruption);

            var advanced = new Foldout { text = "粒子外观与高级设置", value = false };
            Add(advanced, "splashMaterial", "喷溅材质");
            Add(advanced, "splashFilter", "喷溅网格");
            Add(advanced, "splashRenderer", "喷溅渲染器");
            Add(advanced, "particleSize", "颗粒基准大小（格）");
            Add(advanced, "sizeRandomness", "大小随机幅度");
            Add(advanced, "trajectoryRandomness", "轨迹随机幅度");
            Add(advanced, "particleLifetime", "飞行寿命（秒）");
            Add(advanced, "settleChance", "落地堆积概率");
            Add(advanced, "settleDuration", "堆积消失时间（秒）");
            Add(advanced, "randomSeed", "随机种子");
            root.Add(advanced);

            var warning = new HelpBox("预制体需要岩浆和喷溅材质，以及喷溅子物体的 MeshFilter / MeshRenderer。", HelpBoxMessageType.Warning);
            root.Add(warning);
            void RefreshWarning()
            {
                serializedObject.UpdateIfRequiredOrScript();
                bool missing = serializedObject.FindProperty("magmaMaterial").objectReferenceValue == null
                    || serializedObject.FindProperty("splashMaterial").objectReferenceValue == null
                    || serializedObject.FindProperty("splashFilter").objectReferenceValue == null
                    || serializedObject.FindProperty("splashRenderer").objectReferenceValue == null;
                warning.style.display = missing ? DisplayStyle.Flex : DisplayStyle.None;
            }
            RefreshWarning();
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshWarning();
                foreach (Object item in targets)
                    if (item is FlowMagmaVisual visual) visual.RefreshPreview();
                SceneView.RepaintAll();
            });
            root.Bind(serializedObject);
            return root;
        }

        private void Add(VisualElement parent, string property, string label)
        {
            parent.Add(new PropertyField(serializedObject.FindProperty(property), label));
        }
    }

    [CustomEditor(typeof(ReactionArtworkSettings))]
    public sealed class ReactionArtworkSettingsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new Label("反应视觉配置") { style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold } });
            var section = new Foldout { text = "共享预制体", value = true };
            section.Add(new PropertyField(serializedObject.FindProperty("magmaPrefab"), "流动岩浆"));
            section.Add(new PropertyField(serializedObject.FindProperty("coolingSteamPrefab"), "冷却蒸汽"));
            root.Add(section);
            root.Add(new HelpBox("岩浆功能自动读取本配置；不需要重建作者场景。视觉不更改碰撞、伤害、颜色进度或房间复原。", HelpBoxMessageType.Info));
            root.Bind(serializedObject); return root;
        }
    }
}
