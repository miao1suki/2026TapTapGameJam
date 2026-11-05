using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(Transform))]
[TrackClipType(typeof(EffectAudioClip))]
[TrackColor(0.98f, 0.64f, 0.18f)]
public class EffectAudioTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Effect/Audio";
    }
}
