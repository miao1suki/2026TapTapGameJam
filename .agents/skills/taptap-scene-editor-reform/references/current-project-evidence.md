# 当前项目证据

本页是 2026-10-27 在 `E:\Unity_Projects\2026TapTapGameJam` 的只读研究基线。实现前仍须重新检查源码和正式资源；数字用于发现意外迁移，不是永远不变的常量。

## 仓库与正式资源

- Unity：`6000.3.12f1`
- 唯一远程：`https://github.com/miao1suki/2026TapTapGameJam.git`
- 正式玩法 Scene：`Assets/_Project/Scenes/Playing/Gaming.unity`
- 旧同名规划：`Assets/_Project/Scenes/Playing/Gaming.planning.json`
- 旧命名规划：`Assets/_Project/Content/LevelMaps/正式关卡.json`
- 美术作者数据：`Assets/_Project/Scenes/Playing/Gaming_Art.asset`

研究前后只读哈希：

| 文件 | SHA-256 |
|---|---|
| `Gaming.unity` | `1763418D57F60AFBB2B83DB7B6111EDB368A853908D1E5D793CEBFF1711CA057` |
| `Gaming.planning.json` | `5714D40118DD1109D0F56F80DD6DD9CCD9A0B622A93867BBA27F200E37EA3C90` |
| `正式关卡.json` | `5714D40118DD1109D0F56F80DD6DD9CCD9A0B622A93867BBA27F200E37EA3C90` |
| `Gaming_Art.asset` | `056395010966A03C8F5AC277051A67BE5173F6EE043E45C743CCA824AC1683F1` |

本次研究没有打开 Unity，没有触发 Scene 序列化。

## 正式关卡现状

- 旧规划文档有 25 个房间/通道拥有者、3010 条顶层物体记录；其中 `room_authored_Connections` 是 1 个通道。
- Scene 有 3004 个 `LevelEditorPlacedBlock`，全部已有非空 `planningBoxId`。
- 3004 个标记对应 2998 个唯一 ID；唯一重复逻辑组 `box_f337f842` 有 7 个实例。新索引不能假定一个 ID 只对应一个 GameObject。
- Scene 有 99 个 `LevelEditorMergedShape`、2854 个 `RoomResetObject`、1 个 `RoomLifecycleManager`、1 个 `GridArtScene`。
- `Gaming_Art.asset` 当前序列化 13817 个美术格锚点。历史文档里的 13655 是此前交付数字，不能拿来自动删“多余”格。
- 正式 Scene 已经包含关卡权威所需的对象、稳定身份、房间区域、实例字段和真实引用；无需从规划 JSON 重建才能开始改版。

## 已有可复用基础

### SceneView 直接编辑

`LevelEditorScenePainter` 已经接入 `SceneView.duringSceneGui`，能把鼠标换算为格、显示网格、框选并直接调用真实 Prefab 放置。这证明 SceneView 路线可行，但当前实现仍有以下限制：

- `LevelEditorBlockFactory.FindAt` 每格只返回一个对象，无法安全处理墙、水、道具同格。
- `EraseAt` 会删除这个任意结果，没有对象通道和逻辑组确认。
- `PlaceProp` 不建立稳定作者 ID，也不绑定房间重置归属。
- 普通 Transform 移动不会回写 `LevelEditorPlacedBlock.Cell`。
- 直接工具默认根是 `__LevelEditorContent`，而正式规划层级使用 `__PlanningMapGenerated/名称 [稳定ID]`。

### 打开工具会写 Scene 的风险

`LevelEditorWindow.CreateGUI` 与 `SetEditMode(true)` 都调用 `LevelEditorBlockFactory.MigratePlacedBlocks()`。该方法可能修改 Transform 缩放、颜色数据和 `cellWorldSize`，并标脏 Scene。新工具接入前必须先移除这些隐式调用；迁移只能是带预览、确认和 Undo 的独立命令。

`LevelEditorViewLock.Exit()` 没有精确恢复先前正交状态：它无条件写 `orthographic=false`，并在先前已正交时改成斜视旋转。新视角会话必须逐个 SceneView 保存并原样恢复全部状态。

### 房间与通道

`RoomLifecycleManager` 已经把 `List<RoomRegion>`、`cellSize`、`RoomResetObject.regionId` 作为运行时真数据：

- `RoomRegion` 已包含 `id`、`label`、`corridor`、`cells`。
- `RebuildOwnership()` 建立精确格所有权并检查重叠。
- `FindRegion()`、`Register()`、`ResetRegion()` 和 `CanPlaceInAuthoredRegion()` 已直接依赖这些区域。
- 当前 `Configure()` 同时写 `planningJson`，不适合作为新编辑器提交入口。应新增只改区域/格尺寸且保留旧隐藏字段的作者 API，Editor 服务在调用前负责 Undo。
- `RoomResetObject.Bind/SetRegion` 可承接显式归属，但不能在打开工具时批量添加或重绑。

### 参数和机关关系

最终运行组件已经保存真实参数和 Unity 引用：

- 门与按钮：`DoorController.buttons`、`DoorButton.owner`，已有 `SetButtons`、`SetOwner`、`ForgetButton`。
- 移动平台：`linkedControls`、`crankController`，已有 `BindSignalSources`、`BindCrank`、`BindBase`。
- 曲柄：`brakeBase`、`sharedController`，已有绑定与注册 API。
- 平台附着：`PlatformMount.platform`，已有 `Bind`。

因此新编辑器应编辑真实组件/Prefab override；不再把参数和关系复制到 `PlanningBox.componentOverrides`、`doorOwnerId`、`mechanism*Id`、`crankOwnerId` 或 `platformMountId`。

### 稳定拾取身份

`ColorKeyPickup` 与 `CollectiblePickup` 都通过 `LevelEditorPlacedBlock.PlanningBoxId + PlanningLocalCell + Scene path` 生成稳定实例身份。改版时可以先给这两个遗留字段增加语义别名，而不是批量改写正式 Scene：

- 新对象创建时必须分配非空、同 Scene 唯一的作者 ID。
- 同一逻辑多实例共享 ID、用稳定局部格区分。
- 移动物体或整个区域时保留 ID 和局部格，只更新实际 Scene Cell/Transform。
- 字段重命名或组件替换必须另做显式迁移；不能借打开工具触发。

### GridArt

`GridArtScenePainter` 已能直接在 SceneView 中绘制，且一个笔画使用一个 Undo 组。保留它比把美术转成 GameObject 更符合现状。

当前风险：

- `GridArtWindow.BindAutomation()` 打开窗口就把 `buildPending=true`，会延迟调用构建。
- `GridArtBuilder.Apply()` 即使输出相同也会写构建签名并无条件 `EditorSceneManager.MarkSceneDirty(scene)`。
- Apply 以完整 Map 为输入并遍历所有层；虽按 32 格分块生成，但还没有以内容哈希跳过未变化块的完整提交协议。

新模式必须让“进入美术模式”零写入，只在真实笔画后修改 Map 和必要的网格块。

## 完成版验证（2026-10-27）

- Unity `6000.3.12f1` 隔离工程批量入口依次运行 Stage 1、Stage 2、Stage 3–6 与 Formal，最终退出码为 `0`；卡片选择器与根实例 Inspector 收口后 Stage 3–6 为 48 项，Formal 为 17 项。
- 原 `LevelEditorPalette.asset` 的方块、道具、Prefab 与模板参数已接入按颜色分组的可搜索卡片选择器；Palette 只负责生成，新实例与既有实例均不保存或认领来源，参数只认真实 Scene 组件与 Prefab override。
- 正交对象模式点击 Renderer/Collider 子物体时统一解析到 `LevelEditorPlacedBlock` 作者根，并打开真实实例参数窗口；选择和开窗保持零写入。
- 直接覆盖现有正式规模：3004 个对象的失效驱动索引构建 `60.932 ms`；10240 次缓存查询 `0.794 ms` 且 `0 B` 分配。49017 个区域格合并为 69 个轮廓矩形，缓存构建 `18.906 ms`。
- GridArt 单格增量构建 `6.054 ms`，只写命中的 32×32 块；大笔刷跨块、锚点缓存擦除、自动地形邻块刷新、稀疏归属一步 Undo/Redo 和保存重开引用均通过。
- 只读正式审计前后 Scene dirty/Undo 不变；`Gaming.unity`、两份历史 JSON 与 `Gaming_Art.asset` 的 SHA-256 均保持本页基线。
- 主 Unity 中的 `Gaming` 仍有未保存状态；实现、审计和性能测量均在隔离副本完成，没有保存主进程中的正式场景。

## 规则冲突已收口

根 `AGENTS.md`、`WORKING_AGREEMENTS.md`、`FORMAL_LEVEL_DELIVERY.md`、提交守卫、开发者说明与系统索引已在同一次改版中切换为 Scene 权威。正常菜单与 Inspector 进入新的 SceneView 工作流；PlanningCanvas、JSON 存档/片段/应用流程及旧 GridArt 画布只保留在 Legacy，历史 JSON 不再作为交付物或第二权威。
