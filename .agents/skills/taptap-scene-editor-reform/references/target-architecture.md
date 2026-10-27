# 目标架构

## 权威数据边界

| 内容 | 权威来源 | 说明 |
|---|---|---|
| 房间、通道与占格 | Scene 中的 `RoomLifecycleManager.regions` | 通道是 `RoomRegion.corridor=true`；连接由四邻接计算 |
| 玩法物体、位姿、参数 | Scene 中真实 GameObject / Prefab instance | 实例参数是组件字段或 Prefab override |
| 机关、门、平台关系 | Scene 中真实 Unity 对象引用 | 使用现有绑定 API，稳定 ID 不代替引用 |
| 作者身份与逻辑组 | `LevelEditorPlacedBlock` 的兼容身份字段 | `AuthoringId/AuthoringLocalCell` 保留身份；实例不保存 Palette 来源 |
| 房间重置归属 | `RoomResetObject.regionId` | 只在显式生成、跨区重绑或整体移动时修改 |
| 可放置模板 | `LevelEditorPalette.asset` | 只定义模板/默认值，不保存关卡实例 |
| 美术格和图层 | Scene 引用的 `GridArtMap` | 稠密作者资产，不是 JSON，不转成数万 GameObject |
| 美术区域归属 | 稀疏 `GridArtRegionOwnership` 伴随资产 | 只记录已认领/新建的 `layerId + anchorCell -> regionId`，不重写所有旧格 |
| GridArt 网格、材质 | `Assets/_Project/Art/Generated/GridArt` | 派生产物，可按块重建，不是作者真源 |

Planning JSON、EditorPrefs 存档槽和 PlanningCanvas 不属于最终权威数据。

## 模块结构

```text
SceneLevelEditorOverlay
├─ SceneEditSession              视角、模式、热键和零写入会话
├─ SceneGridSpace                唯一坐标换算
├─ SceneRegionService            房间/通道绘制、拆分、整体移动
├─ SceneObjectIndex              对象、逻辑组、占格通道和反向引用索引
├─ ScenePlacementService         放置、移动、复制、擦除和归属
├─ SceneBindingService           门/按钮/平台/曲柄/附着关系
├─ SceneParameterPanel           真实实例 Inspector/批量参数入口
├─ SceneArtController            GridArt SceneView 笔画与脏状态
├─ SceneArtOwnershipService      稀疏区域美术归属
└─ SceneEditTransaction          Undo、校验、提交和失败回滚
```

Scene GUI 只做命中、预览和手势，把完整意图交给服务；服务不依赖 Overlay 的 VisualElement。

对象模式必须提供独立、醒目的 Palette 卡片选择器，而不是把全部方块/道具塞进一个无上下文下拉框。选择器复用旧工具的颜色分组、缩略图和方块/道具分类，并补充搜索、Prefab/模板资产、对象通道与默认参数摘要；单击选择，双击或主按钮开始绘制。

Palette 是一次性生成模板。选择模板只改 Editor 会话状态，实际绘制时实例化 Prefab 并应用默认参数；实例落入 Scene 后不保留栏目来源、不做旧对象认领、不按名称或 Prefab 反推来源，也不接受模板后续回写。现有对象点击时始终解析到 `LevelEditorPlacedBlock` 作者根，并直接打开绑定真实 Scene 实例的 UI Toolkit Inspector。

## Scene 编辑会话

Overlay 采用 UI Toolkit，模式互斥：

1. `区域`：房间、通道、拆分、重命名、整体移动。
2. `对象`：地形、道具、水、标记的放置、选择、移动、删除。
3. `绑定/参数`：编辑真实组件、建立引用、显示关系线和问题。
4. `美术`：GridArt 图层、笔刷、绘制、擦除和区域归属。

标题区可拖动、折叠、关闭并从 `Tools/2026TapTap/关卡编辑器/场景编辑器` 重开。正文只突出当前模式的主动作，高级选项折叠。进入 Play Mode 自动退出写模式，但不改作者数据。

每个进入编辑模式的 SceneView 保存以下原值：

- `orthographic`
- `rotation`
- `pivot`
- `size`
- Unity 2D/相机绘制模式及工具状态（若代码主动修改）

锁定期间保持前视正交、编辑平面 Z 和 XY 导航；退出、禁用、脚本重编译或 SceneView 销毁时精确恢复原值。保存/恢复这些编辑器视图状态不能标脏 Scene。

## 唯一坐标换算

所有服务共享 `SceneGridSpace`，禁止各自复制 `-y-1`：

```text
SceneCell       X 右、Y 上，LevelEditorPlacedBlock.Cell 使用
RegionCell      X 右、Y 下，RoomRegion.cells 使用
WorldCenter     真实 Scene 世界坐标
ArtCell         X 右、Y 上，GridArtMap 使用
```

换算必须考虑 `RoomLifecycleManager.transform` 与 `cellSize`，不能假定管理器永远在世界原点。若 GridArt cellSize 与关卡 cellSize 不一致，禁用跨系统整体移动并显示原因，不静默取整。

## 房间与通道绘制

`SceneRegionService` 用一个事务直接修改管理器区域列表并调用 `RebuildOwnership()`：

- 新建房间/通道后生成稳定 ID，改名不改 ID。
- 支持单格、连续笔画和矩形增删。
- 新区域必须四邻接连通且不得与其他区域重叠。
- 旧区域即使已有分离岛也能只读显示、整体移动和执行“不增加连通分量”的编辑；不因打开工具自动修复。作者可显式预览并“拆成独立区域”。
- 擦除导致新增断裂时默认拒绝；拆分动作给每个新区域分配 ID，并在提交前预览对象与美术如何重新归属。
- 通道与房间接触关系从四邻接实时计算；不保存 `fromRoomId/toRoomId`。
- 绘制区域不自动生成墙，擦除区域也不自动删除对象。

区域擦除命中所属对象时默认阻止并列出对象。作者只能显式选择：先移动对象、删除所列逻辑组，或让对象变为未归属。每个选择都必须可撤销且不能波及其他区域。

## 区域整体移动

整体移动是用户所说的“合并/装配”，只在当前 Scene 中工作，不读写 JSON。

移动前建立不可变清单：

- 选中区域的全部 `RoomRegion.cells`。
- `RoomResetObject.regionId` 属于这些区域的对象。
- 对应 `名称 [稳定ID]` 容器下的作者对象。
- 与上述对象同一作者 ID 的全部逻辑组成员。
- 被稀疏美术归属表认领的 GridArt 锚点。
- 用户显式收归区域容器的天气、装饰或辅助对象。

以下全局根默认禁止移动：玩家、Gameplay Camera、`RoomLifecycleManager`、`GridArtScene` 输出根、GameFlow/UI/EventSystem、全局天气根。需要例外时必须显示逐对象清单并由作者确认。

提交前预检：目标区域重叠、对象占格同通道冲突、越界、跨区引用、未归属对象、锁定美术层和 GridArt 尺寸。预检失败不产生 Undo 或脏标记。

一次提交使用一个 Undo 组：

1. 平移区域格。
2. 平移逻辑组根对象和被收归的附属对象。
3. 更新每个 `LevelEditorPlacedBlock.Cell`，保留作者 ID 与作者局部格。
4. 保持/修正 `RoomResetObject.regionId`，然后 `RebuildOwnership()`。
5. 平移已认领的美术格；自动墙地层按区域/墙体来源重新生成。
6. 保留所有 Unity 引用、实例参数、平台相对路线和拾取身份。

跨选中/未选中区域的引用不会失效，但必须在预览中以警告线显示，并提供“连同关联区域选择”入口；不得擅自扩大移动范围。

## 对象索引与占格通道

`SceneObjectIndex` 按 Scene 建立只读内存索引，不在每帧全场景扫描。它把一个作者 ID 映射为一个逻辑组，并把每个 SceneCell 映射为多个候选。

至少区分：

- `TerrainSolid`：墙、平台、实体地形。
- `WaterVolume`：水域、水流、气泡柱。
- `GameplayProp`：钥匙、按钮、门、曲柄、植物、可搬运物、收集品。
- `MarkerTrigger`：出生点、字幕、Timeline/辅助标记。
- `DecorationObject`：非 GridArt 的场景装饰/天气辅助。

同格是否允许由通道矩阵和真实覆盖决定，不用单一 `FindAt`。擦除默认只影响当前通道；同格有多个候选时显示命中列表或允许循环选择。删除作者 ID 时删除整个逻辑组，禁止只删藤蔓链或其他多实例组的一部分。

索引在显式事务完成、Hierarchy 改变、Undo/Redo 或 Scene 切换后增量失效；刷新是只读操作。

## 放置、移动与删除

放置：

- 通过 `PrefabUtility.InstantiatePrefab` 直接生成最终实例。
- 模板默认参数只在实例化时应用一次，之后实际组件字段是权威。
- 使用 Undo 添加/配置 `LevelEditorPlacedBlock`，分配全 Scene 唯一作者 ID；多部件共享 ID 和稳定局部格。
- 按目标格查找区域，显式设置父容器和 `RoomResetObject.regionId`。区域外放置必须由支持未归属的栏目显式允许。
- 为 SurfaceTile、重生点、钥匙、收集品等调用既有作者初始化 API；不得在窗口打开时补做。

移动：

- 自定义手柄和 Unity Move Tool 都必须走同一事务或同步器，吸附后同时写 Transform 与 Cell。
- 普通移动保留作者 ID/局部格；跨区域默认拒绝，并提供“移动并重新归属”显式动作。
- 合并形状按实际覆盖移动；组内多个实例作为整体选择。
- Z、旋转、缩放只有栏目声明允许时可改，2D 网格移动不覆盖作者深度。

删除：

- 先显示目标逻辑组、占格、区域和反向引用。
- 有门/机关/平台引用时默认拒绝，作者可选择“解除引用并删除”；解绑和删除同一 Undo 组。
- 不删除 Prefab、Palette 条目、美术或区域，除非当前动作明确包含它们。

## 参数与关系

实例参数面板使用 `InspectorElement` 或现有 UI Toolkit 自定义 Inspector，直接编辑所选真实组件。不要继续生成第二份 `componentOverrides` 数据模型。

绑定工具采用“选择消费者 → 选择来源/目标 → 预览 → 提交”：

- 门/按钮同时维护 `DoorController.SetButtons` 与 `DoorButton.SetOwner`。
- 平台信号使用 `BindSignalSources`；曲柄使用 `BindCrank`；基座使用既有消费者绑定方向。
- 附着物使用 `PlatformMount.Bind`。
- 引用双方都先 `Undo.RecordObject`，提交后运行关系一致性检查。
- Scene 中用 Handles 画关系线；线只是编辑器覆盖，不生成 GameObject。

## GridArt 场景直绘

保留 GridArt 是性能选择，不保留旧画布工作流。

```text
SceneView 笔画
→ Undo 记录 GridArtMap 与稀疏归属资产
→ 修改当前图层格
→ revision 增加一次
→ 只重建内容哈希改变的 32×32 网格块
→ SceneView 立即显示
```

要求：

- 打开 Overlay、美术模式或切换图层不排队 Apply。
- 构建前比较 Map revision、Palette hash、图层/块内容 hash；相同则完全不写。
- 只有输出层级、绑定或引用真正变化时才标脏 Scene；只改 Mesh/Material 资产时只标脏对应资产。
- 一个笔画一个 Undo；Undo/Redo 只重建受影响块。
- Gameplay 绘制与美术绘制互斥，导航和 Alt 操作不落笔。
- 自动墙地层继续由真实 Scene 墙体/区域读取；普通装饰层手绘。

美术归属用稀疏伴随资产，不给现有每个 `GridArtCell` 增加字符串字段：

- 新笔画可选择“跟随当前区域”，只记录锚点归属。
- 旧格默认无归属且不动。
- “认领选中区域内美术”先显示将被认领、冲突、跨边界和大笔刷清单，确认后才写。
- 背景、视差、天气、全局前景默认不认领。
- 擦除或移动美术同步维护稀疏归属；孤儿归属记录在同一事务清理。

## 保存语义

没有存档槽、共享存档或“应用到场景”。

- Scene 修改遵循 Unity Scene dirty/保存。
- GridArt Map 与归属资产显示独立脏状态；用户显式保存 Scene/项目或点“保存场景与美术”时保存。
- 自动预览可以重建内存/派生网格，但不得自动保存作者资产。
- 外部文件变更保护继续保留；发现磁盘签名变化时停止写入并提示重新载入，不生成新的作者分叉存档。
