using UnityEditor;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

[InitializeOnLoad]
public static class TimelineClipInfoOverlay
{
    static TimelineClipInfoOverlay()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private static void OnSceneGUI(SceneView view)
    {
        TimelineClip clip = TimelineEditor.selectedClip;
        if (clip == null)
        {
            return;
        }
        string summary = BuildSummary(clip);
        if (summary == null)
        {
            return;
        }
        string title = string.IsNullOrEmpty(clip.displayName) ? clip.asset.GetType().Name : clip.displayName;

        Handles.BeginGUI();
        GUILayout.BeginArea(new Rect(12f, 12f, 340f, 170f), GUI.skin.box);
        GUILayout.Label(title, EditorStyles.boldLabel);
        GUILayout.Label(summary, EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(2);
        GUILayout.Label("选中片段后：拖 Scene 手柄微调 · 参数在 Inspector 修改", EditorStyles.centeredGreyMiniLabel);
        GUILayout.EndArea();
        Handles.EndGUI();
    }

    private static string BuildSummary(TimelineClip clip)
    {
        if (clip.asset is TransformTimelineClip t)
        {
            string mode = t.moveMode switch
            {
                MoveMode.FixedEndPos => "瞬移(穿墙)",
                MoveMode.SpeedAndDistance => "匀速直线",
                MoveMode.VariableSpeed => "变速直线",
                MoveMode.Jump => "跳跃",
                _ => "绕圈"
            };
            string detail = t.moveMode == MoveMode.CircleRotate
                ? $"圆心+半径 {t.circleRadius:F1}m · 转角 {t.circleTotalAngle:F0}°"
                : $"方向 {t.direction} · 距离 {t.totalDistance:F1}m · 碰撞 {(t.useCollision ? "滑行" : "关")}";
            string speed = t.moveMode == MoveMode.SpeedAndDistance ||
                           t.moveMode == MoveMode.VariableSpeed ||
                           t.moveMode == MoveMode.Jump
                ? $"\n速度 {(t.useSpeedCurve ? "使用 AnimationCurve" : "使用旧参数")}"
                : string.Empty;
            string jump = t.moveMode == MoveMode.Jump
                ? $"\n跳跃 高度基准 {t.jumpHeight:F2}m · 时长 {t.jumpDuration:F2}s"
                : string.Empty;
            return $"位移 · {mode}\n{detail}{speed}{jump}\n时长 {clip.duration:F2}s";
        }
        if (clip.asset is HitBoxClip h)
        {
            return $"攻击判定\n窗口 {h.startTime:F2}s ~ {h.endTime:F2}s · {(h.hitBoxShape == HitBoxShape.Sphere ? "球" : "盒")}\n伤害 {h.damage:F0} · 击退 {h.HitForce:F0} · 扫描 {(h.useRepeatScan ? $"每{h.scanInterval:F2}s" : "进窗一次")}";
        }
        if (clip.asset is EffectAudioClip e)
        {
            string content = e.sound != null ? e.sound.name : "仅特效";
            if (e.effectPrefab != null)
            {
                content = (e.sound != null ? "音效+特效" : "仅特效") + $" · {e.effectPrefab.name}";
            }
            return $"特效/音效\n{content}\n触发 {(e.useRepeatSpawn ? $"每{e.spawnInterval:F2}s" : "进片一次")}";
        }
        if (clip.asset is CameraTimelineClip c)
        {
            string mode = c.cameraMoveMode switch
            {
                CamMoveMode.SmoothLerp => "平滑运镜",
                CamMoveMode.Teleport => "瞬移切镜",
                CamMoveMode.ResetOrigin => "归位",
                _ => "未知运镜"
            };
            string detail = c.cameraMoveMode == CamMoveMode.ResetOrigin
                ? $"{(c.resetSubMode == ResetCamSubMode.Teleport ? "瞬移" : "平滑")}回正常机位"
                : (c.useSurroundMode
                    ? $"环绕半径 {c.surroundRadius:F1}m · 角度 {c.surroundTotalAngle:F0}°"
                    : $"机位 {c.cameraTargetLocalPos} · 连续运镜 {(c.useLastFrameAsOrigin ? "开" : "关")}");
            string projection = c.modeRequest switch
            {
                TimelineCameraModeRequest.Side2D => " · 申请正交 2D",
                TimelineCameraModeRequest.Perspective3D => " · 申请透视 3D",
                _ => " · 跟随 Project 模式"
            };
            if (c.modeRequest == TimelineCameraModeRequest.Side2D &&
                c.overrideSide2DYaw)
            {
                projection += $" · Yaw {c.side2DYawDegrees:F0}°";
            }
            if (c.constrainOrthographicAxes)
            {
                string axes =
                    (c.allowPositionX ? "X" : "") +
                    (c.allowPositionY ? "Y" : "") +
                    (c.allowPositionZ ? "Z" : "");
                projection += string.IsNullOrEmpty(axes)
                    ? " · 2D 全轴锁定"
                    : $" · 2D 轴 {axes}";
            }
            if (c.modeRequest == TimelineCameraModeRequest.Side2D &&
                c.turnTiming != Camera2DTurnTiming.None)
            {
                string timing = c.turnTiming == Camera2DTurnTiming.AtClipStart
                    ? "片头"
                    : "片尾";
                projection += $" · {timing}转 {c.turnAngleDegrees:F0}°";
            }
            return $"运镜 · {mode}{projection}\n{detail}";
        }
        return null;
    }
}
