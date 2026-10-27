# 场景关卡编辑器

入口：`Tools/2026TapTap/关卡编辑器/场景编辑器`，快捷键 `M`；也可从 SceneView 的 Overlays 启用“场景关卡编辑器”。Scene 是关卡唯一权威，不选择 JSON、不使用存档槽、不再点击“应用到场景”。现有 `Gaming.unity` 中的房间、通道、3000+ 真实对象、稳定 ID、参数和 Unity 引用可直接继续编辑。

## 共通规则

- 点击“开始搭建（锁定正交 2D）”后，当前 SceneView 固定为 XY 前视正交；从对象工具开始放置时也会自动进入该视角。对象原有 Z 只作为渲染深度保留。退出、关闭 Overlay、脚本重载或进入 Play Mode 会逐字段恢复原视角。
- 打开、关闭、切模式、导航、选择模板和选择实例均为零写入。只有显式绘制、擦除、移动、改参、绑定或区域/美术归属等作者动作才修改数据。
- 每个笔画或操作是一个 Unity Undo 组。Esc 取消尚未完成的区域/美术笔画。
- 正式 `Gaming` 不会在开窗时迁移、补组件、重建或保存；保存仍遵循 Unity Scene/资产 dirty 语义。
- Boss 复活位置标记不会再因场景打开、层级刷新或保存而自动补建/写回。需要恢复被删除的标记时，在对应重生点 Inspector 点击“定位附属标记”；这是显式可撤销的作者操作。

## 区域

- 直接编辑 `RoomLifecycleManager.Regions`；房间和通道共用真实区域数据，通道由 `corridor` 标记，连接关系由四邻接实时推导。
- 可新建、选择、重命名、切换房间/通道、连续绘制或擦除。区域不能重叠，新格必须四邻接；会造成断裂的擦除默认拒绝。
- “区域整体移动”输入一个或多个稳定 ID 及 Scene XY 格增量。先预检目标区域、同通道对象冲突、受保护根、GridArt 格尺寸和锁定层，再一次提交区域格、完整逻辑组、Transform/Cell、房间重置归属、显式 `SceneRegionAttachment` 和已认领美术。
- 玩家、相机、UI、管理器、全局天气和 GridArt 输出根不会因落在选区内被隐式带走。旧美术默认未归属并保持原位。

## 对象

- Overlay 默认进入“2 物体”，按“第 1 步选择内容、第 2 步在场景操作”排列。顶部蓝色“打开方块与道具列表…”打开独立卡片选择器；菜单 `Tools/2026TapTap/关卡编辑器/选择方块和道具` 也可直接打开。选择器读取原有 `LevelEditorPalette.asset`，用“全部内容/只看方块/只看道具”三个显式按钮筛选，支持名称搜索、结果数量、缩略图和当前选择预览。
- 单击卡片查看，双击卡片或点击底部蓝色“开始在 Scene 中放置”会自动切到对象模式、锁定正交 2D 并返回 SceneView。对象页持续高亮“点选并改参数/连续放置/擦除”中的当前工具；通道、Prefab 路径、新建与模板管理收在高级折叠区，不阻挡日常搭建。
- 栏目直接实例化真实 Prefab。模板参数只在新实例化时应用一次；放到 Scene 后，真实组件字段和 Prefab override 是权威，不会再由 JSON 或 Palette 覆盖已有实例。
- 命中按地形、水体、玩法道具、标记/触发器、装饰五个通道区分。单击选择优先使用鼠标实际点到的可见 Renderer/Collider，并统一提升到作者根；格命中回退和擦除仍只处理当前通道，同通道多个候选必须先明确选择。
- 同一稳定作者 ID 的多实例始终作为完整逻辑组移动或删除，合并形状按真实覆盖（含孔洞）索引。
- 方向按钮或 Unity Move Tool 会吸附同一 XY 网格并同步 `Transform` 与 `LevelEditorPlacedBlock.Cell`，保留 Z、稳定 ID 和作者局部格。跨区域默认回滚；启用“跨区重新归属”才更新 `RoomResetObject` 和兼容父容器。
- 删除前扫描反向 Unity 引用。有引用默认拒绝；“解绑并删除”使用现有门/按钮、平台/曲柄/基座、PlatformMount API，在同一个 Undo 组解除关系并删除整个逻辑组。

### 模板与实例边界

- Palette 只是一组“出生模板”，不是 Scene 对象的数据源。选择模板只改编辑器会话状态；绘制时实例化 Prefab、应用一次默认参数并建立作者 ID/房间归属。
- 对象落入 Scene 后即完全独立：`LevelEditorPlacedBlock` 不保存 Palette 栏目 ID，不扫描或认领旧实例来源，也不会因为模板改名、删除或修改而覆盖现有对象。
- 正交对象模式单击方块/道具时，命中会从 Renderer、Collider 等子物体统一上溯到 `LevelEditorPlacedBlock` 作者根。Hierarchy 选择固定为该根对象，并立即打开“实例参数”窗口，不会跳进易误选的子层级。
- “实例参数”窗口优先显示通道、区域、位置格和占格，并提供“在 Scene 中定位”；作者 ID 和逻辑组数量收在“详细信息”。玩法组件默认展开，Transform、Renderer、Collider、Rigidbody、Animator 等引擎组件默认折叠，仍可按需展开编辑。所有参数修改进入 Unity Undo 和 Prefab override。

## 绑定/参数

- “真实实例参数”既可在绑定/参数模式嵌入查看，也会在对象模式单击时独立弹出；两处都绑定作者根的真实 Unity `InspectorElement`，因此现有中文自定义 Inspector、条件显隐和 Prefab override 继续生效，不生成第二份 component override JSON。
- 门/按钮使用 `DoorController.SetButtons`/`DoorButton.SetOwner`；移动平台、信号源、曲柄、基座及平台附着物使用各自类型安全绑定 API。保存、移动、Undo/Redo 后仍是同一真实 Unity 引用。

## 改版后接口

- 工具入口统一调用 `SceneLevelEditorOverlay.Open()`；正常功能不得再打开 `PlanningEditorWindow` 或 `GridArtWindow`。旧窗口只允许 Legacy 菜单和历史审计使用。
- 房间/通道作者提交使用 `RoomLifecycleManager.ConfigureAuthoredRegions(...)`，只改 Scene 区域与格尺寸并保留隐藏的旧 planning 字段；运行时与新工具不再从该字段恢复布局。
- 对象身份使用 `LevelEditorPlacedBlock.AuthoringId`、`AuthoringLocalCell`，格位置通过 `SetAuthoredCell(...)` 同步；旧 `Planning*` 名称只作为序列化兼容别名，不能作为 JSON 工作流接口。组件不保存 Palette 来源。
- `ScenePlacementPaletteCatalog` 只读展开 Palette 并维护当前画笔，`ScenePlacementPaletteWindow` 提供卡片选择，`SceneObjectInspectorWindow` 负责作者根实例参数；三者在浏览、选择和开窗时不得写 Scene。
- 非格对象跟随区域使用 `SceneRegionAttachment`；GridArt 跟随关系使用 `GridArtRegionOwnership` 与 Scene 上的 `GridArtRegionOwnershipLink`。两者都必须显式创建/认领，工具打开时不得自动补写。
- 区域、对象、引用和美术的写入集中在 `SceneRegionAuthoring`、`SceneObjectAuthoring`、`SceneRegionAssembly`、`SceneArtAuthoring`；外围工具调用这些接口，不直接散落修改 JSON、销毁对象或全量 Apply。

## 美术

- “4 美术”与对象模式一样采用两步工作流：先点击蓝色“打开美术素材卡片…”，再选择图层并在 Scene 中绘制。卡片窗口沿用旧关卡美术工作台的缩略图素材库，支持名称搜索、分类筛选、自动地形分类、结果数量、当前素材尺寸预览和选择高亮。
- 单击美术卡片只切换当前素材，不写 Map；双击卡片或点击“开始在 Scene 中绘制”会自动返回 SceneView、切到美术模式、锁定正交 2D 并启用所选素材。Overlay 持续显示当前素材缩略图、分类和占格尺寸。
- GridArt 仍使用 Scene 引用的稠密 `GridArtMap`，避免把上万美术格转成 GameObject；图层、笔刷、旋转、矩形和擦除均在同一 SceneView 完成。
- 进入美术模式和切换选项不触发 Apply。一次笔画只在真实变化时增加 revision，重复写同格是空操作；提交后使用现有按 32×32 块保留式构建器刷新预览。
- “创建稀疏归属资产”是显式动作。旧格不会自动认领；“认领当前层区域内美术”只添加 `layerId + anchorCell + regionId` 记录，不重写格内容。新笔画可选择跟随当前区域。
- 区域整体移动只带走已认领锚点；未认领旧格、背景、视差、天气和全局前景保持原位。GridArt 与关卡格尺寸不同或图层锁定时拒绝跨系统移动。

## 性能与卡顿边界

- Overlay 只有绑定目标真实变化时才刷新；同一 SceneView/管理器/Map 的周期检查不会触发 `RepaintAll`。
- 区域轮廓按 `RoomLifecycleManager.RegionRevision` 缓存为合并矩形；49017 格正式区域实测合并为 69 个矩形，不再逐格分配和绘制四边。
- 对象命中使用失效驱动的 Scene 索引。正式 3004 对象首次构建实测 `60.932 ms`；10240 次缓存查询 `0.794 ms`、`0 B` GC。放置、删除、移动、Undo/Redo 和层级变化后才失效重建。
- 卡片选择器只处理 Palette 条目，不扫描 3000+ Scene 对象；实例选择直接复用失效驱动索引，不在周期刷新和鼠标 Hover 路径做全场景来源推断。
- GridArt 笔画只重建受影响的 32×32 块；大笔刷和自动地形会精确补入跨界块/邻块。单格增量实测 `6.054 ms`，未命中块 `0` 写入；自动预览不再同步保存 Mesh/Material。
- 以上数字来自 Unity `6000.3.12f1` 隔离副本中的正式规模数据，用于发现性能回退，不是不同电脑的帧耗时保证。

## 交付

提交 Scene/`.meta`、Scene 引用的 GridArt Map/区域归属资产及 `.meta`、新增 Palette/Prefab/材质/贴图/生成资源依赖。旧 `.planning.json` 和命名正式关卡 JSON 只作历史只读基线，不再同步或用于重建。

提交前运行：

```powershell
pwsh -NoProfile -File Ops/LevelMaps/Test-PlanningMapCommit.ps1
```

脚本文件名为兼容保留，现在检查 Scene 与 Unity 资源元数据完整性。
