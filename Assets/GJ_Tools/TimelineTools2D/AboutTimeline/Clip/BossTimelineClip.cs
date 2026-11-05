using Project.BossLogic;
using UnityEngine;

[System.Serializable]
public sealed class BossTimelineClip : ActorTimelineClipBase
{
    [Header("Boss 生成")]
    public bool spawnBoss;
    public BossController bossPrefab;

    [Header("操作输入")]
    [Range(-1f, 1f)] public float moveInput;

    [Header("Boss 状态")]
    public bool setTrackingOnStart;
    public bool trackingOnStart;
    public bool setTrackingOnEnd;
    public bool trackingOnEnd = true;
    public bool useBossJumpState = true;
    public bool killAtEnd;
}
