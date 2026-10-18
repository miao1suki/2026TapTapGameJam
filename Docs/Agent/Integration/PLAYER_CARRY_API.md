# 玩家搬运与提示

## 2026-10-18 运行时有效占用

- `LevelEditorPlacedBlock.Cell/SizeCells/ContainsCell` 是作者布置及重置数据，不随重力、绿绳、平台运动或燃烧改写。不得只因为 `ContainsCell` 命中就拒绝放下。
- PlayerController 用作者占格筛出候选，再用该候选**当前启用、活动、非Trigger的实体Collider与目标格相交**确认有效占用。Box从当前Transform计算世界包围盒，不为每帧预览调用全局Physics.SyncTransforms；子物体和后加Collider使用复用List读取，不缓存失效的启用状态/碰撞体清单。
- 实体世界Overlap与搬运目标注册表继续检查物体的新位置；完整搬运体积与区域归属检查保留。烧毁植物、已关闭Collider的开门通道、隐藏/移走物体不能留下历史占格；原位恢复后按现有Collider立即重新阻挡。
- 放置方向防薄墙射线、格子Overlap与作者候选核验统一忽略纯Trigger（包括水/交互检测区），但仍阻挡真实墙/实体。NonAlloc结果填满时保守拒绝，不因缓冲截断放进实体。
- `CarryRuntimeOccupancyVerification.Run` 仅在静音FlowRetirementProbe隔离工程执行：真实MRoom2红块[-95,-19]向基座[-95,-13]右侧烧空格F拿起/Q放下、移动前后占用、8轮烧毁/R、合并孔洞、薄墙、水Trigger、动态子碰撞体、不同深度及热查询分配。不会保存Scene/planning；夹具临时关闭钥匙避免开局拾取演出抢控制权，生产场景不变。
- 2026-10-18回归共142项：上述专用验证43项、MobileCarryRegression68项、CarryGround9项、FacingInteraction22项通过。不是新增R重置修复，原有植物/红块重置入口保持不变。

## 2026-10-16 朝向与落点预览

- `PlayerController.FacingDirection` 为最后有效水平输入朝向（-1/1），与角色 Sprite 共用；释放移动不复原朝向。输入驱动在交互扫描前更新朝向。
- 按键交互与搬运队列排除背后目标；相近候选保留当前目标并使用稳定次序，避免左右提示闪跳。鼠标染色仍按屏幕命中，不受此筛选影响。
- 逻辑占格依照配置固定大小，默认 1×1；移动经过格线不会临时扩为 2×2。中心取`CarryCenterWorldPosition`（实际CapsuleCollider.bounds.center），不以预制体根位置猜测；标准玩家碰撞中心已归零，自定义碰撞偏移仍须使用真实中心。拾取邻格、左右落点和防薄墙射线共用身体中心，避免负Y场景站地时取到地板格。完整实体重叠仍另外校验。
- `TryGetCarryPlacementPreview(out Bounds bounds, out bool valid)` 返回面朝侧的预计放下包围盒。Q/E 保持显式左/右放下，并转向该侧；不新增键位。合法为青绿虚线，非法为红色虚线，暂停/放下/复位/未携带时隐藏。
- 可选 `IPlayerCarryPlacementTarget.GetCarryPlacementBounds(position)` / `CanPlaceCarryAt(host, position)` 使预览与执行共用完整实体、运动参与者和房间边界校验。`CarryableFeature` 已实现；不能只以一个格子的空闲状态放下合并块。
- 预览仅一个缓存 Mesh，不添加 Collider、Canvas、EventSystem 或第二个刚体写入者。碰撞体拿起后虽停用，尺寸仍由缓存的原始实体计算。
- 网格占用查询使用 `LevelEditorPlacedBlock.ActiveInstances` 启停注册表，不因每帧预览调用全场景扫描。注册/注销采用索引交换删除，场景卸载不逐项线性查找；Enter Play Mode 可在启动时补登记已有实例。
- `Project.Player.Editor.FacingInteractionVerification.Run` 是隔离项目的 Play Mode 回归入口。

## 物理所有权

### 安全放下与玩家分离

- `IPlayerCarrySeparationHost.ResolveCarryOverlap(bounds,item,false)`为只读的路线/终点检查；`true`只排队约0.16秒的水平让位，不立即修改位置。`PlayerController.ConstrainCarryVelocity`在物理步统一写速度，保留竖直运动和实体阻挡。
- 终点采用真实胶囊穿透检测，避免安全外扩导致一格玩家无法放进一格夹墙通道。最多允许0.65格的水平自重叠，数值接触容差0.003格；深重叠或实际无路仍非法。
- `CarryTargetMotion`保持Collider关闭，动画后通过`IsCarrySeparationPending(item)`和`IsCarrySeparated(bounds)`确认完成，再恢复运动参与者与实体；总时限0.8秒，落点被占或分离失败保持举起。
- `CancelCarrySeparation(item)`用于取消/房间重置/死亡，防止残留位移；额外Timeline运动锁不被覆盖。不使用AddForce与正常控制并行竞争，也不直接移动玩家Transform。
- `GamingCarryPlacementVerification.Run`仅在隔离工程打开真实Gaming，使用原标准玩家、红色ContactCube、白墙与区域重置，验证F拾取、轻点左转、Q放下和R复位，不保存Scene。

`IPlayerCarryTarget` 保持原有拾取、头顶跟随、放下与复位契约。`CarryableFeature` 额外实现可选接口：

```csharp
Vector3 IPlayerCarryCollisionTarget.ConstrainCarryDisplacement(
    Vector3 anchor, Vector3 displacement, float skin);
```

- `anchor` 是本物理步的头顶根位置；`displacement` 是候选世界位移，返回不穿透的位移。
- 由 `PlayerController` 在写 Rigidbody 速度及执行平台位移前调用。普通、冲刺、游泳、攀爬均经同一约束，不增加第二个刚体写入者。
- `CarryTargetMotion` 缓存拿起前启用的实体 Collider。Box 使用实际中心、尺寸、旋转及缩放做无分配 OBB 扫掠，支持合并块及伸长绿块；Sphere/Capsule/Mesh 使用保守包围盒。
- 拾取动画期间按目标头顶位置查询，不依赖 Tween 当前帧或渲染插值位置。沿各轴裁剪，保留沿障碍平行和反向离开的运动；Trigger、不碰撞的 Layer、搬运物自身及当前携带者排除。稠密查询溢出保守阻挡。
- 不启用搬运物 Collider，不给玩家附加临时刚体或父子关系；原 `ICarryMotionParticipant` 挂起/恢复、放下检测、R/死亡/离区逻辑不变。
- 每次拿起重新缓存碰撞体及原始启用状态，避免生长/重置后沿用旧缓存。
- 这是移动扫掠，不会自动把一开始已嵌入墙体的作者物体推出墙；放置时仍须保持空间合法。

## 世界提示

三色 ContactCube、旧能源兼容预制体和万能方块统一绑定 `Cubic_11 SDF`。新挂载组件默认引用同字体；未配置时优先使用场景 `WordNoteUI.DefaultFont`。

队列首项提示使用实体包围中心，独立世界字号，不继承合并物体的拉伸。按现有规范显示黑字白细描边、2.7 字号、最高排序/Always 深度。可见期间刷新设备和重绑定文案，只有首项显示。

屏幕提示仍全部经 `PlayerInputDriver` → `WordNoteUI`，不新增独立业务 Canvas。正式 Gaming 使用 SharedGameUI 中的组件，Test 沿用已有组件，Pick 新增独立的同型 WordNote_UI；每个场景恰好一份，不添加 EventSystem。提示服务晚于玩家初始化时，驻留搬运/复活消息允许下一帧重试。

## 回归

`Project.ContactCubes.Editor.CarryCollisionVerification.Run` 仅允许在 `Library/FlowRetirementProbe` 隔离项目运行。验证三场景接线、长块字体/尺寸/中心、触发器、墙/顶面、反向离开以及真实 PlayerController 冲刺撞头、复位恢复。不可用该命令覆盖作者场景。
