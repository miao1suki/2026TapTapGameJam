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
            swimming.Add(PlayerInspectorFields.Create(
                serializedObject,
                "swimRiseSpeed",
                "上浮速度",
                "水体中按住跳跃键时的缓慢上浮速度。"));
            swimming.Add(PlayerInspectorFields.Create(
                serializedObject,
                "swimSinkSpeed",
                "下潜速度",
                "水体中未按住跳跃键时的缓慢下沉速度。"));
            swimming.Add(PlayerInspectorFields.Create(
                serializedObject,
                "swimVerticalAcceleration",
                "垂直游泳加速度",
                "水体中纵向速度在上浮、下沉之间变化的加速度。"));
            root.Add(swimming);

            Foldout climbing = PlayerInspectorFields.CreateFoldout(
                "攀爬");
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbSpeed",
                "上爬速度",
                "按住上方向时向上攀爬的速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbSlideSpeed",
                "自然下滑速度",
                "不按上下方向时沿藤蔓缓慢下滑的速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbDownSpeed",
                "主动下滑速度",
                "按住下方向时快速下滑的速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbHorizontalSpeed",
                "横向离开速度",
                "按藤蔓外侧方向离开的速度；按内侧方向则上爬。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbKickHorizontalSpeed",
                "蹬藤蔓水平速度",
                "按住朝藤蔓方向并跳跃时，朝远离藤蔓方向弹出的速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbKickVerticalSpeed",
                "蹬藤蔓上升速度",
                "蹬藤蔓跳跃时的初始上升速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbKickDetachSeconds",
                "蹬跳脱离时间",
                "蹬跳后暂时不重新抓住藤蔓；前半段保持外抛速度。"));
            climbing.Add(PlayerInspectorFields.Create(
                serializedObject,
                "climbAcceleration",
                "攀爬加速度",
                "攀爬横纵速度逼近目标速度的加速度。"));
            root.Add(climbing);

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

            Foldout pickup = PlayerInspectorFields.CreateFoldout(
                "拾取");
            pickup.Add(PlayerInspectorFields.Create(
                serializedObject,
                "pickupSenseRadiusBlocks",
                "拾取感知半径（格）",
                "以玩家为中心，半径多少格以内的掉落物可以被感知。数值使用关卡网格格数，不使用世界单位。"));
            root.Add(pickup);

            Foldout death = PlayerInspectorFields.CreateFoldout(
                "死亡");
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "lethalFallHeightBlocks",
                "致死高度（格）",
                "玩家从大于等于该网格高度的位置落到实体地面时直接死亡；落入水中、弹性植物或进入攀爬可免除。0 表示关闭致死高度。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "respawnInvulnerabilitySeconds",
                "复活无敌时间（秒）",
                "复活后暂时免疫伤害和摔落死亡；岩浆持续检测，无敌结束后仍在岩浆中会立即死亡。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "deathAction",
                "死亡动作",
                "死亡时通过 PlayerActionRunner 播放的 ActSO Timeline；播放结束后保持最后一帧。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "respawnPoint",
                "默认复活点",
                "可选。未设置时会优先使用已注册的复活点；仍无可用复活点时在死亡地点周围随机复活。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "respawnRandomRadiusBlocks",
                "随机复活半径（格）",
                "没有复活点时，在死亡位置周围按关卡格数寻找可落地位置；默认 4 格。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "respawnRandomAttempts",
                "随机尝试次数",
                "随机复活时最多尝试多少个安全位置。"));
            death.Add(PlayerInspectorFields.Create(
                serializedObject,
                "respawnSafetyRadius",
                "复活安全半径",
                "复活位置检查岩浆时使用的半径。位置会先向下吸附到实体地面。"));
            root.Add(death);

            Foldout debug = PlayerInspectorFields.CreateFoldout(
                "运行时调试（只读）");
            Label moveDebug = CreateDebugLabel();
            Label jumpDebug = CreateDebugLabel();
            Label actionDebug = CreateDebugLabel();
            Label pickupDebug = CreateDebugLabel();
            debug.Add(new Label("移动"));
            debug.Add(moveDebug);
            debug.Add(new Label("跳跃"));
            debug.Add(jumpDebug);
            debug.Add(new Label("动作与控制"));
            debug.Add(actionDebug);
            debug.Add(new Label("拾取感知"));
            debug.Add(pickupDebug);
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
                    pickupDebug.text = "进入 Play Mode 后显示实时数据";
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
                    $"垂直速度 {player.VerticalVelocity:0.##} · " +
                    $"当前下落 {player.CurrentFallDistance:0.##} · " +
                    $"上次落地 {player.LastLandingFallDistance:0.##} · " +
                    $"致死高度 {player.LethalFallHeightWorld:0.##} · " +
                    $"无敌 {player.InvulnerabilityRemaining:0.##}s";
                actionDebug.text =
                    $"状态 {player.CurrentStateId} · " +
                    $"死亡 {(player.IsDead ? "是" : "否")} · " +
                    $"锁定 {(player.IsControlLocked ? "是" : "否")}" +
                    $"({player.ControlLockDepth}) · " +
                    $"动作 {(player.CurrentAction != null ? player.CurrentAction.name : "无")} · " +
                    $"播放 {(player.IsActionPlaying ? "是" : "否")}";
                pickupDebug.text =
                    $"感知半径 {player.PickupSenseRadiusBlocks:0.##} 格 · " +
                    $"世界半径 {player.PickupSenseRadiusWorld:0.##}";
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
