# 无色道具接入

Runtime：`Assets/_Project/Code/Systems/WorldItems/Runtime`。资源：`Assets/_Project/Content/WorldItems`。

## 收集品

`Project.Items.CollectiblePickup : DropItemBase, IRoomColorResettable`。普通球形实体碰撞只负责方块/基座推挤，子 Trigger 用于玩家接触；收集品与玩家实体碰撞体建立局部`Physics.IgnoreCollision`，不修改全局Layer矩阵，不忽略拾取Trigger，玩家穿过并拾取而不是被球体挡住。普通收集过程不申请玩家控制锁，不中断走跑/跳跃。Rigidbody无重力、锁 Z/旋转，仍为动态刚体以保留方块/基座推动。Prefab与新建工具默认关闭重力，启用、房间重置和对象池复用也强制关闭，兼容旧场景保存的useGravity=true，无需重建关卡。接触其他道具不结算。三色接触方块与运动中的基座通过物理接触推动，无场景全量扫描。

玩家碰撞过滤在启用/重置时读取`PlayerController.ActivePlayers`，并订阅`PlayerController.PlayerActivated`覆盖稍后生成或重新启用的玩家；不在每件收集品的Update/FixedUpdate轮询。仅记录本组件新建的忽略对，禁用时恢复并退订，不恢复其他系统原先建立的忽略对。拾取后关闭碰撞，复位重新启用碰撞时重建过滤。玩家的地面/搬运查询也必须尊重`Physics.GetIgnoreCollision`，不能把此推挤实体当作支撑或墙。

过滤与拾取资格还必须校验`other.attachedRigidbody == player.Motor`；不能只看Transform父层级。搬运中的独立刚体方块可能临时挂在玩家子节点下，不得因此忽略它与收集品的推挤碰撞，也不得把它当成玩家结算奖励。

通过 `CollectiblePickup.Collected` 静态事件取得 `(item, actor)`，用 `ItemId`、`Amount` 结算；订阅者在 Disable 时注销。或使用 Inspector 的 `onCollected` UnityEvent。事件只在拾取飞行完成后触发；中途房间重置取消动画，不发过期回调。

`DropItemBase.ResetForReuse()` 停止 Tween、调用可选 `IDropItemCollectionReset.ResetCollectionAnimation()`、重置收集/吸引标志及外观。`PickupFlightAnimator` 实现该复位契约，恢复子外观位姿、显隐与 Renderer。不得在每个道具子类重新写飞行 Tween。

收集后仅在 `RecycleItem` 隐藏作者物体，不 Destroy；Rooms 已扫描 DropItemBase，区域重置保留作者身份和原位置。当前收集品非永久进度，重复收集默认可触发奖励；永久计分应由接入系统按稳定实例身份去重，不用临时 instanceID。

专项入口：`Project.WorldItems.Editor.CollectibleContactAudit.Run`，仅在`Library/FlowRetirementProbe`以`-noaudio`运行，覆盖真实Trigger拾取与无阻挡穿过、拾取飞行不锁玩家、晚生成玩家、启停/对象池/房间复位、原有忽略对所有权，以及三色方块/基座真实物理推挤。不要通过反射调用拾取处理器冒充物理触发测试；不保存作者Scene。

## 尖刺

`Project.WorldItems.NeutralSpike` 不实现 IColorApplicationTarget，不挂 ColorObject，不复用受红色解锁限制的旧 SpikeFeature。OnTriggerEnter/Stay 接触存活且非无敌玩家时调用 `PlayerController.EnterDeath()`，保持统一死亡动画、声音、重生与控制清理。四向预制体使用旋转的凸三角柱 MeshCollider Trigger，根 Transform 对齐一格，避免视觉外整格杀伤。

子碰撞体由运行时 `SpikeContactRelay` 转发 Trigger/Collision 的 Enter 与 Stay，归属以最近的 `NeutralSpike` 为准；静态子 Collider 不会自动把事件交给任意父脚本，不能删除转发后只保留父脚本。禁用尖刺组件不致死；复活保护结束后，仍在触发区内的玩家会正常死亡。不新增 Rigidbody，不轮询全场物理重叠，不在每格挂 Update。

合并尖刺读取 `LevelEditorPlacedBlock.SizeCells` / `LevelEditorMergedShape` 的实际占格，在运行时逐占格复制原始方向的凸三角柱，共享一份临时 Mesh。禁用原先被拉伸的整排触发器，保留非矩形空洞；`IRoomColorResettable.ResetForRoom()` 在房间还原后重新保证这一状态，不重复创建碰撞体。不要将一整排合并成一枚放大的三角触发区，否则会漏掉两端。

玩家重生安全落点同时排除岩浆、无色尖刺与旧尖刺；只排除玩家自身层级的 Collider，不排除与玩家共享地图父物体的地面或危险物。

真实物理回归入口：`HazardContactAudit.Run`，仅允许在 `Library/FlowRetirementProbe` 隔离工程运行。使用 Unity `-noaudio`，专项测试内也关闭 AudioListener；不得通过游戏音量 API 改写作者的音量偏好。覆盖四向、合并端点/孔洞、保护结束后的 Stay、房间重置、岩浆冷却/还原与重生安全，不反射直接调用伤害处理器。

## 编辑器

WorldItemSetup.CreateMissing 只创建缺失资源并按 prefab 引用补五个 PropEntry，既有 EntryId 保留。参数含中文 InspectorName，使用 UI Toolkit Inspector。无需新建平行关卡编辑器，不修改成员测试场景。提交时 prefab、mesh、material 与所有 meta 一并交付；关卡另带 planning.json。
