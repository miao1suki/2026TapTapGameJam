using Project.BossLogic;
using UnityEngine;

[System.Serializable]
public sealed class BossTimelineClip : ActorTimelineClipBase
{
    [Header("Boss 生成")]
    public bool spawnBoss;
    public BossController bossPrefab;

    [Header("移动")]
    [Min(0f)] public float moveSpeed = 5f;

    [Header("Boss 状态")]
    public bool setTrackingOnStart;
    public bool trackingOnStart;
    public bool setTrackingOnEnd;
    public bool trackingOnEnd = true;
    public bool useBossJumpState = true;
    public bool killAtEnd;
}
