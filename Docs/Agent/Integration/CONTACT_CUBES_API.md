# 独立三色接触方块 API

源码：`Code/Systems/ContactCubes`。预制体：`Content/ContactCubes/Red_ContactCube`、`Green_ContactCube`、`Blue_ContactCube`。道具栏目显示“红/绿/蓝色接触方块”，区别于已有固定颜色功能物体。

## 所有权

- 三种固定身份，不实现 IColorApplicationTarget，不接受玩家点击染色或万能方块改色。预制体挂 ColorObject，仅用于基础色钥匙门控、共享外观/HSV 与搬运激活；初始未解锁，解锁对应颜色后才参与反应、搬运和供能。身份不会随解锁改变。三个预制体均挂 CarryableFeature，使用既有玩家搬运流程；黏住尚未接入。预制体已有动态 Rigidbody、重力和 ContactCubeMotion，限制 Z 位移和旋转，适配横版运动。
- ContactCube 只拥有状态、长度、实体子物体尺寸及反应水柱；不能由移动程序缩放根物体表达生长。根坐标是原始一格中心，增长保留此锚点。尺寸读取 GridCellSizeUtility，一体模型/BoxCollider 伸长，不生成节段。不可把它组为旧藤蔓母根。
- 固定类型字段只供预制体作者配置，不提供运行时切色 API。Length、State、ColorId、GrowthDirection 只读，LengthChanged / StateChanged 提供状态通知。
- `IsUnlocked` 只读。红块实现 `IEnergySource.IsEnergyAvailable`，需已解锁、实体启用且未处于搬运动画；任意红块可供能任意基座，不持久绑定特定红块。旧 Red_Battery 预制体保留 GUID，但已替换为红色 ContactCube 及运动/重置组件；新放置统一使用 Red_ContactCube。
- 绿色钩子挂接只暂停红块主动接触反应，不暂停被动能源输出。拉到基座后，即使未按跳跃/交互松钩也立即按原接触规则供能；不能把`hookSuspended`当作能源不可用条件。搬运、锁色、禁用实体仍排除。

## 有限状态机

| 绿色当前接触 | 状态 | 行为 |
|---|---|---|
| 无红无蓝 | Idle | 保留长度 |
| 仅蓝 | Growing | 默认每秒长一格，到 maximumLength |
| 仅红 | Shrinking | 默认每秒短一格，最小一格 |
| 红蓝同时 | Locked | 保留当前长度，暂停变化 |

接触使用作者逻辑OBB加0.01格容差及双方真实contactOffset，NonAlloc候选查询合并同物体多个碰撞体；不只看世界AABB或必须等实体穿透。碰撞回调仅补充非默认接触偏移的候选，每步仍严格几何复核，不用计时续租保留脱离的接触。红蓝运动器的`LogicalContactShapeSize`保留完整作者尺寸，不受单格坑物理避让皮肤影响；候选召回余量不扩大真实接受范围或既有物体的染色范围。禁用碰撞箱暂停反应并清空接触、水柱。状态/主蓝邻居变化后重新计时，不累计锁定期时间。

首次从一格长出时方向为蓝块中心→绿块根中心的反向远离方向（即远离蓝块）；一旦长出固定轴，避免切换邻居时甩动现有长块。完全缩回一格后可重新选方向。多个蓝邻居按本次运行实例优先级选择，不把 instanceID 存入场景或存档；方块身份/颜色不改变。

蓝块与每个接触红块各拥有一个水柱：方向为红块→蓝块，起点蓝块根中心，射程/宽度按格，速度可配。离开、禁用、重置立即停止并清理，不向自身或激发本水柱的红块反馈自推。其余红绿蓝方块可受推。

`CubeWaterJet.FixedUpdate`自身也检查当前蓝/红资格及真实接触，不能依赖两个组件执行先后而多推一个物理步。红块挂钩时仍可被蓝块作为接触/被动能量来源；蓝块自身挂钩仍暂停主动反应。真实落稳水柱不得因支撑探针过长、把蓝块锁在空中离开接触壳而断流。

## 运动对接

`ContactCube.PushRequested(Component source, Vector3 velocity, float deltaTime)`：每个物理步发出速度请求；离开水柱时发出零速度。ContactCubeMotion 按 source 保存并相加速度源，统一写方块 Rigidbody。反应组件不写根位移。后续如替换运动控制器，先禁用 ContactCubeMotion，不能同时运行两个刚体写入者。

落稳需要竖直速度小于容差且下方有非玩家支撑；已落稳且无水柱时，只要支撑仍存在便保留锁定，避免接触抖动解除锁定。支撑通过固定厚度底部探针，不随伸长高度扩大底部间隙。红/蓝空中仍可忽略玩家侧面/下方实体，但从上面落脚且正在接近顶面时就恢复该玩家碰撞，不必等待 `IsSettled`；避免填坑方块松钩/换目标后的落稳空窗让玩家穿透进岩浆。已由其他系统设置的忽略对不擅自接管。绿块及伸长部分始终保持实体碰撞，没有水柱时始终锁水平位置，落稳同时锁竖直位置，玩家不能推走且可以站立。有效水柱速度源可释放移动锁。水平喷出初始默认 0.25 秒关闭重力，持续喷射不无限续期；之后恢复重力。IsSettled / GravityProtected / IsSuspended 只读。

2026-10-18：可动红蓝实体仅横向每侧留0.02格物理余量，contactOffset不超过0.001格；美术与逻辑占格仍完整1格，绿色固定攀爬/生长体不改。重复挂钩、搬运、重置和启用幂等，不累积缩小；松钩不扩箱把井内方块弹出。足底接受距离收紧为0.01格加自身contactOffset，并尊重局部IgnoreCollision。配合绿色拉取的窄坑制动，等宽1格坑可自然落入，详见[绿钩物理契约](GREEN_HOOK_API.md)。

`CarryableFeature` 通过可选 `ICarryMotionParticipant` 对接 ContactCubeMotion：`SetCarrySuspended(true)` 立即清除接触/水柱，保留长度，清除速度并挂起刚体；CarryTargetMotion 独占动画位姿并关闭 Collider。放下/复位动画完成或 Cancel 时回调解除挂起，同步 Rigidbody 姿态、恢复重力与碰撞。房间重置先 Cancel，再恢复作者姿态/长度；不能同时运行两个刚体写入者。

普通藤蔓生长时可通过 `VineGrowthLift` 短时托举顶部重力方块及其堆叠。ContactCubeMotion.FixedUpdate 在租约期间跳过重力/落稳约束写入并保留玩家碰撞；搬运、绿钩子接管、禁用和房间重置先Cancel租约，避免多写入者或错误保存临时无重力状态。颜色反应仍保留，红块接触藤蔓仍会燃烧。详见 [藤蔓托举](../../Developer/VINE_GROWTH_LIFT.md)。

`CanPlaceCarriedAt(worldPosition)` 校验平移后的完整实体 OBB，含伸长绿块，不只检查根的一格；拒绝墙体、玩家实体及查询缓冲溢出的落点。来源房间校验仍由 CarryableFeature 调用 CanPlaceInAuthoredRegion，区域根中心规则不变。玩家选择目标沿用根格相邻规则，未新增按长块任意节段拾取。输入、提示和头顶飞行动画全部复用原搬运系统。

绿色实体挂 HorizontalCubeVine：母块与伸长实体落稳后两侧可通过 EnterClimb/ExitClimb 攀爬，顶部高度随生长更新；横向长出部分上方脚部接触或下方头部接触会续租玩家横向攀附，原始一格不进入横向攀附，脱离自动清除。`PlayerController.SetHorizontalVineSurface(Component source, float rootY, float speed)` / `ClearHorizontalVineSurface(source)` 只发送目标高度和移动速度，由玩家自己的物理步处理。新增状态 HorizontalClimbing=6，不改原枚举编号和输入键位；左右缓行、维持高度，跳跃操作脱手下落并短暂禁止重吸附。租约超时、来源禁用、玩家重置都会清除。

向上外角保护由 `PlayerController.ProtectClimbCorner(source,bounds,speed,wrapEntireEdge)` 统一处理。横向长绿块保留完整上下绕边；单格/竖向绿块以及普通藤蔓每个真实节段调用 `wrapEntireEdge:false`，只在接近露出的顶部、向上或向内输入时交接到顶面。顶部探针检查真实实体（包括同根上方节段），避免在链条中间提前翻顶；路径逐步提交刚体速度，不能穿墙/瞬移。横爬/保护期间跳跃脱手并短暂禁止重吸附；来源禁用、重置、死亡取消所有权。`IsClimbCornerProtectedBy(source)` 供节段保持并清理自己的交接，不保留无效全局抓附。

其他受推对象可实现 `ICubeJetPushReceiver.SetCubeJetVelocity(source, velocity, deltaTime)`。零值表示解除该来源。玩家已有适配走 EnterWater/SetWaterVelocity/ExitWater，由 PlayerController 在自己的物理步处理水柱推动，不写玩家 Transform 或刚体。

## 接触既有颜色物体

绿钩子拉红/蓝块时，`ContactCubeMotion`继续独占刚体、暂停接触反应，但不关闭重力。横向命令只控制X，保留碰撞解算后的Y速度；零命令停止X并继续自然下落。四向限制指主动拉力方向，不是把总速度锁为四向。入钩先取消`VineGrowthLift`租约，再移除落稳锁；不能保留托举临时关闭的重力或暂停控制器遗留的FreezeY。主动结束、禁用、搬运和房间重置恢复既有控制器；绿色块荡绳不使用这条拉物路径。

方块触碰已有 IColorApplicationTarget，转发固定 ColorId，通过目标 CanApplyColor/ApplyColor 使用与玩家相同的规则，actor 为方块自身；仍服从目标激活与颜色解锁条件。一次持续接触中每个成功目标只执行一次，失败目标后续可重试（例如刚解锁）；分离后可再触发。三种新方块彼此只走上表 FSM，不走此广播接口，不形成递归反应。

## 区域生命周期

预制体有 RoomResetObject，规划生成按既有区域绑定。ResetForRoom 清空接触/水柱/计时并恢复初始长度、初始轴，位置由 RoomResetObject 统一恢复。不清除钥匙进度，不销毁作者方块。

原始根位姿在 Awake、重力前捕获，不随落地/搬运重采样。RoomLifecycleManager 按固定布置区域约束方块根中心，越界恢复原位置原状态；玩家跨区取消携带。后续搬运程序放下前调用 CanPlaceInAuthoredRegion，不得把搬运目的区域写成新的 RegionId。

## 验证

ContactCubeAssets.Build 与 ContactCubeVerification.Run 只用于 Library/FlowRetirementProbe 隔离项目，不覆盖用户场景。nographics 验证不代替真实画面/打包验收。

`Project.ContactCubes.Editor.ContactCubeJetContactAudit.Run`仅静音隔离运行，检查真实掉落/接触/落稳180物理步保持同一水柱、不同接触偏移、横向作者邻接、旋转OBB、实际脱离立即清力、锁色/携带/挂钩资格与重置。不要把静态贴近两块物体冒充真实掉落回归。

`Project.Player.Editor.ClimbCornerVerification.Run` 强制隔离项目及 `-noaudio`，以标准玩家和真实绿块/藤蔓预制体验证双侧向上翻角、单格/横向/竖向绿块、链条露顶、顶端阻挡、跳跃脱手、来源禁用与原有上下绕边。`ClimbCornerLiveVerification.Run` 另跑真实 Update/FixedUpdate，检查八组两侧翻顶后租约过期仍稳定站在真实顶面。脚本只使用空测试场景，不保存作者关卡。
