---
name: taptap-scene-flow
description: 2026TapTap 正式场景流程规范；修改启动、MainMenu、Gaming、场景跳转、关卡入口、相机随行或暂停预制体接线时必须使用。
---

# 正式场景流程

当前正式场景序列只有：

1. `Assets/_Project/Scenes/Start/MainMenu.unity`
2. `Assets/_Project/Scenes/Playing/Gaming.unity`

不再使用 Bootstrap、旧 `Flow` 目录、五个 Systems 场景、Level_01～03 或 Ending。不要恢复或复制这些退役内容。

## MainMenu

- 是唯一启动入口。
- `__GameFlow` 根持有 `GameFlowController`，引用 `Assets/_Project/Content/GameFlow/Resources/GameSceneCatalog.asset`。
- `StartMenuPrototype` 持有 `StartMenuController` 和 `StartMenuFlowBridge`。
- `GameFlowController` 在流程根安装并初始化 `GameAudioService`，音频切换服务随流程根保留；不依赖旧 Audio 系统场景。玩法入口播放 `music.atmosphere.e_minor`。
- 开始游戏必须通过 `StartMenuFlowBridge` 请求 `GameFlowSceneId.Gaming`。
- `GameSceneCatalog` 只登记 MainMenu 与 Gaming 两个流程场景。
- 运行时存在兜底：catalog 为空时从 `Resources/GameSceneCatalog` 加载，缺少流程根或桥接组件时自动补齐；场景文件仍必须保持正确接线。
- MainMenu 启动时叠加预载真实 Gaming，并保留 MainMenu Scene 承载界面；菜单不再使用滚动背景和黑圈揭示。逻辑 `ActiveSceneId` 仍为 MainMenu，Unity 实际活动 Scene 为 Gaming。
- `WorldMenuPresentation` 通过 `CameraControlManager` 近景租约构图；开始时菜单淡出并释放近景到正常跟随，返回时反向运镜并显示菜单。玩家及玩法输入/收集由可释放的预览租约挂起，世界美术继续运行；禁止用全局 timeScale=0 或停用 BlockFeature 达成菜单预览。
- 首次开始复用预载的同一 Gaming；再次开始按原流程恢复关卡对象、保留本次运行颜色进度。重载先留存短期画面底图，再卸载旧 Gaming 后加载新 Gaming，禁止两份同房间ID的世界同时运行。不能新增 ResetProgress 来清空颜色。
- MainMenu 存活期间必须恒定保证鼠标可见；2026-10-17按用户要求，Windows/Linux独立包有焦点时通过DesktopCursorPolicy使用Confined限制在游戏窗口，失焦立即释放。Editor/安卓仍None。不能恢复旧的每帧None或Locked居中路径；绝对指针与菜单点击保留。
- 开始菜单退出游戏仍经 `StartMenuFlowBridge` 请求 `GameSceneLoader`；融合菜单路径不播放旧黑圈，不允许按钮直接调用 `Application.Quit`。

## Gaming

- 是唯一正式玩法入口，`LevelSceneContext.sceneId` 固定为 `GameFlowSceneId.Gaming`。
- 玩法 Player 必须使用 Unity `Player` Tag，供 `LevelSceneContext` 自动查找。
- Main Camera 必须挂：
  - `CameraControlManager`
  - `CameraModeController`
  - `CameraFollowController`
  - `LevelSceneContext`
- `CameraFollowController.target` 和 `CameraControlManager.focusTarget` 直接指向标准 `Player`；`LevelSceneContext.playerPrefab` 只使用 `Assets/_Project/Content/Player/Player.prefab`。旧 RuntimePlayer/Player(暂定) 已退役。`LevelSceneContext.Activate()` 仍负责在流程进入 Gaming 时按 `Player` Tag 复核并切换到 Side2D；接入先读 `Docs/Agent/Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。
- 暂停界面使用 `Assets/_Project/Content/UI/SharedGameUI.prefab`，作为 Gaming 的根级 PrefabInstance 放入。
- 不重复添加 Camera、AudioListener 或 EventSystem；MainMenu/Gaming 同时加载时只启用 Gaming 的一套，菜单旧设备保持关闭。测试统计启用中的设备而非所有载入对象。
- 返回主菜单、重载、流程切换和 UI 销毁须立即清理暂停；即使暂停关闭动画尚未结束、`isPaused` 已为 false，也必须恢复时间缩放及音频。不要依赖即将卸载对象的 Tween 回调恢复全局状态。
- Gaming 返回 MainMenu 时保留当前真实世界，解除暂停并获取预览租约，再反向近景运镜、淡入菜单；不播放旧收黑或滚动背景。玩法UI隐藏，不重复实例化主菜单。
- 正式加载/开发者慢复活仍由 `GameSceneLoader` + `GameTransitionOverlay` 管理；融合菜单只是 MainMenu/Gaming 的展示路径，不能破坏其他显式加载调用，也不在 `GameUiRouter` 新建加载流程。

## 修改规则

- 场景路径、ID 或跳转变化时，必须同步更新 `GameFlowSceneId`、`GameSceneCatalog`、`EditorBuildSettings`、MainMenu 桥接和 Gaming 的场景上下文。
- 不允许把 MainMenu 的 `GameFlowController` 挂到会随关卡卸载的 UI 子物体上。
- 不允许在 Gaming 里重新放一套主菜单 UI；复用常驻 MainMenu Scene 的 Canvas，不能复制整个关卡作为菜单背景。
- 新增正式场景前先征得用户确认，并同步修改本 skill；未确认前只作为开发测试场景。

## Boss 入场 Timeline 硬约束

- Boss 入场中的每一个 `BossTimelineClip` 只能操作 Boss 做一件事：发呆、移动、跳跃、设置运动开关或死亡。
- 一个 Boss Clip 禁止同时承担移动、跳跃、状态开关、死亡等多个操作；需要多个动作时必须新增多条 `BossTimelineTrack`，按时间堆叠。
- 所有 Boss 动作的下游必须是 `BossController`：移动使用其外部移动/高度接口，跳跃使用 `RequestJumpTo`，瞬移使用 `TeleportTo`，状态与死亡使用控制器 API。Timeline/Actor 层禁止直接写 Boss Rigidbody 或 Transform。
- Boss 入场相机轨只允许 2D 运镜：保持正交 `Side2D`，只允许 2D 平移、缩放、停留和平面转向；不得申请透视 3D、三维环绕或自由 3D 机位。
- 修改 Boss 入场 Timeline 前必须检查当前 Clip 是否违反“一 Clip 一操作”，并在交付时说明使用的 Track 堆叠关系。
