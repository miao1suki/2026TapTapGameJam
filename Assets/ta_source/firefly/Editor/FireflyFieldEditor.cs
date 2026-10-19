using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Fireflies.Editor
{
    [CustomEditor(typeof(FireflyField)), CanEditMultipleObjects]
    public sealed class FireflyFieldEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.Add(new Label("萤火虫区域")
            {
                style = { fontSize = 17, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 }
            });
            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("中性美术，不随颜色解锁。硬光点固定为 1/16 格；柔光用于照明，编辑时静态预览。", HelpBoxMessageType.Info));
            var main = Group(root, "范围与外观", true);
            Field(main, "areaSize", "活动范围（格）");
            Field(main, "fireflyCount", "萤火虫数量");
            Field(main, "renderDepthCells", "可见平面（格）");
            Field(main, "haloColor", "光晕颜色");
            Field(main, "pixelColor", "光点颜色");
            Field(main, "glowRadius", "光晕半径（格）");
            Field(main, "glowIntensity", "光晕强度");
            var movement = Group(root, "游动与群聚", false);
            Field(movement, "flightSpeed", "常态速度（格/秒）");
            Field(movement, "flightPrecision", "转向灵敏度");
            Field(movement, "flowSpeed", "噪声变化速度");
            Field(movement, "wanderStrength", "独立游走强度");
            Field(movement, "flockStrength", "邻近群聚强度");
            var flee = Group(root, "靠近与驱散", false);
            Field(flee, "threat", "靠近对象（可空）");
            Field(flee, "findPlayerByTag", "自动寻找玩家");
            Field(flee, "detectionRadius", "感知半径（格）");
            Field(flee, "fleeSpeed", "受惊速度（格/秒）");
            Field(flee, "calmDownSeconds", "平复时间（秒）");
            var pulse = Group(root, "光晕呼吸", false);
            Field(pulse, "minimumPulseSize", "最小光晕比例");
            Field(pulse, "maximumPulseSize", "最大光晕比例");
            Field(pulse, "pulseSpeed", "呼吸频率");
            var lights = Group(root, "实时补光", false);
            lights.Add(new HelpBox("默认关闭，最多 4 盏无阴影点光。仅对受光材质有效，移动端建议保持关闭。", HelpBoxMessageType.Info));
            Field(lights, "realtimeLightCount", "点光数量");
            Field(lights, "lightIntensity", "补光强度");
            Field(lights, "lightRange", "补光范围（格）");
            Field(lights, "illuminatedLayers", "受光物体层级");
            var advanced = Group(root, "固定种子与材质", false);
            Field(advanced, "layoutSeed", "布局种子");
            Field(advanced, "motionSeed", "游动种子");
            Field(advanced, "pulseSeed", "呼吸种子");
            Field(advanced, "haloMaterial", "柔光材质");
            Field(advanced, "pixelMaterial", "硬光点材质");
            root.Add(new Button(FireflyRegionWindow.Open) { text = "打开萤火虫区域创建", style = { marginTop = 8 } });
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                foreach (Object item in targets) if (item is FireflyField field) field.RefreshPreview();
                SceneView.RepaintAll();
            });
            root.Bind(serializedObject);
            return root;
        }

        private static Foldout Group(VisualElement parent, string title, bool open)
        {
            var foldout = new Foldout { text = title, value = open };
            foldout.style.marginTop = 8;
            parent.Add(foldout);
            return foldout;
        }

        private void Field(VisualElement parent, string property, string label) =>
            parent.Add(new PropertyField(serializedObject.FindProperty(property), label));
    }
}
