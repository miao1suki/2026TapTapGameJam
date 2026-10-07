# 颜色物体 API

## 状态职责

| 模块 | 负责 |
|---|---|
| `ColorCatalog` | 颜色 ID、显示名、材质、Unity 层、解锁事件 ID |
| `ColorRuntimeService` | 当前解锁集合、房间重置和颜色物体注册 |
| `IColorObject` / `ColorObject` | 固定颜色属性、激活状态和视觉 |
| `BlockAbilityHost` | 启停同一物体上已经存在的固定功能组件 |
| `HSVColorFadeManager` | 白色失效与原色恢复的饱和度过渡 |
| `ColorKeyPickup` | 钥匙拾取和颜色组解锁 |
| `PlayerColorWheel` | 保存当前选色；左键通过屏幕射线选择交互圈内目标 |
| `IColorApplicationTarget` | 接收固定颜色并执行目标自身逻辑 |
| `UniversalColorBlock` | 自身显示纯色，并向四个紧邻目标广播颜色 |

最新定稿以钥匙解锁和固定功能组件为准；万能方块只作为前期代点击入口。

## 功能组件约束

具体玩法组件继承 `BlockFeature`，并声明功能类别和交互类型：

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

可用的交互接口：

```text
IPlayerContactReceiver
IObjectContactReceiver
IColorReactionReceiver
IColorApplicationTarget
IRoomColorResettable
```

组件类型决定固定形态。不要新增一个枚举让同一组件在水源、水流、气泡柱之间切换。

当前已实现：

```text
LavaHazardFeature
EnergyBlockFeature
MechanismBaseFeature
ButtonLockFeature
CrankPlatformFeature
MovingPlatformFeature
LiftFeature
SpikeFeature
LadderFeature

WaterSourceFeature
DirectionalCurrentFeature
BuoyancyColumnFeature
WaterVisualFeature

ClimbableVineFeature
BouncePlantFeature
PlantObstacleFeature
```

玩家水体推动与浮力通过 `PlayerController` 统一汇入游泳速度计算：

```csharp
player.SetWaterVelocity(source, new Vector2(4f, 0f));
player.SetBuoyancy(source, 3.5f);
```

每个 `BlockFeature` 都实现 `IFeatureVisualTarget`，提供“专属材质”和“材质目标渲染器”接口，后续可按组件接入专属材质，接口会负责在启用/失效时覆盖和恢复原材质。

按格计算的功能组件通过 `IGridCellSizeProvider` 获取关卡编辑器写入的 `CellWorldSize`；`BlockFeature.GridCellWorldSize` 在组件 Attach 时解析并缓存该值，组件自身不再声明“每格大小”字段。

跨色物体反应通过 `BlockFeature` 的 `IColorApplicationTarget` 直接实现；每个具体组件重写 `OnColorApplied`，不再依赖交互图。当前已接入红色清除绿色物、蓝色生长藤蔓、红色替换蓝色物、蓝色作用于岩浆。

每个 `BlockFeature` 也实现 `IColorApplicationTarget`，因此玩家左键和万能方块广播都能直接命中功能组件。`CanApplyColor` 统一检查组件是否已激活和颜色是否已解锁；具体反应写在 `OnColorApplied`。

机关附加接口：

```csharp
IEnergySource
IMechanismSignalSource
IMechanismSignalReceiver
IInteractionHoldTarget
```

`EnergyBlockFeature.CanMigrate` 是全游戏能源方块唯一迁移标记。能源方块通过交互圈长按拿起、鼠标网格吸附拖拽、松手放下；按钮使用 `IInteractionTarget`，曲柄使用 `IInteractionHoldTarget`。基座只实现 `IMechanismSignalSource`；移动平台等消费者在自己的 `linkedControls` 中绑定基座、按钮、曲柄，基座不保存机关列表。

## 运行时调用

```csharp
ColorRuntimeService colors =
    ColorRuntimeService.Instance;

bool unlocked = colors.IsUnlocked("blue");
colors.Unlock("blue");
colors.ResetForRoom();

HSVColorFadeManager.Instance.SetColorFaded(
    "blue",
    false,
    1.2f);
```

## 颜色应用

目标 B 实现：

```csharp
bool CanApplyColor(string colorId, GameObject actor);
bool ApplyColor(string colorId, GameObject actor);
```

玩家选择颜色后：

```text
左键点击屏幕上的 B
→ 检查 B 是否在交互圈内
→ B.ApplyColor(selectedColor)

左键点击屏幕上的万能方块
→ 检查万能方块是否在交互圈内
→ UniversalColorBlock 自身显示纯色
→ 向左右上下四个紧邻目标广播 ApplyColor(selectedColor)
```

## 新增颜色物体

1. 添加 `ColorObject` 并填写固定 `baseColorTypeId`。
2. 保留预制体自带的 `BlockRuntime` 和 `BlockAbilityHost`。
3. 在预制体上添加一个或多个具体 `BlockFeature`。
4. 不添加 Definitions、Catalog 或 GraphView 数据。
5. 需要接收玩家颜色时实现 `IColorApplicationTarget`；继承 `BlockFeature` 时已经默认实现，只需要按需重写 `OnColorApplied`。

## 房间重置

`ResetForRoom()` 清除解锁集合，使所有 `IColorObject` 失效，并触发 `EventMgr.OnRoomColorReset`。固定物体自身只切回白色；运行时生成物由各自拥有者监听重置并清理。

## 删除边界

不要再新增：

```text
InteractionManager
InteractionObject
InteractionObjectDefinition
InteractionObjectCatalog
InteractionGraph
InteractionNodeKind
GraphView
```

完整规范位于 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。
