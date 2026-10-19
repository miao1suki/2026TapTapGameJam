---
name: taptap-scene-flow
description: 2026TapTap 正式场景流程规范；修改启动、MainMenu、Gaming、场景跳转、关卡入口、相机随行或暂停预制体接线时必须使用。
---

# 正式场景流程

当前正式启动及游玩只有 `Assets/_Project/Scenes/Playing/Gaming.unity` 一份场景。
MainMenu 是该世界里的逻辑 UI 状态，不是第二份运行场景。`Assets/_Project/Scenes/Start/MainMenu.unity`
仅保留为旧编辑器入口，打开运行时重定向到 Gaming，不得加入正式构建。

不再使用 Bootstrap、旧 `Flow` 目录、五个 Systems 场景、Level_01～03 或 Ending。不要恢复或复制这些退役内容。

## MainMenu

- 正式入口为 Gaming；`GameFlowController` 在首帧前自动安装流程根和 `Resources/UI/WorldMainMenu.prefab`。
- 菜单 Prefab 仅有界面、`StartMenuController` 和 `StartMenuFlowBridge`，不能包含相机、Listener 或 EventSystem。
- `GameFlowController` 在流程根安装并初始化 `GameAudioService`，音频切换服务随流程根保留；不依赖旧 Audio 系统场景。玩法入口播放 `music.atmosphere.e_minor`。
- 开始游戏必须通过 `StartMenuFlowBridge` 请求 `GameFlowSceneId.Gaming`。
- `GameSceneCatalog` 保留 MainMenu/Gaming 两个逻辑 ID，两者 ScenePath 都指向同一 Gaming。
- 运行时存在兜底：catalog 为空时从 `Resources/GameSceneCatalog` 加载，缺少流程根或桥接组件时自动补齐；场景文件仍必须保持正确接线。
- Gaming 启动时直接显示真实世界与新菜单，不经历预载第二场景、黑色等待或无相机交接。逻辑 `ActiveSceneId` 初始为 MainMenu，物理 Scene 始终为 Gaming。
- `WorldMenuPresentation` 通过 `CameraControlManager` 近景租约构图；开始时菜单淡出并释放近景到正常跟随，返回时反向运镜并显示菜单。玩家及玩法输入/收集由可释放的预览租约挂起，世界美术继续运行；禁止用全局 timeScale=0 或停用 BlockFeature 达成菜单预览。
- 每次开始均继续同一个世界与玩家，不重载、不截图、不重置房间或颜色进度。返回菜单只是展示状态；玩法 R 重置和开发者慢复活仍归原流程所有。
- 运镜过程中保留菜单相机租约，通过 `CameraControlManager.Retarget` 到标准玩法机位；完成最后一帧后才释放。禁止在开始／返回时再次调用 `LevelSceneContext.Activate` 或 Snap 覆盖缓动。
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
- `CameraFollowController.target` 和 `CameraControlManager.focusTarget` 直接指向标准 `Player`；`LevelSceneContext.playerPrefab` 只使用 `Assets/_Project/Content/Player/Player.prefab`。旧 RuntimePlayer/Player(暂定) 已退役。`LevelSceneContext.Activate()` 仅首次初始化时按 `Player` Tag 复核并切换到 Side2D，不能在菜单入退中重做；接入先读 `Docs/Agent/Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。
- 暂停界面使用 `Assets/_Project/Content/UI/SharedGameUI.prefab`，作为 Gaming 的根级 PrefabInstance 放入。
- 不重复添加 Camera、AudioListener 或 EventSystem；首帧起只使用 Gaming 自身的一套，预览不能禁用它们。测试逐帧检查启用设备与真实中间机位，不只检查最终状态。
- 返回主菜单、重载、流程切换和 UI 销毁须立即清理暂停；即使暂停关闭动画尚未结束、`isPaused` 已为 false，也必须恢复时间缩放及音频。不要依赖即将卸载对象的 Tween 回调恢复全局状态。
- Gaming 返回 MainMenu 逻辑状态时保留当前真实世界，解除暂停并获取预览租约，再反向近景运镜、淡入菜单；不播放旧收黑或滚动背景。玩法UI隐藏，不重复实例化主菜单。
- 正式加载/开发者慢复活仍由 `GameSceneLoader` + `GameTransitionOverlay` 管理；融合菜单只是 MainMenu/Gaming 的展示路径，不能破坏其他显式加载调用，也不在 `GameUiRouter` 新建加载流程。

## 修改规则

- 场景路径、ID 或跳转变化时，必须同步更新 `GameFlowSceneId`、`GameSceneCatalog`、`EditorBuildSettings`、MainMenu 桥接和 Gaming 的场景上下文。
- `GameFlowController` 自动安装在独立流程根；菜单在 Gaming 中只实例化共享纯 UI Prefab 一次，不能加载 MainMenu Scene 或复制整个关卡作背景。
- 正式构建列表和 `MobileBuildSetup` 只收录 Gaming；旧 MainMenu 的磁盘 UI 也须移除蓝底／旧布局，不能只在 Play 时换图。
- 独立地图继续直接运行；显式加载正式 Gaming 的隔离玩法夹具可在流程初始化后调用仅 Editor 编译的 `EnterGameplayImmediatelyForDevelopment()`，生产包不包含该接口。
- 新增正式场景前先征得用户确认，并同步修改本 skill；未确认前只作为开发测试场景。

## Boss 入场 Timeline 硬约束

- Boss 入场中的每一个 `BossTimelineClip` 只能操作 Boss 做一件事：发呆、移动、跳跃、设置运动开关或死亡。
- 一个 Boss Clip 禁止同时承担移动、跳跃、状态开关、死亡等多个操作；需要多个动作时必须新增多条 `BossTimelineTrack`，按时间堆叠。
- 所有 Boss 动作的下游必须是 `BossController`：移动使用其外部移动/高度接口，跳跃使用 `RequestJumpTo`，瞬移使用 `TeleportTo`，状态与死亡使用控制器 API。Timeline/Actor 层禁止直接写 Boss Rigidbody 或 Transform。
- Boss 入场相机轨只允许 2D 运镜：保持正交 `Side2D`，只允许 2D 平移、缩放、停留和平面转向；不得申请透视 3D、三维环绕或自由 3D 机位。
- 修改 Boss 入场 Timeline 前必须检查当前 Clip 是否违反“一 Clip 一操作”，并在交付时说明使用的 Track 堆叠关系。
