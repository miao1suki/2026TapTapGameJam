using UnityEngine.Playables;
using UnityEngine;
using UnityEngine.Timeline;

[TrackBindingType(typeof(TimelineCamRig))]
[TrackClipType(typeof(CameraTimelineClip))]
[TrackColor(0.48f, 0.38f, 0.96f)]
public class CameraTimelineTrack : TrackAsset
{
    [Header("轨道策略")]
    [Tooltip("允许玩家在轨道播放期间手动拖动视角。")]
    public bool allowManualCamera;

    [Tooltip("轨道结束后平滑交还 Project 玩法相机；关闭时立即交还。")]
    public bool restoreOriginOnEnd = true;

    protected override void OnCreateClip(TimelineClip clip)
    {
        clip.displayName = "Camera";
    }

    public override Playable CreateTrackMixer(
        PlayableGraph graph,
        GameObject go,
        int inputCount)
    {
        CameraTimelineMixerBehaviour behaviour = new CameraTimelineMixerBehaviour
        {
            track = this
        };
        return ScriptPlayable<CameraTimelineMixerBehaviour>.Create(
            graph,
            behaviour,
            inputCount);
    }
}
