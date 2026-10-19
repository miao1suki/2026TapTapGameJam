# 美术工作台区域效果接口

## 依赖与所有权

`Project.GridArt.Editor` 不能引用 Assembly-CSharp。`GridArtEffectsSource` 只声明 `Read / Create / BuildInspector` 委托及区域 Bounds 数据；`Assets/ta_source/Rain/Editor/GridArtEffectsAdapter` 在 Assembly-CSharp-Editor 中初始化，直接复用 TA 的 `RainRegionWindow.CreateRegion` / `FireflyRegionWindow.CreateRegion`，不反射、不复制玩法数据、不增加运行时 Editor 依赖。

`GridArtWindow.Effects.cs` 独立 partial 持有工具模式、选择、矩形拖动及框预览。窗口主文件仅接 `BuildEffectsPanel`、`EffectPointerDown/Move/Up/CancelGesture` 和 `DrawEffectOverlay(Painter2D, Func<Rect,Rect>, Rect)`。坐标回调接世界格数，TA snapshot 的世界单位 Bounds 在 partial 内按当前美术格尺寸换算。新建区域不要求 GridArtMap，但显式“保存场景”才保存独立根。

`ArtEffectRegionGrid` 是仅存单格世界尺寸的同根 provider，避免独立区域失去非1格尺寸，也避免挂进可重建的玩法生成根。它不是 `LevelEditorPlacedBlock`、`RoomResetObject` 或 `ColorObject`。区域 Undo 整组创建 / 删除，Prefab 参数覆盖、provider 和 Transform 一起保存到 Scene。密集动态 Mesh 保持 TA 原有 DontSave。

桥接扫描仅在层级 / 参数 / Undo / 场景变化时触发工作台缓存失效，不从 Painter2D 或鼠标移动逐帧搜索所有区域。范围展示只画矩形，没有按区域面积生成格子或每颗效果预览对象。现有 TA runtime 不增加全场扫描。

## 萤火深度

`FireflyField.RenderDepthCells` / `SetRenderDepthCells(float)` 控制硬点与光晕共同的局部可见平面，默认-0.25格，保持区域根Z不变。原 Shader 的 LEqual 深度测试不变；排序20使原生透明道具后绘制，墙的实体深度仍遮挡。独立创建窗口新默认根Z=0，既有作者根不自动重写。实时补光与原游动 / 驱散 / 固定种子行为保持不变。

## 回归入口

`Project.Rain.Editor.GridArtEffectsIntegrationAudit.Run`：仅静音 `Library/FlowRetirementProbe`，不要 `-nographics`。检查委托、真实创建、非1格尺寸、512/128预算、原像素、独立美术归属、完整区域 Bounds、只读缓存、原生 Inspector、Undo/Redo、Scene保存重载；实际 GPU 对照透明道具前景显示与不透明墙同时遮挡硬点/光晕。

继续执行原 `RainIntegrationAudit`、`FireflyIntegrationAudit`；后者 Inspector 字段计数随新增深度参数增加1，不放宽像素 / 缓存 / GPU标准。窗口交互审计由工作台集成者统一执行，覆盖半笔画取消与中键/滚轮输入路由。不得将代码审查或自造图当成运行结果。
