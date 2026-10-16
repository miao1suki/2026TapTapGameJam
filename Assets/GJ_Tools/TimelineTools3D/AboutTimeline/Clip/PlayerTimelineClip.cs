using UnityEngine;

[System.Serializable]
public sealed class PlayerTimelineClip : ActorTimelineClipBase
{
    [Header("玩家输入")]
    public bool applyMoveInput;
    public Vector2 moveInput;
    public bool applySprint;
    public bool sprint;
    public bool applyJumpHeld;
    public bool jumpHeld;
    public bool requestJump;
    public float jumpBoostSpeed = -1f;
    public bool restoreInputOnEnd = true;

    [Header("玩家控制锁")]
    public bool lockControlWhileClip;
}
