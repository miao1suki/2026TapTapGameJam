using UnityEngine;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    [TrackColor(.78f, .18f, .12f)]
    [TrackClipType(typeof(BossExecutionClip))]
    public sealed class BossExecutionTimelineTrack : TrackAsset
    {
    }
}
