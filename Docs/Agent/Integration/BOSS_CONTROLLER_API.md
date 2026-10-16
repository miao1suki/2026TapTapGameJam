# Boss 控制器接入 API

## 源码

- `Assets/Boss_Logic/BossController.cs`
- `Assets/Boss_Logic/BossAnimationDriver.cs`
- `Assets/Boss_Logic/BossRightMoveExecutor.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Track/BossTimelineTrack.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Clip/BossTimelineClip.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Behavior/ActorTimelineBehaviour.cs`

## 状态

`BossStateId`：

- `Chase`
- `Hit`
- `Death`
- `Jump`

`BossController` 实现 `IDamageable`，可通过 Timeline HitBox 或外部陷阱调用 `TakeDamage` / `ReceiveHit`。

检查器中的目标标签使用 Unity `TagField`；`enableAutonomousChase` 默认关闭，常规运行时不自动追击或右移。`trackingEnabled` 作为运动门，可由 Timeline 开启；关闭后不阻断受击、跳跃与死亡状态。

`BossRightMoveExecutor` 是右移马夫。它在 `waitForBossMotionEnabled` 开启时等待 `trackingEnabled=true`，然后向 `BossController` 下发 `SetExternalMove(1)`、`SetExternalHeight(rightMoveHeightY)` 和 `SetPassThroughTerrain(true)`，不直接操作 Rigidbody。速度使用 `BossController.moveSpeed`，长廊默认 `2.5`。`SetMoving(bool)`、`SetRightMoveHeightY(float)` 可在运行时切换。Timeline 演出期间 Collider 保持正常世界交互；开始右移后切为 Trigger，Boss 可以穿过地形。Renderer 使用 `BossTop`、排序值 `32767`、Renderer Priority `32767`。道具伤害仍可通过 Trigger 事件调用 `TakeDamage` / `ReceiveHit`。

## 分离边界

- `BossController` 只写 Rigidbody、目标、状态和受击数值。
- `BossRightMoveExecutor` 只下发右移与高度命令，不修改 Boss 速度、重力或跳跃能力。
- `BossController.SetPassThroughTerrain(bool)` 只切换碰撞体 Trigger 状态；Timeline 期间默认关闭，右移马夫启动后开启。
- 动画、Timeline 和动作实现只听事件，不反向驱动控制状态。
- `BossAnimationDriver` 可以完全不配置 Animator；控制器仍然工作。
- `RequestJumpTo(Vector3 worldTargetPosition)` 请求物理跳跃；`RequestJump(Vector2 direction, float distance)` 按方向请求。目标超出最大跳高、最大垂直速度或自身水平速度时拒绝，不会瞬移追人。跳跃物理参数只由 `BossController` 的 Inspector 配置。
- Jump 状态会临时接管 Rigidbody，落地后恢复原刚体设置并回到 Chase；受击和死亡会安全取消 Jump。

## Boss 入场 Timeline 接口

- Timeline 资源：`Assets/_Project/Content/Boss/Timelines/BossIntro.playable`
- `BossIntroTrack` / `BossIntroClip` / `BossIntroBehaviour` 已废除，不再作为可用接口。
- 入场由多条 `BossTimelineTrack` 组成：生成/发呆、关闭运动、跳跃、落地发呆、开启运动。
- `BossTimelineClip` 每个 Clip 只能配置一个动作；`spawnBoss` 为真时使用 `bossPrefab` 生成，其他 Clip 通过 `Boss` Tag 查找已生成 Boss。
- 相机使用 `CameraTimelineTrack`，入场资源只允许 2D 正交运镜。
- 相机轨结束时释放 Timeline 相机控制，并把 `TimelineCamRig.target` 交回 `CameraControlManager.FocusTarget`。

## 事件

- `StateChanged(previous, next)`
- `Damaged(amount, remainingHealth)`
- `Died`
- `BossAnimationDriver.StateForwarded(previous, next)`

## 预制体

`Assets/_Project/Content/Boss/Boss(暂定).prefab` 已挂：

- `Rigidbody`
- `BossController`
- `BossAnimationDriver`
- `BossRightMoveExecutor`
