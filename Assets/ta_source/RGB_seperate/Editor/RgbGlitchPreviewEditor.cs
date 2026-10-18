using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TapTap.TA.RgbGlitch.Editor
{
    [CustomEditor(typeof(RgbGlitchPreview))]
    [CanEditMultipleObjects]
    public sealed class RgbGlitchPreviewEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.Add(new Label("RGB 故障预览")
            {
                style = { fontSize = 16f, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6f }
            });

            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("编辑模式可查看场景图案；Play 后预览全屏故障。", HelpBoxMessageType.Info));

            var preview = new Foldout { text = "演示", value = true };
            AddField(preview, "fearLevel", "惊恐强度");
            AddField(preview, "automaticCycle", "自动循环死亡与复活");
            root.Add(preview);

            var tuning = new Foldout { text = "画面参数", value = true };
            AddField(tuning, "vignetteDarkness", "暗角黑度");
            AddField(tuning, "shakeRate", "抖动速率（次/秒）");
            AddField(tuning, "shakeStrength", "抖动幅度（屏幕比例）");
            AddField(tuning, "colorSeparationPixels", "色彩分离（像素）");
            AddField(tuning, "colorSeparationJitter", "色彩分离跳动（0～1）");
            root.Add(tuning);

            root.Bind(serializedObject);
            return root;
        }

        private void AddField(VisualElement parent, string propertyName, string label)
        {
            parent.Add(new PropertyField(serializedObject.FindProperty(propertyName), label));
        }
    }
}
