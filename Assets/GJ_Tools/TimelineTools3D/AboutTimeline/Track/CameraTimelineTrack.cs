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

    [Tooltip("轨道结束后交还 Project 玩法相机的平滑时间。")]
    [Min(0f)]
    public float returnTransitionDuration = .25f;

    [Tooltip("Timeline 接管相机时的投影切换时间；0 表示第一帧直接进入演出投影。")]
    [Min(0f)]
    public float entryTransitionDuration = .25f;

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
