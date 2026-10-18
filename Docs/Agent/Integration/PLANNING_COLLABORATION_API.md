# 规划地图协作接口

仅 Editor，源码在 `PlanningEditorPrototype/Editor`，沿用现有 `PlanningDocument` JSON、1:1 单元格和稳定 ID，不引入新运行时房间系统。使用说明见 [LEVEL_EDITOR_COLLABORATION.md](../../Developer/LEVEL_EDITOR_COLLABORATION.md)。

## 方块与道具栏目显示

共享的 LevelEditorPanel 在独立放置器和规划窗口中按颜色分组，只改变 Editor 展示，不修改栏目 ID、顺序、预制体或已放置实例。GetPropColorGroup 优先读取 ColorObject.BaseColorTypeId、ColorKeyPickup.ColorTypeId 及匹配组件路径/类型/序号的默认字段覆盖；无颜色组件时回退到预制体根节点 red/blue/green Layer。多色及未知颜色进入其他有色，未归色项进入无色。重复默认条目仍只隐藏显示副本，选择回调使用原始下标；参数模式继续走 EditPlanningDefaults。紧凑卡片不随剩余行宽拉伸，操作条与预览分开，分组折叠状态按项目写入 SessionState。

方块栏目同样使用紧凑卡片与选中项操作条；GetBlockColorGroup 仅读取 LevelEditorBlockEntry.Color，HSV 低饱和、近黑及透明归无色，其余按色相进入红蓝绿或其他有色组。分类不代表运行时颜色归属，也不读取图片像素或迁移颜色组件。折叠状态使用独立的 LevelEditor.BlockGroup 项目会话键，保留原下标与数据；与道具共用 ConfigureCompactAddCard 样式。方块列表 GeometryChangedEvent 以实际内容高度更新 ScrollView 高度（独立模式上限280、嵌入模式420），分组折叠/换行后道具栏目随内容收紧，超出上限仍滚动。

## 数据与事务

道具组件参数窗口 `LevelEditorPropComponentEditorWindow` 对Rigidbody显式绘制六轴约束勾选；Unity原生 `m_Constraints` 不在 `NextVisible` 枚举中，不能仅靠通用属性遍历展示。六个勾选只按位更新该字段，并复用 `CaptureOverride` / `LevelEditorComponentOverrideUtility` 的持久化、应用与重置路径，保存的是约束位掩码，不是六个独立字段。不要把预览对象或临时instanceID写回资产；源prefab保持不变。

- `PlanningFragmentUtility.Extract(source, owners, area, out detached)` 返回深拷贝；owners 为 null 表示不限所有者，area 为 null 表示不限矩形。裁剪后重锚局部坐标和 mergeParts，切过完整物体拒绝，不改源数据。
- `Import(target, source, anchor, includePlayer, out detached)` 返回合并后的新文档，失败不写目标。anchor 是导入源房间占格包围盒左上格。内部房间/物体/合并原始片/门钥匙锁/区域 ID 全量重映射，门按钮、基座、曲柄绑定按新 ID 重连。跨片段绑定解除，孤立按钮移除；不能把绑定随意指向目标地图同名或旧 ID。
- `ValidatePlacement` 为轻量占格预检，UI 不在每次鼠标绘制时重复序列化完整地图。
- `ValidateDependencies` 在选择文件/确认时调用，递归检查合并快照、栏目和资源 GlobalObjectId；拒绝缺失资源与场景对象引用。`Import` 深拷贝并规范化后再验位置，旧比例不能在迁移前通过碰撞预检；按 ID 优先、同名回退规范化复制对象的栏目 ID，避免名称预检通过但 SceneBuilder 因旧 ID 丢物体。同层全局装配物体另做精确占格检查，水/前景允许叠放。
- UI 替换 document 后调用 `OnCanvasDocumentChanged`，使用现有快照撤回/重做与同步队列。不得添加 Unity Undo 物体快照路径。
- 保留 prefab/栏目 ID、持久化参数和资产 GlobalObjectId；临时 instanceID 不作为引用。JSON 不打包缺失资源，协作者必须提交同一栏目和 prefab 依赖。

## 场景与共享文件

`PlanningSaveStore.SaveFolder` 为项目 `Assets/_Project/Content/LevelMaps`；`LegacySaveFolder` 只作兼容读取。写入先到 `.writing` 临时文件，成功后替换，不在重复保存时重写相同内容；Unity 元数据不重建。旧粗网格文件沿用首次覆盖前备份。

`PlanningSharedSaveUtility.SceneSavePath` 返回 Scene 同名 `.planning.json`；SceneBuilder 应用后与 `PlanningSceneSaveProcessor` 的 `sceneSaved` 回调自动导出（覆盖首次保存与另存场景）。编辑器优先采用发生变化的共享副本，避免本机 EditorPrefs 遮住队友更新。保存 Scene 不应拿旧生成几何覆盖已有较新的规划存档。更名存档展示名不改变 Scene 配对文件名。

`Ops/LevelMaps/Test-PlanningMapCommit.ps1` 检查暂存的规划场景是否有有效配对 JSON、meta 和漏暂存修改。新克隆启用 `.githooks`；已有 hook 组合调用，不覆盖。它检查交付完整性，不尝试自动修复布局或擅自暂存文件。

窗口读入/成功保存后调用 `ObserveSceneCopy` 记录共享文件哈希；`VerifySceneCopy` 在覆盖或生成前检查外部更新，冲突时将本地数据备份到 Library/PlanningConflicts 并拒绝覆盖。禁止绕过此检查从失效的窗口写回。未保存场景的应用文档按 scene handle 暂存，首次保存后导出，关闭场景清理暂存。

自动同步的 `BindingMemory` 记录 `(PlanningBoxId, Cell)` 和采集时持久化文档的 `documentHash`。只有同一文档版本的会话记录才可用于推断关窗期间的手动删除；重导入/外部更新/旧版无哈希缓存一律重新建基线，不能把坐标变化或合并重新分块当作删除。保存记录时使用采集版本，而非尚未应用的新草稿。开窗期间发现 Unity 对象引用失效，先按稳定 ID+格重新查找当前生成实例，排除单纯的实例重建后才删除规划项；无失效引用时不做额外全场景扫描。自动同步开关保持原值，真实删除仍可同步并使用规划撤回恢复。

## 移动与水层

`PlanningCanvas.Interaction` 在画布拥有拖拽指针捕获期间使用 UI Toolkit 定时器做边缘平移；越界指针按最近边界限速，不能在 PointerLeave 时终止仍被捕获的手势。平移量同时补偿 dragStart/movingBoxStart，保留世界起点；仅更新视图和拖拽预览，完成时仍走原单次历史事务。释放、取消、失焦/丢捕获和卸载窗口均停止，未拖拽的普通悬停不会滚动。

`TryMove` 保留房间物体/出生点局部位置，并移动所选所有者的全局装配补丁。跨选区的完整补丁拒绝移动；`Reanchor` 递归同步 mergeParts。数据移动失败必须保持原图。

`PlanningWaterLayerUtility.IsWater` 识别旧 Water 类型及带 `WaterVisualFeature` 的道具（含水流、气泡柱）。水可与所有非岩浆物体同格；水/岩浆互斥在绘制、替换、移动、片段导入与场景应用前统一检查，按不规则形状而非包围矩形判定，拒绝时不先改动场景。房间水与全局装配补丁同样适用。

普通水、水流和气泡柱允许彼此叠放，SceneBuilder 依实际 `WaterVisualFeature` 子类分别去重占格；同类型同格不重复生成，不同类型保留各自功能。共同水底色只绘制一次，避免透明度叠深；运行时及 Scene 美术预览同样去重，不影响各水类型原有施力。增量匹配必须使用 `(PlanningBoxId, Cell)`，不能只以 Cell 匹配，同格水和物品属于不同实例。不得为此改变颜色 Gameplay 的真实材质/HSV/解锁路径。

预览独立排序水→物品，选取/擦除顺序相反。“仅水层”筛选同时覆盖参数、移动、合并、绘制与装配擦除；前景层 opacity=0.2，玩家覆盖层不接受点击。窗口重建必须恢复实际开关值，不仅更新 Toggle 外观。

## 不规则合并与覆盖

- `PlanningShapeUtility` 从既有 `mergeParts` 快照恢复实际覆盖并缓存为压缩 RectInt；`Contains/Overlaps/Cells/Rectangles` 是编辑命中、生成占格和美术铺绘的依据，不再把包围矩形当实际形状。缓存随文档修改失效，平移不重复解析 JSON。
- 不新增 PlanningBox 存档字段或迁移版本；没有快照的旧合并继续视为完整矩形。移动/解除合并沿用原始片段重锚，机关引用仍只使用稳定 PlanningBoxId。
- SceneBuilder 以首个实际格作为生成锚点，元数据仍保存完整包围范围；不能要求左上角有实体。增量签名继续含 mergeParts，未修改对象保留实例。
- `LevelEditorMergedShape` 保留一个根对象/玩法状态，生成一个形状网格和压缩区域的同根 BoxCollider。不要为每格添加功能脚本。`LevelEditorPlacedBlock.ContainsCell`、运行时水体、平台承载/阻挡、岩浆替换与美术工作台都须尊重实际覆盖。
- 实例参数、复制存档、片段交换及场景反向导入应保留 mergeParts，不能把孔洞拍平成 width×height。片段边界切过完整物体仍要求扩大选区或解除合并。
- `WaterLayerShapeAudit.Run` 只允许隔离工程，覆盖同格物体、孔洞、存档/拆合/增量、实际物理接触、墙遮挡与点击优先、冷却替换及房间重置。主项目不执行该场景生成入口。

## 验证

隔离项目执行 `PlanningEditorPrototype.PlanningCollaborationVerification.Run`。入口限制项目路径含 `FlowRetirementProbe`，测试会生成独立 Scene，不在主工作区执行。覆盖深拷贝、重复导入、ID/绑定、边界与冲突拒绝、合并原始片、整房间移动、磁盘原子保存、真实 prefab 的水/物品同格生成与增量保留、分别擦除、Scene 配对保存。交互窗口人工拖拽和多人真实 Git 往返仍需验收。

补充专项 `FragmentSafetyAudit.Run` 检查旧比例冲突、栏目 ID 迁移、缺失/场景引用拒绝、深层快照绑定、非矩形孔洞和跨所有者装配重叠；`EditorInteractionAudit.Run` 使用附着的真实 UI Toolkit 窗口测试拖放、捕获丢失、四向边缘平移、视图高亮及明确的导入/区域导出入口。均仅在隔离工程执行。

`PlanningEdgePanAudit.Run` 使用真实附着窗口的定时回调验证静止边缘持续平移、四向跨界、三视图拖拽、绘制/区域框选、世界起点补偿、单次事务以及释放/取消/丢捕获停止；不以直接调用 TickEdgePan 代替持续滚动验证。仅在隔离工程执行。

`PlanningAutoSyncAudit.Run` 依赖隔离工程内完整 MRoom2 第二版；可用 `Library/PlanningAutoSyncPrevious.json`（第一版仅作为过期会话夹具）重现现场，无此本机文件则生成合成旧版本。使用附着的真实窗口和 Editor 更新循环覆盖旧缓存开窗、重复应用/重开、无版本缓存、外部生成实例替换，以及开窗/关窗期间真正删除与撤回；结束时还原夹具 Scene/规划文件，不在主工程执行。
