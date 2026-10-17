using Project.BlockFeatures;
using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(ContactTimelineTrigger))]
public sealed class ContactTimelineTriggerEditor : Editor
{
    private SerializedObject colliderObject;
    private Collider cachedCollider;
    private VisualElement colliderFields;
    private VisualElement cooldownRow;
    private Label triggerSummary;
    private SerializedProperty triggerModeProperty;

    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle(
            "接触触发 Timeline"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "独立道具组件，不需要 ColorObject、BlockRuntime 或 BlockAbilityHost。" +
            "激活时会隐藏 Renderer，保留 Collider，并按接触播放 Timeline。"));
        triggerSummary = new Label();
        triggerSummary.style.whiteSpace = WhiteSpace.Normal;
        triggerSummary.style.marginTop = 6f;
        triggerSummary.style.marginBottom = 6f;
        triggerSummary.style.paddingLeft = 6f;
        triggerSummary.style.paddingRight = 6f;
        triggerSummary.style.paddingTop = 4f;
        triggerSummary.style.paddingBottom = 4f;
        triggerSummary.style.backgroundColor =
            new Color(.18f, .23f, .28f, .9f);
        root.Add(triggerSummary);

        Foldout timeline = ProjectInspectorUtility.CreateFoldout(
            "Timeline",
            true);
        timeline.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "timeline",
            "Timeline 资源",
            "留空时使用同物体 PlayableDirector 已配置的资源。"));
        timeline.Add(ProjectInspectorUtility.CreateHelp(
            "“播放期间申请玩家控制锁”通过 IPlayerControlLockTarget 申请，" +
            "不持有 PlayerController 引用。"));
        timeline.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "requestPlayerControlLockDuringTimeline",
            "播放期间申请玩家控制锁"));
        root.Add(timeline);

        Foldout collider = ProjectInspectorUtility.CreateFoldout(
            "碰撞体设置",
            true);
        collider.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetCollider",
            "管理的碰撞体",
            "留空时使用同物体 Collider。"));
        colliderFields = new VisualElement();
        collider.Add(colliderFields);
        root.Add(collider);

        Foldout contact = ProjectInspectorUtility.CreateFoldout(
            "触发对象",
            true);
        contact.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "contactFilter",
            "接触对象筛选"));
        contact.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "requiredTag",
            "指定标签"));
        root.Add(contact);

        Foldout trigger = ProjectInspectorUtility.CreateFoldout(
            "触发规则",
            true);
        PropertyField triggerMode = ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "triggerMode",
            "触发模式");
        triggerModeProperty = serializedObject.FindProperty("triggerMode");
        trigger.Add(triggerMode);
        cooldownRow = new VisualElement();
        cooldownRow.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "repeatCooldownSeconds",
            "重复冷却（秒）"));
        trigger.Add(cooldownRow);
        trigger.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "restartIfPlaying",
            "重复接触时重新开始"));
        root.Add(trigger);

        Foldout visual = ProjectInspectorUtility.CreateFoldout(
            "表现设置",
            true);
        visual.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hideRenderersOnEnable",
            "自动隐藏物体"));
        visual.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyAfterTrigger",
            "触发后销毁"));
        root.Add(visual);

        AddDebug(root);
        ProjectInspectorUtility.Bind(root, serializedObject);
        RefreshColliderFields();
        RefreshTriggerModeVisibility();
        root.schedule.Execute(() =>
        {
            colliderObject?.ApplyModifiedProperties();
            serializedObject.Update();
            RefreshColliderFields();
            RefreshTriggerModeVisibility();
        }).Every(100);
        return root;
    }

    private void RefreshTriggerModeVisibility()
    {
        bool repeatable = triggerModeProperty != null &&
                          triggerModeProperty.enumValueIndex == 1;
        if (cooldownRow != null)
        {
            cooldownRow.style.display = repeatable
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        if (triggerSummary == null)
        {
            return;
        }

        ContactTimelineTrigger trigger =
            target as ContactTimelineTrigger;
        triggerSummary.text = repeatable
            ? "当前触发模式：可重复触发" +
              (trigger != null
                  ? $" · 冷却 {trigger.RepeatCooldownSeconds:0.##} 秒"
                  : string.Empty)
            : "当前触发模式：只触发一次";
    }

    private void RefreshColliderFields()
    {
        if (colliderFields == null)
        {
            return;
        }

        ContactTimelineTrigger trigger =
            target as ContactTimelineTrigger;
        SerializedProperty targetProperty =
            serializedObject.FindProperty("targetCollider");
        Collider current = targetProperty != null
            ? targetProperty.objectReferenceValue as Collider
            : null;
        if (current == null && trigger != null)
        {
            current = trigger.GetComponent<Collider>();
            if (current != null && targetProperty != null)
            {
                targetProperty.objectReferenceValue = current;
                serializedObject.ApplyModifiedProperties();
            }
        }

        if (cachedCollider == current && colliderObject != null)
        {
            return;
        }

        cachedCollider = current;
        colliderObject = current != null
            ? new SerializedObject(current)
            : null;
        colliderFields.Clear();
        if (colliderObject == null)
        {
            colliderFields.Add(ProjectInspectorUtility.CreateHelp(
                "当前物体没有可编辑的 Collider。",
                HelpBoxMessageType.Warning));
            return;
        }

        AddColliderField("m_Enabled", "启用 Collider");
        AddColliderField("m_IsTrigger", "Trigger");
        if (current is BoxCollider)
        {
            AddColliderField("m_Size", "BoxCollider 尺寸");
            AddColliderField("m_Center", "BoxCollider 中心");
        }
    }

    private void AddColliderField(
        string propertyPath,
        string label)
    {
        SerializedProperty property =
            colliderObject.FindProperty(propertyPath);
        if (property == null)
        {
            return;
        }

        var field = new PropertyField(property, label);
        field.Bind(colliderObject);
        colliderFields.Add(field);
    }

    private void AddDebug(VisualElement root)
    {
        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        ContactTimelineTrigger trigger =
            target as ContactTimelineTrigger;
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "已触发",
            () => trigger != null && trigger.HasTriggered
                ? "是"
                : "否"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "当前接触",
            () => trigger != null
                ? trigger.ContactCount.ToString()
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "触发模式",
            () => trigger == null
                ? "无"
                : trigger.TriggerMode ==
                  ContactTimelineTriggerMode.Once
                    ? "只触发一次"
                    : "可重复触发"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "剩余冷却",
            () => trigger == null
                ? "无"
                : $"{trigger.CooldownRemaining:0.##} 秒"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "Timeline",
            () => trigger != null &&
                  trigger.Director != null &&
                  trigger.Director.playableAsset != null
                ? trigger.Director.playableAsset.name
                : "未配置"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "控制锁申请",
            () => trigger == null
                ? "无"
                : trigger.RequestsPlayerControlLock
                    ? trigger.ControlLockActive
                        ? "已持有"
                        : "未持有"
                    : "关闭"));
        debug.Add(new Button(() => trigger?.DebugTrigger())
        {
            text = "调试触发 Timeline"
        });
        debug.Add(new Button(() => trigger?.ResetTrigger())
        {
            text = "重置触发状态"
        });
        root.Add(debug);
    }
}
