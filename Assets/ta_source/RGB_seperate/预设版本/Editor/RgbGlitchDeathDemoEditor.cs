using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TapTap.TA.RgbGlitch.Editor
{
    [CustomEditor(typeof(RgbGlitchDeathDemo))]
    [CanEditMultipleObjects]
    public sealed class RgbGlitchDeathDemoEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.Add(new Label("死亡与复活演示")
            {
                style = { fontSize = 16f, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6f }
            });

            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("仅用于演示场景，不要放入游戏预设体。Play 后先显示正常画面；2 秒时模拟死亡，4 秒时模拟复活并渐退。流程只播放一次。",
                HelpBoxMessageType.Info));
            root.Bind(serializedObject);
            return root;
        }
    }
}
