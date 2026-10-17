# 玩家复活方式 API

入口为 `Assets/Player/PlayerController.cs`，唯一标准来源仍为 `Assets/_Project/Content/Player/Player.prefab`。复活方式由开发者或关卡流程设置，不是玩家设置项；默认 `Standard`，不改现有输入动作。

```csharp
using Project.Player;

// Boss 战开始时设置；只改变之后的复活，不立即复活。
player.ConfigureRespawn(PlayerRespawnMode.LoadingTransition, loadingSeconds: 2f);

// 返回普通关卡时恢复原表现。
player.ConfigureRespawn(PlayerRespawnMode.Standard);

// 如需流程主动复活，仍调用唯一入口。
player.RequestRespawn();
```

`RespawnMode`、`RespawnLoadingSeconds`、`IsRespawning` 为只读查询。停留时间是背景与加载文字完全出现后的时间，不含遮罩、滑动和文字渐变；负值归零，非有限值回退 2 秒。运行中修改配置只影响下次复活，不改变正在播放的转场。设置不写 PlayerPrefs，不跨玩家实例或游戏重启保存。

## 时序与所有权

- `Standard` 保留同步复位及原摄像机跟回表现。
- `LoadingTransition`：收拢黑幕 → 背景入场 → 加载文字出现 → 调用原玩家/区域复位 → 停留 → 文字隐去 → 背景沿来路退场 → 展开黑幕 → 恢复操作。
- `RespawnCompleted` 在常规复位完成或慢速揭示完全结束时触发一次。取消不会触发。慢速期间重复请求被忽略。
- 死亡仍由 `CanAutomaticallyRespawn` 等待死亡动画，存活 R 仍走 `RequestRespawn()`；没有增加自动死亡计时器或新键位。
- 慢速转场冻结唯一玩家运动器，持有独立 `AcquireControlLock`，不清除其他系统的控制锁。揭示结束重新开始原配置的复活无敌时间。暂停打开请求在该期间被拒绝；转场 Tween 和停留用非缩放时间。
- 房间和重生点仍走 `RoomLifecycleManager.BeforeRespawn`、原安全落点和 `CameraFollowRelocated`，不换场景、不清空颜色/能力/钥匙/门进度。参见 [区域契约](ROOM_LIFECYCLE_API.md)。

安全落点查询排除岩浆、无色四向尖刺与旧尖刺的有效 Collider；自身过滤只跳过玩家本体及子物体，不跳过与玩家共用规划根物体的地板或危险物。尖刺接触在复活保护期间不致死，保护到期后仍停留在尖刺内会走正常死亡流程，见 [无色道具接入](WORLD_ITEMS_API.md)。

表现复用 `Project.GameFlow` 的 `GameSceneLoader` / `GameTransitionOverlay`，不新增加载 Canvas 或 Boss 专用场景。收黑与背景入场沿用 `Resources/GameTransitionSettings` 的返回大厅参数；背景取最近主菜单背景状态，直接开关卡则读取 `GameUiArtwork.menuBackground`。字体和遮罩 Shader 是转场配置的显式资源引用。

`GameSceneLoader.RespawnPhase` 提供六阶段状态，`IsRespawnTransitioning` 仅表示复活表现。底层 `TryStartRespawnTransition(owner, seconds, restoreWhileCovered, finished)` 不负责玩家复位或控制锁，玩法应优先使用 `PlayerController`，避免绕过房间契约。

## 中断

`CancelRespawnTransition(owner)` 只取消匹配的所有者；空参数供正式流程抢占。返回菜单、加载其他场景、退出、玩家禁用/销毁和加载器禁用均清理 Tween、背景、黑幕、输入拦截与本次控制锁。正式流程随后接管同一遮罩。

取消不是回滚：如果已在黑幕下完成复位，保留复位结果；尚未复位则保留原状态。取消后不补发 `RespawnCompleted`。Boss 系统自己的血量、阶段或生成物恢复仍由其房间重置接入负责，本 API 不自行推测。

## 验证入口

`Project.Player.Editor.RespawnTransitionVerification.Run` 仅在 `Library/FlowRetirementProbe` 隔离项目允许执行，使用真实 Gaming 玩家和加载资源，不保存 Scene。覆盖常规/死亡/R、停留时间、进度保留、重复请求、独立控制锁、各阶段取消、禁用、菜单抢占与非缩放时间。配置/资源自动测试位于 `GameFlow/Tests/Editor/RespawnConfigurationTests.cs`。
