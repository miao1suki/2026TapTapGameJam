# 实施阶段与放行门槛

改版必须纵向分阶段，不能在一次大改中同时替换画布、身份、房间、美术和正式 Scene。每阶段都先更新 `ACTIVE_WORK.md` 的范围，并在隔离工程或一次性测试 Scene 验证。

## 阶段 0：保护基线

只读完成：

1. 记录正式 Scene、两份旧 JSON、GridArt Map 和关键 Palette/Prefab 哈希。
2. 统计区域、对象、作者 ID、逻辑组、合并形状、RoomResetObject、美术层/格/块。
3. 导出已知门、按钮、平台、曲柄、附着物、钥匙、收集品和房间归属关系清单。
4. 建立“打开/关闭旧窗口、进入/退出旧编辑模式、打开 GridArt”造成哪些 dirty/文件变化的复现测试。

放行：正式资源哈希未变，风险清单和测试基线可重复。

## 阶段 1：零写入外壳

实现只读 `SceneLevelEditorOverlay`、`SceneEditSession`、`SceneGridSpace` 和只读对象/区域摘要。

同时修复：

- 从 `LevelEditorWindow.CreateGUI`、`SetEditMode` 移除自动 `MigratePlacedBlocks()`。
- GridArt 绑定时不再因打开窗口设置 `buildPending`。
- GridArt Apply 在构建签名相同时零写入。
- 视角会话精确恢复原正交/旋转/轴心/尺寸。

迁移命令可保留为高级菜单，但必须先扫描、预览变更数量、显式确认，并提供 Undo；默认永不执行。

放行：打开、关闭、切模式、切 Scene、脚本重编译、进入/退出编辑状态后，Scene/资产 dirty、Undo 和磁盘哈希均不变。

## 阶段 2：房间与通道

实现 `SceneRegionService`：

- 从 `RoomLifecycleManager.Regions` 只读建索引。
- 新增只改区域和 cellSize、不写 planning JSON 的明确作者 API。
- 房间/通道新建、单格/连续/矩形绘制、擦除、改名、选择。
- 重叠、连通性、旧分离区域兼容和拆分预览。
- 通道相邻房间实时显示。

此阶段不生成、删除或移动玩法物体。

放行：区域操作一个手势一个 Undo；保存重开后区域一致；`FindRegion`、`ResetRegion`、通道识别和旧区域均通过；无作者动作时零写入。

## 阶段 3：对象索引与基本编辑

实现多候选 `SceneObjectIndex` 和集中事务：

- 按作者 ID 聚合逻辑组，支持重复 ID 的多实例。
- Terrain/Water/Prop/Marker/Decoration 占格通道。
- 真实 Prefab 放置、稳定 ID、Cell、房间父容器和 RoomResetObject 归属。
- 可搜索的 Palette 方块/道具资源库、模板资产/参数摘要、新实例稳定栏目来源，以及旧实例零写入来源扫描和显式认领。
- 当前通道擦除、命中列表、整组删除。
- 网格移动与跨区显式重绑。
- 合并形状与多格覆盖索引。

先在测试 Scene 覆盖墙水道具同格、门 1×2、多格平台、藤蔓多实例和合并白块。

放行：无任意对象删除；创建/移动/删除均可一步撤销；Prefab 资产不变；保存重开后 Transform、Cell、作者 ID、栏目来源和区域归属一致。旧实例批量认领只处理唯一匹配，歧义项保持零写入。

## 阶段 4：参数、身份与引用

实现真实 Inspector/绑定工具：

- 编辑 Prefab instance 的实际组件字段，不再创建 Planning component override。
- 门/按钮、平台/基座/按钮/曲柄、PlatformMount 等已知关系适配器。
- 反向引用预检与安全解绑。
- 为钥匙和收集品验证稳定身份；移动和整体区域平移后身份不变。
- 标准玩家只读取唯一 `Player.prefab`，工具不复制玩家。

放行：实例参数重开不丢、Prefab 默认不变；所有双向关系一致；相关玩法专项和房间重置通过。

## 阶段 5：区域整体移动

实现同 Scene 的区域装配：

- 单区/多区选择与格增量预览。
- 收集区域格、逻辑组、RoomResetObject、容器对象和显式附属物。
- 冲突、跨区引用、受保护根和未归属对象预检。
- 一个 Undo 组提交全部 Scene 数据。

此阶段先不移动旧 GridArt；若选区有未归属美术，UI 明确显示“美术保持原位”。

放行：区域来回移动与 Undo/Redo 后序列化等价；机关引用、平台相对路线、门按钮、拾取 ID 和房间重置均保持。

## 阶段 6：美术场景直绘与跟随

把现有 GridArt ScenePainter 接入同一 Overlay：

- 不再要求打开独立画布。
- 图层/笔刷/旋转/矩形/擦除在 SceneView 可用。
- 构建器按受影响块提交并跳过相同内容。
- 新增稀疏 `GridArtRegionOwnership` 伴随资产。
- 支持新笔画自动归属、旧美术显式预览认领和区域移动。
- 自动墙地层从真实 Scene 墙体/区域刷新；手工层不被覆盖。

不要给全部旧 `GridArtCell` 增加 `ownerRegionId` 后一次性重写 1.38 万条 YAML。

放行：开美术模式零写入；一笔只改预期 Map/归属/块；Undo/Redo 视觉和数据一致；未认领旧美术哈希不变；区域移动只带走已认领美术。

## 阶段 7：正式 Scene 试运行

此前阶段全通过后，才允许对正式 Gaming 启用新工具：

1. 首次进入只读扫描，显示区域、对象、缺失身份、归属歧义、关系问题和美术归属统计。
2. 扫描本身不得修复、补组件或标脏。
3. 先执行一个可逆的小区域操作，保存副本并与基线逐项对比。
4. 通过后才开放正式作者动作；仍不从 JSON 应用或全量重建。

放行：正式 Scene 现有对象、参数、引用、房间和未编辑美术全部保留；Play Mode 核心专项通过。

## 阶段 8：退役规划画布与 JSON

这是产品切换，不是清理顺手做：

- 删除/隐藏 PlanningCanvas、存档槽、片段协作、共享 JSON 和“应用到场景”的正常菜单入口。
- Runtime 和正式 Editor 流程不再读取 PlanningDocument/Planning JSON。
- 旧代码可先移入明确 Legacy 命名空间供历史回读，但不能自动执行；确认无调用后再删除。
- 把场景回采、参数覆盖、关系 ID 等仍有价值的逻辑迁到 Scene 服务或测试，不继续维护双写。
- 更新根 `AGENTS.md`、`WORKING_AGREEMENTS.md`、`FORMAL_LEVEL_DELIVERY.md`、Level Editor/Planning/GridArt 文档、`SYSTEM_INDEX.md`、提交 hook 与 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`。
- 新交付规则改为 Scene、Scene `.meta`、GridArt Map/归属资产、Palette/Prefab/美术依赖和生成资源成套提交。

删除历史 JSON 是破坏性动作。先确认新 Scene 已完整覆盖、仓库有可追溯历史并得到用户明确授权；未授权时可停止生成并将其标记为只读旧基线，但不能让它继续充当第二权威。

放行：新克隆只凭 Scene 与引用资产即可继续编辑和运行；旧 JSON 缺失不会影响工具、运行时、构建或提交检查。

## 阶段 9：性能与文档收口

- 真实 3000+ 对象、约 1.38 万美术格下采样 SceneView CPU、GC、笔画延迟、区域移动和 Undo。
- 对象索引不逐帧扫描；GridArt 不全量重建相同块；远距离可见性工具继续只影响 SceneView。
- 策划文档只描述场景直绘工作流；Agent 接口文档描述所有权、事务、扩展对象通道/绑定适配器的方法。
- 旧术语“装配图/详情画布/应用/共享存档”从当前功能说明移除或明确标为 Legacy。
