# 美术工作台：关卡只读参考层

工作台可以直接参照当前关卡中的墙、房间与正式二维道具，不需要先去 Hierarchy 选白块。默认显示参考，透明度为 35%；可关闭或调透明度。参考层在美术笔触下面，不接收点击，不修改场景对象的材质、渲染器、透明度或存档。

## 数据与位置

- 优先使用已绑定当前场景的关卡编辑器草稿；其次为该场景配套规划存档，再次为场景 `RoomLifecycleManager` 的内嵌规划。只有完全没有规划时才只读扫描场景中已放置对象。扫描发生在来源刷新，不在拖动/缩放或每帧。
- 房间、墙与道具均保持真实世界 XY。规划向下的 Y 转换为世界向上的 Y。当前选房优先用于初始定位；无选房时按选中场景对象、玩家位置、首个房间依次回退。视图定位/记忆由工作台窗口统一管理，不自动把已有视图反复拉回原点。
- 显示顺序为房间边框、水、墙、道具、仅编辑器可见的触发器。参考中的水不会压到墙或道具上。正常游戏不会因为打开参考而出现触发器贴图。

## 像素和表现

正式道具复用 `ObjectArtworkPlanningPreview` 的同一份二维素材与缓存，不临时实例化/拍摄三维预制体。遵循 `ART_GRID_CONVENTIONS.md` 与 `SCENE_ART_PIXEL_CONVERSION.md` 的 16 有效美术像素 = 1 世界格。

- 自然尺寸与逻辑占格分离：32px 收集品仍显示 2×2 格，不压缩成逻辑的 1 格。
- 旧的一格平台中心裁取 16px；五格竖平台由旋转 90° 的三格加两格原像素裁片组成，不拉伸。
- 同列相邻藤蔓先取一张连续整柱缓存，再按行切 UV；不让每个刷入格重启一个藤蔓尖端。
- 水先构建世界格集合，同格不同施力水只绘一个公共蓝色底图。水面/水域/水底按上下相邻关系选择；L 形、空洞与房间边界不被强行补矩形。
- 按钮编号/朝向、尖刺朝向与作者实例参数跟随正式预览解析。隐藏的告示牌不出现。前景透明素材保留作者顺序，仅相邻兼容项合批，不按纹理重排重叠物品。

## 性能

来源刷新时一次生成只读 quad；墙/水按纹理及 16 世界格空间桶组织，每个 retained Mesh 批次最多 256 quad。前景道具保留绘制顺序并按连续纹理/空间范围合批。每房间只有四条边框，绝不逐格 Painter2D 描线。

平移/缩放只更新父变换、可见批次和透明度；不会重扫 Scene、重建贴图或重新采样藤蔓。关卡参考不是运行时渲染器替身，不进行动画播放或物理模拟。

## API 与验证

`Project.GridArt.Editor` 不依赖 `Assembly-CSharp-Editor`。规划端通过已有 `GridArtLevelSource.Read(Scene)` 委托提供 `GridArtLevelSnapshot`：

- `rooms`：`id`、`label`、`boundsWorld`；`currentRoomId`；完整 `worldBounds` / `hasWorldBounds`。
- `referenceItems`：只读 `GridArtQuad`（世界矩形、纹理、UV、90°旋转）、颜色与显示层。`groups` 仍保留墙地联动原契约。
- `RecalculateBounds()` 合并房间、墙与物体的自然显示范围，不修改来源数据。

`GridArtLevelReference` 是独立 `VisualElement`：

```csharp
reference.Configure(snapshot); // 只在数据来源变化时
reference.SetView(panPixels, zoomPixelsPerArtCell / mapCellWorldSize, viewport);
reference.SetReferenceVisibility(visible, opacity);
```

`QuadCount`、`BatchCount`、`VisibleBatchCount`、`GeometryRevision` 供回归验证。参考元素放在 retained 美术层之前；`Configure(null)` 用于切场景清理。

隔离工程执行入口：`PlanningEditorPrototype.PlanningArtReferenceAudit.Run`，报告 `Library/PlanningArtReferenceAudit.txt`。覆盖远坐标房间/墙定位、只读保证、无三维实例、非矩形水与去重、连续藤蔓、一格/竖平台裁切、朝向与32px收集品、万格合批/100次导航不重建、开关透明度及透明层顺序。窗口实际挂接/位置记忆另由 `Project.GridArt.Editor.GridArtNavigationAudit.Run` 验证。

协作交付仍需同时提交关卡场景与配套规划存档；只提交场景可能无法还原作者可编辑的规划数据。
