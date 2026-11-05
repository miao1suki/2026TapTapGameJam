using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum ActorTimelineMotionMode
{
    [InspectorName("无")]
    None,
    [InspectorName("瞬移")]
    Teleport,
    [InspectorName("平滑移动")]
    Linear,
    [InspectorName("弧线跳跃")]
    JumpArc
}

public abstract class ActorTimelineClipBase :
    PlayableAsset,
    ITimelineClipAsset
{
    [Header("角色解析")]
    [Tooltip("未显式绑定轨道对象时，按此 Unity Tag 查找角色。")]
    public string actorTag = "Player";
    [Tooltip("优先使用 Timeline 轨道绑定；找不到时再按 Tag 查找。")]
    public bool useTrackBinding = true;

    [Header("运动")]
    public ActorTimelineMotionMode motionMode =
        ActorTimelineMotionMode.None;
    [Tooltip("以片段挂载物体为原点；关闭时 targetOffset 作为世界坐标终点。")]
    public bool useOwnerAsOrigin = true;
    public Vector3 targetOffset;
    [Min(0.01f)] public float motionDuration = 1f;
    [Min(0f)] public float jumpArcHeight = 1f;
    public AnimationCurve motionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    public bool driveRigidbody = true;
    public bool clearVelocity = true;

    [Header("控制")]
    [Tooltip("直接驱动运动时临时锁住玩家控制，避免物理状态机同时写入速度。")]
    public bool lockControlWhileDriving = true;

    public virtual ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ActorTimelineBehaviour behaviour =
            new ActorTimelineBehaviour
            {
                clip = this,
                owner = owner
            };
        return ScriptPlayable<ActorTimelineBehaviour>.Create(
            graph,
            behaviour);
    }
}
