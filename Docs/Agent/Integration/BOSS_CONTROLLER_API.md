# Boss 控制器接入 API

## Boss 精灵表现（2026-10-20）

标准 `Boss(暂定).prefab` 已用 `Boss Artwork` 子物体替换根占位模型：`Resources/26TAPTAP/Boss_Idle&Move_` 的12帧80×80原画，固定中心、16有效像素/格、12fps循环。子物体抵消原根缩放，自然尺寸5×5格；不改变 Boss 碰撞或根缩放。素材 Point、无Mip、无压缩，不裁每帧不透明范围。

Animator 与现有 `BossAnimationDriver` 连接。存活各动作复用当前交付的待机／移动序列；死亡 Timeline 调用 `PlayDeathAnimationNow()` 后触发 `Death`，停留在无 Motion 的 `Death VFX Handoff`，不新增死亡精灵动画。TA 继续通过原 `deathStarted` 事件／死亡 Timeline 接入粒子，原默认等待时长仍有效；恢复追击后回到循环状态。

显式重建入口：`Tools/2026TapTap/Boss/接入待机与移动美术`（不在打开场景时自动执行）。隔离验证入口 `Project.BossLogic.Editor.BossArtworkVerification.Run`，仅允许 `Library/FlowRetirementProbe`；报告 `Library/BossArtworkVerification.txt`。本次未交付或替换死亡粒子。

## 源码

- `Assets/Boss_Logic/BossController.cs`
- `Assets/Boss_Logic/BossAnimationDriver.cs`
- `Assets/Boss_Logic/BossRightMoveExecutor.cs`
- `Assets/Boss_Logic/BossCameraSize.cs`
- `Assets/_Project/Code/Systems/CameraModes/Runtime/CameraControlManager.cs`
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

`bossDisplayName` 显示在 Boss 血条左侧；为空时使用 Boss 物体名称。血条动态文本固定使用 `Cubic_11 SDF`，支持字号、颜色、描边、最大宽度和自动缩小。当前名字字号为 `57.8`（原 `34` 的 `1.7` 倍），文本高度按字号同步扩容，避免 TMP 自动缩放抵消字号变化。

`BossMonitorUI` 与 `BossHealthBarUI` 同挂 `Boss Health Canvas`：

- 监控窗锚定屏幕左上角，宽度为 Canvas 宽度约 `23.3%`、高度为 `35%`（原 1/3×1/2 整体缩小 30%）。
- `RectMask2D` 只改变揭示尺寸，从中心向四周展开；隐藏时按同一路径倒放。
- 独立正交 Camera 输出 RenderTexture，只跟随 Boss 世界位置，不接管玩法相机或 Timeline 相机；Culling Mask 为除 `UI` 外全部图层。
- 默认死亡事件后等待 `5` 秒，与血条一起淡出；外部禁用/销毁立即停止相机并释放 RenderTexture。
- 渲染分辨率按窗口大小和 Canvas Scale Factor 计算，默认 `1.5` 倍、最大宽度 `2048`；窗口比例和纹理尺寸随 PC/移动端实际 Canvas 大小自适应。

玩家提供两个运动信号：`PlayerDied` 与 `PlayerRespawnCompleted`。右移马夫在死亡时立即停止外部移动；标准复活在 `PerformRespawn` 完成时视为成功，加载转场复活在 `FinishRespawnPresentation` 最后结束时才恢复移动。

`BossController` 同时实现 `IBossRespawnStateReceiver`。Boss 关卡重生点完成同步后调用：

```csharp
IBossRespawnStateReceiver.ApplyBossRespawnState(
    float health,
    Vector3 worldPosition);
```

- 重生点按全局唯一 `Boss` Tag 查找控制器，不保存场景引用。
- `BossController.OnEnable` 自注册到 `BossRespawnStateRegistry`，因此 Timeline 后生成的 Boss 可以接收等待中的重生点请求；`OnDisable` 注销。
- `health` 按 `maxHealth` 夹取；大于 0 时恢复死亡状态、激活血条并允许受伤。
- 位置直接通过 `TeleportTo` 落到指定世界位置并清空刚体速度。
- 若外部右移马夫已经锁定高度，同时更新锁定高度，避免下一物理帧把 Boss 拉回旧 Y。
- 应用后广播 `RespawnStateApplied(health, position)`，接口同时提供 `CurrentHealth` 与 `WorldPosition` 供重生点回读验证。

检查器中的目标标签使用 Unity `TagField`；`enableAutonomousChase` 默认关闭，常规运行时不自动追击或右移。`trackingEnabled` 作为运动门，可由 Timeline 开启；关闭后不阻断受击、跳跃与死亡状态。

`BossRightMoveExecutor` 是右移马夫。它在 `waitForBossMotionEnabled` 开启时等待 `trackingEnabled=true`，首次接管时记录 Boss 当前世界 Y，然后持续向 `BossController` 下发 `SetExternalMove(1)`、该记录高度和 `SetPassThroughTerrain(true)`，不直接操作 Rigidbody。速度使用 `BossController.moveSpeed`，长廊默认 `2.5`。`SetMoving(bool)` 可在运行时切换。Timeline 演出期间 Collider 保持正常世界交互；开始右移后切为 Trigger，Boss 可以穿过地形。Renderer 使用 `BossTop`、排序值 `32767`、Renderer Priority `32767`。道具伤害仍可通过 Trigger 事件调用 `TakeDamage` / `ReceiveHit`。

右移马夫新增弱垂直追踪：

- `enableWeakVerticalTracking`：是否跟随玩家高度，默认开启。
- `verticalDeadbandBlocks`：玩家与 Boss 的高度差死区，当前默认 `0` 格；Boss 会持续追随玩家 Y，不再保留高度容差。
- `verticalTrackSpeedBlocksPerSecond`：无死区追随时的最大高度变化速度，默认 `0.18` 格/秒。
- 追踪目标直接取玩家高度，但受上述速度限制，不瞬移贴齐；实际世界位移使用 `GridCellSizeUtility` 的格尺寸。
- 玩家死亡立即停止右移；标准复活落位后恢复，加载转场复活在最后 `FinishRespawnPresentation` 后恢复。

出生点弱补助：

- `RespawnAnchorFeature.NewlyActivatedByPlayer` 只在首次激活某个出生点时广播。
- `BossCheckpointAssistController` 订阅全部场景出生点；若玩家存活，调用 `BossRightMoveExecutor.BeginCheckpointAssist`。
- 补助目标是该出生点的 `BossRespawnPosition`，水平速度默认 `1.35` 倍、安全上限 `15` 秒、垂直辅助 `0.75` 格/秒。
- 新出生点再次触发时直接覆盖旧目标；玩家死亡时马夫清空补助，并由复活链传送到当前点配置位置并重置血量。
- 补助不是传送，不修改生命、重力、基础速度或跳跃参数。

玩家接触与连线：

- `BossController.killPlayerOnContact` 默认开启；碰到玩家父级上的 `PlayerController` 时调用 `EnterDeath`。
- `BossPlayerLinkEffect` 只做显示，不产生碰撞、伤害或交互。
- 三个 `LineRenderer` 各使用一份运行时材质实例，分别直接设置 `_Style=1/2/3`、火线/水线/藤蔓线颜色和参数，避免 MaterialPropertyBlock 回退到材质默认 `_Style=0`。
- 连线起点固定为 Boss 中心；`HealthSystemActivated` 时用 `_RevealProgress` 生成，Boss `Died` 时用 `_DissolveProgress` 按玩家交互线的噪波方式消散。
- 距离越远，`_Opacity` 和 `_LineThickness` 越低；最远实体不透明度为 `0.4`，最近为 `0.78`，距离曲线使用 `1.35` 次幂让中段变化更平缓。当前整条粗细曲线为上一版的 3 倍，最远 `0.024`、最近 `0.9`，最近有效线粗为普通线的 `1.5` 倍。三条线的中心距按当前线粗的 `0.4` 倍计算，使其保持约 `60%` 重叠。曲线分段、弧高、波动、样式间距、排序图层与距离区间均可调。

Boss 音乐：

- `BossController.Awake` 会自动补齐 `BossMusicRequest`，不需要每个 Boss 预制体手工接线。
- 它订阅现有 `HealthSystemActivated` 委托，在血条开始显示时申请 `AudioCueIds.MusicBoss`；同时全局查找 `PlayerController`，只有玩家与 Boss 位于同一有效场景且该场景存在启用中的 Boss 时才申请。
- `AudioManager` 将 Boss 曲作为可释放的音乐覆盖层。覆盖期间底层随机/区域 BGM 仍可继续更新状态，但不会抢掉 Boss 曲；Boss `Died`、禁用或销毁后释放覆盖，恢复申请期间最后一次设置的底层 BGM。
- Boss 复活并应用正血量后会重新申请；重复激活、Timeline 重播和对象禁用不会叠加多个音乐请求。
- Boss Cue 在 `AudioCatalog` 中固定走 Music 通道并循环，继续受总音量和音乐滑杆控制。

Boss 存活规则：

- Boss 启用且未死亡时通过 `PlayerController.AcquireSprintLock` 持有疾跑锁；玩家即使继续输入疾跑也不会加速。
- Boss 进入死亡或禁用时释放同一租约，之后玩家可重新使用疾跑。
- 玩家每次实际复活时，Boss 状态应用延后到房间重建之后；`PrepareForBossRespawn()` 先销毁场景中与 Boss 碰撞体左边缘至 Boss 当前中心 X 范围相交的全部 `LavaHazardFeature`，同时覆盖普通岩浆与重力岩浆。
- 清障完成后才执行 `ApplyBossRespawnState` 血量与位置归位。

## 分离边界

- `BossController` 只写 Rigidbody、目标、状态和受击数值。
- `BossRightMoveExecutor` 只下发右移与高度命令，不修改 Boss 速度、重力或跳跃能力。
- `BossController.SetPassThroughTerrain(bool)` 切换碰撞体 Trigger 状态；Timeline 期间默认关闭，右移马夫启动后开启。
- Boss 预制体根缩放为 3；作者碰撞为本地 `1×1×1` BoxCollider。`SetPassThroughTerrain(true)` 时按 `passThroughColliderHeight`（默认 12）临时拉高碰撞，世界空间约 `3×36×3` 格；退出穿行时恢复作者尺寸。该碰撞体不再用于隐藏或移除物体。
- 动画、Timeline 和动作实现只听事件，不反向驱动控制状态。
- `BossAnimationDriver` 可以完全不配置 Animator；控制器仍然工作。
- `RequestJumpTo(Vector3 worldTargetPosition)` 请求物理跳跃；`RequestJump(Vector2 direction, float distance)` 按方向请求。目标超出最大跳高、最大垂直速度或自身水平速度时拒绝，不会瞬移追人。跳跃物理参数只由 `BossController` 的 Inspector 配置。
- Jump 状态会临时接管 Rigidbody，落地后恢复原刚体设置并回到 Chase；普通受伤不会取消 Jump，死亡会安全取消 Jump。
- Timeline 移动输入调用 `BossController.SetWalkInput(direction)`，速度来自控制器字段 `walkSpeed`。
- Timeline 跳跃输入调用 `BossController.RequestJump()`，不传目标位置、高度或速度。
- `BossTimelineClip.moveInput` 只表示 `-1…1` 的操作方向；Clip 不保存目标 X、跳跃落点或移动速度。

## Boss 入场 Timeline 接口

- Timeline 资源：`Assets/_Project/Content/Boss/Timelines/Boss_appear.playable`
- `BossIntroTrack` / `BossIntroClip` / `BossIntroBehaviour` 已废除，不再作为可用接口。
- 入场资产只有一条覆盖全程的 `PlayerTimelineTrack` 和一条 Boss 专用 `BossAppearCinematicPlayable` 轨道。
- `PlayerTimelineTrack` 从 `0` 秒锁住 `Player` Tag；实际演出由 `BossAppearCinematicPlayable` 完成并主动停表，Timeline 的 `20` 秒固定时长只作为安全上限。
- 当前 `Boss_appear.playable` 的轨道列表只保留一条覆盖全程的玩家锁轨和一条 Boss 专用 `BossAppearCinematicPlayable`；旧 Boss 走/跳、旧相机轨和旧对话轨已从 `m_Tracks` 移除。相机全程固定在玩家初始位置，不横移、不跟 Boss；Boss 在玩家 `X-7 / Y+13` 生成并假加速度下落至玩家同高，之后播放盯视、警告并开战。
- appear 不通过共享 `TimelineCamRig` 抢 `_shotState`，而是直接作为最高优先级 `ICameraControlSource` 向 `CameraControlManager` 注册；其他相机轨即使残留也不能覆盖镜头 Y。
- 演出期间额外启用 `BossAppearCameraGuard`，在 `LateUpdate` 最高执行顺序最后锁定 Camera Transform 和镜头参数；这是 Boss appear 的最终画面兜底，结束时随 Playable 清理。
- Boss 入场触发器场景实例的 `requestPlayerControlLockDuringTimeline` 继续保持开启，作为场景入口保险；它通过 `IPlayerControlLockTarget` 申请锁，Timeline 结束、停止、重置或禁用时释放。
- `BossTimelineClip` 每个 Clip 只能配置一个动作；`spawnBoss` 为真时使用 `bossPrefab` 生成，其他 Clip 通过 `Boss` Tag 查找已生成 Boss。
- Boss 生成与其他显式空间演出使用 `BossTimelinePerformanceFrame`：优先取 owning `PlayableDirector` 所在物体的 `ContactTimelineTrigger`，脚本驱动的死亡轨则取 Director 自身；所有点/偏移都是表演坐标系局部值。
- `Boss_appear.playable` 的生成 `targetOffset` 已改为激发器局部坐标，整张地图或触发器平移、旋转时不需要改演出数值。
- 出场相机完全固定：初始机位由玩家位置 + `playerCameraOffset=(0,0,-10)` 建立；进入 appear 时停止其他正在播放且包含 `CameraTimelineTrack` 的 Director，强制释放残留 `TimelineCamRig` 租约，之后字幕、下落、盯视和警告阶段每帧只写同一个固定机位，不存在左移或回程运镜。
- Boss 生成使用 `bossSpawnOffsetFromPlayer=(-7,13,0)`。`BeginPresentationFall()` 将 Boss 临时设为 Kinematic、关闭 Collider，`SetPresentationFallPosition()` 每帧只更新由加速度计算出的 Y，因此不会受地面、高台或天花板影响；演示结束后 `EndPresentationFall()` 恢复原刚体和 Collider。
- 普通死亡、处决、真/假结局的相机、钥匙吸附、收集品和黑屏演出参数均序列化在 Timeline/Playable 中；通用 `TimelineTools3D` 不为 Boss 专属演出改代码。
- Boss 下落由 Playable 调用 `BeginPresentationFall()` / `SetPresentationFallPosition()` 完成，不写刚体速度。
- 入场资源不再包含 `CameraTimelineTrack`；相机由 Playable 的最高优先级控制租约和 `BossAppearCameraGuard` 共同固定。
- 演出结束时销毁相机守卫、释放控制租约，并把相机交回 `CameraControlManager`。
- Side2D 镜头强制使用平面 Yaw，不再把 LookRotation 的 3D 俯仰写回相机。
- Boss 死亡 Timeline 的相机释放仍按死亡 Playable 的序列化时长执行。
- 出场完成时由 `BossAppearCinematicPlayable` 同时开启追击、血条、监控和 `BossCameraSize`。

## Boss 存活期视野接口

`CameraControlManager` 新增镜头倍率申请，倍率请求与相机控制权独立：

```csharp
CameraLensRequestHandle handle =
    cameraManager.RequestLensMultiplier(owner, 2f, CameraTransition.Ease(1f));
handle.Set(2f, CameraTransition.Ease(1f));
handle.Release(CameraTransition.Ease(1f));
```

- 多个请求取最高倍率，不累乘。
- 只有当前相机源优先级低于 `CameraControlPriorities.Cutscene` 时才应用倍率；Timeline 演出镜头保持自身大小。
- 倍率变化由 `CameraControlManager` 平滑推进，Boss 或玩家代码不得直接写 `Camera.orthographicSize`。
- 请求不会因玩家死亡、复活、相机重新跟焦或模式重新应用而清除；它只由持有者释放、相机管理器销毁或场景卸载清理。

`BossCameraSize` 是 Boss 预制体上的持有者：

- `RequestEnable()`：提交 `sizeMultiplier`，默认 `2`，平滑时间默认 `1` 秒。
- `RequestDisable()`：平滑恢复倍率 `1`。
- `SetRequested(bool)`：统一开关接口。
- `requestOnEnable` 默认关闭，因此 Boss 出生时不会自动扩大视野。

区域型缩小视野使用独立 Timeline `Assets/_Project/Content/Boss/Timelines/camera_close.playable`。它由 `ContactTimelineTrigger` 的“进入播放，离开停止”模式驱动，进入申请 `0.5×` 平滑缩放，离开停止 Director 后释放请求并恢复；不修改任何 Timeline 原生轨道脚本。接入见 [Camera Close](CAMERA_CLOSE_TIMELINE_API.md)。
- 组件不再订阅 `BossController.Died`。击败 Timeline 在返回镜头开始时通过 `BossDeathCameraRestorePlayable` 调用 `RequestDisable()`；普通玩家死亡/复活和外部对象销毁路径互不影响。

## Boss 死亡 Timeline

- Timeline：`Assets/_Project/Content/Boss/Timelines/Boss_defeat.playable`
- 播放器：`BossDefeatTimelinePresenter`，挂在 Boss 预制体，并由 `BossController.defeatPresentationRequested` 调用。
- 只响应 `EnterDeath` 发出的击败事件；外部 `Destroy` 不触发 `EnterDeath`，因此不会播放。
- 当前资产由一条覆盖全程的 `PlayerTimelineTrack` 和 `BossDeathCinematicPlayable(mode=Natural)` 组成；旧双目标相机、旧死亡镜头、旧动画触发和视野恢复轨已静音。
- 开局同时锁玩家、把玩家传送到最近的已激活重生点、关闭 Boss 接触击杀；镜头平滑移到 Boss 时 Boss 开始慢速 Renderer 闪烁。
- 镜头到位并等待 `1` 秒后，依次播放 Boss 故障字幕 `咳咳……`、`嘁，你以为我没留后手吗！`；两句都显示 `2` 秒，走底部 WordNote 队列。
- 字幕结束后镜头回玩家，玩家强制面向 Boss，并完整闪烁两次。随后六把钥匙按红/蓝/绿两轮从玩家身体逐把抽出，在玩家上方排成竖直列并呼吸悬停。
- 全部钥匙悬停 `1` 秒后播放玩家打字机字幕 `身体好难受……使不上劲`；再等 `0.5` 秒，钥匙逐把曲线进入 Boss，镜头同步移到 Boss。
- Boss 播放故障字幕 `哈哈哈哈……我不好过你也别想好过！` 后调用 `BossAnimationDriver.PlayDeathAnimationNow()`；驱动通过 Animator 死亡状态长度或 `deathAnimationFallbackSeconds`（默认 `2` 秒）提供播放进度。
- 死亡动画完成时先隐藏 Boss 全部 Renderer/Collider，再回镜头到玩家；随后启动独立场景级 Player Thinking Director，再删除 Boss 物体并进入 `player_thinking.playable`。
- 独立 Player Thinking Director 避免结局 Timeline 随 Boss 删除而中断；`Boss Post Death Director` 在沉思 Timeline 停止后自动清理。
- 演出钥匙关闭 Collider、`ColorKeyPickup`、`DropItemBase`、`PickupFlightAnimator`、`MechanismArtworkPresentation`、子 `PlayableDirector` 与 `RoomResetObject`，仅作为视觉对象存在。

## Boss 处决 Timeline

- 直接资产：`Assets/_Project/Content/Boss/Timelines/Boss_execution.playable` 和 `Assets/_Project/Content/Boss/BossExecutionTrigger.prefab`。
- 玩家进入 Trigger 后播放 Timeline；`PlayerTimelineTrack` 按 `Player` Tag 覆盖整段锁定普通操作，在 Clip 自然结束、Timeline 停止或销毁时由 `ActorTimelineBehaviour` 统一释放。`ContactTimelineTrigger` 不再额外申请玩家锁，避免双锁。
- 当前资产由一条 `120` 秒玩家锁轨和 `BossDeathCinematicPlayable(mode=Execution)` 组成；旧 `BossExecutionClip`、`BossExecutionTimelineTrack` 与旧处决相机轨已移除/静音。
- 开局立刻锁玩家、关闭 Boss 接触击杀、暂停普通右移，先保持 Boss X/Z 不变并把 Y 对齐到玩家当前 Y 向下 `3` 格，再把 Boss 切入 execution 控制；因此从开局起任何正伤害都会直接致命。
- 等待 `0.5` 秒后，镜头以更快的 `0.55` 秒插值移向 Boss；镜头进度到两者距离的一半时，Boss 开始以 `10` 格/秒向右移动，镜头继续追踪。
- 受击后 Boss 进入同一个故障闪烁节点，后续字幕、玩家闪烁、六钥匙、死亡动画、删除 Boss 与真假结局和自然死亡完全共用。
- Timeline 默认时长 `120` 秒；未受伤时持续处决追踪，受击后进入共享死亡收束。
- `ContactTimelineTrigger` 默认仅玩家、只触发一次；位置和触发区域大小由场景实例自行摆放。

## 事件

- `StateChanged(previous, next)`
- `Damaged(amount, remainingHealth)`
- `Died`
- `DefeatPresentationRequested(BossController boss)`
- `BossAnimationDriver.StateForwarded(previous, next)`

死亡演出接口：

- `BossAnimationDriver.PlayDeathAnimationNow()`：在演出指定阶段触发死亡动画。
- `BossAnimationDriver.DeathAnimationProgress` / `IsDeathAnimationComplete`：优先读取 Animator 当前 `Death` 状态长度，没有状态时使用 `deathAnimationFallbackSeconds`。
- `BossController.SetPlayerContactKillEnabled(bool)`：Timeline 期间关闭或恢复 Boss 接触击杀。
- `PlayerController.TryTeleportToNearestRespawnForPresentation()`：锁操作期间把玩家移到最近已激活重生点的安全落点，并给予复活保护时间。
- `PlayerController.FaceWorldPosition(Vector3)`：演出中强制角色朝向世界位置。

## 受击、无敌与接触伤害

`BossController` 提供同一调试分区内的生存与接触伤害参数：

- `maxHealth` / `startingHealth`：最大及初始受击值，当前默认均为 9。
- `destroyOnlyWhileExternalMoving`：只有右移马夫或 execution 接管时处理岩浆与玩家接触伤害。
- `lavaContactDamage`：岩浆首次接触伤害，默认 1。
- `passThroughColliderHeight`：穿行阶段的本地碰撞高度，默认 12；停止穿行后恢复作者尺寸。
- `invulnerabilitySeconds`：受击后无敌时间，当前默认 4 秒。
- `invulnerabilityFlashInterval`：无敌期间 Renderer 启用/关闭切换间隔，默认 0.08 秒。
- 无敌开始时自动收集 Boss 自身及当前全部子 Renderer，并保留每个 Renderer 原始启用状态；后续加入的手部动画等 Renderer 会在下一次受伤进入无敌时自动纳入，无需手工配置。

Boss 不再隐藏、停用或销毁普通方块、机关和岩浆，也不会给接触对象添加隐藏状态。只有右移马夫或 execution 接管阶段才处理伤害接触，避免入场和 Timeline 演出误触岩浆。岩浆接触在非无敌时调用 `ReceiveHit(lavaContactDamage, Vector3.zero)`；`ReceiveHit` 在无敌期间忽略后续伤害。普通受伤只扣血、进入无敌和闪烁，不切换 `Hit`、不施加硬直或击退，也不会打断跳跃。死亡时立即停止闪烁并恢复正常 Renderer 状态。

血量归零进入 Death 时，`BossRightMoveExecutor` 通过 `StateChanged` 立即停止外部右移并恢复原碰撞状态。控制器同时广播 `DefeatPresentationRequested(BossController)`，并调用 Inspector 中可配置的 `defeatPresentationRequested` UnityEvent。该接口预留给后续玩家 Timeline 桥接组件；控制器本身不持有 `PlayableDirector`。

`BossRightMoveExecutor.invulnerableMoveSpeedMultiplier` 当前默认 `0.7`。Boss 无敌期间右移不中断，只把下发的右移方向倍率改为该值；无敌结束后自动恢复完整速度。按基础速度 `2.5` 计算，每段无敌移动 `2.5 × 0.7 × 4 = 7` 格。

## 血条与战斗激活

Boss 预制体带有子物体 `Boss Health Canvas`，使用 `Screen Space - Overlay` 和 `CanvasScaler`，Canvas 排序值当前为 1000。实际血条由 `BossHealthBarUI` 在激活时创建，因此 Boss 出生时不会显示，也不会开始涨条动画。

战斗入口：

```csharp
BossController.ActivateHealthSystem();
```

调用前置条件：

- `IsHealthSystemActive == false` 时，`TakeDamage` / `ReceiveHit` 不结算伤害。
- 首次调用后广播 `HealthSystemActivated`，血条开始初始化并永久开放受伤。
- 已开启、死亡或重复调用不会重新初始化。

血条规则：

- 段数由初始化时的 `MaxHealth` 四舍五入得到；当前 9 点血生成 9 段和 8 条棱角分明的纯黑竖直分隔线。
- 初始化从 0 快速涨到当前血量比例，默认耗时 0.35 秒。
- 当前血量层使用纯色肉红 `#B23E3E`，底层拖尾血皮使用纯白；红色当前血层变化快，白色底层血皮变化慢。两者均为帧率无关 Lerp，速度可在 `BossHealthBarUI` Inspector 调整。
- 血条默认锚定屏幕中心上方，长度、高度、顶部偏移、分隔线宽度、外框和三组颜色均可调；外框以外保持透明，不创建底板或图片描边。
- 外框由无 Sprite 的纯色 `Image` 矩形拼成：1px 黑外缘、项目暗红 `#523B42` 框体，以及左上/右下 `#F65332` 角部强调；外框宽度与强调长度可调。
- 血条完全使用无 Sprite 的纯色 UGUI `Image` 矩形，不依赖图片材质、字体、Mipmap 或压缩资源；所有边缘保持硬直，且不会接收鼠标事件。

## 预制体

`Assets/_Project/Content/Boss/Boss(暂定).prefab` 已挂：

- `Rigidbody`
- `BossController`
- `BossAnimationDriver`
- `BossRightMoveExecutor`
- `BossCheckpointAssistController`
- `BossCameraSize`
- `PlayableDirector`
- `BossDefeatTimelinePresenter`
- `BossMusicRequest`（运行时由 `BossController.Awake` 自动补齐）
