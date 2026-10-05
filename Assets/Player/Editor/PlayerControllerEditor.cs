using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Player.Editor
{
    [CustomEditor(typeof(PlayerController))]
    public sealed class PlayerControllerEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            root.Add(PlayerInspectorFields.CreateScriptField(
                serializedObject));

            Foldout movement = PlayerInspectorFields.CreateFoldout(
                "移动");
            movement.Add(PlayerInspectorFields.Create(
                serializedObject,
                "moveSpeed",
                "移动速度",
                "固定 XY 平面上的水平移动速度。"));
            movement.Add(PlayerInspectorFields.Create(
                serializedObject,
                "moveAcceleration",
                "起步加速度",
                "从静止或低速向目标速度逼近时的加速度。"));
            movement.Add(PlayerInspectorFields.Create(
                serializedObject,
                "moveDeceleration",
                "停止减速度",
                "松开移动、反向移动或速度回落时的减速度。"));
            movement.Add(PlayerInspectorFields.Create(
                serializedObject,
                "accelerationResponseCurve",
                "加速度曲线",
                "横轴是当前速度/目标速度，纵轴是起步加速度倍率。"));
            movement.Add(PlayerInspectorFields.Create(
                serializedObject,
                "sprintMultiplier",
                "冲刺倍率",
                "按住冲刺时对移动速度的倍率。"));
            root.Add(movement);

            Foldout swimming = PlayerInspectorFields.CreateFoldout("游泳");
            swimming.Add(PlayerInspectorFields.Create(
                serializedObject,
                "swimSpeed",
                "游泳速度",
                "水体内水平和竖直移动的最大速度。"));
            swimming.Add(PlayerInspectorFields.Create(
                serializedObject,
                "swimAcceleration",
                "游泳加速度",
                "进入水体后逼近目标速度的速率。"));
            root.Add(swimming);

            Foldout jump = PlayerInspectorFields.CreateFoldout(
                "跳跃");
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "jumpSpeed",
                "跳跃速度",
                "跳跃瞬间应用的上向速度。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "jumpGravityMultiplier",
                "上升重力倍率",
                "按住跳跃键上升时的重力倍率，越大上升越短。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "jumpReleaseGravityMultiplier",
                "松键上升重力倍率",
                "上升中松开跳跃键后的重力倍率，用于短跳。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "fallGravityMultiplier",
                "下落重力倍率",
                "开始下落后的重力倍率，越大落地越快。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "maxFallSpeed",
                "最大下落速度",
                "下落速度上限，0 表示不限制。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "coyoteTime",
                "离地宽限时间",
                "离开地面后仍允许起跳的宽限时间。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "jumpBufferTime",
                "跳跃缓冲时间",
                "落地前提前按下跳跃时保留输入的时间。"));
            jump.Add(PlayerInspectorFields.Create(
                serializedObject,
                "groundMask",
                "地面层",
                "用于地面检测的物理层。"));
            root.Add(jump);

            Foldout debug = PlayerInspectorFields.CreateFoldout(
                "运行时调试（只读）");
            Label moveDebug = CreateDebugLabel();
            Label jumpDebug = CreateDebugLabel();
            Label actionDebug = CreateDebugLabel();
            debug.Add(new Label("移动"));
            debug.Add(moveDebug);
            debug.Add(new Label("跳跃"));
            debug.Add(jumpDebug);
            debug.Add(new Label("动作与控制"));
            debug.Add(actionDebug);
            root.Add(debug);
            root.schedule.Execute(() =>
            {
                PlayerController player =
                    target as PlayerController;
                if (player == null)
                {
                    return;
                }

                if (!Application.isPlaying)
                {
                    moveDebug.text = "进入 Play Mode 后显示实时数据";
                    jumpDebug.text = "进入 Play Mode 后显示实时数据";
                    actionDebug.text = "进入 Play Mode 后显示实时数据";
                    return;
                }

                moveDebug.text =
                    $"输入 X {player.MoveInput.x:0.##} · " +
                    $"冲刺 {(player.SprintRequested ? "是" : "否")} · " +
                    $"水平速度 {player.HorizontalVelocity:0.##}";
                jumpDebug.text =
                    $"着地 {(player.IsGrounded ? "是" : "否")} · " +
                    $"模式 {player.GravityMode} · " +
                    $"跳跃键 {(player.JumpHeld ? "按住" : "松开")} · " +
                    $"离地 {player.AirTime:0.##}s · " +
                    $"Coyote {player.CoyoteRemaining:0.##}s · " +
                    $"Jump Buffer {player.JumpBufferRemaining:0.##}s · " +
                    $"垂直速度 {player.VerticalVelocity:0.##}";
                actionDebug.text =
                    $"状态 {player.CurrentStateId} · " +
                    $"锁定 {(player.IsControlLocked ? "是" : "否")}" +
                    $"({player.ControlLockDepth}) · " +
                    $"动作 {(player.CurrentAction != null ? player.CurrentAction.name : "无")} · " +
                    $"播放 {(player.IsActionPlaying ? "是" : "否")}";
            }).Every(100);

            HelpBox note = new HelpBox(
                "输入由 PlayerInputDriver 处理；本组件只提供马的动作能力。",
                HelpBoxMessageType.Info);
            note.style.marginTop = 6;
            root.Add(note);

            root.Bind(serializedObject);
            return root;
        }

        private static Label CreateDebugLabel()
        {
            Label label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.opacity = .85f;
            label.style.marginBottom = 6f;
            return label;
        }
    }
}
