# 区域重生与重置 API

源码：`Assets/_Project/Code/Systems/Rooms`，与现有 Player / ColorBlocks / BlockFeatures 同 Assembly-CSharp。

## 区域与对象

移动平台即使颜色尚未解锁、从未 Attach，也会收到房间恢复。平台须在首次恢复／Attach 时捕获作者局部原点，此后不得用默认零向量恢复，也不得在重复 Attach 时将路途中位置当成新原点。`PlatformColdResetVerification.Run` 覆盖冷启动、重复重置及解锁；`RunFormal` 覆盖正式 Gaming 的全部平台。

`RoomLifecycleManager.Configure(regions, cellSize, planningJson)` 保存世界图。RoomRegion.id 为稳定规划 ID；cells 为世界图坐标（Y 向下）。世界位置映射为 `floor(localX / size), -floor(localY / size)-1`。初始化所有权字典；每帧只查玩家一个格，不逐帧扫描物体或全场景物理查询。

规划增量/全量应用都写入管理器及 `RoomResetObject.regionId`。规划文档嵌入 Scene，首次打开可回读，不把 EditorPrefs 作为团队共享数据源。

- `FindRegion(worldPosition)`：按真实占格查找，区域外 null。
- `CurrentRegionId`、`RegionChanged(previous, next)`：当前区域及变化。
- `ResetRegion(id)`：只恢复该区，快照遍历与重入保护；先全部恢复姿态，再恢复状态。
- `Register(member)`：动态作者对象生成完成后显式注册；临时反应产物不成为新永久作者对象。
- `CanPlaceInAuthoredRegion(member, worldPosition)`：三色方块/搬运程序在放下前校验目标是否仍属于布置区域。尚未绑定区域时不猜测边界。
- `ForScene(scene)`：每场景一个管理器，当前支持一个游戏玩家；跨 Scene 常驻玩家/多人需专门扩展。
- `BeforeRespawn(player, destination)`：来源和目标区域复位，相同时仅一次，由 PlayerController 调用。

规划物体自动添加 RoomResetObject。手工物体可添加，区域 ID 留空则按初始位置识别。启动时也识别规划方块、BlockRuntime、万能方块、可搬运物及掉落物。原始姿态/碰撞只捕获一次，搬运不改变区域身份。

三色方块及 CarryableFeature 在注册时加入区域内物品表。LateUpdate 只检查这张表（不扫描墙体或场景），按物体根中心检查所属区域；跨房间、跨通道或区域外即恢复布置位姿/状态，挂起或携带中的物品同时取消玩家携带。玩家切区先取消携带并恢复整个来源区。CarryableFeature.BeginCarryDrop 也先拒绝跨区目的地；新搬运程序应调用上述校验，不能通过重新 Register/SetRegion 将携带物改属新区域。

新组件实现 `IRoomColorResettable`，或 BlockFeature 的 `OnResetForRoom`，清理协程、临时产物、接触缓存和交互状态。BlockAbilityHost 恢复后按仍保留的颜色激活状态挂接。不要逐帧注册或扫描。

切区复原仍在该帧完成，不延迟布置/碰撞恢复。`BlockAbilityHost.ResetForRoom` 先调用各功能的重置钩子，再用 `BlockRuntime.RefreshFeatureSet(true, true)` 保留仍启用、仍属于该物体的已有功能连接，只挂接新增或重新启用的功能、拆除真正离开的功能，同时清空信号/命令/状态队列。不要用无条件 Detach/Attach 代替重置：水体的销毁/重新注册会改变全局相邻水拓扑，使其他房间水网格也一起重建。水体重置只清蒸汽和玩家接触，保留未变的TA水面片/气泡；真实禁用、颜色停用和销毁仍完整清理。藤蔓重置立即恢复作者长度并清生长/燃烧，不依赖重建精灵。`RoomResetObject.RestorePose` 只写实际改变的姿态、激活和Collider属性，避免静态墙反复进入原生碰撞更新。

`RoomResetObject` 每个恢复阶段和 `Persistent` 查询枚举一次根物体的组件，复用非分配列表，不为每类持久对象分别进行原生查找。颜色/功能宿主钩子可能在重激活时添加运动与搬运辅助组件，这类物体会在宿主钩子后再刷新列表，保留原有自定义 reset 分发边界；普通静态墙不追加查找。此列表每次刷新而不是永久缓存：运行时添加/销毁的门提示、拾取组件或 Boss 临时隐藏组件必须立即参与下一次查询与恢复，不能因为首次查询为空就永远漏掉。

气泡柱重置时与禁用时使用相同的 `disconnecting` 重入保护：先清浮力/循环音，再清接触。清理触发的 `OnPlayerExited` 不是玩家物理离开水面，禁止借此重新加回浮力、重播进入/循环音效，或施加出水助推；玩家真实离开柱顶的正常助推仍保留。

不要 Destroy 应可恢复的作者对象：`RoomResetObject.HideForReaction(original, replacement)` 休眠原对象、记录临时替代物供重置清理。无区域管理器时返回 false，调用者可沿用旧销毁行为。预置藤蔓节段不销毁。

门、一次按下保持模式的门按钮、门提示、锚点及 CollectionRequested 的颜色钥匙不恢复。普通 `CollectiblePickup` 在 `CollectionRequested` 或 `IsCollected` 后也不恢复，前者保持收集飞行与唯一结算，后者保持隐藏；未收集品仍恢复布置位置。持续压住模式的门按钮参与区域重置，取消按下状态并重新评估门；若释放持续信号，门关闭，但其他保持按钮的进度保留。DropItemBase.CancelAttractionForRoomReset 仅取消未结算磁吸/出现动画，不中断已开始收集的钥匙或上述受保护收集品。普通收集品的稳定身份与后续存档恢复接口见 WORLD_ITEMS_API；本轮不新增磁盘存档系统。

`Persistent` 仅保留玩法进度，不保留 Boss 的临时隐藏。`RestorePose()` 在跳过持久对象姿态前归还 `TemporaryObjectHideState`，门再按当前 `DoorState` 恢复阻挡；已开始收集的掉落物仍由收集生命周期保护。每次恢复推进 `ResetRevision`，Boss 隐藏队列必须核对入队时版本，禁止在 R／死亡／离区后执行旧碰撞隐藏。详见 [BOSS_ROOM_RESET_VISUALS.md](BOSS_ROOM_RESET_VISUALS.md)。

三色接触方块的 RoomResetObject 在 Awake（早于重力/反应）捕获编辑器布置根位姿；归属保留为布置区域，不跟随搬运或水柱迁移。R 入口通过 BeforeRespawn 恢复来源和重生目标区域，RestorePose 后 RestoreState 清除速度/水柱/长度/攀附缓存。基础位姿不会在落地、搬运或按钮接触后重采样。

平台兜底：启动扫描会显式注册所有 `MovingPlatformFeature` 和 `OneWayPlatform`，不要求它们先经过常规 BlockRuntime 注册。`RoomResetObject.RestoreState` 会在宿主重置之后再对所有移动平台无条件执行 `ResetForRoom`，确保停用/脱附状态下遗留的路线位置、承载玩家、时间轴和音频都回到作者原点。

## 输入与颜色

开发者可通过 `PlayerController.ConfigureRespawn` 切换常规/加载转场复活，默认常规；两者都复用以下同一复位链。加载方式只在遮罩完整覆盖后调用复位，不切换场景、不改变区域或进度语义。接口与取消契约见 [PLAYER_RESPAWN_API.md](PLAYER_RESPAWN_API.md)。

`PlayerInputDriver -> PlayerController.RequestRespawn()` 是已有 Respawn 动作入口，存活时也可请求。保持键位，不直接读 Keyboard。取消携带、清理玩家临时状态，恢复房间后再寻找安全落点，同步 Rigidbody 与 Transform。

真实关卡作为主菜单背景时，`RequestRespawn()` 统一拒绝 `GameplayPresentationGate.IsSuppressed`；自动复活和手动R共用此入口。死亡后返回菜单不会在背景中复位房间或启动慢复活黑幕，进入正式游戏后再开放，不以改变重生规则或清空进度规避。

死亡自动重生先检查 `PlayerController.CanAutomaticallyRespawn`，等待精灵死亡动画和配置的死亡Timeline表现时间完成，不在死亡当帧调用重生。主动 `RequestRespawn()` 不受此等待限制；暂停不会推进死亡播放或自动重生时间。重复死亡不会重置等待，手动复活后旧等待条件立即失效，无异步延迟回调。

`PlayerRespawnPointRegistry.Bind(player, point)` 记录最近触碰。`TryFindPreferred(player, origin, out point)` 优先此绑定，旧链表优先级仅是无绑定的兼容后备。绑定为本次运行，未新增跨运行存档协议。

默认触发半径 1.5 格。`RespawnAnchorFeature.IsCurrent` 与历史 `IsActivated` 分离，只有当前点使用 activatedMaterial；新绑定通知旧点恢复 inactiveMaterial。`CurrentChanged(bool)` 供美术表现监听，`IPlayerRespawnBindingListener.OnRespawnBindingChanged(player, current)` 是注册表的类型安全回调；禁用点会取消绑定。历史链激活不会点亮非当前点，重复绑定同一点不重复播放动画。

`RespawnAnchorFeature` 直接保存 Boss 关卡重生数据：

- `isBossArenaRespawnPoint`：是否在玩家复活后同步 Boss。
- `isBossEntryRespawnPoint`：仅用于读档回退的 Boss 前置出生点标记，不改变该重生点的正常激活、绑定或复活行为。
- `bossRespawnHealth`：玩家完成复活后设置的 Boss 当前血量，按 Boss 最大值夹取。
- `bossRespawnLocalX`：相对重生点的世界 X 偏移；Y 固定继承重生点，Z 也继承重生点。

读档时，如果 `currentRespawnAnchorId` 指向 `isBossArenaRespawnPoint`，`RunSessionCoordinator` 会在恢复重生点链表后查找同场景、已激活且链表编号小于 Boss 点的 `isBossEntryRespawnPoint`，优先取编号最大的前置点恢复玩家。正常死亡、R、传送地图和普通读档点不经过该分支。

Boss 战复活兜底：`PlayerController.PerformRespawn` 在正常重生点解析后检查同场景活动 Boss。若没有任何 `IsRespawnPointAvailable` 且 `IsBossArenaRespawnPoint` 为真的已解锁出生点，则扫描同场景全部启用的 Boss 关出生点，即使尚未解锁也参与候选；按 `RespawnPosition.x` 取最左侧点作为本次复活目标，X 相同时按实例 ID 稳定选择。该目标继续走原有安全落位和 `IPlayerRespawnPreRoomResetListener` 链，因此 Boss 血量/位置重置不会被绕过；兜底不会改写玩家当前绑定出生点。

`PlayerController` 在确定本次实际选中的重生点后、调用 `RoomLifecycleManager.BeforeRespawn` 之前，通过 `IPlayerRespawnPreRoomResetListener.OnPlayerRespawnBeforeRoomReset` 建立 Boss 待应用请求；房间恢复完成后通过 `IPlayerRespawnPostRoomResetListener.OnPlayerRespawnAfterRoomReset` 才应用 Boss 状态。这样顺序固定为“房间重建 → Boss 复活前清障 → Boss 归位”。若 Boss 尚未由 Timeline 生成，请求保持等待，每 0.1 秒查询 `BossRespawnStateRegistry` 和 `Boss` Tag 后备查找。Boss 可用后先调用 `PrepareForBossRespawn()`，再收到 `ApplyBossRespawnState(health, worldPosition)`；重生点随后回读 `CurrentHealth` / `WorldPosition`，Inspector 显示“已应用并检测（一致/不一致）”。不在重生点保存 Boss 引用，也不在玩家脚本里依赖 Boss 类型。

运行时调试区提供“模拟应用 Boss 血量与位置”按钮，并显示待应用状态、最近检测血量和位置；用于确认后生成 Boss 是否真正收到状态。

附属 `BossRespawnMarker` 是一对一 Scene 标记，没有 Collider/Renderer，不保存业务数据。Inspector 和关卡画布均只允许横向拖动；Y 始终与重生点一致。标记或标记组件被删除后会补建并重新绑定。规划参数中的 Boss 开关、血量和 X 与其他组件覆盖一样随组件存档保存。

关卡参数窗口对重生点提供“生成 Boss 复活标记”，对应规划物体 `propEntryId = "__boss_respawn_marker"`。该标记格通过 `PlanningBox.bossRespawnOwnerId` 连接父重生点，不生成额外场景实体；拖动时只更新 X，Y 始终复制父重生点。标记格会随房间规划存档、片段导入导出和场景同步保留；删除后会按父点开关自动补回。

玩家接近触发统一经 `RespawnAnchorFeature.ActivateForPlayer(player)`：激活链、切换当前绑定，并在首次解锁时播放 `CheckpointUnlock`，切回已解锁点时播放 `CheckpointSwitch`。`Activate()` 仍是静默历史激活入口；注册表本身不播声音，因此重生、初始化和链式解锁不会重复提示。

`ColorRuntimeService.ResetForRoom` 只请求区域重置，保留 unlocked，不发全局 OnRoomColorReset。仅 `ResetProgress` 明确清空颜色；历史事件用于显式进度重置，不用于房间移动。

## 验证

RoomIntegrationVerification.BuildAndVerify / RunPlayVerification 在 Library/FlowRetirementProbe 隔离项目执行；前者核对实际 MVMap 往返和世界占格、生成两个 Scene，后者进入真正 PlayMode 检查重生、区域恢复及持久状态。不可覆盖用户 Scene；nographics 不代表窗口视觉、手感或打包验收。

`Project.Rooms.Editor.RoomTransitionPerformanceAudit.Run` 对真实正式Gaming预热后逐区记录同步复原、随后水网格更新耗时和重新创建的水实例数量；`RunVerified` 另检查未变水实例零重建、解锁保留、每一对实际相邻房间双向切换仅复原来源一次、临时蒸汽/藤蔓生长清理、真实水体隐藏恢复、墙体遮水缓存、运行时新增/销毁持久和隐藏组件，以及玩家仍在柱内/刚过柱顶时重置无浮力、音频和出水助推残留。输出 `Library/RoomTransitionPerformanceAudit.txt`。运行只允许隔离工程；它测代码路径，不把编辑器/显卡等待或完整包帧率当成已经验收。
