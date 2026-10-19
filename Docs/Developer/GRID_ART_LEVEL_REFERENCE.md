# 美术工作台：关卡只读参考层

工作台可以直接参照当前关卡中的墙、房间与正式二维道具，不需要先去 Hierarchy 选白块。默认显示参考，透明度为 35%；可关闭或调透明度。参考层在美术笔触下面，不接收点击，不修改场景对象的材质、渲染器、透明度或存档。

看不到墙地时先点“刷新关卡轮廓”，再点顶部“定位关卡”或房间栏“定位房间”。“关卡参考”的开关与透明度控制整套参考；不需要在 Hierarchy 逐个选择黑块或白块。

## 数据与位置

- 优先使用已绑定当前场景的关卡编辑器草稿；其次为该场景配套规划存档，再次为场景 `RoomLifecycleManager` 的内嵌规划。只有完全没有规划时才只读扫描场景中已放置对象。扫描发生在来源刷新，不在拖动/缩放或每帧。
- 房间、墙与道具均保持真实世界 XY。规划向下的 Y 转换为世界向上的 Y。当前选房优先用于初始定位；无选房时按选中场景对象、玩家位置、首个房间依次回退。视图定位/记忆由工作台窗口统一管理，不自动把已有视图反复拉回原点。
- 显示顺序为房间边框、水、墙、道具、仅编辑器可见的触发器。参考中的水不会压到墙或道具上。正常游戏不会因为打开参考而出现触发器贴图。
- 黑块、白块和自定义方块栏目都参与显示与墙地自动铺绘，使用各自方块颜色。分类与场景生成器一致：有道具引用才走道具路径，不能仅靠旧 `Solid` 枚举判断。旧白块保存为 `Platform` 也必须显示；本轮不迁移或重写作者存档。ID 优先、名称其次、最后按旧类型回退。
- 不规则合并按实际覆盖矩形显示并铺绘，孔洞不补满；完全没有规划、只读扫描场景时也保留 `LevelEditorPlacedBlock` 的黑白颜色及合并形状。

## 像素和表现

正式道具复用 `ObjectArtworkPlanningPreview` 的同一份二维素材与缓存，不临时实例化/拍摄三维预制体。遵循 `ART_GRID_CONVENTIONS.md` 与 `SCENE_ART_PIXEL_CONVERSION.md` 的 16 有效美术像素 = 1 世界格。

- 自然尺寸与逻辑占格分离：32px 收集品仍显示 2×2 格，不压缩成逻辑的 1 格。
- 旧的一格平台中心裁取 16px；五格竖平台由旋转 90° 的三格加两格原像素裁片组成，不拉伸。
- 同列相邻藤蔓先取一张连续整柱缓存，再按行切 UV；不让每个刷入格重启一个藤蔓尖端。
- 水先构建世界格集合，同格不同施力水只绘一个公共蓝色底图。水面/水域/水底按上下相邻关系选择；L 形、空洞与房间边界不被强行补矩形。
- 按钮编号/朝向、尖刺朝向与作者实例参数跟随正式预览解析。隐藏的告示牌不出现。前景透明素材保留作者顺序，仅相邻兼容项合批，不按纹理重排重叠物品。
- 没有正式 2D 素材的道具或定位标记（例如万能方块、隐藏方块、传送目标、Start）显示橙色占格轮廓，并在右栏说明名称；不是替它们编造美术，也不把它们算作墙地。已明确设为隐藏的告示牌仍不显示。轮廓仅存在于工作台只读参考，不加到游戏场景或材质中。

## 性能

来源刷新时一次生成只读 quad；墙/水按纹理及 16 世界格空间桶组织，每个 retained Mesh 批次最多 256 quad。前景道具保留绘制顺序并按连续纹理/空间范围合批。每房间只有四条边框，绝不逐格 Painter2D 描线。

平移/缩放只更新父变换、可见批次和透明度；不会重扫 Scene、重建贴图或重新采样藤蔓。关卡参考不是运行时渲染器替身，不进行动画播放或物理模拟。

## API 与验证

`Project.GridArt.Editor` 不依赖 `Assembly-CSharp-Editor`。规划端通过已有 `GridArtLevelSource.Read(Scene)` 委托提供 `GridArtLevelSnapshot`：

- `rooms`：`id`、`label`、`boundsWorld`；`currentRoomId`；完整 `worldBounds` / `hasWorldBounds`。
- `referenceItems`：只读 `GridArtQuad`（世界矩形、纹理、UV、90°旋转）、颜色与显示层。`groups` 仍保留墙地联动原契约。
- `referenceOnlyNames`：缺少正式素材、使用定位轮廓的道具名称集合，供右栏解释，不能作为墙地或生成素材。
- `RecalculateBounds()` 合并房间、墙与物体的自然显示范围，不修改来源数据。

`GridArtLevelReference` 是独立 `VisualElement`：

```csharp
reference.Configure(snapshot); // 只在数据来源变化时
reference.SetView(panPixels, zoomPixelsPerArtCell / mapCellWorldSize, viewport);
reference.SetReferenceVisibility(visible, opacity);
```

`QuadCount`、`BatchCount`、`VisibleBatchCount`、`GeometryRevision` 供回归验证。参考元素放在 retained 美术层之前；`Configure(null)` 用于切场景清理。

隔离工程执行入口：`PlanningEditorPrototype.PlanningArtReferenceAudit.Run`，报告 `Library/PlanningArtReferenceAudit.txt`。覆盖远坐标房间/墙定位、只读保证、无三维实例、非矩形水与去重、连续藤蔓、一格/竖平台裁切、朝向与32px收集品、万格合批/100次导航不重建、开关透明度及透明层顺序。窗口实际挂接/位置记忆另由 `Project.GridArt.Editor.GridArtNavigationAudit.Run` 验证。

黑白块补充回归：`PlanningEditorPrototype.PlanningArtBlockReferenceAudit.Run`，仅允许在隔离工程执行。覆盖真实 Pick/MRoom2/MRoom3/Gaming 来源、旧 Platform 白块、黑块/自定义颜色、旧 ID 名称回退、不规则孔洞、无规划场景读取、无素材轮廓和显式隐藏。使用同一个 `GridArtLevelReference` 在 UI Toolkit 面板中实际 GPU 渲染并读回黑白像素，另输出默认透明度与实际 Pick 来源的图片，不只验证数据计数。报告及图片位于隔离工程 `Library/`。

协作交付仍需同时提交关卡场景与配套规划存档；只提交场景可能无法还原作者可编辑的规划数据。
