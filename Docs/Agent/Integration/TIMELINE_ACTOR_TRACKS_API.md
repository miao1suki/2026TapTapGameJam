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

- `spawnBoss` / `bossPrefab`：是否生成 Boss，以及生成使用的 Boss 预制体。
- `moveInput`：移动动作的 `-1…1` 操作方向；调用 `BossController.SetWalkInput`。
- 跳跃动作调用 `BossController.RequestJump()`，不传目标位置或速度。
- `setTrackingOnStart` / `trackingOnStart`：片段开始时设置追击。
- `setTrackingOnEnd` / `trackingOnEnd`：片段结束时设置追击。
- `useBossJumpState`：弧线跳跃通过 `BossController.RequestJump` 执行。
- `killAtEnd`：片段结束时调用 `BossController.Kill`。

## Boss 入场约束

- 每个 `BossTimelineClip` 只能配置一个操作：`Idle`、`Motion`、`Jump`、`SetTracking` 或 `Kill`。
- 多操作必须通过多条 `BossTimelineTrack` 在 Timeline 中堆叠；Inspector 不再提供自定义组合模式。
- Inspector 只显示当前动作所需数据，以及 `spawnBoss` / `bossPrefab`；移动动作显示 `moveInput`，跳跃不显示目标或速度，运动开关显示开启状态，死亡无额外参数。
- Boss 入场相机轨只允许正交 2D 运镜，使用 `CameraTimelineTrack` 时必须保持 `Side2D`，不得使用透视 3D 或 3D 环绕。
- CameraTimelineClip 的 `anchorTag` 可按 Tag 查找片段有效期内的运镜锚点，留空时使用 `TimelineCamRig.target`。Boss 入场使用 `Boss`。
- `CameraTimelineTrack.returnTransitionDuration` 控制轨道结束交还玩法相机时的平滑时间；Boss 死亡演出用镜头曲线的最后 1 秒回到原机位，轨道结束只保留极短清理过渡。
- 角色轨不得直接改写 `TimelineCamRig.target`。相机 Clip 在自然末尾主动释放控制并恢复 `CameraControlManager.FocusTarget`，兼容 PlayableDirector 的 Hold 模式；不要只依赖 Graph 停止回调。

行为在直接驱动 Rigidbody 时会临时切换为 Kinematic，并在结束、暂停或 Playable 销毁时恢复原状态；玩家轨可同时通过 `PlayerController.AcquireControlLock` 锁住输入。
