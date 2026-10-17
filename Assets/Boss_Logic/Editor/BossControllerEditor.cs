using Project.BossLogic;
using Project.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomEditor(typeof(BossController))]
public sealed class BossControllerEditor : Editor
{
    public override VisualElement CreateInspectorGUI()
    {
        VisualElement root = ProjectInspectorUtility.CreateRoot(
            serializedObject);
        root.Add(ProjectInspectorUtility.CreateTitle("Boss 控制器"));
        root.Add(ProjectInspectorUtility.CreateScriptField(
            serializedObject));
        root.Add(ProjectInspectorUtility.CreateHelp(
            "只负责追击、操作输入、受击、死亡和跳跃状态；Timeline 与马夫只下发命令，动画由 BossAnimationDriver 可选接入。"));

        Foldout chase = ProjectInspectorUtility.CreateFoldout(
            "追击设置",
            true);
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "target",
            "追击目标"));
        SerializedProperty targetTag =
            serializedObject.FindProperty("targetTag");
        var targetTagField = new TagField("目标标签")
        {
            value = targetTag.stringValue,
            tooltip = "从 Unity Tag 中选择追击目标标签。"
        };
        targetTagField.RegisterValueChangedCallback(evt =>
        {
            targetTag.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
        });
        chase.Add(targetTagField);
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "enableAutonomousChase",
            "启用自动目标追击",
            "默认关闭。Boss 常规移动由右移马夫负责；只有特殊需要时再启用。"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "trackingEnabled",
            "启用运动 / 追击",
            "Timeline 和右移马夫读取的运动门；自动目标追击还受“启用自动目标追击”控制。"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "targetRefreshSeconds",
            "目标刷新间隔"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "moveSpeed",
            "追击速度"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "acceleration",
            "加速度"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "stopDistance",
            "停止距离"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "chaseVertical",
            "允许上下追击"));
        chase.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "faceTarget",
            "记录目标朝向"));
        root.Add(chase);

        Foldout input = ProjectInspectorUtility.CreateFoldout(
            "Timeline 操作输入",
            true);
        input.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "walkSpeed",
            "Walk 输入速度",
            "Timeline 只发送移动方向；实际行走速度统一由此处控制。"));
        root.Add(input);

        Foldout jump = ProjectInspectorUtility.CreateFoldout(
            "跳跃设置",
            true);
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpDuration",
            "最短跳跃时长"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpHorizontalSpeed",
            "跳跃水平速度"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxJumpRiseHeight",
            "最大跳跃上升高度"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxJumpVerticalSpeed",
            "最大跳跃垂直速度"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "jumpCooldownSeconds",
            "跳跃冷却"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "faceJumpDirection",
            "跳跃时更新朝向"));
        jump.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "clearVelocityOnLanding",
            "落地清空速度"));
        root.Add(jump);

        root.Add(ProjectInspectorUtility.CreateDivider());
        Foldout survival = ProjectInspectorUtility.CreateFoldout(
            "碰撞清除、受击与无敌",
            true);
        survival.Add(ProjectInspectorUtility.CreateHelp(
            "调试区：右移阶段碰到物体时清除，岩浆会先造成一次伤害；无敌期间身体闪烁，但仍会清除岩浆和其他物体。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxHealth",
            "最大受击值"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startingHealth",
            "初始受击值"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "lavaContactDamage",
            "岩浆单次伤害",
            "每次岩浆接触结算的受击值。默认 1。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "invulnerabilitySeconds",
            "受击无敌时间（秒）",
            "受击后免疫后续伤害的时间；无敌期间仍然会清除碰到的岩浆和物体。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "invulnerabilityFlashInterval",
            "无敌闪烁间隔（秒）",
            "身体显示/隐藏切换的间隔。"));
        survival.Add(ProjectInspectorUtility.CreateDivider());
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyTouchedObjects",
            "碰撞隐藏物体"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyOnlyWhileExternalMoving",
            "仅外部移动时隐藏",
            "默认只在右移马夫接管后隐藏障碍，避免入场碰撞影响脚下地图。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "useRoomResetForDestroyedObjects",
            "隐藏后随房间恢复",
            "有 RoomResetObject 的关卡物体按房间生命周期隐藏并恢复；其他对象直接停用，不再销毁。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxHiddenObjectsPerFrame",
            "每帧最多隐藏",
            "碰撞只负责登记目标，实际隐藏延迟到 LateUpdate 并限制每帧数量，避免一次物理回调停用大量对象。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "passThroughColliderHeight",
            "穿行碰撞高度（本地）",
            "右移马夫接管、Boss 进入穿行状态时，把 BoxCollider 的本地上下的总高度扩到该值；停止穿行时恢复作者尺寸。"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyAfterDeath",
            "死亡后销毁"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyDelaySeconds",
            "销毁延迟"));
        survival.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "defeatPresentationRequested",
            "死亡演出请求（接口）",
            "血量归零时广播给后续玩家 Timeline 桥接组件；当前只留请求，不直接绑定 PlayableDirector。"));
        root.Add(survival);

        Foldout debug = ProjectInspectorUtility.CreateFoldout(
            "运行时调试",
            false);
        BossController controller = target as BossController;
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "状态",
            () => controller != null
                ? controller.State.ToString()
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "受击值",
            () => controller != null
                ? $"{controller.CurrentHealth:0.##}/{controller.MaxHealth:0.##}"
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "目标",
            () => controller != null && controller.Target != null
                ? controller.Target.name
                : "未绑定"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "无敌",
            () => controller != null
                ? controller.IsInvulnerable
                    ? $"是（{controller.InvulnerabilityRemaining:0.##}秒）"
                    : "否"
                : "无"));
        debug.Add(ProjectInspectorUtility.CreateReadOnlyRow(
            "血条 / 受伤",
            () => controller != null &&
                  controller.IsHealthSystemActive
                ? "已开启"
                : "未开启"));
        debug.Add(new Button(() =>
            controller?.ActivateHealthSystem())
        {
            text = "开启血条与受伤"
        });
        debug.Add(new Button(() => controller?.ReceiveHit(
            1f,
            Vector3.zero))
        {
            text = "调试受击 1"
        });
        debug.Add(new Button(() => controller?.Kill())
        {
            text = "调试死亡"
        });
        debug.Add(new Button(() =>
        {
            if (controller == null || controller.Body == null)
            {
                return;
            }

            controller.RequestJump(
                Vector2.down,
                3f);
        })
        {
            text = "调试向下跳跃 3 格"
        });
        root.Add(debug);

        ProjectInspectorUtility.Bind(root, serializedObject);
        return root;
    }
}
