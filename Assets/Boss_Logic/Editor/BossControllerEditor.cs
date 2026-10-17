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
            "只负责追击、受击、死亡和跳跃状态；动画由 BossAnimationDriver 可选接入。"));

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

        Foldout hit = ProjectInspectorUtility.CreateFoldout(
            "受击与死亡",
            true);
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "maxHealth",
            "最大受击值"));
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "startingHealth",
            "初始受击值"));
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hitStunSeconds",
            "受击硬直时长"));
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "hitKnockbackSpeed",
            "受击击退速度"));
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyAfterDeath",
            "死亡后销毁"));
        hit.Add(ProjectInspectorUtility.CreateProperty(
            serializedObject,
            "destroyDelaySeconds",
            "销毁延迟"));
        root.Add(hit);

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
