# 无色道具接入

Runtime：`Assets/_Project/Code/Systems/WorldItems/Runtime`。资源：`Assets/_Project/Content/WorldItems`。

## 收集品

`Project.Items.CollectiblePickup : DropItemBase, IRoomColorResettable`。普通球形实体碰撞只负责方块/基座推挤，子 Trigger 用于玩家接触；收集品与玩家实体碰撞体建立局部`Physics.IgnoreCollision`，不修改全局Layer矩阵，不忽略拾取Trigger，玩家穿过并拾取而不是被球体挡住。普通收集过程不申请玩家控制锁，不中断走跑/跳跃。Rigidbody无重力、锁 Z/旋转，仍为动态刚体以保留方块/基座推动。Prefab与新建工具默认关闭重力，启用、房间重置和对象池复用也强制关闭，兼容旧场景保存的useGravity=true，无需重建关卡。接触其他道具不结算。三色接触方块与运动中的基座通过物理接触推动，无场景全量扫描。

玩家碰撞过滤在启用/重置时读取`PlayerController.ActivePlayers`，并订阅`PlayerController.PlayerActivated`覆盖稍后生成或重新启用的玩家；不在每件收集品的Update/FixedUpdate轮询。仅记录本组件新建的忽略对，禁用时恢复并退订，不恢复其他系统原先建立的忽略对。拾取后关闭碰撞，复位重新启用碰撞时重建过滤。玩家的地面/搬运查询也必须尊重`Physics.GetIgnoreCollision`，不能把此推挤实体当作支撑或墙。

菜单展示的`GameplayPresentationGate`只门控拾取资格，不跳过实体穿过过滤。真实关卡预览会暂时禁用玩家Collider；最后一个展示租约释放时，在Collider恢复之后发布`GameplayRestored`，收集品一次性重施已有玩家碰撞对，避免首次开始或重新开始后收集品挡路。订阅随组件启停配对，不增加逐帧扫描。专项包含真实`StageWorld→EndPreview→Physics.Simulate`穿过与Trigger收集。

过滤与拾取资格还必须校验`other.attachedRigidbody == player.Motor`；不能只看Transform父层级。搬运中的独立刚体方块可能临时挂在玩家子节点下，不得因此忽略它与收集品的推挤碰撞，也不得把它当成玩家结算奖励。

通过 `CollectiblePickup.Collected` 静态事件取得 `(item, actor)`，用 `ItemId`、`Amount` 结算；订阅者在 Disable 时注销。或使用 Inspector 的 `onCollected` UnityEvent。事件只在拾取飞行完成后触发一次，触发前已设置 `IsCollected=true`。接触已进入 `CollectionRequested` 后，R、死亡与离区不再中断收集飞行或恢复该物品，不丢失奖励，也不重复结算。

`DropItemBase.ResetForReuse()` 停止 Tween、调用可选 `IDropItemCollectionReset.ResetCollectionAnimation()`、重置收集/吸引标志及外观。`PickupFlightAnimator` 实现该复位契约，恢复子外观位姿、显隐与 Renderer。不得在每个道具子类重新写飞行 Tween。

收集后仅在 `RecycleItem` 隐藏作者物体，不 Destroy；`RoomResetObject.Persistent` 将已开始/已完成的普通收集品视作进度，区域复位不恢复其姿态、显隐或收集资格。未收集物仍正常还原布置位置。`ResetForRoom` 同样保护这两种收集状态；重新启用已收集对象会立即隐藏，不再发奖励。

后续存档接口：`IsCollected` 为结算完成状态；`TryGetStableInstanceId(out string)` 用 Scene 路径、`PlanningBoxId`、`PlanningLocalCell` 生成稳定实例键，字符串长度前缀与 invariant 格坐标避免歧义；缺少作者身份返回 false，不以运行时 instanceID、当前位置或随机GUID代替。`ItemId` 是奖励类别，不是实例ID。存档加载可调用 `RestoreCollectedState()` 静默恢复已收集/隐藏状态，不重放动画或事件。**本组件不新增磁盘/全局存档服务；场景销毁重建、退出重进后的保留由后续存档所有者接入这些接口完成。**

专项入口：`Project.WorldItems.Editor.CollectibleContactAudit.Run`，仅在`Library/FlowRetirementProbe`以`-noaudio`运行，覆盖真实Trigger拾取与无阻挡穿过、拾取飞行不锁玩家、晚生成玩家、启停/对象池/房间复位、原有忽略对所有权，以及三色方块/基座真实物理推挤。不要通过反射调用拾取处理器冒充物理触发测试；不保存作者Scene。

`Project.WorldItems.Editor.CollectiblePersistenceAudit.Run` 专测收集飞行中/完成后的区域重置、复用防重复奖励、稳定实例键及静默恢复。共享 `DropItemBase.ResetForReuse` 契约不变，不把其他掉落物一律改成永久收集。

## 尖刺

`Project.WorldItems.NeutralSpike` 不实现 IColorApplicationTarget，不挂 ColorObject，不复用受红色解锁限制的旧 SpikeFeature。OnTriggerEnter/Stay 接触存活且非无敌玩家时调用 `PlayerController.EnterDeath()`，保持统一死亡动画、声音、重生与控制清理。四向预制体使用旋转的凸三角柱 MeshCollider Trigger，根 Transform 对齐一格，避免视觉外整格杀伤。

子碰撞体由运行时 `SpikeContactRelay` 转发 Trigger/Collision 的 Enter 与 Stay，归属以最近的 `NeutralSpike` 为准；静态子 Collider 不会自动把事件交给任意父脚本，不能删除转发后只保留父脚本。禁用尖刺组件不致死；复活保护结束后，仍在触发区内的玩家会正常死亡。不新增 Rigidbody，不轮询全场物理重叠，不在每格挂 Update。

合并尖刺读取 `LevelEditorPlacedBlock.SizeCells` / `LevelEditorMergedShape` 的实际占格，在运行时逐占格复制原始方向的凸三角柱，共享一份临时 Mesh。禁用原先被拉伸的整排触发器，保留非矩形空洞；`IRoomColorResettable.ResetForRoom()` 在房间还原后重新保证这一状态，不重复创建碰撞体。不要将一整排合并成一枚放大的三角触发区，否则会漏掉两端。

玩家重生安全落点同时排除岩浆、无色尖刺与旧尖刺；只排除玩家自身层级的 Collider，不排除与玩家共享地图父物体的地面或危险物。

真实物理回归入口：`HazardContactAudit.Run`，仅允许在 `Library/FlowRetirementProbe` 隔离工程运行。使用 Unity `-noaudio`，专项测试内也关闭 AudioListener；不得通过游戏音量 API 改写作者的音量偏好。覆盖四向、合并端点/孔洞、保护结束后的 Stay、房间重置、岩浆冷却/还原与重生安全，不反射直接调用伤害处理器。

## 岩浆的实体遮挡

`LavaHazardFeature`通过`LavaContactOcclusion`判断玩家是否真正接触裸露岩浆。方块填入岩浆格后，站在实体遮住的顶部不会受伤；不是让整格或整片岩浆免伤。按玩家与岩浆的实际相交区域减去实体Box覆盖，再以物理形状查询确认剩余裸露接触；支持多块接缝、稀疏合并与孔洞。Trigger、装饰、禁用碰撞体及与玩家忽略碰撞的物体不能遮挡；移开方块后危险立即恢复。不受伤时也不播放冷却/死亡声音。

动态方块落稳可能比网格岩浆顶低一点：只对玩家实际落脚的支撑顶面容忍最多0.03格的物理解算薄层，不扩大侧面、不掩盖真实露边/缝隙或深处浸入。红/蓝方块尚未 `IsSettled` 时也须保留从上落脚的玩家碰撞，避免松钩切目标后短暂穿透进岩浆；旁侧/下方仍沿用空中规则，详见 CONTACT_CUBES_API。

专项入口`LavaOcclusionAudit.Run`，静音隔离环境中走真实Trigger/PhysX接触，覆盖实体填坑、侧面/边缘裸露、移走方块、合并孔洞与冷却/房间重置；不直接反射调用伤害处理冒充接触测试。

## 编辑器

WorldItemSetup.CreateMissing 只创建缺失资源并按 prefab 引用补五个 PropEntry，既有 EntryId 保留。参数含中文 InspectorName，使用 UI Toolkit Inspector。无需新建平行关卡编辑器，不修改成员测试场景。提交时 prefab、mesh、material 与所有 meta 一并交付；关卡另带 planning.json。
