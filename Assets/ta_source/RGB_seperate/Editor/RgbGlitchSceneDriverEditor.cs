using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TapTap.TA.RgbGlitch.Editor
{
    [CustomEditor(typeof(RgbGlitchSceneDriver))]
    [CanEditMultipleObjects]
    public sealed class RgbGlitchSceneDriverEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.Add(new Label("全屏 RGB 故障")
            {
                style = { fontSize = 16f, unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 6f }
            });

            var script = new PropertyField(serializedObject.FindProperty("m_Script"), "脚本");
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new HelpBox("标准玩家已绑定死亡与复活完成事件。仅当前主驱动控制全屏效果；旧场景演示 Rig 不会抢占玩家。Boss 惊恐须显式调用 BeginBossFear，不自动开启。",
                HelpBoxMessageType.Info));

            var control = new Foldout { text = "触发与目标", value = true };
            control.Add(new PropertyField(serializedObject.FindProperty("targetPlayer"), "玩家（留空则自动查找）"));
            control.Add(new PropertyField(serializedObject.FindProperty("fearLevel"), "惊恐强度"));
            control.Add(new PropertyField(serializedObject.FindProperty("deathFadeSeconds"), "死亡增强时长（秒）"));
            control.Add(new PropertyField(serializedObject.FindProperty("respawnFadeSeconds"), "复活恢复时长（秒）"));
            root.Add(control);

            var tuning = new Foldout { text = "画面参数", value = true };
            tuning.Add(new PropertyField(serializedObject.FindProperty("vignetteDarkness"), "暗角黑度"));
            tuning.Add(new PropertyField(serializedObject.FindProperty("shakeRate"), "抖动速率（次/秒）"));
            tuning.Add(new PropertyField(serializedObject.FindProperty("shakeStrength"), "抖动幅度（屏幕比例）"));
            tuning.Add(new PropertyField(serializedObject.FindProperty("colorSeparationPixels"), "色彩分离（像素）"));
            tuning.Add(new PropertyField(serializedObject.FindProperty("colorSeparationJitter"), "色彩分离跳动（0～1）"));
            root.Add(tuning);

            var boss = new Foldout { text = "Boss 显式调用预设（默认关闭）", value = false };
            boss.Add(new PropertyField(serializedObject.FindProperty("bossFearLevel"), "惊恐强度"));
            boss.Add(new PropertyField(serializedObject.FindProperty("bossVignetteDarkness"), "暗角黑度"));
            boss.Add(new PropertyField(serializedObject.FindProperty("bossShakeRate"), "抖动速率（次/秒）"));
            boss.Add(new PropertyField(serializedObject.FindProperty("bossShakeStrength"), "抖动幅度（屏幕比例）"));
            boss.Add(new PropertyField(serializedObject.FindProperty("bossColorSeparationPixels"), "色彩分离（像素）"));
            boss.Add(new PropertyField(serializedObject.FindProperty("bossColorSeparationJitter"), "色彩分离跳动（0～1）"));
            root.Add(boss);

            var test = new Foldout { text = "运行时测试", value = false };
            test.Add(new HelpBox("进入 Play 后可手动测试；玩家死亡与重生仍会自动触发。",
                HelpBoxMessageType.Info));
            var driver = target as RgbGlitchSceneDriver;
            var deathButton = new Button(() =>
            {
                if (Application.isPlaying && driver != null && driver.isActiveAndEnabled)
                    driver.PlayDeath();
            }) { text = "播放死亡强化" };
            var respawnButton = new Button(() =>
            {
                if (Application.isPlaying && driver != null && driver.isActiveAndEnabled)
                    driver.PlayRespawn();
            }) { text = "播放复活恢复" };
            deathButton.SetEnabled(Application.isPlaying && targets.Length == 1 && driver != null && driver.isActiveAndEnabled);
            respawnButton.SetEnabled(Application.isPlaying && targets.Length == 1 && driver != null && driver.isActiveAndEnabled);
            test.Add(deathButton);
            test.Add(respawnButton);
            root.Add(test);

            root.Bind(serializedObject);
            return root;
        }
    }
}
