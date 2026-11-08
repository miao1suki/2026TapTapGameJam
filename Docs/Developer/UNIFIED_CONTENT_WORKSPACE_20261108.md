# 统一内容工作台：第一阶段

本轮从已快进合入藤蔓/结局修复的 `codex/2d-long-term-migration` / `69a8a4b` 建立 `codex/2d-editor-art-workflow`。此交付是编辑器改造第一阶段，不是正式关卡的 Tilemap 迁移。

## 策划与美术操作

菜单 `Tools/2026TapTap/关卡编辑器/场景编辑器` 或快捷键 M 打开可停靠“关卡工作台”。左侧一个库同时搜索方块、机关道具和当前场景引用 Map 的美术笔刷；按类型筛选，双击内容或点击“开始在场景中绘制”。SceneView 锁定到正交 2D，使用已有绘制事务。美术选择目标图层；结束绘制恢复视图状态。

房间导航定位当前 Scene 的真实 RoomRegion。场景中选中物体后，点击“编辑选中物体参数”显示真实组件 Inspector。区域形状、机关连接及美术归属仍从顶栏“区域与机关连接”打开已验证的高级面板，不重复实现关系/撤销。

打开、浏览、搜索、筛选、定位和关闭不会迁移数据或自动重刷。绘制/擦除才会经现有服务修改作者数据。方块和美术在同一内容库选择，但本阶段仍是两种数据与独立写入操作：刷美术不会隐式生成碰撞，刷方块不会隐式刷墙图。

## 实现和所有权

`SceneContentCatalog` 只读索引 Palette 方块/道具及 Scene 的 GridArt 笔刷。`SceneContentWorkspace` 使用虚拟化 ListView，选择后分别路由到 `LevelEditorScenePainter` 和 `SceneArtAuthoring`，不新增作者状态副本。工作台占用绘制时旧面板停止绑定输入，避免两套工具争抢。索引由 Scene/Hierarchy/Project/Undo 和事务事件失效触发重建，不在每个重绘中全量扫描；定时器仅检查失效与会话状态。

只读浏览不打开所有自定义 Inspector；实际组件面板在用户显式点击编辑时创建，切换选择后释放。原有对象事务、模板生成、稳定 ID、机关引用和美术 Undo 沿用现有实现。

## 验证及后续阶段

- Unity 6000.3.12f1 在独立工作区编译；一次性空场景验证方块/道具/美术目录覆盖、分类/搜索、窗口创建与关闭不增加 Undo、不改 Scene dirty、Map 与 Palette。
- 正式 `Gaming.unity`、引用 `Gaming_Art.asset` 和 Palette 在验证前后 SHA256 不变。
- 本轮没有重做整个关卡，也没有把旧 GridArt 隐式转换为 Tilemap。
- 下一阶段先在独立测试 Scene 做 Unity 原生 Tilemap/规则瓦片地形与合并碰撞。现有假想姿态查询 `PlanarPoseQuery.Shape.Copy` 尚不支持所有 Tilemap/Composite 形状，必须先完成放置、玩家移动、房间重置、颜色对象和 Undo 的兼容验收，再启用正式地图迁移；不能只加一个“打开 Tile Palette”按钮就宣称已完成地形一体化。
- 人工 Scene 手势、实际重开编辑器、长期使用性能和 Tilemap 路线仍需要后续阶段验收；批处理 UI 检查不代表这些均已通过。

架构选型见 [编辑器资源分析](2D_LEVEL_EDITOR_TOOL_SELECTION_20261108.md) 与 [长期美术流程](LONG_TERM_2D_ART_AND_EDITOR_PIPELINE_20261108.md)。外部制作软件和实际可导入的流程见 [本机工作流](../../Ops/ArtWorkflow/README.md)。
