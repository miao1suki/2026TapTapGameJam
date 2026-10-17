using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class BossTimelineSceneDrawer
{
    private static readonly Color BossRouteColor =
        new Color(1f, .3f, .12f, .95f);

    static BossTimelineSceneDrawer()
    {
        SceneView.duringSceneGui += OnSceneDraw;
    }

    private static void OnSceneDraw(SceneView sceneView)
    {
        TimelineClip timelineClip = TimelineEditor.selectedClip;
        if (timelineClip == null)
        {
            return;
        }

        PlayableDirector director = TimelineEditor.inspectedDirector;
        TrackAsset track = timelineClip.GetParentTrack();
        if (director == null || track == null)
        {
            return;
        }

        if (timelineClip.asset is not BossTimelineClip bossClip)
        {
            return;
        }

        Transform actor = ResolveActor(director, track, bossClip);
        if (actor == null)
        {
            TimelineSceneStyle.Tag(
                "Boss 轨迹：找不到 Boss 角色",
                director.transform.position + Vector3.up * 1.5f,
                TimelineSceneStyle.Warn);
            return;
        }

        Vector3 start = actor.position;
        Vector3 end = ResolveInputPreviewEnd(
            bossClip,
            start);

        if (bossClip.motionMode == ActorTimelineMotionMode.None)
        {
            DrawMarker(start);
            TimelineSceneStyle.Tag(
                "Boss 轨迹：本片段无位移",
                start + Vector3.up * .6f,
                BossRouteColor);
            sceneView.Repaint();
            return;
        }

        DrawDottedLine(start, end);

        DrawMarker(start);
        DrawMarker(end);
        DrawLabel(bossClip, start, end);
        sceneView.Repaint();
    }

    private static Transform ResolveActor(
        PlayableDirector director,
        TrackAsset track,
        BossTimelineClip clip)
    {
        if (clip.useTrackBinding)
        {
            Transform bound =
                director.GetGenericBinding(track) as Transform;
            if (bound != null)
            {
                return bound;
            }
        }

        if (string.IsNullOrWhiteSpace(clip.actorTag))
        {
            return null;
        }

        GameObject tagged =
            GameObject.FindGameObjectWithTag(clip.actorTag);
        return tagged != null ? tagged.transform : null;
    }

    private static Vector3 ResolveInputPreviewEnd(
        BossTimelineClip clip,
        Vector3 start)
    {
        if (clip.motionMode == ActorTimelineMotionMode.Linear)
        {
            return start +
                   Vector3.right *
                   Mathf.Sign(clip.moveInput) *
                   1.5f;
        }

        if (clip.motionMode == ActorTimelineMotionMode.JumpArc)
        {
            return start + Vector3.up * 1.5f;
        }

        return start;
    }

    private static void DrawDottedLine(
        Vector3 start,
        Vector3 end)
    {
        const int sampleCount = 24;
        Handles.color = BossRouteColor;
        for (int index = 0; index < sampleCount; index += 2)
        {
            Vector3 from = Vector3.Lerp(
                start,
                end,
                index / (float)sampleCount);
            Vector3 to = Vector3.Lerp(
                start,
                end,
                (index + 1) / (float)sampleCount);
            Handles.DrawLine(
                from,
                to,
                TimelineSceneStyle.MainLineWidth);
        }
    }

    private static void DrawMarker(Vector3 position)
    {
        Handles.color = BossRouteColor;
        Handles.SphereHandleCap(
            0,
            position,
            Quaternion.identity,
            HandleUtility.GetHandleSize(position) * .11f,
            EventType.Repaint);
    }

    private static void DrawLabel(
        BossTimelineClip clip,
        Vector3 start,
        Vector3 end)
    {
        string mode = clip.motionMode switch
        {
            ActorTimelineMotionMode.Teleport => "瞬移",
            ActorTimelineMotionMode.Linear => "移动输入",
            ActorTimelineMotionMode.JumpArc => "跳跃请求",
            _ => "无位移"
        };
        if (clip.motionMode == ActorTimelineMotionMode.Linear)
        {
            mode += $"（X = {clip.moveInput:0.##}）";
        }
        else if (clip.motionMode == ActorTimelineMotionMode.JumpArc &&
                 clip.useBossJumpState)
        {
            mode += "（Boss Jump 状态：实际受重力与落地影响）";
        }

        TimelineSceneStyle.Tag(
            $"Boss 轨迹：{mode}",
            Vector3.Lerp(start, end, .5f) + Vector3.up * .7f,
            BossRouteColor);
        TimelineSceneStyle.Tag(
            "起点",
            start + Vector3.up * .35f,
            BossRouteColor);
        TimelineSceneStyle.Tag(
            "输入方向",
            end + Vector3.up * .35f,
            BossRouteColor);
    }
}
