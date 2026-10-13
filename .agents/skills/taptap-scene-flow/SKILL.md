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
- 开始游戏必须通过 `StartMenuFlowBridge` 请求 `GameFlowSceneId.Gaming`。
- `GameSceneCatalog` 只登记 MainMenu 与 Gaming 两个流程场景。
- 运行时存在兜底：catalog 为空时从 `Resources/GameSceneCatalog` 加载，缺少流程根或桥接组件时自动补齐；场景文件仍必须保持正确接线。

## Gaming

- 是唯一正式玩法入口，`LevelSceneContext.sceneId` 固定为 `GameFlowSceneId.Gaming`。
- 玩法 Player 必须使用 Unity `Player` Tag，供 `LevelSceneContext` 自动查找。
- Main Camera 必须挂：
  - `CameraControlManager`
  - `CameraModeController`
  - `CameraFollowController`
  - `LevelSceneContext`
- `CameraFollowController.target` 和 `CameraControlManager.focusTarget` 直接指向 `Player(暂定)`；`LevelSceneContext.Activate()` 仍负责在流程进入 Gaming 时按 `Player` Tag 复核并切换到 Side2D。
- 暂停界面使用 `Assets/_Project/Content/UI/SharedGameUI.prefab`，作为 Gaming 的根级 PrefabInstance 放入。
- 不重复添加 Camera、AudioListener 或 EventSystem；场景中三者各保留一份。

## 修改规则

- 场景路径、ID 或跳转变化时，必须同步更新 `GameFlowSceneId`、`GameSceneCatalog`、`EditorBuildSettings`、MainMenu 桥接和 Gaming 的场景上下文。
- 不允许把 MainMenu 的 `GameFlowController` 挂到会随关卡卸载的 UI 子物体上。
- 不允许在 Gaming 里重新放一套主菜单 UI。
- 新增正式场景前先征得用户确认，并同步修改本 skill；未确认前只作为开发测试场景。
