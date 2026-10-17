using Project.BossLogic;
using Project.Editor;
using UnityEditor;
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
