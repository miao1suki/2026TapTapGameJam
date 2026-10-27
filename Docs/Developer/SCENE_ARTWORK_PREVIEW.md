# Scene 编辑态的正式美术预览

关卡编辑器生成或替换道具之后，Scene 视图直接使用已接入的二维素材；不必先进入 Play 才能看到正式尖刺、梯子、机关、植物、重生点和三色方块。打开旧场景也使用同一路径，不重建关卡或修改原有占格。

## 为什么曾经不一致

部分旧预制体（例如四向尖刺）只有碰撞和三维占位模型。旧的编辑态补齐只在打开场景或重导入美术配置时执行；规划窗口随后新建、替换实例时没有再次补齐。Play 的场景加载兜底会补出正式外观，因此出现“规划图正确、Scene 是旧模型、运行才正确”。

此外，原 `ExecuteAlways` 表现组件会在普通 Scene 开启时创建可序列化的外观子物体并修改原 Renderer 的 `enabled`。当前自动编辑预览不再依赖此安装流程。

## 当前链路与边界

- `Project.BlockFeatures.Editor.ArtworkScenePreview` 只存在于 Editor。场景开启、规划重建、对象/实例参数修改、Undo 和美术更新均通过事件刷新；空闲与 Scene 重绘不扫描整个场景。无 Undo 的原生新建/销毁由合并后的 Hierarchy 结构事件补充发现；批量构建在参数写完后调用 `QueueObject`，避免每放一物体就立即重扫整图。
- 素材复用 `ObjectArtworkPlanningPreview` 的 Texture、UV、有效像素尺寸和旋转/镜像规则。缓存网格仅由 Editor 持有，标记 `HideAndDontSave`，不创建 Scene 对象、组件、碰撞体或运行时材质。
- 旧三维模型及已保存的旧精灵只临时设置 `Renderer.forceRenderingOff`；不改变作者 `enabled`、材质、碰撞或颜色进度。进入 Play、脚本重载和关闭场景前清理并恢复自己持有的隐藏状态。
- 平台保留作者原占格。短平台从原图中央裁取原像素，不把三格图压进一格；长平台重复、末段裁切。竖平台旋转后仍保持方形像素。非矩形合并的孔洞保留。
- 水在底层，按真实相邻格选水面、水中和水底；墙可遮住水，同格其他道具在上层。合并水不填补原本无水的孔洞。
- 作者藤蔓只绘制一根母体，使用纯读的 `ArtworkRoot`、`AuthoredArtworkCells` 和序列配置；不调用会修复玩法状态的 `RestoreInitialGrowthLinks`。
- Scene 中可直接点击预览选择作者物体，Shift/Ctrl 追加或取消选择；实际合并孔洞不会被命中。原生变换手柄、Alt 导航和已经开始的拖动优先，不接管拖拽或制造撤回记录；Scene 隐藏及当前编辑 Stage 仍受尊重。
- 字幕告示牌沿用自身已有的临时 `SubtitleSignVisual`，Timeline 触发器沿用顶层编辑器标记；不把这些编辑标记变成游戏可见物。

正常 Play 仍由 `MechanismArtworkPresentation`、`WorldObjectSpritePresentation`、颜色表现管理器及各功能组件负责。资源生成器可显式调用 `Ensure/Refresh` 配置正式预制体；这与打开用户 Scene 时的只读自动预览是两个不同入口。

Scene/`.meta`、Scene 引用的作者资产与新增依赖仍须一起交付；历史 `.planning.json` 不再同步。预览缓存不是作者数据，也不会代替 Scene/Map。

## 隔离验证

`SceneArtworkPreviewAudit.Run` 仅允许在带 `-noaudio` 的 `Library/FlowRetirementProbe` 执行。它检查真实旧预制体无 Undo 新建/替换后的自动编辑预览、方向参数修改与 Undo 自动刷新、四向尖刺及其他已交付物体、不添加组件、保留作者状态、短平台裁片、水体孔洞与三层素材、保存前后 Scene 字节一致、重复相机绘制复用缓存，以及清理后恢复原状态。实际 GPU 图为 `Library/SceneArtworkPreview.png`，结果为 `Library/SceneArtworkPreviewAudit.txt`。测试不得覆盖作者 Scene。
