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
            root.Add(new HelpBox("编辑模式显示静止雨滴，进入 Play 后下落；选中物体可看到雨区边框。", HelpBoxMessageType.Info));

            var main = new Foldout { text = "雨滴与范围", value = true };
            Field(main, "color", "雨滴颜色");
            Field(main, "rainMaterial", "雨滴材质");
            Field(main, "areaWidth", "下雨宽度（世界单位）");
            Field(main, "areaHeight", "下落高度（世界单位）");
            Field(main, "dropletSize", "雨滴大小（世界单位）");
            Field(main, "sizeVariation", "大小随机幅度");
            Field(main, "density", "密度（每单位宽度）");
            Field(main, "fallSpeed", "下落速度（单位/秒）");
            Field(main, "speedVariation", "速度随机幅度");
            Field(main, "frameRate", "四帧切换速度（帧/秒）");
            root.Add(main);

            var previewCount = new Label();
            previewCount.style.marginTop = 4;
            previewCount.style.marginBottom = 8;
            root.Add(previewCount);

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
            void RefreshCount()
            {
                serializedObject.UpdateIfRequiredOrScript();
                SerializedProperty width = serializedObject.FindProperty("areaWidth");
                SerializedProperty density = serializedObject.FindProperty("density");
                previewCount.text = width.hasMultipleDifferentValues || density.hasMultipleDifferentValues
                    ? "静态预览：多选对象分别计算雨滴数量"
                    : $"静态预览雨滴数：{Mathf.Clamp(Mathf.RoundToInt(width.floatValue * density.floatValue), 0, 512)}（宽度 × 密度，最多 512）";
            }

            RefreshCount();
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshCount();
                foreach (Object item in targets)
                {
                    if (item is RainSpriteField rain) rain.RefreshPreview();
                }
                SceneView.RepaintAll();
            });
            root.Bind(serializedObject);
            return root;
        }

        private void Field(VisualElement parent, string name, string label)
        {
            parent.Add(new PropertyField(serializedObject.FindProperty(name), label));
        }
    }
}
