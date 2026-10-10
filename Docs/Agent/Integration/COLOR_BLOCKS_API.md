# 颜色物体 API

## 分类表现与纯 HSV

`HSVColorFadeManager` 不决定解锁、材质、透明度或物体显隐，也不扫描场景 Renderer。纯 API：

```csharp
// 参数范围均为 0..1。seconds 是实际秒数，0 在当前调用内写入。
HSVColorFadeManager.Instance.SetHsv("green", saturation: 0f, whiteAmount: 1f);
HSVColorFadeManager.Instance.SetHsv("green", saturation: 1f, whiteAmount: 0f, seconds: 3f);
```

`SetColorFaded` 现在只是纯饱和度便利 API，不再驱动外观交接；旧调用者必须改用分类表现入口。自动外观演出运行期间由演出独占同颜色的 HSV 写入；自行编排时不要同时启动自动演出。

`ColorAppearanceManager.Play(id, unlocked, duration)` 管理同颜色组的两阶段流程：前 30% 基础外观变白并由不透明降为透明；零 Alpha 交接；后 70% 正式外观从全白、零 Alpha 恢复到原色、原始透明度。`GetBaseOpacity` / `GetRealOpacity` 提供两种外观的独立 Alpha，`ShowsUnlockedAppearance` 表示阶段，`IsComplete` 供 Timeline 等待。默认 5 秒，正数指定时长乘既有倍率（默认 3），0 立即完成。重复请求不从头开始，反向从当前进度回退。房间重置走 `ColorRuntimeService.ResetForRoom`，同步复原外观与 HSV。

分类逻辑位于同一管理器：

- 基础方块：`ColorObject` 只在启用、激活和阶段通知时绑定共享运行时材质，常态没有 Update / LateUpdate。不会把编辑识别材质当作正式材质。
- 水体：`RegisterWater` / `UnregisterWater`；水源、水流、水柱统一调用。管理器在过渡期间批量 `SetReveal`；保留各实例波纹/反射纹理，不能直接共用整份水材质。完成后不继续写入。
- 藤蔓：`RegisterVine` / `UnregisterVine`；母根在粒子初始化后注册，先隐藏 Renderer，再绑定透明运行时材质和遮罩，最后按当前 Alpha 显示。燃尽/禁用注销，不会被管理器重新打开。生长/燃烧姿态仍由藤蔓功能组件负责，不归 HSV。
- 普通材质复用接口：`GetSharedMaterial(source, colorId, realAppearance)`，同源/同颜色/同阶段共享一份副本。调用者不得修改返回的共享外观参数。正式材质须支持透明混合与 `_ColorRevealOpacity`；不透明 Shader 单写 Alpha 不能渐显。

藤蔓透明 Shader 为 `ColorVineReveal.shader`，通过 Resources 中 `VineReveal.mat` 保留构建引用，不依赖裸 Shader.Find。运行时保留 TA 源纹理及 Tint，使用顶点颜色和纹理 Alpha，源预制体与源材质不改。新建效果需检查独立透明 Shader 是否保留源材质所需的全部效果；此适配仅针对现有白膜藤蔓。

遮罩通过一次性 PropertyBlock 记录颜色组和基础/正式角色，并读取管理器的全局 Alpha 判断零透明裁剪；覆盖区域写 1，不重复乘渐显 Alpha。HSV 仍在选择性像素化之后、后处理之前。透明重叠、拖尾 UV、SceneColor 采样和排序必须 GPU 实测；不保证各层透明物体的对象级隔离。

运行时材质缓存按资源种类增长，不按方块数增长；水体因独立纹理仍按实例更新，集中调度不等于零成本。稳定状态不逐帧写外观，不逐帧查场景，不在 MonoBehaviour 构造/字段初始化创建 Unity 原生对象。计数可在“颜色表现管理器”中文 Inspector 查看。

### 水体透明渲染与生命周期

`_WaterMeshDepth` 必须生成材质属性（Generate Property Block），由水体初始化按自身深度设置；不可声明成共享全局值，否则顶面采样可能使用零深度，且不同尺寸水体相互污染。

水面与横截面由 MeshRenderer 渲染，Shader Graph 使用 URP Unlit 透明子目标，不使用 Sprite Lit：后者依赖 SpriteRenderer 的翻转参数、颜色与 Alpha，不能仅修透明度参数解决网格渲染问题。保留波纹、折射、水下颜色、焦散与反射节点，最终 Alpha 保留原水体透明度并乘 `_ColorRevealOpacity`，不得替换为编辑识别材质。注销生成水体时先停用并移出所属物体，再延迟销毁；同帧重新绑定不得发现已预约销毁的旧实例。

## 玩家运动接入

藤蔓用 `SetClimbTopHeight(source, worldHeight)` 更新根系顶部，先 `EnterClimb(source)`；退出与重置清理记录。气泡/蒸汽/弹跳调用既有 `ApplyVerticalBounce` / `ApplyVerticalBounceImmediate`，由 PlayerController 增强向上速度并提供短暂保护，不在功能组件写 Rigidbody、不逐帧累加冲量。顶部向内、中段向外蹬跳，参数见玩家中文 Inspector。

## 像素化与藤蔓根系

颜色普通 Layer 不变；水体与母根 Renderer 使用 `renderingLayerMask |= 128u`，像素化先于 HSV，描边 API 默认关闭，见 PIXELIZATION_API.md。旧 TA 不透明 Equal 深度通道不用于透明水体。

`ClimbableVineFeature.Root` 为唯一母根，红色命中任意节段只沿该根系顶到底逐节燃烧；预置节段休眠以供重置，生成物按生命周期清理。母根共享 30.13→32.43 秒的粒子/拖尾姿态缓存，不使用负模拟速度。首次缓存有 CPU/内存开销，需 Profiler 验证。

## 状态职责

| 模块 | 负责 |
|---|---|
| `ColorCatalog` | 颜色 ID、显示名、材质、Unity 层、解锁事件 ID |
| `ColorRuntimeService` | 当前解锁集合、房间重置和颜色物体注册 |
| `IColorObject` / `ColorObject` | 固定颜色属性、激活状态和视觉 |
| `BlockAbilityHost` | 启停同一物体上已经存在的固定功能组件 |
| `HSVColorFadeManager` | 白色失效与原色恢复的饱和度过渡 |
| `ColorKeyPickup` | 感知范围启动磁吸，碰撞玩家后解锁颜色并隐藏钥匙 |
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
