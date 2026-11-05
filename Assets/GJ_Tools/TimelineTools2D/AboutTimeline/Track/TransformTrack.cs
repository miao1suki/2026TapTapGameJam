using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(TransformTimelineClip))]
[TrackColor(0.22f, 0.72f, 0.42f)]
public class TransformTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Transform";
    }
}
