using UnityEngine;
using UnityEngine.Playables;

public enum CamMoveMode
{
    [InspectorName("平滑运镜")]
    SmoothLerp,
    [InspectorName("瞬移运镜")]
    Teleport,
    [InspectorName("归位")]
    ResetOrigin
}

public enum ResetCamSubMode
{
    [InspectorName("瞬移")]
    Teleport,
    [InspectorName("平滑")]
    SmoothLerp
}

public enum TimelineCameraModeRequest
{
    [InspectorName("不申请")]
    None,
    [InspectorName("正交 2D")]
    Side2D,
    [InspectorName("透视 3D")]
    Perspective3D
}

public enum Camera2DTurnTiming
{
    [InspectorName("不处理")]
    None,
    [InspectorName("片段开始时转向")]
    AtClipStart,
    [InspectorName("片段结束时转向")]
    AtClipEnd
}

public class CameraTimelineClip : PlayableAsset
{
    [Header("镜头移动总模式")]
    [Tooltip("运镜模式：平滑跟到目标机位 / 瞬移切镜 / 归位回播放前机位")]
    public CamMoveMode cameraMoveMode;

    [Header("归位模式专用参数")]
    [Tooltip("归位方式：瞬移 或 平滑拉回")]
    public ResetCamSubMode resetSubMode;
    [Tooltip("归位平滑速度(越大越快)")]
    public float resetLerpFactor = 10f;

    [Header("环绕圆弧运镜")]
    [Tooltip("沿圆弧环绕角色(忽略目标坐标的距离部分，只取方位)")]
    public bool useSurroundMode = false;
    [Tooltip("环绕半径")]
    public float surroundRadius = 3.2f;
    [Tooltip("环绕扫过的角度(180 = 绕到侧面/脑后，负值反向)")]
    public float surroundTotalAngle = 180f;
    [Tooltip("环绕圆心的高度(相对角色脚底)")]
    public float surroundFixedHeight = 1.2f;
    [Tooltip("使用曲线控制环绕半径倍率；1=surroundRadius")]
    public bool useSurroundRadiusCurve;
    [Tooltip("X=片段进度，Y=半径倍率")]
    public AnimationCurve surroundRadiusCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [Tooltip("使用曲线控制环绕高度倍率；1=surroundFixedHeight")]
    public bool useSurroundHeightCurve;
    [Tooltip("X=片段进度，Y=高度倍率")]
    public AnimationCurve surroundHeightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

    [Header("目标机位参数")]
    [Tooltip("目标机位坐标(相对角色，角色转身目标会跟着走)")]
    public Vector3 cameraTargetLocalPos;
    [Tooltip("目标机位朝向(相对角色的欧拉角)")]
    public Vector3 cameraTargetEuler;

    [Header("朝向设置")]
    [Tooltip("片段内持续看向角色(否则使用目标机位朝向)")]
    public bool lockLookAtPlayer = true;

    [Header("Project 2D / 3D 模式申请")]
    [Tooltip("片段开始时向 Project CameraModeController 申请切换模式")]
    public TimelineCameraModeRequest modeRequest;
    [Tooltip("申请 2D 时同时指定绝对 Yaw 角度")]
    public bool overrideSide2DYaw;
    [Tooltip("转向完成后的正交 2D 绝对 Yaw 角度")]
    public float side2DYawDegrees;
    [Tooltip("模式切换申请使用的过渡时间")]
    [Min(0f)] public float projectionTransitionDuration = 0.4f;

    [Header("2D 平面转向")]
    [Tooltip("正交 2D 下可选择在片段开始或结束时围绕目标做平面转向")]
    public Camera2DTurnTiming turnTiming = Camera2DTurnTiming.None;
    [Tooltip("平面右手方向为正，90 表示向右转一个面")]
    public float turnAngleDegrees = 90f;
    [Tooltip("单次转向持续时间")]
    [Min(0.01f)] public float turnDuration = 0.6f;
    [Tooltip("转向速度曲线，X=转向时间进度，Y=角度进度")]
    public AnimationCurve turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("2D 正交轴约束")]
    [Tooltip("仅在目标投影为正交时限制相机位置；适合 2D 侧视或固定纵深关卡")]
    public bool constrainOrthographicAxes;
    [Tooltip("允许相机沿世界 X 轴移动")]
    public bool allowPositionX = true;
    [Tooltip("允许相机沿世界 Y 轴移动")]
    public bool allowPositionY = true;
    [Tooltip("允许相机沿世界 Z 轴移动")]
    public bool allowPositionZ;
    [Tooltip("限制世界 X 坐标范围")]
    public bool clampPositionX;
    public Vector2 positionXRange = new Vector2(-10f, 10f);
    [Tooltip("限制世界 Y 坐标范围")]
    public bool clampPositionY;
    public Vector2 positionYRange = new Vector2(-10f, 10f);
    [Tooltip("限制世界 Z 坐标范围")]
    public bool clampPositionZ;
    public Vector2 positionZRange = new Vector2(-10f, 10f);

    [Header("平滑插值系数")]
    [Tooltip("平滑跟手的插值系数(越大越跟手)")]
    public float smoothLerpFactor = 10f;

    [Header("运动曲线")]
    [Tooltip("用曲线控制归一化进度；适合非匀速的推拉、平移和环绕")]
    public bool useMotionCurve;
    [Tooltip("X=时间进度，Y=运动进度")]
    public AnimationCurve motionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("变速插值设置")]
    [Tooltip("启用变速(起/终点速度线性过渡)")]
    public bool useVariableSpeed = false;
    [Tooltip("变速起始速度(0=静止起步)")]
    public float startSpeed = 1f;
    [Tooltip("变速结束速度")]
    public float endSpeed = 3f;

    [Header("以上一帧相机位置为本段起点")]
    [Tooltip("连续运镜：勾选后以片段开始时的相机位置为插值起点，不做归位")]
    public bool useLastFrameAsOrigin;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        CameraTimelineBehaviour behaviour = new CameraTimelineBehaviour();
        behaviour.clip = this;
        return ScriptPlayable<CameraTimelineBehaviour>.Create(graph, behaviour);
    }
}
