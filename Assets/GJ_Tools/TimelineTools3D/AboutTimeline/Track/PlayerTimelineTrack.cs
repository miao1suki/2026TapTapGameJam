using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(PlayerTimelineClip))]
[TrackColor(.25f, .62f, .96f)]
public sealed class PlayerTimelineTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "玩家";
    }
}
