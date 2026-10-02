using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum MoveMode
{
    [InspectorName("瞬移(穿墙)")]
    FixedEndPos,
    [InspectorName("匀速直线")]
    SpeedAndDistance,
    [InspectorName("变速直线")]
    VariableSpeed,
    [InspectorName("跳跃")]
    Jump,
    [InspectorName("绕圈")]
    CircleRotate,
}

public class TransformTimelineClip : PlayableAsset, ITimelineClipAsset
{
    [Header("位移模式")]
    [Tooltip("位移模式，下方参数面板会随模式切换")]
    public MoveMode moveMode;

    [Header("通用参数")]
    [Tooltip("直线移动方向(本地坐标)。自动去掉垂直分量；全零时改用角色面朝方向")]
    public Vector3 direction = Vector3.forward;
    [Tooltip("直线移动撞墙时是否贴墙滑行")]
    public bool useCollision = true;
    [Tooltip("撞墙检测球半径，越大越“胖”，越早碰墙")]
    public float castRadius = 0.5f;

    [Header("跳跃")]
    [Tooltip("跳跃模式的起跳时间，相对 clip 起点")]
    [Min(0f)] public float jumpStartTime;
    [Tooltip("跳跃持续时间；超出片段结束时间时自动截断")]
    [Min(0.01f)] public float jumpDuration = 0.5f;
    [Tooltip("高度曲线基准值；最终高度=基准值×曲线值")]
    [Min(0f)] public float jumpHeight = 2f;
    [Tooltip("跳跃形状曲线，X=跳跃进度，Y=高度倍率；最终高度=jumpHeight*曲线值")]
    public AnimationCurve jumpHeightCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f, 0f));

    [Header("固定终点(瞬移,可穿墙)")]
    [Tooltip("进入片段瞬间传送到的终点(本地偏移)，不检测碰撞、可穿墙")]
    public Vector3 endPos;
    [Tooltip("传送完成后锁定的朝向(本地欧拉)")]
    public Vector3 endEuler;

    [Header("速度+距离(匀速直线)")]
    [Tooltip("匀速移动速度(米/秒)")]
    public float moveSpeed;
    [Tooltip("直线总距离，走满即停；0 = 不限制，走到片段结束为止")]
    public float totalDistance;

    [Header("速度曲线")]
    [Tooltip("使用曲线直接控制直线速度(米/秒)；关闭时继续使用旧的初末速度或固定速度")]
    public bool useSpeedCurve;
    [Tooltip("X=片段归一化进度，Y=实际移动速度(米/秒)")]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("变速直线")]
    [Tooltip("起始速度(0=从静止起步)")]
    public float startSpeed;
    [Tooltip("结束速度，播放中从起始速度线性过渡到此值")]
    public float endSpeed;

    [Header("进度曲线")]
    [Tooltip("用于非匀速的绕圈等进度类位移；直线模式优先使用速度曲线")]
    public bool useProgressCurve;
    [Tooltip("X=时间进度，Y=位移进度")]
    public AnimationCurve progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("绕圈旋转")]
    [Tooltip("绕圈圆心(本地偏移，相对进入片段时的位置)")]
    public Vector3 circleCenterLocal;
    [Tooltip("绕圈半径")]
    public float circleRadius;
    [Tooltip("绕圈总角度(度)，360 = 完整一圈")]
    public float circleTotalAngle;
    [Tooltip("旋转方向：勾选 = 俯视顺时针")]
    public bool circleClockwise = true;
    [Tooltip("绕圈是否使用角速度变速插值")]
    public bool circleVariableSpeed;
    [Tooltip("变速绕圈起始角速度(度/秒)")]
    public float circleStartAngSpeed;
    [Tooltip("变速绕圈结束角速度(度/秒)")]
    public float circleEndAngSpeed;

    public ClipCaps clipCaps => ClipCaps.Blending;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var behaviour = new TransformBehaviour();
        behaviour.clip = this;
        return ScriptPlayable<TransformBehaviour>.Create(graph, behaviour);
    }
}
