using UnityEngine;
using UnityEngine.Timeline;

[TrackClipType(typeof(DialogueTimelineClip))]
[TrackColor(.28f, .66f, .92f)]
public class DialogueTrack : TrackAsset
{
    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "对话";
    }
}
