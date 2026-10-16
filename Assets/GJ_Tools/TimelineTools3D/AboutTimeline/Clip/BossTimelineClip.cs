using UnityEngine;

[System.Serializable]
public sealed class BossTimelineClip : ActorTimelineClipBase
{
    [Header("Boss 状态")]
    public bool setTrackingOnStart;
    public bool trackingOnStart;
    public bool setTrackingOnEnd;
    public bool trackingOnEnd = true;
    public bool useBossJumpState = true;
    public bool killAtEnd;
}
