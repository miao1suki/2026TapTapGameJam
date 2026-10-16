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
- 加载期间冻结 MainMenu 背景滚动；玩家输入锁从进入 Gaming 起保持到黑屏扩散完全结束。
- MainMenu 存活期间必须恒定保证鼠标可见且 `lockState` 为 `None`，任何染色、搬运或暂停逻辑遗留的隐藏状态都要在主菜单帧更新中纠正。
- 开始菜单退出游戏必须经 `StartMenuFlowBridge` 请求 `GameSceneLoader`：黑屏反向缩圈到中心、保持 1 秒后再退出，不允许按钮直接调用 `Application.Quit`。

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
- 不重复添加 Camera、AudioListener 或 EventSystem；场景中三者各保留一份。
- 返回主菜单、重载、流程切换和 UI 销毁须立即清理暂停；即使暂停关闭动画尚未结束、`isPaused` 已为 false，也必须恢复时间缩放及音频。不要依赖即将卸载对象的 Tween 回调恢复全局状态。
- Gaming 返回 MainMenu 时先以当前玩家为中心收黑，黑屏完整覆盖后保持 0.5 秒，再启动菜单背景从右侧回场；不得让背景先移动。
- 正式加载过渡统一由 `GameSceneLoader` + `GameTransitionOverlay` 管理。不要新增第二套加载画面或直接在 `GameUiRouter` 里控制过渡。

## 修改规则

- 场景路径、ID 或跳转变化时，必须同步更新 `GameFlowSceneId`、`GameSceneCatalog`、`EditorBuildSettings`、MainMenu 桥接和 Gaming 的场景上下文。
- 不允许把 MainMenu 的 `GameFlowController` 挂到会随关卡卸载的 UI 子物体上。
- 不允许在 Gaming 里重新放一套主菜单 UI。
- 新增正式场景前先征得用户确认，并同步修改本 skill；未确认前只作为开发测试场景。
