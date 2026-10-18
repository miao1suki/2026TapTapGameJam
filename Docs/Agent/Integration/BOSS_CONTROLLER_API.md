# Boss 控制器接入 API

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

检查器中的目标标签使用 Unity `TagField`；`enableAutonomousChase` 默认关闭，常规运行时不自动追击或右移。`trackingEnabled` 作为运动门，可由 Timeline 开启；关闭后不阻断受击、跳跃与死亡状态。

`BossRightMoveExecutor` 是右移马夫。它在 `waitForBossMotionEnabled` 开启时等待 `trackingEnabled=true`，首次接管时记录 Boss 当前世界 Y，然后持续向 `BossController` 下发 `SetExternalMove(1)`、该记录高度和 `SetPassThroughTerrain(true)`，不直接操作 Rigidbody。速度使用 `BossController.moveSpeed`，长廊默认 `2.5`。`SetMoving(bool)` 可在运行时切换。Timeline 演出期间 Collider 保持正常世界交互；开始右移后切为 Trigger，Boss 可以穿过地形。Renderer 使用 `BossTop`、排序值 `32767`、Renderer Priority `32767`。道具伤害仍可通过 Trigger 事件调用 `TakeDamage` / `ReceiveHit`。

## 分离边界

- `BossController` 只写 Rigidbody、目标、状态和受击数值。
- `BossRightMoveExecutor` 只下发右移与高度命令，不修改 Boss 速度、重力或跳跃能力。
- `BossController.SetPassThroughTerrain(bool)` 只切换碰撞体 Trigger 状态；Timeline 期间默认关闭，右移马夫启动后开启。
- Boss 预制体根缩放为 3；作者碰撞为本地 `1×1×1` BoxCollider。`SetPassThroughTerrain(true)` 时按 `passThroughColliderHeight`（默认 12）临时拉高碰撞，世界空间约 `3×36×3` 格；退出穿行时恢复作者尺寸。
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
- 入场由多条 `BossTimelineTrack` 组成：生成/发呆、并行移动输入、跳跃请求、落地停顿、开启运动。
- 入场另有一条覆盖 `0–6.52` 的 `PlayerTimelineTrack`，按 `Player` Tag 解析角色并开启 `lockControlWhileClip`。该轨道在最终开战事件前 `0.02` 秒结束，避免 Hold 模式的末帧暂停未触发释放；行为层还会在片段自然末尾主动清理锁。
- Boss 入场触发器场景实例的 `requestPlayerControlLockDuringTimeline` 继续保持开启，作为场景入口保险；它通过 `IPlayerControlLockTarget` 申请锁，Timeline 结束、停止、重置或禁用时释放。
- `BossTimelineClip` 每个 Clip 只能配置一个动作；`spawnBoss` 为真时使用 `bossPrefab` 生成，其他 Clip 通过 `Boss` Tag 查找已生成 Boss。
- 移动输入与跳跃请求同时处于 `4–5.4` 秒；Timeline 不直接写 Transform/Rigidbody，也不传送或纠偏最终位置。
- 相机使用 `CameraTimelineTrack`，入场资源只允许 2D 正交运镜。
- 相机轨结束时释放 Timeline 相机控制，并把 `TimelineCamRig.target` 交回 `CameraControlManager.FocusTarget`。
- 最后一帧 `BossHealthBarActivationPlayable` 同时开启血条和 `BossCameraSize`。

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
- `releaseOnBossDeath` 默认开启；订阅 `BossController.Died`，Boss 死亡时只释放视野请求，不改变玩家控制、相机跟随或玩家重生状态。

## 事件

- `StateChanged(previous, next)`
- `Damaged(amount, remainingHealth)`
- `Died`
- `DefeatPresentationRequested(BossController boss)`
- `BossAnimationDriver.StateForwarded(previous, next)`

## 碰撞隐藏与受击无敌

`BossController` 提供同一调试分区内的生存与清障参数：

- `maxHealth` / `startingHealth`：最大及初始受击值，当前默认均为 9。
- `destroyTouchedObjects`：保留原序列化字段名，行为已改为开启碰撞物体隐藏。
- `destroyOnlyWhileExternalMoving`：默认开启，只在右移马夫接管后清障，避免入场落地删除地形。
- `useRoomResetForDestroyedObjects`：开启时通过 `TemporaryObjectHideState` 隐藏并支持 `RoomResetObject` 恢复；关闭时仍回退为直接停用对象。
- `maxHiddenObjectsPerFrame`：碰撞回调只把唯一目标加入延迟队列，`LateUpdate` 每帧最多实际隐藏该数量，默认 4。
- 启用房间恢复时，实际隐藏使用 `Project.TemporaryObjectHideState`，只关闭 Renderer、Collider，并缓存后切换 3D Rigidbody 的 Kinematic/重力状态；对象保持激活，`RoomResetObject.RestorePose` 会恢复这些状态。
- `lavaContactDamage`：岩浆首次接触伤害，默认 1。
- `invulnerabilitySeconds`：受击后无敌时间，当前默认 4 秒。
- `invulnerabilityFlashInterval`：无敌期间 Renderer 启用/关闭切换间隔，默认 0.08 秒。
- 无敌开始时自动收集 Boss 自身及当前全部子 Renderer，并保留每个 Renderer 原始启用状态；后续加入的手部动画等 Renderer 会在下一次受伤进入无敌时自动纳入，无需手工配置。

隐藏始终跳过玩家和其他 `BossController`。碰撞目标按实例 ID 去重，避免同一物体的多个 Collider 重复排队；实际停用不发生在物理回调内。岩浆接触在非无敌时先调用 `ReceiveHit(lavaContactDamage, Vector3.zero)`，随后无论是否处于无敌都排队隐藏该岩浆对象。`ReceiveHit` 在无敌期间忽略所有后续伤害；普通受伤只扣血、进入无敌和闪烁，不切换 `Hit`、不施加硬直或击退，也不会打断跳跃。死亡时立即停止闪烁并恢复正常 Renderer 状态。

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
- `BossCameraSize`
