using System.Collections.Generic;
using Project.Editor;
using UnityEditor;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossTimelineClip))]
public sealed class BossTimelineClipInspector :
    ActorTimelineClipInspectorBase
{
    private enum Operation
    {
        Motion,
        SetTracking,
        Kill,
        Custom
    }

    private static readonly List<string> OperationNames =
        new List<string>
        {
            "角色位移",
            "设置追击",
            "杀死 Boss",
            "自定义组合"
        };

    private SerializedProperty motionMode;
    private SerializedProperty setTrackingOnStart;
    private SerializedProperty setTrackingOnEnd;
    private SerializedProperty useBossJumpState;
    private SerializedProperty killAtEnd;

    private PopupField<string> operationPopup;
    private VisualElement trackingStartGroup;
    private VisualElement trackingEndGroup;
    private VisualElement jumpStateGroup;
    private HelpBox jumpStateHelp;
    private Foldout trackingSection;
    private Foldout killSection;
    private Operation operation;

    protected override string Title => "Boss Timeline 片段";

    protected override string HelpText =>
        "控制 Boss 位移、跳跃和追击状态。轨道绑定优先，也可按 Boss Tag 查找。";

    protected override void AddOperationSelector(VisualElement root)
    {
        motionMode = serializedObject.FindProperty("motionMode");
        setTrackingOnStart = serializedObject.FindProperty(
            "setTrackingOnStart");
        setTrackingOnEnd = serializedObject.FindProperty(
            "setTrackingOnEnd");
        useBossJumpState = serializedObject.FindProperty(
            "useBossJumpState");
        killAtEnd = serializedObject.FindProperty("killAtEnd");

        operation = DetectOperation();
        operationPopup = new PopupField<string>(
            "本片段操作",
            OperationNames,
            (int)operation);
        operationPopup.tooltip =
            "一个片段建议只控制一个操作；高级组合时选择“自定义组合”。";
        operationPopup.RegisterValueChangedCallback(evt =>
        {
            operation = (Operation)OperationNames.IndexOf(evt.newValue);
            ApplyOperation();
            RefreshSpecificVisibility();
        });
        root.Add(operationPopup);
        root.Add(ProjectInspectorUtility.CreateHelp(
            "选择操作后，只显示这一类操作需要的参数。",
            HelpBoxMessageType.None));
    }

    protected override void AddSpecificSections(VisualElement root)
    {
        trackingSection = ProjectInspectorUtility.CreateFoldout(
            "设置追击",
            true);
        trackingSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "setTrackingOnStart",
            "片段开始时设置追击"));
        trackingStartGroup = new VisualElement();
        trackingStartGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "trackingOnStart",
            "开始时追击"));
        trackingSection.Add(trackingStartGroup);
        trackingSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "setTrackingOnEnd",
            "片段结束时设置追击"));
        trackingEndGroup = new VisualElement();
        trackingEndGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "trackingOnEnd",
            "结束时追击"));
        trackingSection.Add(trackingEndGroup);
        root.Add(trackingSection);

        jumpStateGroup = new VisualElement();
        jumpStateGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useBossJumpState",
            "跳跃使用 Boss Jump 状态",
            "开启后弧线跳跃交给 BossController，而不是直接写 Transform。"));
        jumpStateHelp = ProjectInspectorUtility.CreateHelp(
            "当前运动模式不是弧线跳跃，本项暂不生效。",
            HelpBoxMessageType.None);
        jumpStateGroup.Add(jumpStateHelp);
        root.Add(jumpStateGroup);

        killSection = ProjectInspectorUtility.CreateFoldout(
            "杀死 Boss",
            true);
        killSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "killAtEnd",
            "片段结束时杀死 Boss"));
        root.Add(killSection);
    }

    protected override void RefreshSpecificVisibility()
    {
        if (operationPopup != null)
        {
            operationPopup.SetValueWithoutNotify(
                OperationNames[(int)operation]);
        }

        bool custom = operation == Operation.Custom;
        bool motion = operation == Operation.Motion || custom;
        bool tracking = operation == Operation.SetTracking || custom;
        bool kill = operation == Operation.Kill || custom;
        SetCommonMotionVisible(motion);
        SetVisible(trackingSection, tracking);
        SetVisible(killSection, kill);

        SetVisible(
            trackingStartGroup,
            setTrackingOnStart != null &&
            setTrackingOnStart.boolValue);
        SetVisible(
            trackingEndGroup,
            setTrackingOnEnd != null &&
            setTrackingOnEnd.boolValue);

        bool jumpMotion = motion &&
                          motionMode != null &&
                          (ActorTimelineMotionMode)
                          motionMode.enumValueIndex ==
                          ActorTimelineMotionMode.JumpArc;
        SetVisible(jumpStateGroup, jumpMotion);
        if (jumpStateHelp != null && jumpMotion)
        {
            jumpStateHelp.text = useBossJumpState != null &&
                                 useBossJumpState.boolValue
                ? "跳跃会进入 BossController.Jump 状态，并触发 Jump 动画接口。"
                : "跳跃直接由 Timeline 驱动 Transform，不进入 Boss Jump 状态。";
        }
    }

    private Operation DetectOperation()
    {
        int count = 0;
        bool motion =
            motionMode.enumValueIndex !=
            (int)ActorTimelineMotionMode.None;
        bool tracking =
            setTrackingOnStart.boolValue ||
            setTrackingOnEnd.boolValue;
        bool kill = killAtEnd.boolValue;
        if (motion)
        {
            count++;
        }

        if (tracking)
        {
            count++;
        }

        if (kill)
        {
            count++;
        }

        if (count > 1)
        {
            return Operation.Custom;
        }

        if (tracking)
        {
            return Operation.SetTracking;
        }

        if (kill)
        {
            return Operation.Kill;
        }

        return Operation.Motion;
    }

    private void ApplyOperation()
    {
        if (operation == Operation.Custom)
        {
            return;
        }

        bool motion = operation == Operation.Motion;
        bool tracking = operation == Operation.SetTracking;
        bool kill = operation == Operation.Kill;
        if (motion)
        {
            if (motionMode.enumValueIndex ==
                (int)ActorTimelineMotionMode.None)
            {
                motionMode.enumValueIndex =
                    (int)ActorTimelineMotionMode.Linear;
            }
        }
        else
        {
            motionMode.enumValueIndex =
                (int)ActorTimelineMotionMode.None;
        }

        setTrackingOnStart.boolValue = tracking;
        setTrackingOnEnd.boolValue = tracking;
        killAtEnd.boolValue = kill;
        serializedObject.ApplyModifiedProperties();
    }
}
