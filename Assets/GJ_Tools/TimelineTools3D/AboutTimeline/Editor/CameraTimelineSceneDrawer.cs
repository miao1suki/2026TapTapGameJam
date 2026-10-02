using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class CameraTimelineSceneDrawer
{
    static CameraTimelineSceneDrawer()
    {
        SceneView.duringSceneGui += OnSceneDraw;
    }

    private static void OnSceneDraw(SceneView sceneView)
    {
        TimelineClip clip = TimelineEditor.selectedClip;
        if (clip == null)
        {
            return;
        }

        CameraTimelineClip camClip = clip.asset as CameraTimelineClip;
        if (camClip == null)
        {
            return;
        }

        PlayableDirector director = TimelineEditor.inspectedDirector;
        if (director == null)
        {
            return;
        }

        TrackAsset track = clip.GetParentTrack();
        if (track == null)
        {
            return;
        }

        TimelineCamRig rig = director.GetGenericBinding(track) as TimelineCamRig;
        if (rig == null)
        {
            return;
        }

        Transform anchor = rig.AnchorTransform;
        if (camClip.cameraMoveMode == CamMoveMode.ResetOrigin)
        {
            TimelineSceneStyle.Tag(
                "归位模式：无目标机位轨迹",
                anchor.position + Vector3.up * 1.2f,
                TimelineSceneStyle.CamLine);
            sceneView.Repaint();
            return;
        }

        Vector3 fallbackOrigin = rig.CurrentPosition;
        Vector3 originWorldPos = ResolveClipStartWorld(
            camClip,
            clip,
            track,
            rig,
            anchor,
            fallbackOrigin,
            0);
        DrawPath(
            camClip,
            rig,
            anchor,
            originWorldPos,
            (float)clip.duration);

        Vector3 targetWorldPos = EvaluatePathPosition(
            camClip,
            rig,
            anchor,
            originWorldPos,
            1f,
            (float)clip.duration);
        Quaternion endTurn = Quaternion.Euler(
            0f,
            EvaluatePlanarYaw(
                camClip,
                rig,
                1f,
                (float)clip.duration),
            0f);
        Quaternion targetWorldRot =
            anchor.rotation *
            endTurn *
            Quaternion.Euler(camClip.cameraTargetEuler);

        Vector3 dragWorldPos = Handles.PositionHandle(
            targetWorldPos,
            camClip.useSurroundMode ? Quaternion.identity : anchor.rotation);
        if (Vector3.Distance(targetWorldPos, dragWorldPos) > 0.0001f)
        {
            Undo.RecordObject(camClip, "拖拽相机轨迹终点");
            if (camClip.useSurroundMode)
            {
                ApplySurroundEndDrag(
                    camClip,
                    anchor,
                    originWorldPos,
                    dragWorldPos);
            }
            else
            {
                Vector3 newLocalOffset =
                    Quaternion.Inverse(endTurn) *
                    anchor.InverseTransformPoint(dragWorldPos);
                camClip.cameraTargetLocalPos = newLocalOffset;
            }
            EditorUtility.SetDirty(camClip);
        }

        targetWorldPos = EvaluatePathPosition(
            camClip,
            rig,
            anchor,
            originWorldPos,
            1f,
            (float)clip.duration);

        if (!camClip.lockLookAtPlayer)
        {
            Quaternion dragWorldRot = Handles.RotationHandle(
                targetWorldRot,
                targetWorldPos);
            if (Quaternion.Angle(targetWorldRot, dragWorldRot) > 0.01f)
            {
                Undo.RecordObject(camClip, "拖拽相机轨迹朝向");
                Quaternion localRot =
                    Quaternion.Inverse(anchor.rotation * endTurn) *
                    dragWorldRot;
                camClip.cameraTargetEuler = localRot.eulerAngles;
                EditorUtility.SetDirty(camClip);
                targetWorldRot = dragWorldRot;
            }
        }

        DrawCameraCone(
            camClip,
            rig,
            targetWorldPos,
            targetWorldRot);

        string tip = camClip.useSurroundMode
            ? "环绕轨迹终点：拖拽可改半径、角度与高度"
            : "相机轨迹终点：拖拽可改位置";
        if (!camClip.lockLookAtPlayer)
        {
            tip += " · 可拖旋转";
        }
        if (camClip.turnTiming != Camera2DTurnTiming.None)
        {
            tip += $" · 2D 转向 {camClip.turnAngleDegrees:F0}°";
        }

        TimelineSceneStyle.Tag(
            tip,
            targetWorldPos + Vector3.up * 0.3f,
            TimelineSceneStyle.CamLine);
        TimelineSceneStyle.Tag(
            "轨迹起点",
            originWorldPos + Vector3.up * 0.3f,
            TimelineSceneStyle.CamLine);

        sceneView.Repaint();
    }

    private static void DrawPath(
        CameraTimelineClip clip,
        TimelineCamRig rig,
        Transform anchor,
        Vector3 originWorldPos,
        float clipDuration)
    {
        const int sampleCount = 48;
        Vector3 previous = EvaluatePathPosition(
            clip,
            rig,
            anchor,
            originWorldPos,
            0f,
            clipDuration);

        Handles.color = TimelineSceneStyle.CamLine;
        Handles.SphereHandleCap(
            0,
            previous,
            Quaternion.identity,
            HandleUtility.GetHandleSize(previous) * 0.08f,
            EventType.Repaint);

        for (int index = 1; index <= sampleCount; index++)
        {
            float timeProgress = index / (float)sampleCount;
            Vector3 current = EvaluatePathPosition(
                clip,
                rig,
                anchor,
                originWorldPos,
                timeProgress,
                clipDuration);
            Handles.DrawLine(
                previous,
                current,
                TimelineSceneStyle.MainLineWidth);
            previous = current;
        }

        for (int index = 1; index < 4; index++)
        {
            float timeProgress = index / 4f;
            Vector3 point = EvaluatePathPosition(
                clip,
                rig,
                anchor,
                originWorldPos,
                timeProgress,
                clipDuration);
            Handles.SphereHandleCap(
                0,
                point,
                Quaternion.identity,
                HandleUtility.GetHandleSize(point) * 0.045f,
                EventType.Repaint);
        }

        Handles.color = TimelineSceneStyle.CamLine;
    }

    private static Vector3 EvaluatePathPosition(
        CameraTimelineClip clip,
        TimelineCamRig rig,
        Transform anchor,
        Vector3 originWorldPos,
        float timeProgress,
        float clipDuration)
    {
        float progress = EvaluateProgress(clip, timeProgress);
        if (!clip.useSurroundMode)
        {
            Quaternion turn = Quaternion.Euler(
                0f,
                EvaluatePlanarYaw(
                    clip,
                    rig,
                    timeProgress,
                    clipDuration),
                0f);
            Vector3 targetWorldPos =
                anchor.TransformPoint(turn * clip.cameraTargetLocalPos);
            return Vector3.Lerp(originWorldPos, targetWorldPos, progress);
        }

        Vector3 foot = anchor.position;
        float radiusScale =
            clip.useSurroundRadiusCurve && clip.surroundRadiusCurve != null
                ? clip.surroundRadiusCurve.Evaluate(progress)
                : 1f;
        float heightScale =
            clip.useSurroundHeightCurve && clip.surroundHeightCurve != null
                ? clip.surroundHeightCurve.Evaluate(progress)
                : 1f;
        Vector3 center = new Vector3(
            foot.x,
            foot.y + clip.surroundFixedHeight * heightScale,
            foot.z);
        Vector3 originFlat = originWorldPos - foot;
        originFlat.y = 0f;
        float startAngle =
            Mathf.Atan2(originFlat.x, originFlat.z) *
            Mathf.Rad2Deg;
        float currentAngle =
            startAngle +
            clip.surroundTotalAngle * progress;
        float radians = currentAngle * Mathf.Deg2Rad;
        return center + new Vector3(
            Mathf.Sin(radians) * clip.surroundRadius * radiusScale,
            0f,
            Mathf.Cos(radians) * clip.surroundRadius * radiusScale);
    }

    private static Vector3 ResolveClipStartWorld(
        CameraTimelineClip clip,
        TimelineClip timelineClip,
        TrackAsset track,
        TimelineCamRig rig,
        Transform anchor,
        Vector3 fallbackOrigin,
        int depth)
    {
        if (depth > 32 || clip == null || timelineClip == null || track == null)
        {
            return fallbackOrigin;
        }

        TimelineClip previousTimelineClip =
            FindPreviousClip(track, timelineClip);
        if (previousTimelineClip == null)
        {
            return fallbackOrigin;
        }

        CameraTimelineClip previousClip =
            previousTimelineClip.asset as CameraTimelineClip;
        if (previousClip == null)
        {
            return fallbackOrigin;
        }

        return EvaluateClipEnd(
            previousClip,
            previousTimelineClip,
            track,
            rig,
            anchor,
            fallbackOrigin,
            depth + 1);
    }

    private static Vector3 EvaluateClipEnd(
        CameraTimelineClip clip,
        TimelineClip timelineClip,
        TrackAsset track,
        TimelineCamRig rig,
        Transform anchor,
        Vector3 fallbackOrigin,
        int depth)
    {
        if (clip.cameraMoveMode == CamMoveMode.ResetOrigin)
        {
            return fallbackOrigin;
        }

        Vector3 startWorld = ResolveClipStartWorld(
            clip,
            timelineClip,
            track,
            rig,
            anchor,
            fallbackOrigin,
            depth + 1);
        return EvaluatePathPosition(
            clip,
            rig,
            anchor,
            startWorld,
            1f,
            (float)timelineClip.duration);
    }

    private static TimelineClip FindPreviousClip(
        TrackAsset track,
        TimelineClip currentClip)
    {
        TimelineClip previous = null;
        foreach (TimelineClip candidate in track.GetClips())
        {
            if (candidate == currentClip ||
                candidate.start >= currentClip.start)
            {
                continue;
            }

            if (previous == null || candidate.start > previous.start)
            {
                previous = candidate;
            }
        }
        return previous;
    }

    private static float EvaluatePlanarYaw(
        CameraTimelineClip clip,
        TimelineCamRig rig,
        float timeProgress,
        float clipDuration)
    {
        float yaw = rig.PlanarYaw;
        if (!IsOrthographicForPreview(clip, rig) ||
            clipDuration <= 0.0001f)
        {
            return yaw;
        }

        if (clip.turnTiming == Camera2DTurnTiming.None)
        {
            return clip.modeRequest == TimelineCameraModeRequest.Side2D &&
                   clip.overrideSide2DYaw
                ? clip.side2DYawDegrees
                : yaw;
        }

        float turnDuration = Mathf.Clamp(
            clip.turnDuration,
            0.01f,
            clipDuration);
        float currentTime =
            Mathf.Clamp01(timeProgress) * clipDuration;
        float localTime = clip.turnTiming == Camera2DTurnTiming.AtClipStart
            ? currentTime
            : currentTime - (clipDuration - turnDuration);
        float progress = Mathf.Clamp01(localTime / turnDuration);
        if (clip.turnCurve != null)
        {
            progress = Mathf.Clamp01(clip.turnCurve.Evaluate(progress));
        }
        float targetYaw =
            clip.modeRequest == TimelineCameraModeRequest.Side2D &&
            clip.overrideSide2DYaw
                ? clip.side2DYawDegrees
                : yaw + clip.turnAngleDegrees;
        return Mathf.LerpAngle(yaw, targetYaw, progress);
    }

    private static float EvaluateProgress(
        CameraTimelineClip clip,
        float timeProgress)
    {
        float progress = Mathf.Clamp01(timeProgress);
        if (clip.useVariableSpeed)
        {
            progress = EvaluateVariableProgress(clip, progress);
        }
        if (clip.useMotionCurve && clip.motionCurve != null)
        {
            progress = Mathf.Clamp01(clip.motionCurve.Evaluate(progress));
        }
        return progress;
    }

    private static float EvaluateVariableProgress(
        CameraTimelineClip clip,
        float normalizedTime)
    {
        float duration = 1f;
        float currentTime = normalizedTime;
        float startSpeed = clip.startSpeed;
        float endSpeed = clip.endSpeed;
        float totalDisplacement =
            (startSpeed + endSpeed) * 0.5f * duration;
        if (totalDisplacement <= 0.0001f)
        {
            return normalizedTime;
        }

        float travel =
            startSpeed * currentTime +
            (endSpeed - startSpeed) *
            currentTime *
            currentTime /
            (2f * duration);
        return Mathf.Clamp01(travel / totalDisplacement);
    }

    private static void ApplySurroundEndDrag(
        CameraTimelineClip clip,
        Transform anchor,
        Vector3 originWorldPos,
        Vector3 dragWorldPos)
    {
        float radiusScale =
            clip.useSurroundRadiusCurve && clip.surroundRadiusCurve != null
                ? clip.surroundRadiusCurve.Evaluate(1f)
                : 1f;
        float heightScale =
            clip.useSurroundHeightCurve && clip.surroundHeightCurve != null
                ? clip.surroundHeightCurve.Evaluate(1f)
                : 1f;
        Vector3 foot = anchor.position;
        Vector3 dragFlat = dragWorldPos - foot;
        dragFlat.y = 0f;
        Vector3 originFlat = originWorldPos - foot;
        originFlat.y = 0f;

        float startAngle =
            Mathf.Atan2(originFlat.x, originFlat.z) *
            Mathf.Rad2Deg;
        float dragAngle =
            Mathf.Atan2(dragFlat.x, dragFlat.z) *
            Mathf.Rad2Deg;
        float angleDelta = Mathf.DeltaAngle(startAngle, dragAngle);
        float existingTurns = Mathf.Round(
            (clip.surroundTotalAngle - angleDelta) / 360f) *
            360f;

        clip.surroundRadius = Mathf.Max(
            0.01f,
            dragFlat.magnitude / Mathf.Max(0.0001f, radiusScale));
        clip.surroundTotalAngle = angleDelta + existingTurns;
        if (heightScale > 0.0001f)
        {
            clip.surroundFixedHeight = Mathf.Max(
                0f,
                (dragWorldPos.y - foot.y) / heightScale);
        }
    }

    private static void DrawCameraCone(
        CameraTimelineClip camClip,
        TimelineCamRig rig,
        Vector3 targetWorldPos,
        Quaternion targetWorldRot)
    {
        float coneLength = 3f;
        bool useOrthographic =
            IsOrthographicForPreview(camClip, rig);
        float coneHalfAngle = useOrthographic
            ? 30f
            : rig != null && rig.Manager != null
                ? rig.Manager.CurrentState.fieldOfView * 0.5f
                : 25f;

        Handles.color = TimelineSceneStyle.CamCone;
        using (new Handles.DrawingScope(Matrix4x4.TRS(
                   targetWorldPos,
                   targetWorldRot,
                   Vector3.one)))
        {
            Vector3 forwardTip = Vector3.forward * coneLength;
            if (useOrthographic)
            {
                float aspect = SceneView.lastActiveSceneView != null
                    ? SceneView.lastActiveSceneView.camera.aspect
                    : 16f / 9f;
                float halfHeight = rig != null &&
                                   rig.Manager != null
                    ? rig.Manager.CurrentState.orthographicSize
                    : 5f;
                float halfWidth = halfHeight * aspect;
                Vector3[] frame =
                {
                    new Vector3(-halfWidth, -halfHeight, coneLength),
                    new Vector3(-halfWidth, halfHeight, coneLength),
                    new Vector3(halfWidth, halfHeight, coneLength),
                    new Vector3(halfWidth, -halfHeight, coneLength)
                };
                for (int index = 0; index < frame.Length; index++)
                {
                    Handles.DrawLine(
                        frame[index],
                        frame[(index + 1) % frame.Length],
                        TimelineSceneStyle.ThinLineWidth);
                    Handles.DrawLine(
                        Vector3.zero,
                        frame[index],
                        TimelineSceneStyle.ThinLineWidth);
                }
                return;
            }

            int segmentCount = 24;
            float radStep = Mathf.PI * 2f / segmentCount;
            float radius =
                Mathf.Tan(Mathf.Deg2Rad * coneHalfAngle) *
                coneLength;
            Vector3 lastPoint = new Vector3(radius, 0f, coneLength);
            for (int index = 1; index <= segmentCount; index++)
            {
                float radians = radStep * index;
                Vector3 currentPoint = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    coneLength);
                Handles.DrawLine(
                    lastPoint,
                    currentPoint,
                    TimelineSceneStyle.ThinLineWidth);
                lastPoint = currentPoint;
            }
            Handles.DrawLine(
                Vector3.zero,
                forwardTip,
                TimelineSceneStyle.MainLineWidth);

            int boneCount = 8;
            float boneStep = Mathf.PI * 2f / boneCount;
            for (int index = 0; index < boneCount; index++)
            {
                float radians = boneStep * index;
                Vector3 ringPoint = new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    coneLength);
                Handles.DrawLine(
                    Vector3.zero,
                    ringPoint,
                    TimelineSceneStyle.ThinLineWidth);
            }
        }
    }

    private static bool IsOrthographicForPreview(
        CameraTimelineClip clip,
        TimelineCamRig rig)
    {
        if (clip.modeRequest == TimelineCameraModeRequest.Side2D)
        {
            return true;
        }
        if (clip.modeRequest == TimelineCameraModeRequest.Perspective3D)
        {
            return false;
        }
        return rig != null && rig.IsCurrentProjectionOrthographic;
    }
}
