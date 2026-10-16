using UnityEngine;
using UnityEngine.Timeline;

namespace Project.BossLogic
{
    [TrackClipType(typeof(BossIntroClip))]
    [TrackColor(.92f, .35f, .22f)]
    public sealed class BossIntroTrack : TrackAsset
    {
        protected override void OnCreateClip(TimelineClip clip)
        {
            clip.displayName = "Boss 入场";
        }
    }
}
