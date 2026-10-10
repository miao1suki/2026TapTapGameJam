# 世界图与装配编辑接入

模块：`Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor`，仅 Editor。
本模块不控制运行时房间切换、颜色重置或加载规则。

## 数据所有权

- `PlanningRoom.cells` 是世界占格的唯一来源，房间和独立通道互不重叠，各自保持四邻域连通。
- `isConnector` 区分房间与通道；新通道必须设置 `independentCells = true`。无需两个端点，允许未连接和多端连接。
- `connectedRoomIds`、通道 `name`、兼容字段 `fromRoomId/toRoomId` 是 `RefreshConnectorPaths` 推导的数据，不用于限制编辑。多端信息读取列表，不只读两个兼容字段。
- 连接由通道占格与房间占格四邻相接判断，不以显示包围盒、距离小于三格或物体碰撞判断。接触不是运行时可通行性证明。
- `PlanningRegion` 是纯预览矩形：稳定 ID、范围、颜色、注释；随规划 JSON 和自定义历史持久化，但不传入 SceneBuilder、不生成模型或运行时组件。

## 坐标

`strideX = worldBlockCellWidth`，`strideY = worldBlockCellHeight`。房间和通道统一以占格包围盒的左上格乘步长作为装配原点；局部物体矩形加原点得到装配矩形。
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

世界图右侧创建与编辑，左侧只列出房间、通道和区域。装配左侧定位必须使用装配坐标，不复用世界坐标；模式切换不可让世界 Pan 状态拦截详情/装配工具。
`PlanningCanvas.World.cs` 管世界交互，`PlanningEditorWindow.World.cs` 管列表/区域配置/场景删除，`PlanningWorldUtility.cs` 管数据运算。绘制回调不能增加/删除 UI 元素；注释标签在调度回调更新。
旧通道迁移仅一次，去除重叠世界占格并重锚已有物体，不重建默认通道线。旧端口字段保留作存档兼容，UI 不再展示无效的线宽配置。固定步长可能改变旧地图的装配间距，升级先另存并检查预览，不自动改写已保存场景。

## 验证

玩家贴纸仅在 `LevelEditorTool.Player` 下拾取鼠标。`PlanningPlayerDragGesture` 使用按下时的玩家全局格坐标与画布像素位移，不混用房间局部原点；格→Scene 统一乘 `CellSize`，Scene→格除 `CellSize`。三像素拖动阈值之前不写 Transform，边缘钉住贴纸不修改真实位置。写入必须同时满足同一玩家、同一 pointer、持有捕获、左键保持和非 Play Mode。`CancelPointerInteraction` 用于窗口失焦/关闭、场景或视图切换；捕获释放前先清空手势，避免 capture-out 回调重入。已发生的拖动结束只通知一次规划历史。

- Runtime 未变；重新编译 Editor 全量源码及新增 partial 文件。
- 独立轻量数学/数据桩运行实际 Utility/Layout 源码：23 项连接、命名、移动原子性、负坐标归属、重锚、删除与兼容迁移断言。
- Unity 菜单：`Tools/2026TapTap/关卡编辑器/验证世界图与装配编辑`；隔离内存数据，不生成 Scene。
- 交互验收：视图定位、世界/装配放置擦除、多选/移动、区域配色与文本、删除后的自定义撤回、自动同步反馈循环；关闭重开及脚本重编译。

本轮静态编译与独立数据断言通过；Unity 菜单和实际窗口交互尚未执行，不把静态验证当作界面已验收。
