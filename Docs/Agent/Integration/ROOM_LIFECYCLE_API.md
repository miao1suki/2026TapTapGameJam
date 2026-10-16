# 区域重生与重置 API

源码：`Assets/_Project/Code/Systems/Rooms`，与现有 Player / ColorBlocks / BlockFeatures 同 Assembly-CSharp。

## 区域与对象

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

不要 Destroy 应可恢复的作者对象：`RoomResetObject.HideForReaction(original, replacement)` 休眠原对象、记录临时替代物供重置清理。无区域管理器时返回 false，调用者可沿用旧销毁行为。预置藤蔓节段不销毁。

门、一次按下保持模式的门按钮、门提示、锚点及 CollectionRequested 的颜色钥匙不恢复。持续压住模式的门按钮参与区域重置，取消按下状态并重新评估门；若释放持续信号，门关闭，但其他保持按钮的进度保留。DropItemBase.CancelAttractionForRoomReset 仅取消未结算磁吸/出现动画，不中断已开始收集的钥匙。

三色接触方块的 RoomResetObject 在 Awake（早于重力/反应）捕获编辑器布置根位姿；归属保留为布置区域，不跟随搬运或水柱迁移。R 入口通过 BeforeRespawn 恢复来源和重生目标区域，RestorePose 后 RestoreState 清除速度/水柱/长度/攀附缓存。基础位姿不会在落地、搬运或按钮接触后重采样。

## 输入与颜色

开发者可通过 `PlayerController.ConfigureRespawn` 切换常规/加载转场复活，默认常规；两者都复用以下同一复位链。加载方式只在遮罩完整覆盖后调用复位，不切换场景、不改变区域或进度语义。接口与取消契约见 [PLAYER_RESPAWN_API.md](PLAYER_RESPAWN_API.md)。

`PlayerInputDriver -> PlayerController.RequestRespawn()` 是已有 Respawn 动作入口，存活时也可请求。保持键位，不直接读 Keyboard。取消携带、清理玩家临时状态，恢复房间后再寻找安全落点，同步 Rigidbody 与 Transform。

死亡自动重生先检查 `PlayerController.CanAutomaticallyRespawn`，等待精灵死亡动画和配置的死亡Timeline表现时间完成，不在死亡当帧调用重生。主动 `RequestRespawn()` 不受此等待限制；暂停不会推进死亡播放或自动重生时间。重复死亡不会重置等待，手动复活后旧等待条件立即失效，无异步延迟回调。

`PlayerRespawnPointRegistry.Bind(player, point)` 记录最近触碰。`TryFindPreferred(player, origin, out point)` 优先此绑定，旧链表优先级仅是无绑定的兼容后备。绑定为本次运行，未新增跨运行存档协议。

默认触发半径 1.5 格。`RespawnAnchorFeature.IsCurrent` 与历史 `IsActivated` 分离，只有当前点使用 activatedMaterial；新绑定通知旧点恢复 inactiveMaterial。`CurrentChanged(bool)` 供美术表现监听，`IPlayerRespawnBindingListener.OnRespawnBindingChanged(player, current)` 是注册表的类型安全回调；禁用点会取消绑定。历史链激活不会点亮非当前点，重复绑定同一点不重复播放动画。

玩家接近触发统一经 `RespawnAnchorFeature.ActivateForPlayer(player)`：激活链、切换当前绑定，并在首次解锁时播放 `CheckpointUnlock`，切回已解锁点时播放 `CheckpointSwitch`。`Activate()` 仍是静默历史激活入口；注册表本身不播声音，因此重生、初始化和链式解锁不会重复提示。

`ColorRuntimeService.ResetForRoom` 只请求区域重置，保留 unlocked，不发全局 OnRoomColorReset。仅 `ResetProgress` 明确清空颜色；历史事件用于显式进度重置，不用于房间移动。

## 验证

RoomIntegrationVerification.BuildAndVerify / RunPlayVerification 在 Library/FlowRetirementProbe 隔离项目执行；前者核对实际 MVMap 往返和世界占格、生成两个 Scene，后者进入真正 PlayMode 检查重生、区域恢复及持久状态。不可覆盖用户 Scene；nographics 不代表窗口视觉、手感或打包验收。
