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

        Transform owner = director.transform;
        Vector3 start = ResolveClipStart(
            actor,
            track,
            timelineClip,
            owner);
        Vector3 end = ResolveTarget(bossClip, owner);

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

        if (bossClip.motionMode == ActorTimelineMotionMode.Teleport)
        {
            DrawDottedLine(start, end);
        }
        else
        {
            DrawMotionPath(bossClip, start, end);
        }

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

    private static Vector3 ResolveClipStart(
        Transform actor,
        TrackAsset track,
        TimelineClip current,
        Transform owner)
    {
        TimelineClip previous = FindPreviousClip(track, current);
        int guard = 0;
        while (previous != null && guard++ < 64)
        {
            BossTimelineClip previousClip =
                previous.asset as BossTimelineClip;
            if (previousClip != null &&
                previousClip.motionMode !=
                ActorTimelineMotionMode.None)
            {
                return ResolveTarget(previousClip, owner);
            }

            previous = FindPreviousClip(track, previous);
        }

        return actor.position;
    }

    private static TimelineClip FindPreviousClip(
        TrackAsset track,
        TimelineClip current)
    {
        TimelineClip previous = null;
        foreach (TimelineClip candidate in track.GetClips())
        {
            if (candidate == current ||
                candidate.start >= current.start)
            {
                continue;
            }

            if (previous == null ||
                candidate.start > previous.start)
            {
                previous = candidate;
            }
        }

        return previous;
    }

    private static Vector3 ResolveTarget(
        BossTimelineClip clip,
        Transform owner)
    {
        return clip.useOwnerAsOrigin && owner != null
            ? owner.TransformPoint(clip.targetOffset)
            : clip.targetOffset;
    }

    private static void DrawMotionPath(
        BossTimelineClip clip,
        Vector3 start,
        Vector3 end)
    {
        const int sampleCount = 64;
        Vector3 previous = EvaluateMotion(
            clip,
            start,
            end,
            0f);

        Handles.color = BossRouteColor;
        Handles.SphereHandleCap(
            0,
            previous,
            Quaternion.identity,
            HandleUtility.GetHandleSize(previous) * .07f,
            EventType.Repaint);

        for (int index = 1; index <= sampleCount; index++)
        {
            float progress = index / (float)sampleCount;
            Vector3 point = EvaluateMotion(
                clip,
                start,
                end,
                progress);
            Handles.DrawLine(
                previous,
                point,
                TimelineSceneStyle.MainLineWidth);
            previous = point;
        }

        Handles.DrawLine(
            start,
            start + Vector3.up * .35f,
            TimelineSceneStyle.ThinLineWidth);
        Handles.DrawLine(
            end,
            end + Vector3.up * .35f,
            TimelineSceneStyle.ThinLineWidth);
    }

    private static Vector3 EvaluateMotion(
        BossTimelineClip clip,
        Vector3 start,
        Vector3 end,
        float progress)
    {
        float curveProgress = clip.motionCurve != null
            ? clip.motionCurve.Evaluate(progress)
            : progress;
        Vector3 position = Vector3.Lerp(
            start,
            end,
            curveProgress);
        if (clip.motionMode == ActorTimelineMotionMode.JumpArc)
        {
            position.y +=
                4f *
                Mathf.Max(0f, clip.jumpArcHeight) *
                progress *
                (1f - progress);
        }

        return position;
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
            ActorTimelineMotionMode.Linear => "平滑移动",
            ActorTimelineMotionMode.JumpArc => "弧线跳跃",
            _ => "无位移"
        };
        if (clip.motionMode == ActorTimelineMotionMode.JumpArc &&
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
            "终点",
            end + Vector3.up * .35f,
            BossRouteColor);
    }
}
