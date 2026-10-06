using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Interactions
{
    /// <summary>
    /// 交互图节点。图以物体定义为边界，颜色只作为物体上的属性参与条件和表现。
    /// </summary>
    public enum InteractionNodeKind
    {
        Invalid = 0,

        // 触发器
        PlayerEntered = 10,
        PlayerLeft = 11,
        ObjectTouched = 12,
        ObjectStay = 13,
        TimeElapsed = 14,
        Manual = 15,

        // 条件 / 上下文
        RequireSelf = 20,
        RequirePlayer = 21,
        RequireOtherObject = 22,
        RequireColor = 23,
        RequireObjectId = 24,
        RequireOtherColor = 25,

        // 调度与效果
        Delay = 30,
        Fade = 31,
        Restore = 32,
        PlayTimeline = 33,
        InvokeMethod = 34,
        SetColor = 35,
        Log = 36
    }

    [Serializable]
    public sealed class InteractionGraphNode
    {
        public string id = Guid.NewGuid().ToString("N");
        public Vector2 position;
        public InteractionNodeKind kind;

        [Tooltip("颜色 ID、物体 ID、方法名或日志内容。")]
        public string value;
        [Tooltip("延迟、时间或淡入淡出时长。")]
        public float number;
        [Tooltip("方法调用的可选字符串参数。")]
        public string argument;
        [Tooltip("Timeline 资源或其他节点引用。")]
        public UnityEngine.Object reference;
    }

    [Serializable]
    public sealed class InteractionGraphEdge
    {
        public string fromId;
        public string toId;
    }

}
