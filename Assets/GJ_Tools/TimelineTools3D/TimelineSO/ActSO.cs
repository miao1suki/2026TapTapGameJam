using UnityEngine;
using UnityEngine.Timeline;

[CreateAssetMenu(
    fileName = "ActSO",
    menuName = "TimelineKit/ActSO")]
public sealed class ActSO : ScriptableObject
{
    [Tooltip("动作编号。")]
    [SerializeField]
    private int actionId;

    [Tooltip("动作名字。")]
    [SerializeField]
    private string actionName;

    [Tooltip("该动作播放的 TimelineAsset。")]
    [SerializeField]
    private TimelineAsset timeline;

    [Tooltip("当前动作结束后没有新输入时，默认切换到的下一个动作数据盒。")]
    [SerializeField]
    private ActSO defaultNextAction;

    [Tooltip("动作优先级，数值越大越优先。")]
    [SerializeField]
    private int priority;

    [Tooltip("是否允许被新的动作请求打断。")]
    [SerializeField]
    private bool interruptible = true;

    [Tooltip("该动作播放期间是否锁定玩家自身移动。")]
    [SerializeField]
    private bool lockMovement;

    [Tooltip("Timeline 播放速度。")]
    [SerializeField, Min(0.01f)]
    private float playbackSpeed = 1f;

    public int ActionId => actionId;
    public string ActionName => actionName;
    public TimelineAsset Timeline => timeline;
    public ActSO DefaultNextAction => defaultNextAction;
    public int Priority => priority;
    public bool Interruptible => interruptible;
    public bool LockMovement => lockMovement;
    public float PlaybackSpeed => playbackSpeed;
}
