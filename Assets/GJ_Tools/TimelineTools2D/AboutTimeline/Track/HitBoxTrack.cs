using UnityEngine;
using UnityEngine.Timeline;

public enum HitBoxShape
{
    [InspectorName("球")]
    Sphere,
    [InspectorName("盒")]
    Box,
    [InspectorName("扇形")]
    Sector,
}

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(HitBoxClip))]
[TrackColor(0.95f, 0.28f, 0.24f)]
public class HitBoxTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "HitBox";
    }
}
