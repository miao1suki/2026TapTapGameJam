# 机关信号与跳跃 API

开发者操作：[机关与跳跃](../../Developer/MECHANISMS_AND_JUMP.md)。颜色和重置规范仍以 COLOR_BLOCKS_API / ROOM_LIFECYCLE_API 为准。

## 所有权

按钮统一由脚部/方块底部压力触发。`ButtonLockFeature` 不再实现 `IInteractionTarget`，兼容 `CanInteract/TryInteract` 固定返回 false；不可通过 F 触发。默认一次保持；参数 `mode=Held` 时松开关闭并随区域/R重置，一次保持不因区域/R重置。门按钮和红色信号按钮共用压力判定；红色按钮仍遵守颜色解锁。`Toggle/SetOn` 只供信号/调试调用，不接玩家按键。

`ContactCube` 固定身份，红块实现 `IEnergySource`；`MechanismBaseFeature` 查询相邻能源并输出 `IMechanismSignalSource`，无能源独占绑定。不要改写旧能源的运动控制或直接写玩家坐标。

`MovingPlatformFeature.BindBase(MechanismBaseFeature)` 将驱动改为 SignalControlled，替换信号来源并安全重新订阅；传 null 解绑并停止。`IsMoving`、`PreviewStart/PreviewEnd`、`LinkedControls` 只读。既有 Any/All 信号组合仍可用，默认编辑器快捷绑定仅一个基座。

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

`PlayerController.CalculateGridRiseSpeed(cells)` 根据网格、重力和固定步长计算所需初速；普通跳跃 `jumpRiseCells` 默认4。玩家胶囊在 Awake 按网格规格归一到1×1，RuntimePlayer 使用一格占位网格。自定义视觉须保持自己尺寸，不再假定玩家两格高。

`BouncePlantFeature.GetRiseCells(fallCells, jumpHeld)` 返回3或8；严格大于4格落差才满足高跳门槛。植物回弹用精确高度路径，不再叠加水柱/蒸汽的额外环境弹射速度；其他环境弹射保持原规则。

`GrantCarryPlantJump(cells)` 赋予一次携带跳跃，`CanCarryPlantJump` 由输入驱动读取。放下/新搬运/死亡重置清空。没有改变 F/Q/E/Jump 的项目绑定，后续输入代码不能一律屏蔽携带时的 Jump，应按此门控放行。

## 验证入口

仅在 Library/FlowRetirementProbe 隔离工程运行：MechanismAssetsUpgrade.RunCranks（曲柄资源升级）、MechanismGameplayVerification.Run（真实物理玩法）、PlanningMechanismVerification.Run（持久绑定）、PlanningCrankVerification.Run（自动创建/多平台/实际合并与解除/重建引用）。这些入口不会自动修改用户场景，不代替窗口手感或打包测试。
