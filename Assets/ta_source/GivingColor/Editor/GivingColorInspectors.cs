using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TaSource.GivingColor.Editor
{
    [CustomEditor(typeof(GivingColorEffectController))]
    internal sealed class GivingColorEffectControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(Title("三色染色覆盖效果"));
            root.Add(Field("overlayShader", "覆盖 Shader"));
            root.Add(Field("colorWheel", "玩家颜色轮盘"));
            root.Add(Field("interactionSensor", "玩家交互传感器"));

            var timing = new Foldout { text = "扩散速度", value = true };
            timing.Add(Field("redDuration", "红色扩散时长"));
            timing.Add(Field("blueDuration", "蓝色扩散时长"));
            timing.Add(Field("greenDuration", "绿色扩散时长"));
            timing.Add(new HelpBox("材质 GivingColorDye 的红、蓝、绿速度会分别缩短或延长上述基础时长；速度 1 为原时长。", HelpBoxMessageType.Info));
            root.Add(timing);

            var tuning = new Foldout { text = "高级效果参数", value = false };
            tuning.Add(Field("dyeStrength", "上色强度"));
            tuning.Add(Field("edgeWidth", "扩散边缘宽度"));
            tuning.Add(Field("noiseScale", "纹理噪波尺度"));
            tuning.Add(Field("fireGlow", "红色火焰亮边"));
            tuning.Add(Field("waterDiffusion", "蓝色水彩扩散"));
            tuning.Add(Field("greenBranchCount", "绿色主枝数量"));
            tuning.Add(Field("greenBranchWidth", "绿色枝条宽度"));
            tuning.Add(Field("alphaThreshold", "透明像素阈值"));
            root.Add(tuning);
            root.Add(new HelpBox("只在玩家染色成功之后播放；开局获得能力与颜色解锁不会触发。蒸汽和植物烧毁不叠加染色。覆盖层只绘制原纹理的非透明像素。", HelpBoxMessageType.Info));
            var status = new Label();
            status.style.marginTop = 8;
            root.Add(status);
            status.schedule.Execute(() =>
            {
                var controller = target as GivingColorEffectController;
                if (controller == null)
                    return;
                status.text = Application.isPlaying
                    ? $"运行状态：当前选色 {Display(controller.LastSelectedColorId)}；最近目标 {Display(controller.LastEffectTargetName)}"
                    : "运行状态：进入 Play Mode 后显示选色和目标信息。";
            }).Every(250);
            return root;
        }

        private PropertyField Field(string name, string label)
        {
            var field = new PropertyField(serializedObject.FindProperty(name), label);
            field.Bind(serializedObject);
            return field;
        }

        private static Label Title(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            label.style.fontSize = 15;
            label.style.marginBottom = 8;
            return label;
        }

        private static string Display(string value) =>
            string.IsNullOrWhiteSpace(value) ? "无" : value;
    }

    [CustomEditor(typeof(GivingColorEffectTarget))]
    internal sealed class GivingColorEffectTargetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var title = new Label("染色效果目标");
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.fontSize = 15;
            root.Add(title);
            var status = new Label();
            root.Add(status);
            status.schedule.Execute(() =>
            {
                var effect = target as GivingColorEffectTarget;
                status.text = effect == null
                    ? "状态：不可用"
                    : effect.IsPlaying
                        ? $"状态：扩散中，进度 {effect.Progress:P0}"
                        : $"状态：待机，当前进度 {effect.Progress:P0}";
            }).Every(150);
            root.Add(new HelpBox("由染色表现控制器临时创建。原材质保持不变；房间重置会清除覆盖层。", HelpBoxMessageType.Info));
            return root;
        }
    }

    [CustomEditor(typeof(GivingColorTestReceiver))]
    internal sealed class GivingColorTestReceiverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var title = new Label("染色演示接收器");
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.fontSize = 15;
            root.Add(title);
            root.Add(new HelpBox("仅用于 Test 场景。接受已解锁颜色并报告染色成功，不执行玩法反应。", HelpBoxMessageType.Info));
            return root;
        }
    }
}
