using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Rain.Editor
{
    [CustomEditor(typeof(RainSpriteField))]
    [CanEditMultipleObjects]
    public sealed class RainSpriteFieldEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 6;
            root.Add(new Label("像素雨滴")
            {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6 }
            });

            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("运行后在本地 XY 面片内循环下落。场景视图选中物体可看到范围边框。", HelpBoxMessageType.Info));

            var main = new Foldout { text = "雨滴与范围", value = true };
            Field(main, "color", "雨滴颜色");
            Field(main, "rainMaterial", "雨滴材质");
            Field(main, "areaWidth", "下雨宽度（世界单位）");
            Field(main, "areaHeight", "下落高度（世界单位）");
            Field(main, "dropletSize", "雨滴大小（世界单位）");
            Field(main, "density", "密度（每单位宽度）");
            Field(main, "fallSpeed", "下落速度（单位/秒）");
            Field(main, "frameRate", "四帧切换速度（帧/秒）");
            root.Add(main);

            var variation = new Foldout { text = "随机性与消失", value = true };
            Field(variation, "randomness", "随机性");
            Field(variation, "reachBottomChance", "到底概率");
            Field(variation, "noiseStrength", "噪波影响");
            Field(variation, "noiseScale", "噪波尺度");
            root.Add(variation);

            var advanced = new Foldout { text = "高级", value = false };
            Field(advanced, "randomSeed", "随机种子");
            root.Add(advanced);

            var material = serializedObject.FindProperty("rainMaterial");
            var warning = new HelpBox("请指定使用 RainSpriteField Shader 的材质。", HelpBoxMessageType.Warning);
            warning.style.display = material.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
            root.Add(warning);
            root.schedule.Execute(() => warning.style.display = material.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None).Every(150);
            root.Bind(serializedObject);
            return root;
        }

        private void Field(VisualElement parent, string name, string label)
        {
            parent.Add(new PropertyField(serializedObject.FindProperty(name), label));
        }
    }
}
