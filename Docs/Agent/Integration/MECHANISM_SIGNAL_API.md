# 机关信号与跳跃 API

开发者操作：[机关与跳跃](../../Developer/MECHANISMS_AND_JUMP.md)。颜色和重置规范仍以 COLOR_BLOCKS_API / ROOM_LIFECYCLE_API 为准。

## 可选基座与平台 Timeline

`MechanismBaseFeature.timelineTrigger` 使用 `MechanismBaseTimelineTrigger`：`Disabled=0` 默认关闭，`Powered=1` 收到充能，`Unpowered=2` 解除充能，`SignalChanged=3` 两种边沿均播放。只响应实际相邻能源导致的信号变化；持续扫描不重播，R/离区恢复过程及恢复后的首次能源基线扫描不播放镜头。基座仍只检测能源、广播信号，不持久保存或搜索平台。`TimelineTrigger / PlatformTimelineMode / IsTimelinePlaying` 是只读调试入口。

平台的 `timelineOrder` 为演出顺序整数，越小越先播放。同一信号边沿中，基座镜头先入队，然后按序播放真正发生所选充能/断电边沿的平台。`MechanismBaseFeature.platformTimelineMode`：`AllOrdered=0` 默认播放全部符合条件的平台；`FirstOnly=1` 仅播放顺序最小的符合条件平台，其他平台的该次边沿会被消费而非在下一物理步补播。同序号按层级顺序；需要稳定的精确顺序时请设置不同整数。基座自身演出关闭时，平台仍可单独启用；选择仅首个平台不会自动启用任何演出。

平台“开始移动”“到达端点”保留原来真实运动边沿，不提前伪造运动/到达；请求同样进入共享串行队列。同一帧同一基座批次中尚未播放的请求按 `timelineOrder` 排序；若不同平台在不同时刻才到达端点，则按实际请求时机入队，不为等待另一个尚未发生的事件而冻结队列。该两种运动事件选“仅首个平台”时，取本基座已启用演出的有效平台中序号最小者。多来源平台仍按原 Any/All 结果判断充能边沿，不会因为某个基座变化而伪造一次平台充能。

`MechanismTimelineSequence` 只保存消费者 Attach/订阅与 Detach/解绑时登记的运行时关系；不是另一套地图绑定，也不逐帧全场搜索。信号反向变化会取消该基座上一次未完成的镜头批次，再按新边沿建立批次。基座重置/停用取消自己及由其触发的平台镜头；平台重置/解绑/停用只取消自己的请求。平台物理运动、承载与信号输出不等待镜头队列，也不因“仅首个镜头”而停止其他平台。

`MovingPlatformFeature.timelineTrigger` 使用 `MovingPlatformTimelineTrigger`：`Disabled=0` 默认关闭（旧场景/旧存档缺字段同样关闭），`Powered=1` 仅信号驱动从未充能到充能时，`MovementStarted=2` 实际停止到开始移动时，`EndpointReached=3` 实际到达行程端点时，新增 `Unpowered=4` 仅信号驱动解除充能时。旧枚举数值不变。停留端点、每个 FixedUpdate 的持续充能/移动不会重复创建演出。已在排队/播放的同平台镜头不会重叠，平台运动与载人仍走既有路径，不等待镜头完成才移动。

显式开启后复用 `MechanismCameraShot`，聚焦该平台（锚点随平台移动），可以调移向、停留、返回时间及是否锁定玩家。门、基座、平台共用串行请求队列；等待期间不先锁玩家、不开 Director，轮到请求且旧 Timeline 释放相机后才取得自己的玩家锁，返回结束或取消后仅释放自己的锁。外部剧情 Timeline 忙碌超过15秒时跳过该镜头，不阻塞机关；队列中正常等待其他机关镜头不消耗这个15秒。丢失相机/管理器、协程异常、宿主失效时清理自有 Director/动态 Timeline/锚点并归还队列。不使用全局强制解锁或强制释放其他剧情的相机句柄。

默认关闭时不创建会话、Director或玩家锁；区域重置、停用/解绑宿主、运行中关闭演出均清理本会话。不改普通开关、路径端点、曲柄进度或区域重置规则。`TimelineTrigger / TimelineOrder / IsTimelinePlaying` 为只读调试入口，字段均有 BlockParameter，Inspector与关卡参数窗口共用，沿用组件覆盖 JSON 持久化，不引入 instanceID。

门使用独立 `DoorTimelineTrigger`，默认也关闭，可选每次按钮真实按下/全部按钮达成开门。接入见 [DOOR_API](DOOR_API.md)。上述是已有门镜头的可选复用，不是假定能自动绑定任意外部 Timeline 资源；自定义剧情继续使用已有 Timeline 触发器及明确轨道绑定。

场景交付仍必须成套提交 Scene、同名可编辑 `.planning.json`、双方 `.meta` 和依赖，不能只交生成物。专项入口 `OptionalMechanismTimelineAudit.Run` 保留门/平台旧行为覆盖；新增 `MechanismSignalTimelineAudit.Run` 覆盖实际能源邻接、基座与多平台充能/断电顺序、仅首个、运行中取消及组件参数 JSON 往返。由整合者在 `Library/FlowRetirementProbe`（新专项也允许 `Library/MechanismSignalProbe`）静音运行，不能在用户工程直接执行。

## 自动运输站顶物体

移动平台默认运输真正落在其顶面的三色接触方块和基座，不需要策划增加 `PlatformMount`。局部顶面检测支持实际叠放的物体，以及站在这些物体上的玩家；侧边擦碰、跳起、正在手持、绿钩主动拉取、藤蔓顶升、作者固定或显式附着中的物体不重复接管。平台每个物理步只查询自身顶面及实际货物上方，不扫描全图、不创建逐帧数组。

`PlatformLoadTransport` 只计算接触、完整位移的碰撞与区域许可，再请求 `ContactCubeMotion`／`MechanismBaseFeature` 应用支撑位移；不写它们的 Transform／刚体、不改作者位置、重力开关或原始锁轴。基座和供能红块一起随动，真实相邻供能继续工作。货物或其上玩家将碰到墙、顶板、作者区域边界时，平台停止／按原往返规则反向，不能继续挤压或穿透。清空接触集合不会改变房间重置所有权；平台、方块、基座分别走原恢复链。

`PlatformLoadTransportAudit.Run` 只允许静音隔离 FlowRetirementProbe；覆盖有／无重力方块及基座的右／上／下运输、堆叠、防顶板挤压，以及真实红块供能基座一起落到绑定平台后持续供能行至终点。普通运输与下面的作者预设附着是两种不同关系。

## 可选平台附着物

`PlatformMount` 保存一个 `MovingPlatformFeature` 与平台局部锚点（`Bind` 时通过 InverseTransformPoint 捕获），不将物体设为平台子级，避免合并平台的缩放拉伸物体。挂载协调器使用物理执行顺序200，在现有平台更新之后处理（包括旧 MonoImporter 的显式顺序覆盖），不修改旧平台执行顺序。曲柄等非接触方块由挂载组件跟随；接触方块由协调器请求 `ContactCubeMotion.ApplyPlatformMount`，仍只由方块自身运动器写入刚体，挂载时锁定位移并暂停重力，保留颜色接触反应。

成功 `CarryableFeature.BeginCarry` 后才 `DetachForCarry`；拒绝或失败的搬起不脱离，放下保持脱离直至房间重置。挂载中临时使用无重力、无插值的运动学刚体，不用动态 FreezeAll 抵抗平台位移；搬起、解除挂载或停用后恢复原物理／插值配置，再走既有搬运或普通运动。挂载组件实现 `IRoomColorResettable`，恢复作者附着关系；不改变曲柄转动／回弹速率或平台既有载人。无该组件的物体走原路径。`Platform / IsMounted / IsDetached / TargetPosition` 只读，中文 UI Toolkit Inspector 显示只读绑定及运行状态。

规划通过 `PlanningBox.platformMountId` 稳定绑定平台；生成后接线、回采、片段 ID 重映射、合并／拆分和删除来源清理配套维护。没有绑定默认为空，不为旧 Scene 加挂载、不重建旧关卡。此关系不允许平台和附着物穿过生成时的实体占格限制；R3 初始平台位于岩浆上方。

## 所有权

按钮统一由脚部/方块底部压力触发。`ButtonLockFeature` 不再实现 `IInteractionTarget`，兼容 `CanInteract/TryInteract` 固定返回 false；不可通过 F 触发。默认一次保持；参数 `mode=Held` 时松开关闭并随区域/R重置，一次保持不因区域/R重置。门按钮和红色信号按钮共用压力判定；红色按钮仍遵守颜色解锁。`Toggle/SetOn` 只供信号/调试调用，不接玩家按键。

`ContactCube` 固定身份，红块实现 `IEnergySource`；`MechanismBaseFeature` 查询相邻能源并输出 `IMechanismSignalSource`，无能源独占绑定。不要改写旧能源的运动控制或直接写玩家坐标。

`MechanismBaseFeature.IsPoweredBy(IEnergySource)` 只读验证该确切能源是否按已有方向/距离规则给此已启用、已通电基座供能，不把其他红块的信号冒认为当前来源。绿钩拉红块或基座**新接通**时暂停拉取、保留绳子并放行普通行走；再次点击可继续，已有接触作为新拉取的基线，不立即重停。此策略不改变基座/平台信号与碰撞体、不加隐式Timeline，见 GREEN_HOOK_API。

`MovingPlatformFeature.BindBase(MechanismBaseFeature)` 将驱动改为 SignalControlled，替换信号来源并安全重新订阅；传 null 解绑，按所选模式原地停止或回原位。`BindSignalSources(IReadOnlyList<MonoBehaviour>)` 可同时绑定基座、`ButtonLockFeature` 与 `DoorButton`。`DoorButton` 实现 `IMechanismSignalSource`，按下/释放广播 `SignalChanged`，同时保留门的按钮状态边沿和默认关闭的可选演出。`IsMoving`、`PreviewStart/PreviewEnd`、`LinkedControls` 只读；既有 Any/All 信号组合继续生效。

`signalMotion`独立于路线几何：`MovingPlatformSignalMotion.PoweredPingPong=0`兼容旧存档（充能往返、断电停留），`PoweredEndpoint=1`充能至`Path.FirstEnd`并保持、断电回作者origin。仅SignalControlled生效，曲柄/自动不受影响。每次目标改变从当前格空间位置建立新缓动段，按剩余路程比例计时，复用既有扫掠检测与QueuePlatformDelta承载；遇实体阻挡不累计进度，清除后重试，不反向。该模式忽略端点停留，反复切换/Reset不瞬移，参数经通用组件覆盖持久保存，不增加规划版本。`SignalMotion`只读；解绑无信号时终点模式同样执行回原位，旧模式才原地停止。

移动路线通过 `MovingPlatformPath.Resolve` 共用格空间计算。`pathMode=Legacy(0)`保留正数`travelBlocks`、`route`和`reverseRoute`（false右/上，true左/下）；旧序列化缺字段时仍为0，不自动迁移。Right/Left/Up/Down(1～4)从作者位置到对应`rightDistance/leftDistance/upDistance/downDistance`；HorizontalRange/VerticalRange(5～6)使用双侧距离、`centerOffset`和`startNegative`。偏移沿路线正轴，以作者位置为基准，限制在[-正向距离,负向距离]内，保证作者位置在区间。负值距离按0；双侧均0静止。距离均以既有格尺寸换算，不受合并物体自身拉伸影响。

信号/自动模式首次从作者位置到指定端点，随后完整两端往返；每段独立缓动，初始短段按总行程比例计时，遇实体阻挡重新从当前位置反向，不跃迁端点。曲柄0→1仍是作者位置到`Path.FirstEnd`，不会将0%映射成负端点而造成解锁瞬移。ResetForRoom恢复作者位置、首程和停留状态，绑定/解锁保留。

`Path`只读格空间路径，`PreviewOrigin`为作者位置，`PreviewStart/PreviewEnd`为路线绘制两端（单向保留从作者位置到目标端的方向），`PreviewCenter`为有效中心、`PreviewFirstTarget`为首程目标。Scene Gizmo和PlanningPlatformPathUtility均走同一Resolve；规划读取预制体/栏目或实例覆盖，包括reverseRoute，最后只翻转屏幕Y。不要再次单独实现方向推导。参数元数据的VisibleWhenValues支持枚举多值显隐；Inspector与参数窗口共享此条件。

`CrankPlatformFeature` 是独立控制器，仅写进度和信号，不持有平台模型，不写平台 Transform。`BindBase(source)` 配置制动；`TryInteract/HoldInteract/EndInteract` 复用交互契约。`Progress` 为0～1，`Platforms` 为消费者注册的只读运行时集合，不是持久引用列表。任一平台载人加快共享进度回落，基座通电冻结共享进度。

`MovingPlatformFeature.BindCrank(CrankPlatformFeature)` 切换为 CrankControlled，取消旧基座信号订阅并更换曲柄注册；null 解绑并停止。消费者在 Attach/Detach 成对注册，自己按作者位置、路线和 travelBlocks 在 FixedUpdate 移动，位移通过 `PlayerController.QueuePlatformDelta` 交给玩家；不是满进度后才触发普通往返。`CrankController`、`IsCrankControlled`、`HasStandingPlayer` 只读。平台停用/删除不遗留注册，重建重新绑定新实例。`SnapToCrankProgress` 仅用于显式设置/重置，不用于逐帧或玩家移动。`LiftPlatform` 仅为旧调用兼容返回首个注册平台，不作为新集成入口。

普通移动平台也可通过统一的“绑定信号源”参数选择任意曲柄，不再只限于“曲柄升降平台”预制体。编辑器识别到曲柄目标时写入 `crankOwnerId` 并调用 `BindCrank`；按钮目标则追加到 `mechanismButtonIds`。`crankOwnerId` 有值时场景重连优先进入曲柄控制，基座和按钮 ID 保留但不参与当前运动；清空信号源或解除曲柄后恢复为 SignalControlled，并按已保留的基座/按钮来源重新计算。

机关通过现有 ColorObject、BlockAbilityHost/BlockRuntime 按固定颜色解锁。正式材质配置为真实占位材质，不使用 ColorCatalog 的编辑边框识别材质作为解锁外观。

## 持久绑定

### 多位置曲柄共享进度

`CrankPlatformFeature.BindSharedController(owner)` 把当前曲柄作为另一独立曲柄的操作位置；`SharedController` 只读。两处交互仍分别检查玩家到本地曲柄的距离，只有 owner 每帧推进一次进度；另一处 Progress/信号跟随 owner，切换位置不清零、不把两份回弹相加。松手等待、每秒转动/回弹使用 owner 配置；制动汇总 owner 及所有有效副柄各自的 `BrakeBase`，任一通电即暂停整组前进和回弹，全部解除后延续原进度。保留每个柄独立的持久绑定，停用、解绑或销毁的副柄不再制动旧组；载人检测包括组内平台。空绑定恢复独立控制，拒绝自身、串联和循环共享；停用释放本地交互，区域重置恢复组进度。

`SharedCrankBrakeAudit.Run` 仅允许静音 FlowRetirementProbe，验证主/副柄制动、恢复、停用重启、解绑重绑和销毁；真实平台移动仍由R1 Play专项验证，不把控制器夹具当作完整通关。

复用 `PlanningBox.crankOwnerId`：用于平台时仍表示控制曲柄，用于曲柄时表示共享进度 owner。SceneBuilder 按稳定 ID 重连并记录 Prefab 覆盖，既有片段 ID 重映射和缺失来源清理同样生效，不新增存档字段或 instanceID。融合编辑器曲柄参数窗口提供“绑定共享曲柄／解除共享曲柄”；主、从应放在同一重置区域。Scene 手工绑定不替代规划文档绑定。

PlanningBox 保存 `mechanismBaseId`、`mechanismBaseCellOffset` 和 `mechanismButtonIds`。基座偏移是相对目标基础对象左下格的偏移，支持一次批量放置多个基座后选具体格；按钮来源列表通过稳定 PlanningBoxId 保存，允许同一平台绑定任意数量的门按钮/红色按钮。旧单按钮字段会在加载与持久化时迁移到列表。`PlanningMechanismUtility.BindScene` 在全量构建和增量应用结束后按 PlanningBoxId/PlanningLocalCell 重连；房间坐标相同不造成串绑。局部擦除批量基座时，RemapCarvedSource 将剩余格的绑定转移到新片段，擦除被绑定格则清除关系。不用 instanceID / 临时 Unity 引用保存地图关系。复制、片段导入导出、合并/拆分和孤儿清理同步重映射或清理按钮列表，不能把旧源 ID 指向不相关副本。

曲柄平台另存 `PlanningBox.crankOwnerId`；`initialCrankPlatformCreated` 记录首次配套创建，删除平台不自动重生。曲柄单实例，不可合并；平台可合并。CanMerge 要求各平台相同控制器；合并结果保留曲柄 ID，解除时以当前合并体绑定覆盖旧快照绑定。基座合并与解除按具体被绑定格重映射，不残留被删除来源 ID。BindScene 在每次构建/增量结束按持久 ID 重建并记录 Prefab override，保存 Scene 后可在运行时独立加载。不依赖 instanceID。

Scene 手工绑定写入组件序列化引用并支持 Undo；它不反向更改地图 JSON。地图生成物应优先使用参数模式的绑定按钮。编辑器 ObjectChangeEvents 仅对新建非规划 Scene 曲柄补建同级平台，跳过预览/Prefab Stage/Play/批处理；规划创建由文档事务负责，避免二次生成。

## 重置与基础道具

沿用 RoomLifecycleManager 两阶段 RestorePose/RestoreState；机关恢复作者姿态与初始行程，基座重新检测能源，不清解锁。区域由世界图定义，不自行按碰撞体推断。旧 Red_Battery 保留 GUID，改为 ContactCube/ContactCubeMotion/RoomResetObject，未来不再添加 EnergyBlockFeature。

`OneWayPlatform` 在物理步对已注册玩家逐对设置 IgnoreCollision，基于脚部位置、上一物理步位置和竖直速度决定顶面支撑，地面查询沿用相同碰撞忽略规则；禁用/重置归还原碰撞设置。不对方块实施穿透。

`HiddenBlock` 没有 Collider，仅显示中性材质，不实现隐藏实体路面。

## 高度接口

`PlayerController.CalculateGridRiseSpeed(cells)` 根据网格、重力和固定步长计算所需初速；普通跳跃的 `jumpRiseCells` 默认4，表示长按满高，`jumpTapRiseCells` 默认1表示点按高度。`JumpHeld` 的按住时长在控制器内部换算为当前目标高度，`jumpHoldAcceleration` 控制按住时追随该高度的柔和程度；松开后停止加力并交还重力，不做瞬间竖直限速。输入驱动仍只传布尔状态，不读取具体设备按键。玩家胶囊在 Awake 按网格规格归一到1×1，RuntimePlayer 使用一格占位网格。自定义视觉须保持自己尺寸，不再假定玩家两格高。

`BouncePlantFeature.GetRiseCells(fallCells, jumpHeld)` 返回3或8；严格大于4格落差才满足高跳门槛。植物回弹用精确高度路径，不再叠加水柱/蒸汽的额外环境弹射速度；其他环境弹射保持原规则。

`GrantCarryPlantJump(cells)` 赋予一次携带跳跃，`CanCarryPlantJump` 由输入驱动读取。放下/新搬运/死亡重置清空。没有改变 F/Q/E/Jump 的项目绑定，后续输入代码不能一律屏蔽携带时的 Jump，应按此门控放行。

## 验证入口

仅在 Library/FlowRetirementProbe 隔离工程运行：MechanismAssetsUpgrade.RunCranks（曲柄资源升级）、MechanismGameplayVerification.Run（真实物理玩法）、PlanningMechanismVerification.Run（持久绑定）、PlanningCrankVerification.Run（自动创建/多平台/实际合并与解除/重建引用）。这些入口不会自动修改用户场景，不代替窗口手感或打包测试。
