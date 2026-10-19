# 全局传送地图

最终发布把各策划迁移关卡和 Boss 统一放入 `Gaming`，不靠独立工作场景间的加载拼接。工作场景 Scene、同名 planning、meta 及新增依赖仍须成套交付；正式 Gaming 另交 `Content/LevelMaps/正式关卡.json`。

## 玩家操作

- 世界收集品达到 **30% 且已击败 Boss** 后开放。此前不能按 T 打开地图；战斗期间及 Boss 所属房间禁用。Boss 门外的普通重生点允许传送。
- T 打开/关闭，Esc 只关闭地图，不顺带打开暂停。触控有「地图」入口；单指拖动、双指缩放，PC 滚轮缩放，也有 ± 和总览按钮。
- 地图只显示房间/通道轮廓、玩家、重生点与房间收集剩余数，不复制内部物体、美术或物理场景。空心点未解锁，实心点可点击；Boss 重生点完全不列入目的地。
- 点击已激活的普通点：拉远镜头 → 平移到目标 → 安全落位 → 拉近。被阻挡的落点拒绝，不退到游戏起点；目标房准备后才提交源房切换，不因失败重置源房谜题。
- 地图界面不修改 Time.timeScale。玩家控制器持有专用安全租约，浏览/传送时防受伤和自然下落，关闭、暂停、死亡、禁用、Boss 重开或相机被抢均清理；其他世界表现照常。

## 开发接口

`Project.FastTravel.WorldTravelMap` 自动装在唯一标准 Player 实例上，不产生第二份玩家预制体。`TryOpen/Close/TryTravel(RespawnAnchorFeature)` 为入口；`IsUnlocked/CanUseHere/IsOpen/IsTravelling` 可查询。

`Resources/UI/TravelMapSettings.asset` 保存字体、门槛和三段镜头时长（默认 0.3 / 0.65 / 0.4 秒）。游戏文字统一 Cubic_11 TMP；不另造占位字体。

`PlayerController.AcquireTravelSafetyLock(owner)` 是可 Dispose 的控制器租约，地图不自行写 Rigidbody。控制器禁用会使旧租约代次失效，旧句柄迟到释放不能清掉重新启用后的新锁。地图同步监听 `GameFlowController.TransitionStarted`，先归还自己临时刚体状态，再让菜单捕获，避免返回游戏永久冻结。

`TryTravelToCheckpoint` 不是死亡，不调用 Boss 复活奖励或复活血量；成功后绑定真实目标重生点。附近落点搜索必须仍在目标点所属房间，不能向下/横移搜进邻房或 Boss 房；玩家组件被禁用时地图不可打开，已打开的会自动清理。

剧情返回使用 `PlayerController.TryTeleportToStoryAnchor(TeleportAnchorFeature)`：同场景清理旧运动/房间状态，通知相机，绑定目标处会话级普通复活兜底，避免回 Start 后又复活到旧 Boss 点。后续踩真实重生点正常替换绑定。没有目标时明确失败，绝不自动加载另一个工作场景。

`BossTravelProgress` 记录 Boss 原始所属房间，不随追逐位置变化。多房间 Gaming 仅禁 Boss 房；不能因为场景里存在一个 Boss 就禁整张地图。专用 Boss 工作场景可整体禁用。详见 `COLLECTIBLE_BOSS_PROGRESS_API.md`。

收集统计读 `CollectibleProgressService`，包括未启用作者物体；演出道具显式排除。剩余数随结算更新，R/死亡/离区不会恢复已收集物。当前不负责写磁盘，后续存档通过稳定作者 ID 导出/恢复接口接入。

## 性能与回归

`TravelMapGraphic` 将房间格缓存为连续行段，整层共用一个裁剪 UI mesh；拖动/缩放不创建每格对象，不扫描场景美术。重生点仅开图时枚举，镜头只由 CameraControlManager 控制。

隔离静音工程入口：`Project.FastTravel.Editor.TravelMapAudit.Run`。覆盖门槛、战斗/Boss 点限制、缓存行段、滚轮/双指、租约恢复、落点阻挡、真实运镜和传送落位、重生绑定、战斗中断/暂停/禁用。真实设备手势与最终合图通关另验，不能把夹具验证当成整关已通关。
