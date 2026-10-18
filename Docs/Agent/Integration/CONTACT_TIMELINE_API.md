# 接触触发 Timeline API

## 源码

`Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/ContactTimelineTrigger.cs`

默认道具预制体：

`Assets/_Project/Content/ColorBlocks/Prefabs/ContactTimeline_Block.prefab`

## 契约

组件直接继承 `MonoBehaviour`，不是 `BlockFeature`，不要求 `ColorObject`、`BlockRuntime` 或 `BlockAbilityHost`，可挂到任意道具或场景物件。

## 运行规则

- `OnEnable` 缓存 Renderer 状态、解析 `PlayableDirector` 并隐藏 Renderer。
- `ContactTimelineTrigger` 的 Inspector 直接绑定所挂物体的 Collider SerializedObject，可直接编辑启用状态、Trigger、BoxCollider 尺寸和中心。
- `OnTriggerEnter` / `OnTriggerExit` 和 `OnCollisionEnter` / `OnCollisionExit` 都参与接触记录。
- 支持 `AnyCollider`、`Player`、`Tag` 三种筛选。
- `triggerMode` 支持只触发一次、可重复触发、进入播放并离开停止。`WhileInside` 模式在最后一个匹配接触离开后停止并重置 Director，适合区域状态型 Playable；`destroyAfterTrigger` 在该模式不会销毁触发器，避免刚进入就失效。
- `requestPlayerControlLockDuringTimeline` 为真时，播放前通过触发对象或场景中的 `IPlayerControlLockTarget` 申请控制锁；播放到总时长末尾、Director 停止、重置或禁用时释放，暂停阶段保留锁。组件不保存 `PlayerController` 引用。
- 接触使用根对象实例 ID 去重，离开后再次进入才可再次触发。
- Inspector 将触发对象、碰撞体、触发规则和表现设置分开显示，并显示当前模式与剩余冷却。
- `ResetTrigger()` 清除触发记录、停止 Timeline 并重新隐藏。它是独立 MonoBehaviour，不存在 BlockFeature 的 OnAttach/OnResetForRoom/OnDetach 回调，不应依靠这些旧文档名称调用。
- `OnDisable` 停止 Timeline、清除接触并交还控制锁；自动隐藏的触发器在运行时仍保持隐藏，不能因只关闭此组件而露出占位立方体。

## 编辑器标记

- 默认 prefab 的 MeshRenderer 关闭且不投射/接收阴影，Collider 与 Director 不变。关卡编辑器与 SceneView 的图片来自 `Resources/UI/棍母.png`，由 Editor 程序集只读加载，不创建运行时 SpriteRenderer/图片材质。
- `ObjectArtworkPlanningPreview` 返回 `EditorOverlay` 的等比缩放预览；道具栏和画布共用图片。画布独立 Timeline 顶层显示已放置图与半透明放置图，全部忽略输入命中，不阻挡选择/绘制。移动和缩放复用 Image 元素。
- 照片仅为编辑器标记，不按正式场景的16源像素/格转换；单图、无mipmap、最大512。保留图片与meta/GUID一起同步。SceneView标记只在非Play时绘制，Game不可见。
- 非Play SceneView 和关卡编辑器画布同时用与门连接线一致的金色虚线绘制真实 Collider 范围；SceneView 的 BoxCollider 使用局部中心、尺寸与旋转，其他 Collider 回退到世界 Bounds，关卡画布读取组件覆盖后的 `m_Center` / `m_Size`。该虚线只属于编辑器，不创建运行时材质或 Renderer。

## 验证

- `TimelinePreviewAudit.Run` 在静音隔离项目验证真实附着画布、图层顺序、放置预览、缩放复用和导入设置。
- `TimelineVineLiftAudit.Run` 在真实Play中验证隐藏触发器仍接收标准玩家Collider，组件禁用/重启不露出模型且交还控制锁。
- `git diff --check` 通过。
