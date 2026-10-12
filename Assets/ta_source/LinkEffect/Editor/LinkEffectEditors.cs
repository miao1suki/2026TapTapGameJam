using TaSource.LinkEffect;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaSource.LinkEffect.EditorUI
{
    [CustomEditor(typeof(InteractionLinkEffect))]
    [CanEditMultipleObjects]
    internal sealed class InteractionLinkEffectEditor : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.style.paddingLeft = 6;
            root.style.paddingRight = 6;
            root.Add(new Label("交互像素连线预览")
            {
                style = { fontSize = 16, unityFontStyleAndWeight = FontStyle.Bold }
            });

            Foldout source = new Foldout { text = "目标与颜色", value = true };
            Add(source, "sensor", "玩家交互检测器");
            Add(source, "colorWheel", "玩家当前选色");
            Add(source, "colorCatalog", "颜色目录");
            Add(source, "targetMask", "目标检测层");
            Add(source, "scanInterval", "扫描间隔（秒）");
            Add(source, "maxLinks", "最多显示线条数");
            Add(source, "sourceAnchor", "链条起点锚点");
            Add(source, "sourceOffset", "无锚点时起点偏移");
            root.Add(source);

            Foldout visual = new Foldout { text = "像素线表现", value = true };
            Add(visual, "linkMaterial", "像素线材质");
            Add(visual, "width", "线条宽度");
            Add(visual, "curveSegments", "曲线段数");
            Add(visual, "arcHeight", "弧度高度");
            Add(visual, "waveHeight", "摆动幅度");
            Add(visual, "cameraFacingOffset", "朝相机偏移");
            Add(visual, "opacity", "透明度");
            root.Add(visual);

            Foldout testing = new Foldout { text = "测试设置", value = false };
            Add(testing, "previewWhenNoColor", "无选色时启用测试色");
            PropertyField testColor = Add(testing, "previewColorId", "测试颜色 ID");
            root.Add(testing);

            Foldout state = new Foldout { text = "运行状态（只读）", value = true };
            Label stateText = new Label();
            state.Add(stateText);
            root.Add(state);
            root.schedule.Execute(() =>
            {
                if (target == null) return;
                testColor.style.display = serializedObject
                    .FindProperty("previewWhenNoColor").boolValue
                    ? DisplayStyle.Flex : DisplayStyle.None;
                if (targets.Length != 1)
                {
                    stateText.text = "多选时不显示运行状态";
                    return;
                }

                InteractionLinkEffect effect = target as InteractionLinkEffect;
                stateText.text = Application.isPlaying && effect != null
                    ? $"颜色：{effect.ActiveColorId}\n首个目标：{effect.CurrentTargetName}\n" +
                      $"识别：{effect.DetectedTargetCount}；显示：{effect.VisibleLinkCount}\n" +
                      $"可行：{(effect.IsInteractionFeasible ? "是" : "否")}；" +
                      $"线条：{(effect.IsLinkVisible ? "显示" : "隐藏")}"
                    : "进入 Play Mode 后显示目标与线条状态";
            }).Every(250);
            return root;
        }

        private PropertyField Add(VisualElement parent, string name, string label)
        {
            PropertyField field = new PropertyField(
                serializedObject.FindProperty(name), label);
            parent.Add(field);
            return field;
        }
    }

}
