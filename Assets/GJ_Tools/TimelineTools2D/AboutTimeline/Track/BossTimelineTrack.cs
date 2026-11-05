using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(BossTimelineClip))]
[TrackColor(.92f, .28f, .22f)]
public sealed class BossTimelineTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Boss";
    }
}
