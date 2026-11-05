using System.Collections.Generic;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(PlayerTimelineClip))]
public sealed class PlayerTimelineClipInspector :
    ActorTimelineClipInspectorBase
{
    private enum Operation
    {
        Motion,
        MoveInput,
        Sprint,
        JumpHeld,
        RequestJump,
        LockControl,
        Custom
    }

    private static readonly List<string> OperationNames =
        new List<string>
        {
            "角色位移",
            "设置移动输入",
            "设置冲刺",
            "设置按住跳跃",
            "请求跳跃",
            "锁住玩家控制",
            "自定义组合"
        };

    private SerializedProperty motionMode;
    private SerializedProperty applyMoveInput;
    private SerializedProperty applySprint;
    private SerializedProperty applyJumpHeld;
    private SerializedProperty requestJump;
    private SerializedProperty lockControlWhileClip;

    private PopupField<string> operationPopup;
    private Foldout moveInputSection;
    private Foldout sprintSection;
    private Foldout jumpHeldSection;
    private Foldout jumpSection;
    private Foldout controlSection;
    private Operation operation;

    protected override string Title => "玩家 Timeline 片段";

    protected override string HelpText =>
        "控制玩家位移、输入与跳跃。轨道绑定优先，也可按 Player Tag 查找。";

    protected override void AddOperationSelector(VisualElement root)
    {
        motionMode = serializedObject.FindProperty("motionMode");
        applyMoveInput = serializedObject.FindProperty(
            "applyMoveInput");
        applySprint = serializedObject.FindProperty("applySprint");
        applyJumpHeld = serializedObject.FindProperty("applyJumpHeld");
        requestJump = serializedObject.FindProperty("requestJump");
        lockControlWhileClip = serializedObject.FindProperty(
            "lockControlWhileClip");

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
        moveInputSection = ProjectInspectorUtility.CreateFoldout(
            "设置移动输入",
            true);
        moveInputSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "applyMoveInput",
            "启用移动输入"));
        moveInputSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moveInput",
            "移动输入"));
        root.Add(moveInputSection);

        sprintSection = ProjectInspectorUtility.CreateFoldout(
            "设置冲刺",
            true);
        sprintSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "applySprint",
            "启用冲刺"));
        sprintSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "sprint",
            "冲刺"));
        root.Add(sprintSection);

        jumpHeldSection = ProjectInspectorUtility.CreateFoldout(
            "设置按住跳跃",
            true);
        jumpHeldSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "applyJumpHeld",
            "启用按住跳跃"));
        jumpHeldSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpHeld",
            "按住跳跃"));
        root.Add(jumpHeldSection);

        jumpSection = ProjectInspectorUtility.CreateFoldout(
            "请求跳跃",
            true);
        jumpSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "requestJump",
            "启用请求跳跃"));
        jumpSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpBoostSpeed",
            "跳跃增强速度",
            "小于 0 时不覆盖正常跳跃速度。"));
        root.Add(jumpSection);

        controlSection = ProjectInspectorUtility.CreateFoldout(
            "玩家控制",
            true);
        controlSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "lockControlWhileClip",
            "片段期间锁住玩家控制"));
        controlSection.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "restoreInputOnEnd",
            "结束时恢复进入前输入"));
        root.Add(controlSection);
    }

    protected override void RefreshSpecificVisibility()
    {
        Operation visibleOperation = operation;
        if (operationPopup != null)
        {
            operationPopup.SetValueWithoutNotify(
                OperationNames[(int)visibleOperation]);
        }

        bool custom = visibleOperation == Operation.Custom;
        SetCommonMotionVisible(
            visibleOperation == Operation.Motion || custom);
        SetVisible(
            moveInputSection,
            visibleOperation == Operation.MoveInput || custom);
        SetVisible(
            sprintSection,
            visibleOperation == Operation.Sprint || custom);
        SetVisible(
            jumpHeldSection,
            visibleOperation == Operation.JumpHeld || custom);
        SetVisible(
            jumpSection,
            visibleOperation == Operation.RequestJump || custom);
        SetVisible(
            controlSection,
            visibleOperation == Operation.LockControl || custom);
    }

    private Operation DetectOperation()
    {
        int count = 0;
        if (motionMode.enumValueIndex !=
            (int)ActorTimelineMotionMode.None)
        {
            count++;
        }

        if (applyMoveInput.boolValue)
        {
            count++;
        }

        if (applySprint.boolValue)
        {
            count++;
        }

        if (applyJumpHeld.boolValue)
        {
            count++;
        }

        if (requestJump.boolValue)
        {
            count++;
        }

        if (lockControlWhileClip.boolValue)
        {
            count++;
        }

        if (count > 1)
        {
            return Operation.Custom;
        }

        if (applyMoveInput.boolValue)
        {
            return Operation.MoveInput;
        }

        if (applySprint.boolValue)
        {
            return Operation.Sprint;
        }

        if (applyJumpHeld.boolValue)
        {
            return Operation.JumpHeld;
        }

        if (requestJump.boolValue)
        {
            return Operation.RequestJump;
        }

        if (lockControlWhileClip.boolValue)
        {
            return Operation.LockControl;
        }

        return Operation.Motion;
    }

    private void ApplyOperation()
    {
        if (operation == Operation.Custom)
        {
            return;
        }

        SetEditorFlags(
            operation == Operation.Motion,
            operation == Operation.MoveInput,
            operation == Operation.Sprint,
            operation == Operation.JumpHeld,
            operation == Operation.RequestJump,
            operation == Operation.LockControl);
    }

    private void SetEditorFlags(
        bool motion,
        bool moveInput,
        bool sprint,
        bool jumpHeld,
        bool requestJumpEnabled,
        bool lockControl)
    {
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

        applyMoveInput.boolValue = moveInput;
        applySprint.boolValue = sprint;
        applyJumpHeld.boolValue = jumpHeld;
        requestJump.boolValue = requestJumpEnabled;
        lockControlWhileClip.boolValue = lockControl;
        serializedObject.ApplyModifiedProperties();
    }
}
