using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TransformTimelineSceneDrawer
{
    static TransformTimelineSceneDrawer()
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
        TransformTimelineClip transClip = clip.asset as TransformTimelineClip;
        if (transClip == null)
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
        Transform bindTrans = director.GetGenericBinding(track) as Transform;
        if (bindTrans == null)
        {
            return;
        }

        MoveMode curMode = transClip.moveMode;
        float clipDuration = (float)clip.duration;
        Vector3 worldStart = ResolveClipStartWorld(
            transClip,
            clip,
            track,
            bindTrans,
            bindTrans.position,
            0);
        bool inheritedStart =
            FindPreviousClip(track, clip) != null;
        string startLabel = inheritedStart
            ? "位移起点(承接上一段终点)"
            : "位移起点";

        bool speedZeroWarn = false;
        if (curMode == MoveMode.SpeedAndDistance && Mathf.Approximately(transClip.moveSpeed, 0f))
        {
            speedZeroWarn = true;
        }
        if (curMode == MoveMode.VariableSpeed)
        {
            if (Mathf.Approximately(transClip.startSpeed, 0f) && Mathf.Approximately(transClip.endSpeed, 0f))
            {
                speedZeroWarn = true;
            }
        }
        if (curMode == MoveMode.Jump &&
            !transClip.useSpeedCurve &&
            Mathf.Approximately(transClip.moveSpeed, 0f))
        {
            speedZeroWarn = true;
        }
        if (speedZeroWarn)
        {
            TimelineSceneStyle.Tag("速度全为0，位移不生效", worldStart + Vector3.up * 1.2f, TimelineSceneStyle.Warn);
        }

        Handles.color = TimelineSceneStyle.Move;

        if (curMode == MoveMode.FixedEndPos)
        {
            Vector3 worldEnd =
                worldStart +
                bindTrans.rotation * transClip.endPos;
            Quaternion endRot = bindTrans.rotation * Quaternion.Euler(transClip.endEuler);
            Handles.DrawLine(worldStart, worldEnd, TimelineSceneStyle.MainLineWidth);
            Handles.SphereHandleCap(0, worldStart, Quaternion.identity, 0.15f, EventType.Repaint);
            TimelineSceneStyle.Tag(startLabel, worldStart + Vector3.up * 0.3f);

            Vector3 newWorldEnd = Handles.PositionHandle(worldEnd, bindTrans.rotation);
            Vector3 newLocalEnd =
                Quaternion.Inverse(bindTrans.rotation) *
                (newWorldEnd - worldStart);
            if (Vector3.Distance(transClip.endPos, newLocalEnd) > 0.0001f)
            {
                Undo.RecordObject(transClip, "修改瞬移终点");
                transClip.endPos = newLocalEnd;
                EditorUtility.SetDirty(transClip);
            }

            Handles.SphereHandleCap(0, worldEnd, endRot, 0.15f, EventType.Repaint);
            Handles.color = TimelineSceneStyle.MoveEnd;
            Handles.DrawLine(worldEnd, worldEnd + endRot * Vector3.forward * 0.6f, TimelineSceneStyle.ThinLineWidth);
            TimelineSceneStyle.Tag("瞬移终点(可穿墙,本地偏移)", worldEnd + Vector3.up * 0.3f);
        }
        else if (curMode == MoveMode.SpeedAndDistance ||
                 curMode == MoveMode.VariableSpeed ||
                 curMode == MoveMode.Jump)
        {
            Vector3 worldDir = ResolveMoveDirection(
                transClip,
                bindTrans);
            float previewDistance = EvaluateMovementDistance(
                transClip,
                clipDuration);
            Vector3 worldEnd =
                worldStart +
                worldDir * previewDistance;

            Handles.DrawLine(worldStart, worldEnd, TimelineSceneStyle.MainLineWidth);
            Handles.SphereHandleCap(0, worldStart, Quaternion.identity, 0.15f, EventType.Repaint);
            TimelineSceneStyle.Tag(startLabel, worldStart + Vector3.up * 0.3f);

            Vector3 newWorldEnd = Handles.PositionHandle(worldEnd, bindTrans.rotation);
            Vector3 deltaWorld = newWorldEnd - worldStart;
            float dragDist = Mathf.Max(0.01f, deltaWorld.magnitude);
            Vector3 newLocalDir = bindTrans.InverseTransformDirection(deltaWorld);
            newLocalDir.Normalize();

            if (Vector3.Distance(worldEnd, newWorldEnd) > 0.0001f && clipDuration > 0.0001f)
            {
                Undo.RecordObject(transClip, "拖拽调整路线，计算速度");
                transClip.direction = newLocalDir;
                transClip.totalDistance = dragDist;
                if (curMode == MoveMode.SpeedAndDistance ||
                    curMode == MoveMode.Jump)
                {
                    if (!transClip.useSpeedCurve)
                    {
                        transClip.moveSpeed = dragDist / clipDuration;
                    }
                }
                else
                {
                    float v0 = transClip.startSpeed;
                    transClip.endSpeed = (2f * dragDist / clipDuration) - v0;
                }
                EditorUtility.SetDirty(transClip);
            }

            Handles.SphereHandleCap(0, worldEnd, Quaternion.identity, 0.15f, EventType.Repaint);
        }
        else if (curMode == MoveMode.CircleRotate)
        {
            Vector3 worldStartPos = worldStart;
            Vector3 originalWorldCenter = PreviewTransformPoint(
                bindTrans,
                worldStart,
                transClip.circleCenterLocal);
            Vector3 dragCenter = Handles.DoPositionHandle(originalWorldCenter, Quaternion.identity);
            bool centerChanged = Vector3.Distance(dragCenter, originalWorldCenter) > 0.0001f;
            if (centerChanged)
            {
                Undo.RecordObject(transClip, "修改绕圈中心点");
                transClip.circleCenterLocal = PreviewInverseTransformPoint(
                    bindTrans,
                    worldStart,
                    dragCenter);
                EditorUtility.SetDirty(transClip);
            }

            Vector3 realWorldCenter = PreviewTransformPoint(
                bindTrans,
                worldStart,
                transClip.circleCenterLocal);
            Vector3 radiusOrigin = realWorldCenter + (worldStartPos - realWorldCenter).normalized * transClip.circleRadius;
            Vector3 dragRadiusPoint = Handles.DoPositionHandle(radiusOrigin, Quaternion.identity);
            float rawRadius = Vector3.Distance(dragRadiusPoint, realWorldCenter);
            bool radiusChanged = !Mathf.Approximately(rawRadius, transClip.circleRadius);
            if (radiusChanged)
            {
                Undo.RecordObject(transClip, "修改转圈半径");
                transClip.circleRadius = rawRadius;
                EditorUtility.SetDirty(transClip);
            }

            Handles.color = TimelineSceneStyle.Move;
            Handles.DrawWireDisc(realWorldCenter, Vector3.up, transClip.circleRadius);
            TimelineSceneStyle.Tag("绕圈中心点(可拖拽)", realWorldCenter + Vector3.up * 2.2f);
            TimelineSceneStyle.Tag($"绕圈半径:{transClip.circleRadius:F2} 总角度:{transClip.circleTotalAngle}°", worldStartPos + Vector3.up * 1.2f);
            if (centerChanged || radiusChanged)
            {
                sceneView.Repaint();
            }
        }

        DrawJumpPreview(
            transClip,
            bindTrans,
            worldStart,
            clipDuration);
        sceneView.Repaint();
    }

    private static void DrawJumpPreview(
        TransformTimelineClip clip,
        Transform bindTransform,
        Vector3 worldStart,
        float clipDuration)
    {
        if (clip.moveMode != MoveMode.Jump ||
            clip.jumpHeight <= 0f ||
            clipDuration <= 0f)
        {
            return;
        }

        float startTime = Mathf.Clamp(clip.jumpStartTime, 0f, clipDuration);
        float endTime = Mathf.Min(clipDuration, startTime + clip.jumpDuration);
        if (endTime <= startTime)
        {
            return;
        }

        Color jumpColor = new Color(0.18f, 0.88f, 1f, 0.95f);
        Handles.color = jumpColor;
        const int sampleCount = 28;
        Vector3 previous = Vector3.zero;
        Vector3 peakWorld = Vector3.zero;
        float peakHeight = float.MinValue;

        for (int index = 0; index <= sampleCount; index++)
        {
            float progress = index / (float)sampleCount;
            float sampleTime = Mathf.Lerp(startTime, endTime, progress);
            Vector3 basePosition = EvaluateBasePosition(
                clip,
                bindTransform,
                worldStart,
                clipDuration,
                sampleTime);
            float height = Mathf.Max(0f, clip.jumpHeightCurve != null
                ? clip.jumpHeightCurve.Evaluate(progress)
                : Mathf.Sin(progress * Mathf.PI));
            Vector3 samplePosition = basePosition + Vector3.up * (clip.jumpHeight * height);
            if (index > 0)
            {
                Handles.DrawLine(previous, samplePosition, TimelineSceneStyle.MainLineWidth);
            }

            if (height > peakHeight)
            {
                peakHeight = height;
                peakWorld = samplePosition;
            }

            previous = samplePosition;
        }

        TimelineSceneStyle.Tag(
            $"高度基准:{clip.jumpHeight:F2} 时长:{clip.jumpDuration:F2}s\n高度请在 Inspector 中修改",
            peakWorld + Vector3.up * 0.35f,
            jumpColor);
    }

    private static Vector3 EvaluateBasePosition(
        TransformTimelineClip clip,
        Transform bindTransform,
        Vector3 worldStart,
        float clipDuration,
        float clipTime)
    {
        float progress = clipDuration > 0f
            ? Mathf.Clamp01(clipTime / clipDuration)
            : 0f;

        if (clip.moveMode == MoveMode.FixedEndPos)
        {
            return worldStart + bindTransform.rotation * clip.endPos;
        }

        if (clip.moveMode == MoveMode.CircleRotate)
        {
            Vector3 center = PreviewTransformPoint(
                bindTransform,
                worldStart,
                clip.circleCenterLocal);
            Vector3 initialDirection = worldStart - center;
            initialDirection.y = 0f;
            if (initialDirection.sqrMagnitude < 0.000001f)
            {
                initialDirection = Vector3.right;
            }
            else
            {
                initialDirection.Normalize();
            }

            float direction = clip.circleClockwise ? -1f : 1f;
            float angle = clip.circleTotalAngle * direction * progress;
            return center + Quaternion.Euler(0f, angle, 0f) *
                (initialDirection * clip.circleRadius);
        }

        Vector3 moveDirection = bindTransform.TransformDirection(clip.direction);
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            moveDirection = bindTransform.forward;
            moveDirection.y = 0f;
        }
        if (moveDirection.sqrMagnitude < 0.0001f)
        {
            moveDirection = Vector3.forward;
        }
        moveDirection.Normalize();

        float distance = 0f;
        const int integrationSteps = 32;
        float stepTime = clipTime / integrationSteps;
        for (int index = 0; index < integrationSteps; index++)
        {
            float sampleTime = stepTime * (index + 0.5f);
            float sampleProgress = clipDuration > 0f
                ? Mathf.Clamp01(sampleTime / clipDuration)
                : 0f;
            distance += EvaluateLinearSpeed(clip, sampleProgress) * stepTime;
        }
        if (clip.totalDistance > 0f)
        {
            distance = Mathf.Min(distance, clip.totalDistance);
        }
        return worldStart + moveDirection * distance;
    }

    private static Vector3 ResolveClipStartWorld(
        TransformTimelineClip clip,
        TimelineClip timelineClip,
        TrackAsset track,
        Transform bindTransform,
        Vector3 fallbackOrigin,
        int depth)
    {
        if (depth > 64 || clip == null || timelineClip == null || track == null)
        {
            return fallbackOrigin;
        }

        TimelineClip previousTimelineClip =
            FindPreviousClip(track, timelineClip);
        if (previousTimelineClip == null)
        {
            return fallbackOrigin;
        }

        TransformTimelineClip previousClip =
            previousTimelineClip.asset as TransformTimelineClip;
        if (previousClip == null)
        {
            return fallbackOrigin;
        }

        return EvaluateClipEnd(
            previousClip,
            previousTimelineClip,
            track,
            bindTransform,
            fallbackOrigin,
            depth + 1);
    }

    private static Vector3 EvaluateClipEnd(
        TransformTimelineClip clip,
        TimelineClip timelineClip,
        TrackAsset track,
        Transform bindTransform,
        Vector3 fallbackOrigin,
        int depth)
    {
        Vector3 startWorld = ResolveClipStartWorld(
            clip,
            timelineClip,
            track,
            bindTransform,
            fallbackOrigin,
            depth + 1);

        if (clip.moveMode == MoveMode.FixedEndPos)
        {
            return startWorld +
                   bindTransform.rotation * clip.endPos;
        }

        if (clip.moveMode == MoveMode.CircleRotate)
        {
            Vector3 center = PreviewTransformPoint(
                bindTransform,
                startWorld,
                clip.circleCenterLocal);
            Vector3 initialDirection = startWorld - center;
            initialDirection.y = 0f;
            if (initialDirection.sqrMagnitude < 0.000001f)
            {
                initialDirection = Vector3.right;
            }
            else
            {
                initialDirection.Normalize();
            }

            float direction = clip.circleClockwise ? -1f : 1f;
            float angle = clip.circleTotalAngle * direction;
            return center + Quaternion.Euler(0f, angle, 0f) *
                (initialDirection * clip.circleRadius);
        }

        Vector3 moveDirection = ResolveMoveDirection(
            clip,
            bindTransform);
        float distance = EvaluateMovementDistance(
            clip,
            (float)timelineClip.duration);
        Vector3 endWorld = startWorld + moveDirection * distance;

        if (clip.moveMode == MoveMode.Jump)
        {
            float clipDuration = (float)timelineClip.duration;
            float jumpStart = Mathf.Clamp(
                clip.jumpStartTime,
                0f,
                clipDuration);
            float jumpEnd = Mathf.Min(
                clipDuration,
                jumpStart + clip.jumpDuration);
            if (clipDuration >= jumpStart && clipDuration <= jumpEnd)
            {
                float progress = Mathf.InverseLerp(
                    jumpStart,
                    jumpEnd,
                    clipDuration);
                float height = clip.jumpHeightCurve != null
                    ? clip.jumpHeightCurve.Evaluate(progress)
                    : Mathf.Sin(progress * Mathf.PI);
                endWorld += Vector3.up *
                            (clip.jumpHeight * Mathf.Max(0f, height));
            }
        }

        return endWorld;
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

    private static Vector3 ResolveMoveDirection(
        TransformTimelineClip clip,
        Transform bindTransform)
    {
        Vector3 direction = bindTransform.TransformDirection(clip.direction);
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = bindTransform.forward;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.forward;
        }
        return direction.normalized;
    }

    private static float EvaluateMovementDistance(
        TransformTimelineClip clip,
        float clipDuration)
    {
        if (clipDuration <= 0f)
        {
            return 0f;
        }

        float distance;
        if (clip.useSpeedCurve && clip.speedCurve != null)
        {
            const int integrationSteps = 64;
            float stepTime = clipDuration / integrationSteps;
            distance = 0f;
            for (int index = 0; index < integrationSteps; index++)
            {
                float sampleProgress =
                    (index + 0.5f) / integrationSteps;
                distance += EvaluateLinearSpeed(
                    clip,
                    sampleProgress) * stepTime;
            }
        }
        else if (clip.moveMode == MoveMode.VariableSpeed)
        {
            distance =
                (clip.startSpeed + clip.endSpeed) *
                0.5f *
                clipDuration;
        }
        else
        {
            distance = clip.moveSpeed * clipDuration;
        }

        if (clip.totalDistance > 0f)
        {
            distance = Mathf.Min(distance, clip.totalDistance);
        }
        return Mathf.Max(0f, distance);
    }

    private static Vector3 PreviewTransformPoint(
        Transform bindTransform,
        Vector3 originWorld,
        Vector3 localOffset)
    {
        return originWorld +
               bindTransform.rotation *
               Vector3.Scale(localOffset, bindTransform.lossyScale);
    }

    private static Vector3 PreviewInverseTransformPoint(
        Transform bindTransform,
        Vector3 originWorld,
        Vector3 worldPosition)
    {
        Vector3 local = Quaternion.Inverse(bindTransform.rotation) *
                        (worldPosition - originWorld);
        Vector3 scale = bindTransform.lossyScale;
        return new Vector3(
            Mathf.Approximately(scale.x, 0f) ? 0f : local.x / scale.x,
            Mathf.Approximately(scale.y, 0f) ? 0f : local.y / scale.y,
            Mathf.Approximately(scale.z, 0f) ? 0f : local.z / scale.z);
    }

    private static float EvaluateLinearSpeed(
        TransformTimelineClip clip,
        float normalizedTime)
    {
        if (clip.useSpeedCurve && clip.speedCurve != null)
        {
            return Mathf.Max(0f, clip.speedCurve.Evaluate(normalizedTime));
        }
        if (clip.moveMode == MoveMode.VariableSpeed)
        {
            return Mathf.Lerp(clip.startSpeed, clip.endSpeed, normalizedTime);
        }
        return clip.moveSpeed;
    }
}
