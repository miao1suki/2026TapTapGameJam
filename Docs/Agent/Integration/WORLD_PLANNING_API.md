# 世界图与装配编辑接入

区域生命周期：增量/全量应用均写入 RoomLifecycleManager、稳定区域 ID 及 RoomResetObject；详见 [ROOM_LIFECYCLE_API.md](ROOM_LIFECYCLE_API.md)。MVMap v1 布局可导入为新存档，保留来源 ID/颜色/备注和不连通片，不转换灰盒或锁规则。已应用规划 JSON 嵌入 Scene 用于首次回读。

独立 MVMap 物体试导入不改变上述布局模式；使用明确转换接口及配套规划存档，见 [MVMAP_OBJECT_IMPORT_API.md](MVMAP_OBJECT_IMPORT_API.md)。外部物体块不当作运行时房间。

模块：`Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor`，仅 Editor。
本模块不控制运行时房间切换、颜色重置或加载规则。

## 数据所有权

- `PlanningRoom.cells` 是世界占格的唯一来源，房间和独立通道互不重叠，各自保持四邻域连通。
- `isConnector` 区分房间与通道；新通道必须设置 `independentCells = true`。无需两个端点，允许未连接和多端连接。
- `connectedRoomIds`、通道 `name`、兼容字段 `fromRoomId/toRoomId` 是 `RefreshConnectorPaths` 推导的数据，不用于限制编辑。多端信息读取列表，不只读两个兼容字段。
- 连接由通道占格与房间占格四邻相接判断，不以显示包围盒、距离小于三格或物体碰撞判断。接触不是运行时可通行性证明。
- `PlanningRegion` 是纯预览矩形：稳定 ID、范围、颜色、注释；随规划 JSON 和自定义历史持久化，但不传入 SceneBuilder、不生成模型或运行时组件。

## 坐标

世界、房间、装配统一以房间图的单元格为 1 格；规范化后 `strideX = worldBlockCellWidth = 1`、`strideY = worldBlockCellHeight = 1`。旧步长字段仅用于存档兼容，不再提供修改比例的 UI。房间和通道以世界占格包围盒左上格作为装配原点；局部物体矩形加原点得到装配矩形。
归属按装配单元格除步长后向下取整查询世界格，负坐标同样生效；不能把带洞房间的包围盒全部视为可放置范围。
`PlanningWorldUtility.TryAssemblyOwner`、`PlanningLayoutUtility.GetAssemblyBoxRect` 是共用查询入口。独立通道的 `GetConnectorAssemblyPath` 仅返回兼容生成锚点，不生成隐式地板。

新增左侧/上侧占格时调用 `Reanchor`，平移局部物体坐标以保留原内容的装配位置；移动整个房间/通道则只移动世界占格和所属世界标记，局部物体布局不变。
`TryMove` 先验证所有选中对象的目的占格，再整笔更新；重叠拒绝时任何对象都不能先行移动。

## 删除、历史与场景

`DeleteOwners` 仅删除指定 ID 的房间/通道及所属规划物体、门/钥匙标记和引用，不级联删除相邻通道；其连接名称重新推导。
画布的 `WorldOwnersRemoving` / `WorldItemsRemoved` 交给 Window 处理场景清理。只有当前活动 Scene、规划生成根节点下、匹配来源 ID 的物体会被清理；清理后移除空的所属容器，未标记的手工物体保留。
整体删除即使未开启自动同步也清理所属场景实例；Scene 只标脏，不自动保存。改动通过规划历史记录，已应用场景在撤回时增量恢复；不要将这些操作重新放进 Unity 全局 Undo。
擦除部分世界格按整笔剩余连通性判断，最后一格可以删除；合并物或门被擦到时仍按整体删除语义处理。门按钮的位置额外检查所属占格，不能落在区域包围盒的空洞中。

Scene 子容器采用 `显示名 [稳定 ID]`，自动改名后按 ID 匹配已有容器；旧容器按物体来源 ID 或原名称兼容定位，避免把名称作为所有权键。场景读取支持房间和独立通道。

## UI 与兼容

右侧新增“平台形状 · 横放 / 竖放 / 单格”：选择移动平台或曲柄升降平台栏目后，设置长边朝向和长度再绘制，默认横向3格，支持1/3/5快捷及自填1～256格。此处控制平台本体占格，不是移动路线。选择既有平台后可明确点击“将上方形状用于选中平台”；先验证所属房间实际占格及实体重叠，失败整笔拒绝，成功保留稳定ID、位置、路线参数与基座/按钮/曲柄绑定。改变画笔设置不会自动修改旧平台，也不会扩大全部旧平台。

`PlanningCanvas.PlatformShape.cs` 管新放与明确改形状，`PlanningEditorWindow.PlatformShape.cs` 管入口；占格继续使用既有 `PlanningBox.width/height/singleInstance`，朝向通过既有组件覆盖保存 `MovingPlatformFeature.bodyOrientation`。`Auto=0` 是旧关卡默认，按原占格推导；`Horizontal=1/Vertical=2` 用于新明确朝向，令1×1也可保留横竖外观。仅显式竖平台生成时旋转根BoxCollider的薄条几何，不旋转物体根或移动路线。旧Auto实例和原逻辑参数不被转换。单向平台的通行方向不在此入口扩展。

外观朝向字段保留序列化但不单独显示在组件参数中；必须由“平台形状”一起写入占格、外观与碰撞。普通参数窗口的恢复默认不清除既有形状朝向。单格平台支持用“合并”拼为连续横排或竖排，含 `singleInstance` 平台；合并须同素材、同房间、同控制源及绑定格偏移、同按钮顺序、同有效路线/移动/演出参数，且本体朝向相容、无重叠和空格。不同配置整笔拒绝，不把后一个平台的参数覆盖成前一个。合并生成一个运动体并保留原记录供拆分，门与所属按钮仍不可合并。新放、改形状及曲柄旁自动找空格使用当前画布的素材栏识别水，允许水叠底，不依赖另一个窗口的全局素材栏。

平台48×16图按16有效美术像素/格使用：1格截中间16像素，3格用完整帧，5格用3+2格接片；竖平台先旋转90°，不可把48像素缩成16像素。运行时、Scene代理与规划预览共用原比例裁片规则。隔离静音审计入口 `PlatformShapePlacementAudit.Run` 检查两类平台的横/竖×1/3/5格，从规划放置、JSON、真实碰撞到Scene与画布预览，并检查2/3个单格实际合并为一个运动体、冲突参数拒绝、跨素材栏水共格及真实UI入口；不能替代窗口真实鼠标操作验收。

世界图右侧创建与编辑，左侧只列出房间、通道和区域。装配左侧定位必须使用装配坐标，不复用世界坐标；模式切换不可让世界 Pan 状态拦截详情/装配工具。
`PlanningCanvas.World.cs` 管世界交互，`PlanningEditorWindow.World.cs` 管列表/区域配置/场景删除，`PlanningWorldUtility.cs` 管数据运算。绘制回调不能增加/删除 UI 元素；注释标签在调度回调更新。
`FromPersistentJson`/`Normalize` 按旧步长展开房间/通道占格和区域规划、换算世界门/钥匙标记，再固定步长为 1。先在旧步长下完成旧通道兼容处理，再展开；物体局部矩形、合并原始片段、资产引用、出生点局部坐标和装配补丁不缩放，房间/物体 ID 保留。再次规范化不重复展开。未保存的新文档默认 1；没有步长字段的历史 JSON 按旧默认 16 处理。读取只转换内存副本，首次覆盖旧文件生成 `.before-unified-grid.bak`，场景作用域 EditorPrefs 的原 JSON 保留在 `.BeforeUnifiedGrid` 键中。升级先另存并检查，不自动改写 Scene。

`PaintWorldArea` 在松开指针时一次提交矩形创建/扩展，跳过其他所有者占格并验证结果连通；拒绝操作不留半笔数据。新增房间手势不走旧的按下立即创建单格路径。世界缩小时用连续行段绘制，连接检测使用占格哈希，避免细格展开后产生平方级查询。

## 验证

玩家贴纸仅在 `LevelEditorTool.Player` 下拾取鼠标。`PlanningPlayerDragGesture` 使用按下时的玩家全局格坐标与画布像素位移，不混用房间局部原点；格→Scene 统一乘 `CellSize`，Scene→格除 `CellSize`。三像素拖动阈值之前不写 Transform，边缘钉住贴纸不修改真实位置。写入必须同时满足同一玩家、同一 pointer、持有捕获、左键保持和非 Play Mode。`CancelPointerInteraction` 用于窗口失焦/关闭、场景或视图切换；捕获释放前先清空手势，避免 capture-out 回调重入。已发生的拖动结束只通知一次规划历史。

- Runtime 未变；重新编译 Editor 全量源码及新增 partial 文件。
- 独立轻量数学/数据桩运行实际 Utility/Layout 源码：23 项连接、命名、移动原子性、负坐标归属、重锚、删除与兼容迁移断言。
- Unity 菜单：`Tools/2026TapTap/关卡编辑器/验证世界图与装配编辑`；隔离内存数据，不生成 Scene。
- 交互验收：视图定位、世界/装配放置擦除、多选/移动、区域配色与文本、删除后的自定义撤回、自动同步反馈循环；关闭重开及脚本重编译。

统一网格验证入口：`Tools/2026TapTap/关卡编辑器/验证统一网格与旧存档`。Unity 6000.3.12f1 隔离项目验证旧默认地图及本地存档的装配矩形、负坐标、非正方形步长、出生点、重复读取、矩形创建/扩展/擦除事务和旧世界/装配测试。实际窗口拖拽与策划完整场景仍需人工验收；不得把数据断言当作 UI 已验收。
