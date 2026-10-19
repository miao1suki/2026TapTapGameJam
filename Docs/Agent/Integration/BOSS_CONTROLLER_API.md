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
- `verticalDeadbandBlocks`：玩家与 Boss 的高度差死区，默认 `2` 格；死区内目标高度完全不变。
- `verticalTrackSpeedBlocksPerSecond`：超出死区后的最大高度变化速度，默认 `0.18` 格/秒。
- 追踪目标是玩家高度减去死区边界，不强制让两者完全同高；实际世界位移使用 `GridCellSizeUtility` 的格尺寸。
- 玩家死亡立即停止右移；标准复活落位后恢复，加载转场复活在最后 `FinishRespawnPresentation` 后恢复。

出生点弱补助：

- `RespawnAnchorFeature.NewlyActivatedByPlayer` 只在首次激活某个出生点时广播。
- `BossCheckpointAssistController` 订阅全部场景出生点；若玩家存活，调用 `BossRightMoveExecutor.BeginCheckpointAssist`。
- 补助目标是该出生点的 `BossRespawnPosition`，水平速度默认 `1.35` 倍、安全上限 `15` 秒、垂直辅助 `0.75` 格/秒。
- 新出生点再次触发时直接覆盖旧目标；玩家死亡时马夫清空补助，并由复活链传送到当前点配置位置并重置血量。
- 补助不是传送，不修改生命、重力、基础速度或跳跃参数。

玩家接触与连线：

- `BossController.killPlayerOnContact` 默认开启；碰到玩家父级上的 `PlayerController` 时调用 `EnterDeath`，然后跳过对该玩家碰撞体的清障处理。
- `BossPlayerLinkEffect` 只做显示，不产生碰撞、伤害或交互。
- 三个 `LineRenderer` 各使用一份运行时材质实例，分别直接设置 `_Style=1/2/3`、火线/水线/藤蔓线颜色和参数，避免 MaterialPropertyBlock 回退到材质默认 `_Style=0`。
- 连线起点固定为 Boss 中心；`HealthSystemActivated` 时用 `_RevealProgress` 生成，Boss `Died` 时用 `_DissolveProgress` 按玩家交互线的噪波方式消散。
- 距离越远，`_Opacity` 和 `_LineThickness` 越低；最远实体不透明度为 `0.4`，最近为 `0.78`，距离曲线使用 `1.35` 次幂让中段变化更平缓。当前整条粗细曲线为上一版的 3 倍，最远 `0.024`、最近 `0.9`，最近有效线粗为普通线的 `1.5` 倍。三条线的中心距按当前线粗的 `0.4` 倍计算，使其保持约 `60%` 重叠。曲线分段、弧高、波动、样式间距、排序图层与距离区间均可调。

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
- 玩家锁从 `0` 秒开始；相机轨在锁生效后延迟 `0.1` 秒启动，并在 `6.54` 秒结束，避免“锁操作”和“移动镜头”同帧开始。`BossWalkTimelineInstaller` 已同步该窗口，重新导入时不会恢复成同时启动。
- Boss 入场触发器场景实例的 `requestPlayerControlLockDuringTimeline` 继续保持开启，作为场景入口保险；它通过 `IPlayerControlLockTarget` 申请锁，Timeline 结束、停止、重置或禁用时释放。
- `BossTimelineClip` 每个 Clip 只能配置一个动作；`spawnBoss` 为真时使用 `bossPrefab` 生成，其他 Clip 通过 `Boss` Tag 查找已生成 Boss。
- 移动输入与跳跃请求同时处于 `4–5.4` 秒；Timeline 不直接写 Transform/Rigidbody，也不传送或纠偏最终位置。
- 相机使用 `CameraTimelineTrack`，入场资源只允许 2D 正交运镜。
- 相机轨结束时释放 Timeline 相机控制，并把 `TimelineCamRig.target` 交回 `CameraControlManager.FocusTarget`。
- Side2D 镜头强制使用平面 Yaw，不再把 LookRotation 的 3D 俯仰写回相机。
- Boss 死亡 Timeline 的相机控制释放在 `9.99` 秒最后一帧执行，并显式调用 `ReleaseTimelineControl(true)`，避免相机轨持续到 `10` 秒后重新接管。
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
- 组件不再订阅 `BossController.Died`。击败 Timeline 在返回镜头开始时通过 `BossDeathCameraRestorePlayable` 调用 `RequestDisable()`；普通玩家死亡/复活和外部对象销毁路径互不影响。

## Boss 死亡 Timeline

- Timeline：`Assets/_Project/Content/Boss/Timelines/Boss_defeat.playable`
- 播放器：`BossDefeatTimelinePresenter`，挂在 Boss 预制体，并由 `BossController.defeatPresentationRequested` 调用。
- 只响应 `EnterDeath` 发出的击败事件；外部 `Destroy` 不触发 `EnterDeath`，因此不会播放。
- 时长 `10` 秒：致命伤后先保持原镜头发呆 `1` 秒；第 `1–2` 秒平滑移动到玩家与 Boss 包围盒的中点；第 `2–5.5` 秒播放六钥匙吸收前缀；之后接原有 `4.5` 秒死亡演出。
- 死亡 Timeline 自然到尾后，`BossDefeatTimelinePresenter` 使用同一个 `PlayableDirector` 切换到 `player_thinking.playable`；两条死亡路径共用后续思考演出。
- 六钥匙前缀由 `BossKeyAbsorptionPlayable` 驱动：按红、蓝、绿顺序重复两轮，从玩家身体依次生成六把颜色钥匙，浮到玩家头顶后沿曲线吸入 Boss 中心；全部吸收后才进入原死亡镜头和死亡动画。
- 钥匙视觉缩放为 prefab 基础尺寸的 `0.55`，横向槽位间距 `0.52`，吸引阶段单把 `0.9` 秒、相邻间隔 `0.15` 秒，完整吸引在 `3.5` 秒内结束。
- 玩家锁轨只通过 `PlayerController.AcquireControlLock` 锁输入，`motionMode=None`、`driveRigidbody=0`，不写玩家 Transform/Rigidbody 位置。
- `1–5.49` 秒由 Boss 专用的 `BossCameraPlayable` 双目标模式取景：按 `Player`/`Boss` Tag 查找双方，镜头中心取两者完整 Collider/Renderer 包围盒中点，并按包围盒尺寸、屏幕宽高比和 `1.2` 世界单位边距计算正交视野。`dualTargetTransitionDuration=1` 让镜头从致命伤时的机位和视野平滑过渡到双目标构图，随后保持到吸收结束。共享 `CameraTimelineClip/Behaviour` 不增加 Boss 专用字段。
- 演出钥匙直接实例化 `ColorKey_red/blue/green.prefab`。实例先挂在未激活的专用根节点下，关闭全部 Collider、`ColorKeyPickup`、`DropItemBase`、`PickupFlightAnimator`、`MechanismArtworkPresentation` 和子 `PlayableDirector` 后再激活，因此只作为视觉对象，不会被玩家吸收、重复解锁、受当前解锁状态改材质或触发房间逻辑。
- 六钥匙全部吸收后的 `5.5` 秒，由第二条 `CameraTimelineTrack` 聚焦 Boss；随后死亡 Timeline 只观看 Boss 的死亡演出。
- 玩家使用 `PlayerTimelineTrack`，按 `Player` Tag 查找并锁定全部普通操作。
- 第 `5.5` 秒的 `BossDeathAnimationTriggerPlayable` 调用 `BossAnimationDriver.PlayDeathAnimationNow()`，让死亡动画在发呆、钥匙吸收和镜头到位后再开始。
- 致命伤害进入 Death 时缓存 Boss 的 Kinematic 状态、清零速度并临时设为 Kinematic，避免死亡前缀期间受物理接触抽搐；Boss 复活时恢复原刚体状态。
- 返回开始时刻由 `BossDeathCameraRestorePlayable` 恢复 `BossCameraSize`。
- 后续有真实死亡动画时，扩展 `5.5–9.5` 秒的停留区间并同步 Timeline 总时长即可；不需要修改玩家锁定或相机回归逻辑。

## Boss 处决 Timeline

- 运行时轨道：`BossExecutionTimelineTrack` / `BossExecutionClip`。
- `BossExecutionClip` 是独立 `PlayableAsset` 脚本资产，必须保存在同名 `BossExecutionClip.cs`；不能与 Track 共用脚本文件，否则 Unity 会报 `No script asset for BossExecutionClip` 并生成无行为的 Timeline Clip。
- 直接资产：`Assets/_Project/Content/Boss/Timelines/Boss_execution.playable` 和 `Assets/_Project/Content/Boss/BossExecutionTrigger.prefab`。
- 玩家进入 Trigger 后播放 Timeline；`PlayerTimelineTrack` 按 `Player` Tag 覆盖整段锁定普通操作，在 Clip 自然结束、Timeline 停止或销毁时由 `ActorTimelineBehaviour` 统一释放。`ContactTimelineTrigger` 不再额外申请玩家锁，避免双锁。
- `Boss_execution.playable` 使用 Boss 专用的 `BossCameraPlayable` 跟随模式：前 `1.2` 秒锁定相机起始 Y，只做 X/Z 追踪；随后 `0.48` 秒校正到 Boss 机位高度并持续跟拍。`BossExecutionClip` 只控制 Boss 执行行为；共享 `CameraTimelineClip/Behaviour` 未修改。
- 处决 Trigger 的 `PlayableDirector` 必须从 `PlayableGraph.GetResolver()` 解析；resolver 失败时才反查正在播放且包含 `BossExecutionTimelineTrack` 的 Timeline。不要用 Clip 的绑定 owner 获取导演；该轨无绑定对象，owner 为空时 `Stop()` 会静默失效，玩家锁和相机轨都不会释放。处决行为同时订阅 `ExecutionSequenceTerminated` 与 `BossController.Died`，任何死亡路径都会停止处决 Timeline 并释放其官方玩家锁轨和相机轨。
- 启动时先把 Boss 的 Y 瞬移到 `Player` Tag 对应玩家当前 Y，再冻结普通右移；与相机轨同步等待 `1.2` 秒后，以 `10` 格/秒开始右移。之后 execution 固定保持启动时记录的 Y，不再持续跟踪玩家高度；X/Z 不变。
- `destroyOnlyWhileExternalMoving` 的清障/伤害接触入口在 execution 期间同样放行，避免右移马夫暂停后岩浆与伤害道具的 Trigger 被过滤。
- execution 状态下任何正伤害都会直接清零血量，不要求血条或普通受伤入口已经激活；随后停止处决 Timeline 并释放玩家锁，再触发现有 `Boss_defeat.playable` 死亡演出。
- Boss 已在处决镜头内，因此 `Boss_defeat.playable` 的 `motionCurve` 从 Boss 机位值开始保持；不再接一段玩家机位到 Boss 的去程。死亡演出最后仍按原曲线回玩家并释放死亡 Timeline 的玩家锁与相机控制。
- Timeline 默认时长 `120` 秒；未受伤时保持处决运行，受伤后由死亡 Timeline 接管。
- `ContactTimelineTrigger` 默认仅玩家、只触发一次；位置和触发区域大小由场景实例自行摆放。

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
- `destroyOnlyWhileExternalMoving`：默认开启，只在右移马夫接管或 Boss execution 状态下处理清障与伤害接触，避免入场落地删除地形。
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
- `BossCheckpointAssistController`
- `BossCameraSize`
- `PlayableDirector`
- `BossDefeatTimelinePresenter`
