using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossAnimationDriver))]
public sealed class BossAnimationDriverEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss 动画接口"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "只订阅 BossController 状态并转发 Animator/动画事件；没有 Animator 时 Boss 逻辑仍然正常。"));

        Foldout animator = ProjectInspectorUtility.CreateFoldout(
            "Animator 接口",
            true);
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "animator",
            "Animator"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "driveAnimator",
            "驱动 Animator 参数"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "waitForPresentationDeathTrigger",
            "死亡动画等待演出触发"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "chaseBoolParameter",
            "追击布尔参数"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hitTriggerParameter",
            "受击 Trigger"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "deathTriggerParameter",
            "死亡 Trigger"));
        animator.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpTriggerParameter",
            "跳跃 Trigger"));
        root.Add(animator);

        Foldout dissolve = ProjectInspectorUtility.CreateFoldout(
            "RGB 死亡溶解",
            true);
        PropertyField dissolveEnabled =
            ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "enableRgbDeathDissolve",
                "启用 RGB 溶解");
        dissolve.Add(dissolveEnabled);

        var dissolveSettings = new VisualElement();
        dissolveSettings.Add(
            ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "rgbDeathDissolveMaterial",
                "溶解材质（留空使用默认）"));
        dissolveSettings.Add(
            ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "rgbDeathDissolveSpeed",
                "消散速度",
                "速度越大，Boss 越快分解消失。默认约 1.54 秒完成。"));
        dissolveSettings.Add(
            ProjectInspectorUtility.CreateProperty(
                serializedObject,
                "rgbDeathHorizontalScatter",
                "横向分散程度",
                "每块碎片在 Boss 局部坐标中的最大横向位移。"));
        bool dissolveIsEnabled = serializedObject
            .FindProperty("enableRgbDeathDissolve")
            .boolValue;
        dissolveSettings.SetEnabled(dissolveIsEnabled);
        dissolveEnabled.RegisterCallback<
            SerializedPropertyChangeEvent>(_ =>
            {
                SerializedProperty enabledProperty =
                    serializedObject.FindProperty(
                        "enableRgbDeathDissolve");
                dissolveSettings.SetEnabled(
                    enabledProperty != null &&
                    enabledProperty.boolValue);
            });

        var preview = new Button(() =>
        {
            if (targets.Length == 1 &&
                target is BossAnimationDriver driver)
            {
                driver.PreviewRgbDeathDissolve();
            }
        })
        {
            text = "预览 RGB 分解消散"
        };
        preview.tooltip = "进入 Play Mode 后播放并自动恢复，可调整参数后重试。";
        preview.style.marginTop = 8f;
        preview.style.height = 26f;
        preview.SetEnabled(Application.isPlaying && targets.Length == 1);
        preview.schedule.Execute(() =>
        {
            preview.SetEnabled(Application.isPlaying && targets.Length == 1);
        }).Every(200);
        dissolveSettings.Add(preview);
        dissolveSettings.Add(ProjectInspectorUtility.CreateHelp(
            "在 Play Mode 中点击预览；结束后会自动恢复 Boss 原图。调整速度或分散程度后可再次播放。"));
        dissolve.Add(dissolveSettings);
        root.Add(dissolve);

        Foldout events = ProjectInspectorUtility.CreateFoldout(
            "动作事件",
            true);
        events.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "stateChanged",
            "状态变化事件"));
        events.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hitStarted",
            "受击开始事件"));
        events.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "deathStarted",
            "死亡开始事件"));
        events.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpStarted",
            "跳跃开始事件"));
        root.Add(events);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
