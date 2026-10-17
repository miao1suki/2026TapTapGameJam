# 机关信号与跳跃 API

开发者操作：[机关与跳跃](../../Developer/MECHANISMS_AND_JUMP.md)。颜色和重置规范仍以 COLOR_BLOCKS_API / ROOM_LIFECYCLE_API 为准。

## 所有权

按钮统一由脚部/方块底部压力触发。`ButtonLockFeature` 不再实现 `IInteractionTarget`，兼容 `CanInteract/TryInteract` 固定返回 false；不可通过 F 触发。默认一次保持；参数 `mode=Held` 时松开关闭并随区域/R重置，一次保持不因区域/R重置。门按钮和红色信号按钮共用压力判定；红色按钮仍遵守颜色解锁。`Toggle/SetOn` 只供信号/调试调用，不接玩家按键。

`ContactCube` 固定身份，红块实现 `IEnergySource`；`MechanismBaseFeature` 查询相邻能源并输出 `IMechanismSignalSource`，无能源独占绑定。不要改写旧能源的运动控制或直接写玩家坐标。

`MovingPlatformFeature.BindBase(MechanismBaseFeature)` 将驱动改为 SignalControlled，替换信号来源并安全重新订阅；传 null 解绑，按所选模式原地停止或回原位。`IsMoving`、`PreviewStart/PreviewEnd`、`LinkedControls` 只读。既有 Any/All 信号组合仍可用，默认编辑器快捷绑定仅一个基座。

`signalMotion`独立于路线几何：`MovingPlatformSignalMotion.PoweredPingPong=0`兼容旧存档（充能往返、断电停留），`PoweredEndpoint=1`充能至`Path.FirstEnd`并保持、断电回作者origin。仅SignalControlled生效，曲柄/自动不受影响。每次目标改变从当前格空间位置建立新缓动段，按剩余路程比例计时，复用既有扫掠检测与QueuePlatformDelta承载；遇实体阻挡不累计进度，清除后重试，不反向。该模式忽略端点停留，反复切换/Reset不瞬移，参数经通用组件覆盖持久保存，不增加规划版本。`SignalMotion`只读；解绑无信号时终点模式同样执行回原位，旧模式才原地停止。

移动路线通过 `MovingPlatformPath.Resolve` 共用格空间计算。`pathMode=Legacy(0)`保留正数`travelBlocks`、`route`和`reverseRoute`（false右/上，true左/下）；旧序列化缺字段时仍为0，不自动迁移。Right/Left/Up/Down(1～4)从作者位置到对应`rightDistance/leftDistance/upDistance/downDistance`；HorizontalRange/VerticalRange(5～6)使用双侧距离、`centerOffset`和`startNegative`。偏移沿路线正轴，以作者位置为基准，限制在[-正向距离,负向距离]内，保证作者位置在区间。负值距离按0；双侧均0静止。距离均以既有格尺寸换算，不受合并物体自身拉伸影响。

信号/自动模式首次从作者位置到指定端点，随后完整两端往返；每段独立缓动，初始短段按总行程比例计时，遇实体阻挡重新从当前位置反向，不跃迁端点。曲柄0→1仍是作者位置到`Path.FirstEnd`，不会将0%映射成负端点而造成解锁瞬移。ResetForRoom恢复作者位置、首程和停留状态，绑定/解锁保留。

`Path`只读格空间路径，`PreviewOrigin`为作者位置，`PreviewStart/PreviewEnd`为路线绘制两端（单向保留从作者位置到目标端的方向），`PreviewCenter`为有效中心、`PreviewFirstTarget`为首程目标。Scene Gizmo和PlanningPlatformPathUtility均走同一Resolve；规划读取预制体/栏目或实例覆盖，包括reverseRoute，最后只翻转屏幕Y。不要再次单独实现方向推导。参数元数据的VisibleWhenValues支持枚举多值显隐；Inspector与参数窗口共享此条件。

`CrankPlatformFeature` 是独立控制器，仅写进度和信号，不持有平台模型，不写平台 Transform。`BindBase(source)` 配置制动；`TryInteract/HoldInteract/EndInteract` 复用交互契约。`Progress` 为0～1，`Platforms` 为消费者注册的只读运行时集合，不是持久引用列表。任一平台载人加快共享进度回落，基座通电冻结共享进度。

`MovingPlatformFeature.BindCrank(CrankPlatformFeature)` 切换为 CrankControlled，取消旧基座信号订阅并更换曲柄注册；null 解绑并停止。消费者在 Attach/Detach 成对注册，自己按作者位置、路线和 travelBlocks 在 FixedUpdate 移动，位移通过 `PlayerController.QueuePlatformDelta` 交给玩家；不是满进度后才触发普通往返。`CrankController`、`IsCrankControlled`、`HasStandingPlayer` 只读。平台停用/删除不遗留注册，重建重新绑定新实例。`SnapToCrankProgress` 仅用于显式设置/重置，不用于逐帧或玩家移动。`LiftPlatform` 仅为旧调用兼容返回首个注册平台，不作为新集成入口。

机关通过现有 ColorObject、BlockAbilityHost/BlockRuntime 按固定颜色解锁。正式材质配置为真实占位材质，不使用 ColorCatalog 的编辑边框识别材质作为解锁外观。

## 持久绑定

PlanningBox 保存 `mechanismBaseId` 和 `mechanismBaseCellOffset`。后者是相对目标基础对象左下格的偏移，支持一次批量放置多个基座后选具体格。`PlanningMechanismUtility.BindScene` 在全量构建和增量应用结束后按 PlanningBoxId/PlanningLocalCell 重连；房间坐标相同不造成串绑。局部擦除批量基座时，RemapCarvedSource 将剩余格的绑定转移到新片段，擦除被绑定格则清除关系。拆分批量平台保留其原基座关系。不用 instanceID / 临时 Unity 引用保存地图关系。复制或合并地图对象时应重映射来源 ID（否则需重新绑定），不能把旧源 ID 指向不相关副本。

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
