# 独立三色接触方块 API

源码：`Code/Systems/ContactCubes`。预制体：`Content/ContactCubes/Red_ContactCube`、`Green_ContactCube`、`Blue_ContactCube`。道具栏目显示“红/绿/蓝色接触方块”，区别于已有固定颜色功能物体。

## 所有权

- 三种固定身份，没有 ColorObject / BlockFeature / IColorApplicationTarget，不受钥匙、HSV、玩家点击染色或万能方块改色。不自动挂 CarryableFeature；搬起、放下、黏住留给后续程序。预制体已有动态 Rigidbody、重力和 ContactCubeMotion，限制 Z 位移和旋转，适配横版运动。
- ContactCube 只拥有状态、长度、实体子物体尺寸及反应水柱；不能由移动程序缩放根物体表达生长。根坐标是原始一格中心，增长保留此锚点。尺寸读取 GridCellSizeUtility，一体模型/BoxCollider 伸长，不生成节段。不可把它组为旧藤蔓母根。
- 固定类型字段只供预制体作者配置，不提供运行时切色 API。Length、State、ColorId、GrowthDirection 只读，LengthChanged / StateChanged 提供状态通知。

## 有限状态机

| 绿色当前接触 | 状态 | 行为 |
|---|---|---|
| 无红无蓝 | Idle | 保留长度 |
| 仅蓝 | Growing | 默认每秒长一格，到 maximumLength |
| 仅红 | Shrinking | 默认每秒短一格，最小一格 |
| 红蓝同时 | Locked | 保留当前长度，暂停变化 |

接触使用实体 OBB + 0.01 格容差的 NonAlloc 查询，合并同物体多个碰撞体；不依赖 Rigidbody 回调，从而可接运动学搬运。禁用碰撞箱暂停反应并清空接触、水柱。状态/主蓝邻居变化后重新计时，不累计锁定期时间。

首次从一格长出时方向为蓝块中心→绿块根中心的反向远离方向（即远离蓝块）；一旦长出固定轴，避免切换邻居时甩动现有长块。完全缩回一格后可重新选方向。多个蓝邻居按本次运行实例优先级选择，不把 instanceID 存入场景或存档；方块身份/颜色不改变。

蓝块与每个接触红块各拥有一个水柱：方向为红块→蓝块，起点蓝块根中心，射程/宽度按格，速度可配。离开、禁用、重置立即停止并清理，不向自身或激发本水柱的红块反馈自推。其余红绿蓝方块可受推。

## 运动对接

`ContactCube.PushRequested(Component source, Vector3 velocity, float deltaTime)`：每个物理步发出速度请求；离开水柱时发出零速度。ContactCubeMotion 按 source 保存并相加速度源，统一写方块 Rigidbody。反应组件不写根位移。后续如替换运动控制器，先禁用 ContactCubeMotion，不能同时运行两个刚体写入者。

落稳需要竖直速度小于容差且下方有非玩家支撑；跳到顶点不会误判落稳。空中忽略与玩家的实体碰撞；落稳恢复原碰撞关系并锁水平/竖直位移，玩家可站立但不能推动。有水柱速度源时解除落稳锁。水平喷出初始默认 0.25 秒关闭重力，持续喷射不无限续期；之后恢复重力。IsSettled / GravityProtected / IsSuspended 只读。

搬运程序调用 `ContactCubeMotion.SuspendMotion(true)` 取得持有期间位姿控制，放下调用 false。持有者还需禁用实体碰撞箱以暂停接触反应，放下恢复；不得用缩放根物体表达长度。房间重置解除挂起并清空速度源。

绿色横向长出部分挂 HorizontalCubeVine：落稳后，上方脚部接触或下方头部接触会续租玩家横向攀附；原始一格不挂附，脱离自动清除。`PlayerController.SetHorizontalVineSurface(Component source, float rootY, float speed)` / `ClearHorizontalVineSurface(source)` 只发送目标高度和移动速度，由玩家自己的物理步处理。新增状态 HorizontalClimbing=6，不改原枚举编号和输入键位；左右缓行、维持高度、禁止跳跃。租约超时、来源禁用、玩家重置都会清除。

其他受推对象可实现 `ICubeJetPushReceiver.SetCubeJetVelocity(source, velocity, deltaTime)`。零值表示解除该来源。玩家已有适配走 EnterWater/SetWaterVelocity/ExitWater，由 PlayerController 在自己的物理步处理水柱推动，不写玩家 Transform 或刚体。

## 接触既有颜色物体

方块触碰已有 IColorApplicationTarget，转发固定 ColorId，通过目标 CanApplyColor/ApplyColor 使用与玩家相同的规则，actor 为方块自身；仍服从目标激活与颜色解锁条件。一次持续接触中每个成功目标只执行一次，失败目标后续可重试（例如刚解锁）；分离后可再触发。三种新方块彼此只走上表 FSM，不走此广播接口，不形成递归反应。

## 区域生命周期

预制体有 RoomResetObject，规划生成按既有区域绑定。ResetForRoom 清空接触/水柱/计时并恢复初始长度、初始轴，位置由 RoomResetObject 统一恢复。不清除钥匙进度，不销毁作者方块。

## 验证

ContactCubeAssets.Build 与 ContactCubeVerification.Run 只用于 Library/FlowRetirementProbe 隔离项目，不覆盖用户场景。nographics 验证不代替真实画面/打包验收。
