# 颜色物功能组件

功能组件使用 `Project.BlockFeatures`，核心入口是 `BlockFeature`、`BlockRuntime` 和薄宿主 `BlockAbilityHost`。

## 编写约束

- 一个组件只负责一种固定功能。
- 组件继承 `BlockFeature`，不要直接写 `Update`、`FixedUpdate`。
- 组件形态由脚本类型和所属预制体固定，不能用运行时枚举切换形态。
- 使用 `[BlockFeature]` 声明中文名、固定颜色组、类别、交互类型、阶段和依赖。
- 读取其他功能通过 `BlockContext.Query.TryGet<T>`。
- 改状态通过 `BlockContext.Commands.Send`，瞬时事件通过 `BlockContext.Signals.Emit`。
- `BlockContext.Presentation` 当前是预留的单物体表现请求接口，默认只记录调试事件；正式表现服务接入前，动画、音效、特效、材质由具体组件自己负责。
- 每个组件必须提供中文 UI Toolkit Inspector、Foldout、条件显隐和运行时调试。

基础属性示例：

```csharp
[BlockFeature(
    DisplayName = "水源功能",
    DefaultColorId = "blue",
    Category = BlockFeatureCategory.Water,
    Interactions =
        BlockFeatureInteraction.PlayerContact |
        BlockFeatureInteraction.RoomReset,
    Writes = new[] { BlockChannel.Water })]
```

## 可用类别

```text
Mechanism / 机关
Hazard / 危险物
Water / 水域
Plant / 植物
Movement / 移动
Reaction / 反应
Utility / 通用
```

## 可用交互

```text
PlayerContact / 玩家接触
ObjectContact / 物体接触
AppliedColor / 接收颜色
RoomReset / 房间重置
```

对应的直接接口：

```csharp
IPlayerContactReceiver
IObjectContactReceiver
IColorReactionReceiver
IColorApplicationTarget
IRoomColorResettable
```

声明了交互类型就必须实现对应接口，`BlockFeatureValidationUtility` 会阻止只声明不实现。

## 固定颜色与激活

`BlockAbilityHost` 只根据 `ColorObject` 的固定 `BaseColorTypeId` 启停预制体已有组件。

它不会：

- 查 Catalog
- 添加组件
- 读取动态 CurrentColor
- 在不同形态之间切换

`BlockFeature.OnValidate` 在编辑器中保持组件禁用，运行时由宿主按钥匙解锁状态统一启用。

## 具体组件状态

当前固定功能组件已经接入对应预制体：

```text
Red_Lava            LavaHazardFeature
Red_Battery         EnergyBlockFeature
Red_Button          ButtonLockFeature
Red_Crank           CrankPlatformFeature
Red_Foundation      MechanismBaseFeature
Red_Platform        MovingPlatformFeature

未挂预制体、已备用的机关形态：
LiftFeature
SpikeFeature
LadderFeature

Blue_WaterSource    WaterSourceFeature
Blue_WaterWay       DirectionalCurrentFeature
Blue_WaterBubble    BuoyancyColumnFeature

Green_Ladder        ClimbableVineFeature
Green_JumpPlant     BouncePlantFeature
Green_PlantObstacle PlantObstacleFeature
```

三色之间的真实反应、机关之间的连线关系和旧喷流、下一跳、旧动力网络尚未实现；它们只能通过公开的类型安全接口逐步接入，不要恢复动态颜色 profile 或交互图。

蓝色三个功能组件统一继承 `WaterVisualFeature`，默认从 `ColorCatalog.blueWaterPrefab` 接入队友的水体表现；水体预制体、本地偏移和缩放可在 Inspector 调整。

机关附加规则：

- 基座只按左右上下四个网格方向检测 `IEnergySource`，并向平台等消费者输出 `IMechanismSignalSource` 信号；基座没有机关绑定列表。
- 只有 `EnergyBlockFeature` 声明 `CanMigrate`。
- 按钮和曲柄通过 `IInteractionTarget`/`IInteractionHoldTarget` 接入玩家交互键。
- 移动平台支持横向/纵向路线、游荡格数、时间、端点停留、自动/信号驱动、强制载人、Tag 筛选和多个开关的任一/全部激活规则。
- 水泡只有上方存在其他蓝色 `ColorObject` 时才提供浮力。
- 弹性植物的弹跳速度由玩家当前坠落高度和“低/高落差回弹比例”换算，落差越高衰减越大，相关比例全部可调。
- 所有功能组件都带 `IFeatureVisualTarget`：可配置专属材质和目标 Renderer，后续像水一样接各颜色道具的正式表现。

第二轮校正已生效：

- 游戏只有岩浆会致死；玩家不结算任何摔落伤害，水与水泡也没有摔落免疫参数。
- 水泡按格数限制最高顶起高度，格高读取关卡编辑器格子世界尺寸，靠标签识别玩家，不再使用图层。
- 水流只提供左/右枚举和推搡速度。
- 弹跳植物用“仅摔落 / 仅跳跃 / 两者都可以”模式。
- 藤蔓提供攀爬倍率和逐块或瞬间生长接口；新藤蔓块克隆藤蔓自身，每块高度读取关卡格子世界尺寸，不单独配置段预制体、段高、动画时长或缩放曲线。
- 按钮是材质切换拉杆；曲柄按每秒进度转动并支持无人回弹动画。
- 基座按左右上下四个网格方向检测能源；平台从自己这一侧绑定多个按钮/基座/曲柄并选择任一或全部激活。

第三轮校正：

- `LevelEditorPlacedBlock` 实现 `IGridCellSizeProvider`，放置和生成时记录关卡编辑器当前格子世界尺寸。
- 能源、曲柄、基座、移动平台、水泡和藤蔓统一通过 `BlockFeature.GridCellWorldSize` 读取该尺寸；不再在各自 Inspector 暴露每格大小。
- 藤蔓逐块生长时每块播放自身出现动画；瞬间长完时只让最上方新块播放。
- 规划关卡编辑器仍逐格放置藤蔓。生成场景时，同一房间内同列、同预制体且世界格尺寸一致的连续藤蔓会合并为一条链：最下方保留母藤蔓，上方改为 `Green_LadderSon` 节段并归属母藤蔓；母藤蔓最大生长块数等于上方节段数，初始即为长满状态。房间重置只清除运行时新长出的节段，不删除这些预置节段；不连续或跨房间的藤蔓不会合并。
- 机关控制方向统一为消费者绑定信号源；平台持有 `linkedControls`，基座、按钮和曲柄只实现 `IMechanismSignalSource`，不持有机关列表。
- 蓝色水域与浮力水柱只处理游泳和浮力；玩家侧的摔落免疫栈已删除，`PlayerFallDamage` 仅提供弹跳植物所需的坠落高度。
- 浮力水柱的上方蓝系检测统一用“向上检测高度（格）”和“横向检测宽度（格）”，检测盒按关卡格子世界尺寸换算。
- 万能方块自身纯色，并向左右上下四个紧邻目标广播颜色；Inspector 支持持续染色或延迟褪色。
- 水族水体按格子中心对齐并配置为格子体积；藤蔓是实体阻挡物，按格距离进入攀爬。W 或朝藤蔓方向的水平键上爬，S 加速下滑，按远离藤蔓方向的水平键离开；按住朝藤蔓方向并按跳跃键会向外上方蹬跳，短暂脱离后可重新操控回到藤蔓。单按跳跃键仍可在藤蔓上定住。
- 弹跳植物是实体阻挡物，只在正确落点触发一次；按钮按格半径检测交互；平台碰撞后反向。
- 能源方块改为交互圈内长按“搬运”键拿起、鼠标网格吸附拖拽、松手放下；搬运键默认右键并锁定长按，拖动时关闭碰撞并置于最上层，同时锁住玩家全部输入。

字幕展示道具：

- `DisplayBlock` 使用 `DisplayBlockFeature`，运行时可隐藏自身 Collider 与方块外观。
- 文本使用世界空间 `TextMeshPro`，支持文字内容、相对方块高度的字号比例、字体、颜色、方块九宫格相对位置和左/中/右对齐。
- 以 `Player` Tag 找到玩家 Renderer 后，文字可以排序在玩家上方或下方。
