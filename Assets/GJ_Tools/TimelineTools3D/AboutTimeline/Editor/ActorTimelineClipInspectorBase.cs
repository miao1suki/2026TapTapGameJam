using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class ActorTimelineClipInspectorBase : Editor
{
    private SerializedProperty actorTag;
    private SerializedProperty useTrackBinding;
    private SerializedProperty motionMode;
    private SerializedProperty useOwnerAsOrigin;
    private SerializedProperty targetOffset;
    private SerializedProperty motionDuration;
    private SerializedProperty jumpArcHeight;
    private SerializedProperty motionCurve;
    private SerializedProperty driveRigidbody;
    private SerializedProperty clearVelocity;
    private SerializedProperty lockControlWhileDriving;

    private VisualElement targetGroup;
    private VisualElement durationGroup;
    private VisualElement jumpArcGroup;
    private VisualElement rigidbodyGroup;
    private VisualElement controlGroup;
    private VisualElement motionSection;
    private HelpBox modeHelp;

    protected abstract string Title { get; }

    protected virtual string HelpText =>
        "轨道绑定优先；绑定为空时按 Unity Tag 查找场景角色。";

    protected virtual void AddSpecificSections(VisualElement root)
    {
    }

    protected virtual void AddOperationSelector(VisualElement root)
    {
    }

    protected virtual void RefreshSpecificVisibility()
    {
    }

    public override VisualElement CreateInspectorGUI()
    {
        CacheProperties();

        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(Title));
        root.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "m_Script",
            "脚本"));
        root.Add(ProjectInspectorUtility.CreateHelp(HelpText));
        AddOperationSelector(root);

        Foldout actor = ProjectInspectorUtility.CreateFoldout(
            "角色查找",
            true);
        var tagField = new TagField("角色标签")
        {
            value = actorTag.stringValue,
            tooltip = "Unity Tag。轨道绑定为空时按该标签查找角色。"
        };
        tagField.RegisterValueChangedCallback(evt =>
        {
            actorTag.stringValue = evt.newValue;
            actorTag.serializedObject.ApplyModifiedProperties();
        });
        actor.Add(tagField);
        actor.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useTrackBinding",
            "优先使用轨道绑定"));
        actor.Add(ProjectInspectorUtility.CreateHelp(
            "轨道绑定存在时直接使用绑定对象；绑定为空时才按上面的 Tag 查找。",
            HelpBoxMessageType.None));
        root.Add(actor);

        Foldout motion = ProjectInspectorUtility.CreateFoldout(
            "运动方式",
            true);
        motionSection = motion;
        motion.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "motionMode",
            "运动模式"));
        modeHelp = ProjectInspectorUtility.CreateHelp(string.Empty);
        motion.Add(modeHelp);

        targetGroup = new VisualElement();
        targetGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useOwnerAsOrigin",
            "以片段挂载物体为原点",
            "关闭后目标坐标按世界坐标处理。"));
        targetGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetOffset",
            "目标位置 / 偏移",
            "开启原点时是相对挂载物体的偏移；关闭时是固定世界坐标。"));
        targetGroup.Add(new Button(FillTargetFromTaggedActor)
        {
            text = "读取场景中 Tag 角色的当前位置"
        });
        motion.Add(targetGroup);

        durationGroup = new VisualElement();
        durationGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "motionDuration",
            "运动时长（秒）"));
        durationGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "motionCurve",
            "运动曲线",
            "X=时间进度，Y=位移进度。"));
        motion.Add(durationGroup);

        jumpArcGroup = new VisualElement();
        jumpArcGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpArcHeight",
            "跳跃弧高"));
        motion.Add(jumpArcGroup);

        rigidbodyGroup = new VisualElement();
        rigidbodyGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "driveRigidbody",
            "直接驱动 Rigidbody"));
        rigidbodyGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "clearVelocity",
            "结束时清空速度"));
        motion.Add(rigidbodyGroup);

        controlGroup = new VisualElement();
        controlGroup.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "lockControlWhileDriving",
            "驱动期间锁住玩家控制"));
        motion.Add(controlGroup);
        root.Add(motion);

        AddSpecificSections(root);

        ProjectInspectorUtility.Bind(root, serializedObject);
        RefreshVisibility();
        root.schedule.Execute(() =>
        {
            serializedObject.Update();
            RefreshVisibility();
        }).Every(100);
        return root;
    }

    protected void RefreshVisibility()
    {
        ActorTimelineMotionMode mode = motionMode != null
            ? (ActorTimelineMotionMode)motionMode.enumValueIndex
            : ActorTimelineMotionMode.None;
        bool moves = mode != ActorTimelineMotionMode.None;
        bool usesTarget =
            mode == ActorTimelineMotionMode.Teleport ||
            mode == ActorTimelineMotionMode.Linear ||
            mode == ActorTimelineMotionMode.JumpArc;
        bool usesDuration =
            mode == ActorTimelineMotionMode.Linear ||
            mode == ActorTimelineMotionMode.JumpArc;
        bool usesJump = mode == ActorTimelineMotionMode.JumpArc;

        SetVisible(targetGroup, usesTarget);
        SetVisible(durationGroup, usesDuration);
        SetVisible(jumpArcGroup, usesJump);
        SetVisible(rigidbodyGroup, moves);
        SetVisible(
            controlGroup,
            moves && driveRigidbody != null &&
            driveRigidbody.boolValue);

        if (modeHelp != null)
        {
            modeHelp.text = mode switch
            {
                ActorTimelineMotionMode.Teleport =>
                    "片段开始时立即到目标位置，不经过中间帧。",
                ActorTimelineMotionMode.Linear =>
                    "从片段起始位置按运动曲线平滑移动到目标。",
                ActorTimelineMotionMode.JumpArc =>
                    "按运动曲线水平移动，同时叠加弧线高度；Boss 可切换为控制器 Jump 状态。",
                _ =>
                    "不驱动角色位移，只执行本片段上的玩家/Boss命令。"
            };
        }

        RefreshSpecificVisibility();
    }

    private void FillTargetFromTaggedActor()
    {
        string tag = actorTag != null
            ? actorTag.stringValue
            : string.Empty;
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        GameObject tagged = GameObject.FindGameObjectWithTag(tag);
        if (tagged == null)
        {
            return;
        }

        Undo.RecordObject(target, "填入角色当前位置");
        useOwnerAsOrigin.boolValue = false;
        targetOffset.vector3Value = tagged.transform.position;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private void CacheProperties()
    {
        actorTag = serializedObject.FindProperty("actorTag");
        useTrackBinding = serializedObject.FindProperty(
            "useTrackBinding");
        motionMode = serializedObject.FindProperty("motionMode");
        useOwnerAsOrigin = serializedObject.FindProperty(
            "useOwnerAsOrigin");
        targetOffset = serializedObject.FindProperty("targetOffset");
        motionDuration = serializedObject.FindProperty(
            "motionDuration");
        jumpArcHeight = serializedObject.FindProperty(
            "jumpArcHeight");
        motionCurve = serializedObject.FindProperty("motionCurve");
        driveRigidbody = serializedObject.FindProperty(
            "driveRigidbody");
        clearVelocity = serializedObject.FindProperty("clearVelocity");
        lockControlWhileDriving = serializedObject.FindProperty(
            "lockControlWhileDriving");
    }

    protected static void SetVisible(
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

    protected void SetCommonMotionVisible(bool visible)
    {
        SetVisible(motionSection, visible);
    }
}
