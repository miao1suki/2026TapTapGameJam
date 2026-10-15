# 无色道具接入

Runtime：`Assets/_Project/Code/Systems/WorldItems/Runtime`。资源：`Assets/_Project/Content/WorldItems`。

## 收集品

`Project.Items.CollectiblePickup : DropItemBase, IRoomColorResettable`。普通球形实体碰撞用于推挤，子 Trigger 用于玩家接触；Rigidbody 锁 Z/旋转。接触其他道具不结算。三色接触方块与运动中的基座通过物理接触推动，无场景全量扫描。

通过 `CollectiblePickup.Collected` 静态事件取得 `(item, actor)`，用 `ItemId`、`Amount` 结算；订阅者在 Disable 时注销。或使用 Inspector 的 `onCollected` UnityEvent。事件只在拾取飞行完成后触发；中途房间重置取消动画，不发过期回调。

`DropItemBase.ResetForReuse()` 停止 Tween、调用可选 `IDropItemCollectionReset.ResetCollectionAnimation()`、重置收集/吸引标志及外观。`PickupFlightAnimator` 实现该复位契约，恢复子外观位姿、显隐与 Renderer。不得在每个道具子类重新写飞行 Tween。

收集后仅在 `RecycleItem` 隐藏作者物体，不 Destroy；Rooms 已扫描 DropItemBase，区域重置保留作者身份和原位置。当前收集品非永久进度，重复收集默认可触发奖励；永久计分应由接入系统按稳定实例身份去重，不用临时 instanceID。

## 尖刺

`Project.WorldItems.NeutralSpike` 不实现 IColorApplicationTarget，不挂 ColorObject，不复用受红色解锁限制的旧 SpikeFeature。OnTriggerEnter/Stay 接触存活且非无敌玩家时调用 `PlayerController.EnterDeath()`，保持统一死亡动画、声音、重生与控制清理。四向预制体使用旋转的凸三角柱 MeshCollider Trigger，根 Transform 对齐一格，避免视觉外整格杀伤。

## 编辑器

WorldItemSetup.CreateMissing 只创建缺失资源并按 prefab 引用补五个 PropEntry，既有 EntryId 保留。参数含中文 InspectorName，使用 UI Toolkit Inspector。无需新建平行关卡编辑器，不修改成员测试场景。提交时 prefab、mesh、material 与所有 meta 一并交付；关卡另带 planning.json。
