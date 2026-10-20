# 二维可见性接入

- 模块：`Assets/_Project/Code/Systems/WorldVisibility`；喷泉表现归 `ObjectArtwork/Runtime/FountainArtwork.cs`。无联网，无玩家控制或房间生命周期写入。
- `WorldVisibility2D` 绑定 Game 正交相机与观察者；默认只影响显式挂载的场景。PC/Mobile Renderer 都增加共享 `WorldVisibilityRendererFeature`，未挂组件时不添加渲染 Pass。
- 默认 `connectedSpaceVisibility=true`：半格四邻接局部洪泛，路径可绕角；默认24格，半径限2～48格，外15%距离渐隐。原画亮度，不模拟角色寻路/能力。静止或半格内移动复用Mesh，普通移动最多10Hz，几何变更及远传送即时更新。`ConnectedCellCount`/`ConnectedBuildCount`提供诊断。
- 连通模式只压暗非墙体房间内容；所有非道具 `LevelEditorPlacedBlock` 的实际 BoxCollider 足迹另生成 `WallMesh` 保留遮罩，墙体和关闭的门保持原画亮度。墙体保留按碰撞足迹合成，不读取或改写作者 Renderer。
- 连通空间重建时保留上一张网格与当前网格，两张 R8 遮罩按 `connectedTransitionDuration` 交叉淡化；玩家移动、开门、传送和几何变化都不再瞬间替换可见区域。
- 连通模式完全跳过玩家灯及附加灯几何，渲染仅一张R8半分辨率遮罩和一次合成，无灯光叠加RT/高斯Pass/新增Unity Light或实时阴影。
- 对外淡入淡出接口：`WorldVisibility2D.SuppressOcclusion(owner, fadeOutDuration, fadeInDuration)` 返回可释放句柄；取得时平滑关闭遮蔽，`Dispose()` 后平滑恢复。句柄按 owner 引用计数，适合 Timeline、演出或临时 UI 共用。三色钥匙 Timeline 已自动接入。
- 共享遮挡数据采用世界 XY 实体线段，静态规划墙以 `LevelEditorPlacedBlock.SolidRevision` 驱动重建及 8 世界单位区块查询；实际各 BoxCollider 独立提边，不用整组 AABB 填孔。门以状态和实际碰撞轮廓参与动态版本。自定义静态改形/动态创建门后调用 `InvalidateAll()`。
- 关闭连通模式才启用旧端点射线/明暗相乘灯光模式。`VisibilityLight2D` 是旧模式的可复用独立局部光，`VisibilityOccluder2D` 为显式非规划遮光体。光源/遮光体启停注册并释放缓存；动态轮廓、观察者和参数变化时才重算网格。源纹理和物理坐标不吸附/重采样。
- RendererFeature 在 `BeforeRenderingPostProcessing+20` 合成，位于像素化与选择性 HSV 之后；世界画面及粒子/连线一起变暗，Overlay UI 在相机完成后照常绘制。材质持有 Shader 资产引用避免仅依赖 Shader.Find 被构建剥离。
- `VisibilitySetup.BuildAssets()` 是显式幂等资源生成入口，不保存 Scene；更新两个 Renderer 资源、喷泉导入器、共享 Material 与 Prefab，必须连同依赖/meta交付。不全项目换渲染器。
- 隔离审计 `VisibilityAudit.Run` 要求真实 GPU；`VisibilityPlayAudit.Run` 需要正常 Editor GameView，不能 batchmode 冒称实际屏幕合成。两者仅允许 `Library/FlowRetirementProbe`，不保存作者场景。
- 菜单与参数使用中文 UI Toolkit Inspector。使用文档：`Docs/Developer/WORLD_VISIBILITY_AND_FOUNTAIN.md`。

- 连通审计入口 `ConnectedSpaceAudit.Run` 仅限独立Library/ConnectedSpaceProbe；真实GPU覆盖绕角/封闭/32灯旁路/门状态/传送/范围渐隐与实际Scene接线，不保存作者Scene。
