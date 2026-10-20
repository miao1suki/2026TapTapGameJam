# 编辑渲染距离工具接口

## 所有权

- 工具：`Assets/_Project/Development/LevelEditor/Orpheus0829/Editor/EditRenderDistanceWindow.cs`
- 类型：纯 Editor `EditorWindow`
- 菜单：`Tools/2026TapTap/编辑器性能/编辑渲染距离`

## 数据范围

工具只扫描当前活动 Scene 根节点 `__PlanningMapGenerated` 的直接子房间容器。

每个房间缓存整房总包围盒和规划方块样本包围盒，来源依次为：

1. 子层级全部 `LevelEditorPlacedBlock`，按 `CellWorldSize × SizeCells × lossyScale` 计算。
2. 没有规划块时使用子层级全部 `Renderer.bounds`。
3. 仍为空时使用容器位置和一格大小。

不会扫描或修改非规划场景对象。

## 可见性规则

- 任一规划方块样本的 `Bounds.SqrDistance(SceneView.pivot) <= (半径格数 × 格尺寸)²`：显示；无样本时退回整房包围盒。
- 当前房间或最近房间：始终显示。
- `Selection.activeTransform` 所属房间：始终显示。

距离判断不再只用不连续房间的总包围盒，避免大型房间因跨区域 AABB 导致全部房间被误判为可见。

## 规划窗口轻量刷新

工具开启后，`EditorAuthoringPerformanceState.ReducedRefreshEnabled` 会驱动：

- `LevelEditorWindow`：面板刷新周期从 `.1` 秒降到 `.35` 秒。
- `PlanningEditorWindow`：规划面板刷新周期从 `.1` 秒降到 `.35` 秒。
- 两套窗口失焦时进一步降到 `.75` 秒；SceneView 操作期间优先让场景响应。
- 调色板同步和缺失引用清理：从 `.3` 秒降到 `1` 秒。
- 规划预览图刷新：从 `.3` 秒降到 `1` 秒。
- `PlanningCanvas` 区域标签从 `.1` 秒降到 `1` 秒。

工具关闭该开关时恢复原有刷新频率。

## 运行诊断

`EditorAuthoringPerformanceState` 记录：

- `EditorApplication.update` 帧间隔。
- `PlanningEditor.Tick` 耗时。
- `LevelEditor.Tick` 耗时。
- `ArtworkScenePreview` 的 SceneView 绘制和拾取耗时。
- 房间缓存重建耗时。
- 可见性应用耗时。
- `PlanningCanvas.Draw` 总耗时及 World/Assembly/Detail 分项。
- 规划预览刷新、区域标签刷新和玩家标记刷新耗时。

`EditorApplication.update` 间隔超过 `60 ms` 时输出 `[EditPerf]` 警告，最多每两秒一次。该间隔不是严格帧时间，Unity 空闲时也可能主动降低回调频率；必须结合画布、预览和可见性分项判断。诊断只读计时，不改变场景或玩法。

房间样本包围盒优先从 `LevelEditorPlacedBlock.CellWorldSize`、`SizeCells` 和 Transform 缩放计算；Renderer 仅作为无规划块房间的后备。SceneVisibilityManager 的 Hide/Show 使用队列，每个窗口 Tick 最多应用一个根节点，避免批量层级可见性更新阻塞编辑器。

## 生命周期

- `EditorApplication.update` 每 `0.15` 秒检查 SceneView 中心和缓存变化。
- `hierarchyChanged`：缓存标脏。
- `sceneOpened`：缓存标脏。
- `sceneSaving`：先恢复工具隐藏的房间，保存后择机重新应用。
- `ExitingEditMode`、`EnteredPlayMode`：恢复全部可见性。
- `EnteredEditMode`：若开关仍启用，重建缓存并重新应用。
- `AssemblyReloadEvents.beforeAssemblyReload`、`EditorApplication.quitting`、窗口 `OnDisable`：恢复全部可见性。

## 存储

仅使用 `EditorPrefs` 保存：

- 是否启用
- 可见半径格数
- 是否降低规划窗口刷新频率

不写入 Scene、Prefab、ScriptableObject、运行存档或规划 JSON。

## 验证

- Editor 程序集编译 0 error。
- 启用后远处房间在 Hierarchy 和 SceneView 隐藏。
- 当前、相邻和选中对象所在房间不隐藏。
- 进入 Play、保存、关闭窗口后恢复。
- 手工取消勾选后全部工具隐藏项恢复，不调用全局 `ShowAll`，避免覆盖用户原本的可见性状态。
