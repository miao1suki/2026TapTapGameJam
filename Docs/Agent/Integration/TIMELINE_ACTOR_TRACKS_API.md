# 玩家 / Boss Timeline 轨道接入

## 源码

- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Track/PlayerTimelineTrack.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Clip/PlayerTimelineClip.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Track/BossTimelineTrack.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Clip/BossTimelineClip.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Behavior/ActorTimelineBehaviour.cs`

## 解析规则

- 轨道绑定存在时，直接使用绑定 Transform。
- 绑定为空时，使用 `actorTag` 调用 Unity `GameObject.FindGameObjectWithTag`。
- 玩家默认 Tag `Player`，Boss 默认 Tag `Boss`。

## 玩家片段参数

- 通用：`motionMode`、`targetOffset`、`motionDuration`、`jumpArcHeight`、`motionCurve`、`driveRigidbody`、`clearVelocity`。
- 玩家：`applyMoveInput`、`moveInput`、`applySprint`、`sprint`、`applyJumpHeld`、`jumpHeld`、`requestJump`、`jumpBoostSpeed`、`restoreInputOnEnd`、`lockControlWhileClip`。

## Boss 片段参数

- 通用运动参数与玩家轨相同。
- `setTrackingOnStart` / `trackingOnStart`：片段开始时设置追击。
- `setTrackingOnEnd` / `trackingOnEnd`：片段结束时设置追击。
- `useBossJumpState`：弧线跳跃通过 `BossController.RequestJumpTo` 执行。
- `killAtEnd`：片段结束时调用 `BossController.Kill`。

行为在直接驱动 Rigidbody 时会临时切换为 Kinematic，并在结束、暂停或 Playable 销毁时恢复原状态；玩家轨可同时通过 `PlayerController.AcquireControlLock` 锁住输入。
