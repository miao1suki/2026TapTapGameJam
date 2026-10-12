# 区域重生与重置 API

源码：`Assets/_Project/Code/Systems/Rooms`，与现有 Player / ColorBlocks / BlockFeatures 同 Assembly-CSharp。

## 区域与对象

`RoomLifecycleManager.Configure(regions, cellSize, planningJson)` 保存世界图。RoomRegion.id 为稳定规划 ID；cells 为世界图坐标（Y 向下）。世界位置映射为 `floor(localX / size), -floor(localY / size)-1`。初始化所有权字典；每帧只查玩家一个格，不逐帧扫描物体或全场景物理查询。

规划增量/全量应用都写入管理器及 `RoomResetObject.regionId`。规划文档嵌入 Scene，首次打开可回读，不把 EditorPrefs 作为团队共享数据源。

- `FindRegion(worldPosition)`：按真实占格查找，区域外 null。
- `CurrentRegionId`、`RegionChanged(previous, next)`：当前区域及变化。
- `ResetRegion(id)`：只恢复该区，快照遍历与重入保护；先全部恢复姿态，再恢复状态。
- `Register(member)`：动态作者对象生成完成后显式注册；临时反应产物不成为新永久作者对象。
- `ForScene(scene)`：每场景一个管理器，当前支持一个游戏玩家；跨 Scene 常驻玩家/多人需专门扩展。
- `BeforeRespawn(player, destination)`：来源和目标区域复位，相同时仅一次，由 PlayerController 调用。

规划物体自动添加 RoomResetObject。手工物体可添加，区域 ID 留空则按初始位置识别。启动时也识别规划方块、BlockRuntime、万能方块、可搬运物及掉落物。原始姿态/碰撞只捕获一次，搬运不改变区域身份。

新组件实现 `IRoomColorResettable`，或 BlockFeature 的 `OnResetForRoom`，清理协程、临时产物、接触缓存和交互状态。BlockAbilityHost 恢复后按仍保留的颜色激活状态挂接。不要逐帧注册或扫描。

不要 Destroy 应可恢复的作者对象：`RoomResetObject.HideForReaction(original, replacement)` 休眠原对象、记录临时替代物供重置清理。无区域管理器时返回 false，调用者可沿用旧销毁行为。预置藤蔓节段不销毁。

门、按钮、门提示、锚点及 CollectionRequested 的颜色钥匙不恢复。DropItemBase.CancelAttractionForRoomReset 仅取消未结算磁吸/出现动画，不中断已开始收集的钥匙。

## 输入与颜色

`PlayerInputDriver -> PlayerController.RequestRespawn()` 是已有 Respawn 动作入口，存活时也可请求。保持键位，不直接读 Keyboard。取消携带、清理玩家临时状态，恢复房间后再寻找安全落点，同步 Rigidbody 与 Transform。

`PlayerRespawnPointRegistry.Bind(player, point)` 记录最近触碰。`TryFindPreferred(player, origin, out point)` 优先此绑定，旧链表优先级仅是无绑定的兼容后备。绑定为本次运行，未新增跨运行存档协议。

`ColorRuntimeService.ResetForRoom` 只请求区域重置，保留 unlocked，不发全局 OnRoomColorReset。仅 `ResetProgress` 明确清空颜色；历史事件用于显式进度重置，不用于房间移动。

## 验证

RoomIntegrationVerification.BuildAndVerify / RunPlayVerification 在 Library/FlowRetirementProbe 隔离项目执行；前者核对实际 MVMap 往返和世界占格、生成两个 Scene，后者进入真正 PlayMode 检查重生、区域恢复及持久状态。不可覆盖用户 Scene；nographics 不代表窗口视觉、手感或打包验收。
