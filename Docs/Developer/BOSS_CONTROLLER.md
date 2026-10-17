# Boss 控制器

## 组件

`BossController` 只负责 Boss 的逻辑和运动状态：

- `Chase`：追击目标。
- `Hit`：受击硬直。
- `Death`：死亡。
- `Jump`：按 Boss 自身重力与速度能力完成一次物理跳跃。

当前 `BossController` 默认关闭 `enableAutonomousChase`，常规运行时不会自己追击或右移，只作为 Timeline 和外部马夫可调用的运动能力。入场 Timeline 结束后通过 `SetTrackingEnabled(true)` 开启运动门。

右移由独立组件 `BossRightMoveExecutor` 负责。它是“马夫”，等待 Boss 运动开启后，只调用 `BossController.SetExternalMove(1)`、`SetExternalHeight(右移世界 Y 坐标)` 和 `SetPassThroughTerrain(true)`，不直接写 Rigidbody。速度由 `BossController.moveSpeed` 提供，长廊默认值为 `2.5`。Inspector 中手动填写固定世界 Y。Timeline 演出期间 Collider 保持正常世界交互；开始右移后切为 Trigger，可以穿过地形，同时保留后续道具通过 Trigger 或直接调用 `TakeDamage` / `ReceiveHit` 造成伤害的入口。Renderer 使用项目最高排序层 `BossTop`、排序值 `32767` 和 Renderer Priority `32767`。

它不引用动画、Timeline 或具体伤害特效，因此没有动画资源也能正常追击、受击、跳跃和死亡。

`BossAnimationDriver` 是可选表现层，订阅控制器状态并转发 Animator 参数或 UnityEvent。没有 Animator 时不会影响控制器。

## 使用

1. 在 `Boss(暂定)` 预制体上使用 `BossController`。
2. 目标留空时按 `Player` Unity Tag 查找玩家。
3. “目标标签”使用 Unity TagField 下拉，不需要手输字符串。
4. “启用自动目标追击”默认关闭；常规长廊不需要打开。
5. `BossRightMoveExecutor` 的“等待 Boss 运动开启”开启后，Timeline 的整个 PlayableGraph 停止并调用 `SetTrackingEnabled(true)` 才会开始右移。
6. “右移世界 Y 坐标”是马夫手动配置的固定移动高度。
7. 需要动画时，把 `Animator` 和参数名填到 `BossAnimationDriver`。
8. 跳跃通过 `RequestJump` 或 `RequestJumpTo` 请求；只有目标在最大跳高、最大垂直速度和水平速度能力内才会执行，进入 Jump 后由 Rigidbody 按配置时长完成物理弹道，向下跳不会被起跳点的地面判定提前取消。
9. 需要接 Timeline/动作时，订阅 `StateChanged`、`hitStarted`、`jumpStarted`、`deathStarted` 或直接使用 `StateForwarded`。

## Boss 入场 Timeline

`Assets/_Project/Content/Boss/Timelines/BossIntro.playable` 不再使用 `BossIntroTrack` / `BossIntroClip`。当前总时长为 `5.41` 秒，由五条 `BossTimelineTrack` 和一条 2D 相机轨组成：

- 生成/发呆：生成 Boss 到编辑模式玩家位置右半格 `(-8.9, -33.19, 0)`，保持 `2` 秒。
- 关闭运动：确认 Boss 运动门处于关闭状态。
- 跳跃：以 `1.4` 秒弹道跳到 `(-6.9, -36.19, 0)`，向下落点也会先起跳再下落。
- 落地发呆：保持 `2` 秒。
- 开启运动：Graph 停止前打开运动门。

每个 Boss Clip 只能有一个动作。面板只显示当前动作的数据，以及 `spawnBoss` / `bossPrefab`；移动动作显示目标位置和 `moveSpeed`，跳跃显示目标，运动开关只显示开关状态。相机轨只允许 2D 正交运镜，负责跟随 Boss。

在 Timeline 窗口中选中 `BossTimelineClip` 时，Scene 视图会显示该片段的世界轨迹预览：起点优先承接同轨上一段位移终点，随后按瞬移、平滑移动或弧线跳跃模式绘制路线、起点和终点。Boss Jump 状态下的实际落地仍受 Rigidbody 与重力影响，预览用于检查路线和目标位置。

相机轨结束后会把 `TimelineCamRig.target` 恢复为 `CameraControlManager.FocusTarget`，并释放 Timeline 相机控制权，让玩法相机重新跟随玩家。

## 后续扩展

Boss 专属的阶段、陷阱受击、路线和胜利条件应继续放在 `Assets/Boss_Logic`，不要把 Boss 逻辑写进颜色 Feature 或共享 BlockFeature Host。
