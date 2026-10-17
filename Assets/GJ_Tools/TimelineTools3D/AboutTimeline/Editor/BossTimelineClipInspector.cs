using System.Collections.Generic;
using Project.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossTimelineClip))]
public sealed class BossTimelineClipInspector : Editor
{
    private enum Action
    {
        Idle,
        Move,
        Jump,
        SetTracking,
        Kill
    }

    private static readonly List<string> ActionNames =
        new List<string>
        {
            "发呆",
            "移动",
            "跳跃",
            "设置运动开关",
            "死亡"
        };

    private SerializedProperty motionMode;
    private SerializedProperty spawnBoss;
    private SerializedProperty bossPrefab;
    private SerializedProperty targetOffset;
    private SerializedProperty useOwnerAsOrigin;
    private SerializedProperty moveSpeed;
    private SerializedProperty useBossJumpState;
    private SerializedProperty setTrackingOnStart;
    private SerializedProperty setTrackingOnEnd;
    private SerializedProperty trackingOnStart;
    private SerializedProperty killAtEnd;

    private PopupField<string> actionField;
    private VisualElement bossPrefabGroup;
    private VisualElement moveDataGroup;
    private VisualElement jumpDataGroup;
    private VisualElement trackingDataGroup;
    private HelpBox actionHelp;
    private HelpBox conflictHelp;
    private Action action;
    private bool conflict;

    public override VisualElement CreateInspectorGUI()
    {
        CacheProperties();
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "Boss Timeline 片段"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "每个 Boss Clip 只能选择一个动作；多个动作请拆成多条 Boss Track。"));

        Foldout source = ProjectInspectorUtility.CreateFoldout(
            "Boss 生成",
            true);
        source.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "spawnBoss",
            "是否生成 Boss"));
        bossPrefabGroup = new VisualElement();
        bossPrefabGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "bossPrefab",
            "Boss 预制体"));
        source.Add(bossPrefabGroup);
        root.Add(source);

        Foldout actionSection = ProjectInspectorUtility.CreateFoldout(
            "片段动作",
            true);
        action = DetectAction();
        actionField = new PopupField<string>(
            "动作",
            ActionNames,
            (int)action);
        actionField.RegisterValueChangedCallback(evt =>
        {
            action = (Action)ActionNames.IndexOf(evt.newValue);
            ApplyAction();
            RefreshVisibility();
        });
        actionSection.Add(actionField);
        actionHelp = ProjectInspectorUtility.CreateHelp(string.Empty);
        actionSection.Add(actionHelp);
        conflictHelp = ProjectInspectorUtility.CreateHelp(
            "这个 Clip 同时配置了多个动作，已按旧数据兼容显示。请重新选择一个动作。",
            HelpBoxMessageType.Warning);
        actionSection.Add(conflictHelp);

        moveDataGroup = new VisualElement();
        moveDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useOwnerAsOrigin",
            "以片段挂载物体为原点"));
        moveDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetOffset",
            "目标位置 / 偏移"));
        moveDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moveSpeed",
            "移动速度"));
        actionSection.Add(moveDataGroup);

        jumpDataGroup = new VisualElement();
        jumpDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useOwnerAsOrigin",
            "以片段挂载物体为原点"));
        jumpDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetOffset",
            "跳跃目标位置 / 偏移"));
        actionSection.Add(jumpDataGroup);

        trackingDataGroup = new VisualElement();
        trackingDataGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "trackingOnStart",
            "开启运动"));
        actionSection.Add(trackingDataGroup);
        root.Add(actionSection);

        ProjectInspectorUtility.Bind(root, serializedObject);
        RefreshVisibility();
        root.schedule.Execute(() =>
        {
            serializedObject.Update();
            RefreshVisibility();
        }).Every(100);
        return root;
    }

    private void CacheProperties()
    {
        motionMode = serializedObject.FindProperty("motionMode");
        spawnBoss = serializedObject.FindProperty("spawnBoss");
        bossPrefab = serializedObject.FindProperty("bossPrefab");
        targetOffset = serializedObject.FindProperty("targetOffset");
        useOwnerAsOrigin =
            serializedObject.FindProperty("useOwnerAsOrigin");
        moveSpeed = serializedObject.FindProperty("moveSpeed");
        useBossJumpState =
            serializedObject.FindProperty("useBossJumpState");
        setTrackingOnStart =
            serializedObject.FindProperty("setTrackingOnStart");
        setTrackingOnEnd =
            serializedObject.FindProperty("setTrackingOnEnd");
        trackingOnStart =
            serializedObject.FindProperty("trackingOnStart");
        killAtEnd = serializedObject.FindProperty("killAtEnd");
    }

    private Action DetectAction()
    {
        int count = 0;
        bool moving =
            motionMode.enumValueIndex !=
            (int)ActorTimelineMotionMode.None;
        bool tracking =
            setTrackingOnStart.boolValue ||
            setTrackingOnEnd.boolValue;
        if (moving)
        {
            count++;
        }

        if (tracking)
        {
            count++;
        }

        if (killAtEnd.boolValue)
        {
            count++;
        }

        conflict = count > 1;
        if (killAtEnd.boolValue)
        {
            return Action.Kill;
        }

        if (tracking)
        {
            return Action.SetTracking;
        }

        return motionMode.enumValueIndex switch
        {
            (int)ActorTimelineMotionMode.JumpArc => Action.Jump,
            (int)ActorTimelineMotionMode.Linear => Action.Move,
            (int)ActorTimelineMotionMode.Teleport => Action.Move,
            _ => Action.Idle
        };
    }

    private void ApplyAction()
    {
        bool move = action == Action.Move;
        bool jump = action == Action.Jump;
        bool tracking = action == Action.SetTracking;
        bool kill = action == Action.Kill;

        motionMode.enumValueIndex = jump
            ? (int)ActorTimelineMotionMode.JumpArc
            : move
                ? (int)ActorTimelineMotionMode.Linear
                : (int)ActorTimelineMotionMode.None;
        setTrackingOnStart.boolValue = tracking;
        setTrackingOnEnd.boolValue = false;
        killAtEnd.boolValue = kill;
        useBossJumpState.boolValue = jump;
        conflict = false;
        serializedObject.ApplyModifiedProperties();
    }

    private void RefreshVisibility()
    {
        if (actionField != null)
        {
            actionField.SetValueWithoutNotify(ActionNames[(int)action]);
        }

        SetVisible(
            bossPrefabGroup,
            spawnBoss != null && spawnBoss.boolValue);
        SetVisible(moveDataGroup, action == Action.Move);
        SetVisible(jumpDataGroup, action == Action.Jump);
        SetVisible(
            trackingDataGroup,
            action == Action.SetTracking);
        SetVisible(conflictHelp, conflict);

        if (actionHelp != null)
        {
            actionHelp.text = action switch
            {
                Action.Idle => "只等待，时长由 Timeline Clip 长度决定。",
                Action.Move => "从片段开始时所在位置移动到目标位置；时长由距离和移动速度计算。",
                Action.Jump => "通过 BossController 的 Jump 状态跳到目标位置。",
                Action.SetTracking => "在片段开始时设置 Boss 运动开关。",
                Action.Kill => "片段结束时杀死 Boss。",
                _ => string.Empty
            };
        }
    }

    private static void SetVisible(
        VisualElement element,
        bool visible)
    {
        if (element != null)
        {
            element.style.display = visible
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }
    }
}
