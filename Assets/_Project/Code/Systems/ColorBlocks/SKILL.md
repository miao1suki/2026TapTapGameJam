---
name: taptap-color-gameplay
description: 2026TapTap 颜色大洗牌后的固定颜色物体、钥匙解锁、房间重置、万能方块代理、直接组件交互和交互图退役规范。
---

# 颜色大洗牌玩法与组件规范

## 适用范围

本规范适用于颜色钥匙、红蓝绿场景物体、万能方块、房间重置、颜色物体之间的直接反应，以及交互图移除后的迁移工作。

当前 `origin/main` 仍是旧的交互图和动态颜色结构。本文件描述目标架构。迁移期间可以暂时保留旧路径做对照，但不得把新功能重新接回交互图。

## 最终玩法结论

1. 红、蓝、绿场景物开局全部是白色失效状态。
2. 玩家拾取对应颜色钥匙后，该颜色的全部物体恢复原色并启用自身固定功能。
3. 离开房间后，全部颜色物体重新变白失效，钥匙效果清空。
4. 物体在放下时已经决定自身形态，不会跨颜色变化，也不会在同颜色内切换形态。
5. 蓝色族的水源、定向水流、浮力柱是三个不同组件和预制体，不共用一个运行时模式枚举。
6. 每个功能只由一个组件负责，组件自己管理触发、条件、状态、表现、生成物和清理。
7. 不再用通用交互图描述物体关系。
8. 物体之间的反应使用类型安全的直接接口调用。
9. 万能方块只负责代替玩家点击目标 B，本身不拥有玩法能力。

## 术语

- `A`：玩家当前选中的、已经解锁的颜色 ID。
- `B`：实际接收颜色并执行自己固定逻辑的物体、机械或机关。
- `ColorObject`：拥有固定基础颜色和解锁状态的场景物体。
- `ColorObjectFeatureHost`：根据颜色解锁和房间重置，统一启停物体上已有的功能组件。
- `UniversalColorBlock`：固定在场景中的颜色交互代理，把 A 转交给 B。
- `Feature`：一个具体功能组件，例如水源、水流、岩浆、移动平台、藤蔓。

## 颜色与形态边界

颜色只表示钥匙解锁分组。

```text
BaseColorId = red / blue / green
```

形态由预制体和组件决定。

```text
WaterSourceFeature
DirectionalCurrentFeature
BuoyancyColumnFeature
```

这三个组件都可以有 `DefaultColorId = blue`，但运行时始终分别执行自己的功能。

禁止以下设计：

- 水源运行时变成水流。
- 水流运行时变成浮力柱。
- 蓝色物体运行时变成红色物体。
- 同一个蓝色功能组件用枚举在三种形态之间切换。
- 通过染色把 A 形态改成 B 形态。
- 用 `CurrentColorId` 决定物体是什么功能。

允许的状态变化只有：

```text
白色失效
→ 恢复原色并启用固定功能
→ 离开房间重新失效
```

## 万能方块

万能方块是固定交互代理，不是颜色源、不是功能本体、不是可搬运动物。

后期完整交互：

```text
玩家选择颜色 A
→ 点击 B
→ B.ApplyColor(A)
```

前期简化交互：

```text
B 旁边放置 UniversalColorBlock
→ 玩家选择颜色 A
→ 点击 UniversalColorBlock
→ UniversalColorBlock 将 A 转交给关联的 B
→ B.ApplyColor(A)
```

B 不应区分颜色来自玩家直接点击还是万能方块转发。

万能方块必须满足：

- 固定在场景中。
- 不允许搬运。
- 不拥有红色、蓝色、绿色玩法能力。
- 不参与红绿、蓝绿、红蓝物体反应。
- 不通过交互图或反射找方法。
- 只保存或解析目标 B，并转发颜色。
- 离开房间后不保留跨房间的颜色效果。

## 标准接口

颜色应用目标由 B 实现：

```csharp
public interface IColorApplicationTarget
{
    bool CanApplyColor(string colorId, GameObject actor);
    bool ApplyColor(string colorId, GameObject actor);
}
```

万能方块实现相同入口并转发：

```csharp
public interface IColorApplicationProxy
{
    IColorApplicationTarget Target { get; }
}
```

玩家交互流程：

```text
PlayerColorController.SelectedColorId
→ PlayerInteractionSensor 选择目标
→ 目标是 IColorApplicationTarget：直接 ApplyColor
→ 目标是 IColorApplicationProxy：转发给 Target.ApplyColor
```

接触事件使用直接接口：

```csharp
IPlayerContactReceiver.Enter/Exit/Stay
IObjectContactReceiver.Touch/Stay
IColorReactionHandler.Apply(otherColor, contactPoint)
IRoomColorReset.ResetForRoom
```

不要使用：

```text
InteractionManager.Trigger
InteractionObjectDefinition
InteractionNodeKind
InvokeMethod
字符串方法名
反射调用
```

## 组件契约

继续沿用现有项目组件规范：

- 一个组件只负责一个功能。
- 组件必须提供中文显示名。
- Editor 界面首行必须显示粗体中文名，脚本名和组件名不变。
- 使用 UI Toolkit Inspector。
- 参数按语义分组、Foldout、条件显隐。
- 可调参数必须暴露。
- 运行时状态只读展示。
- 调试按钮只在合理状态可用。
- `OnAttach` 建立状态。
- `OnDetach` 清理状态。
- `OnTick` 只处理真正需要逐帧运行的功能。
- 生成物必须记录拥有者并能在 Reset/Disable/Destroy 时清理。
- 多 Collider 按物体实例去重，Enter/Exit 按 Actor 去重。
- 不在高频路径中全场景查找、反射或启动无意义协程。

`DefaultColorId` 的新语义：

```text
物体固定属于哪个钥匙解锁组
```

它不再表示：

```text
当前颜色
运行时可切换的能力配置
```

## ColorObjectFeatureHost

Host 只做薄层启停，不再动态给物体添加全部能力。

正确流程：

```text
预制体已经挂好固定功能组件
→ Host 读取 ColorObject.BaseColorId
→ 对应颜色未解锁：disable 功能
→ 对应颜色已解锁：enable 功能
→ 房间退出：disable、Reset、清理生成物
```

禁止流程：

```text
读取 CurrentColorId
→ 扫描 BlockAbilityCatalog
→ 给物体添加所有功能
→ 动态切换当前颜色对应的功能
```

`BlockAbilityCatalog` 可以保留为编辑器发现和校验工具，不得继续承担运行时形态切换。

## 现有项目迁移映射

### 保留并改造

- `ColorRuntimeService`
  - 从只管理 `ColorBlock` 改为管理任意 `ColorObject`。
  - 增加房间级解锁和重置入口。

- `ColorCatalog` / `ColorTypeDefinition`
  - 保留颜色定义。

- `HSVColorFadeManager`
  - 保留白色失效与恢复原色的渐变。

- `ColorKeyPickup`
  - 保留直接入口。
  - 不依赖交互图。

- `InteractiveWater`
  - 作为水源、水流、浮力柱的表现基础。

- `PlayerController`
  - 保留游泳、攀爬、受伤、击退、移动能力。

- `PlayerHealth` / `PlayerFallDamage`
  - 保留。

- `PlayerInteractionSensor` / `IInteractionTarget`
  - 保留目标查找和点击入口。

- `PlayerColorWheel`
  - 可保留为选色 UI。
  - 只保存未锁颜色与当前选色。
  - 不再触发 `Manual` 图。

- `BlockRuntime`
  - 可作为单物体内部信号、命令、状态和调试总线。
  - 不再承担跨物体通用图职责。

### 退役或重做

- `ColorBlock`
  - 动态当前颜色、动态能力切换语义退役。
  - 换成固定 `ColorObject` 或等价组件。

- `BlockAbilityHost`
  - 改成薄层激活器。
  - 不按当前颜色查目录和切换全部功能。

- `BlockWaterFeature`
  - 拆分或替换为：
    - `WaterSourceFeature`
    - `DirectionalCurrentFeature`
    - `BuoyancyColumnFeature`

- `BlockVineFeature`
  - 改成固定 `ClimbableVineFeature`。

- `GreenBouncePad`
  - 改成固定 `BouncePlantFeature`。

- `BlockSprayEmitter`
  - 新策划案不再需要时退役。

- `NextJumpBounceEmitter`
  - 红绿关系改为烧植物后退役。

### 删除

- `InteractionManager`
- `InteractionGraph`
- `InteractionObjectDefinition`
- `InteractionObjectCatalog`
- `InteractionManagerWindow`
- `InteractionGraphView`
- `InteractionEditorService` 中的图同步职责
- `Assets/_Project/Content/Interactions/Definitions`
- `Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`
- 预制体和关卡栏目上的 `interactionDefinition`
- `InteractionObject` 的图来源实现
- 所有 `InteractionManager.Register/Raise/Trigger`
- `GetCompiledGraph`
- 节点队列、逐次协程和 `InvokeMethod`

## 建议功能组件拆分

红色：

```text
LavaHazardFeature
MechanismBaseFeature
MovingPlatformFeature
LiftFeature
SpikeFeature
LadderFeature
ButtonLockFeature
CrankPlatformFeature
```

蓝色：

```text
WaterSourceFeature
DirectionalCurrentFeature
BuoyancyColumnFeature
```

绿色：

```text
ClimbableVineFeature
BouncePlantFeature
PlantObstacleFeature
```

固定反应：

```text
BurnPlantReaction
VineGrowthReaction
HardPlatformReaction
SteamPushReaction
```

## 固定反应规则

红色加绿色：

```text
岩浆或高温接触植物障碍
→ 植物烧毁
→ 清除碰撞
→ 打开路径
```

蓝色加绿色：

```text
水流接触藤蔓或植物
→ 藤蔓延伸
→ 生成新攀爬路径
```

红色加蓝色：

```text
岩浆接触水体
→ 生成硬质通行平台

高温接触水体
→ 生成蒸汽推力
→ 推动玩家或机关
```

反应组件必须负责：

- 重复触发保护。
- 生成物拥有者。
- 离开、禁用、销毁和房间重置时清理。
- 参数暴露，不把距离、持续时间、力度硬编码在全局服务。

## 房间重置

房间重置至少处理：

- 清除当前房间解锁颜色。
- 所有 `ColorObject` 恢复白色失效。
- 所有 `ColorObjectFeatureHost` 停用功能。
- 清除硬平台、蒸汽、藤蔓、喷流等生成物。
- 清除玩家身上的水体、攀爬、击退、免疫和临时速度。
- 清除万能方块转发状态。
- 不清理永久存档数据，除非策划明确要求。

钥匙是否重新生成、是否要求永久解锁，需要策划确认后单独实现，不要自行猜测。

## 迁移顺序

1. 盘点 Definitions、Catalog、预制体、场景和编辑器入口引用。
2. 新增 `ColorObject`、房间重置入口和固定颜色激活流程。
3. 将一个具体玩法迁移成单功能组件，与旧图路径对照验收。
4. 迁移万能方块代理和玩家选色入口。
5. 迁移三色固定反应。
6. 删除所有图运行时引用。
7. 删除 Definitions、Catalog、GraphView 和编辑器同步。
8. 更新文档、System Index、API 文档和本 Skill。

## 验收重点

### 颜色和房间

- 开局所有颜色物体白色失效。
- 钥匙只解锁对应颜色组。
- 解锁后物体恢复自身固定形态。
- 离开房间后全部失效并清除效果。
- 重新进入房间不会残留状态。

### 万能方块

- 不能搬运。
- 点击后只把颜色转给关联的 B。
- B 的反应与直接点击 B 相同。
- 万能方块自身不产生玩法效果。

### 固定形态

- 水源永远是水源。
- 水流永远是水流。
- 浮力柱永远是浮力柱。
- 不跨颜色变化。
- 不在同颜色内换形态。

### 直接反应

- 红绿只烧植物。
- 蓝绿只触发水流滋养和藤蔓生长。
- 红蓝只生成约定的平台或蒸汽。
- 非目标颜色不触发。
- 重复接触不堆叠生成物。
- 禁用和房间重置后清理完整。

### 静态检查

- Runtime 和 Editor 编译 0 错误。
- `git diff --check` 通过。
- 项目中不再存在图类型和 GraphView 入口。
- 没有 Missing Script、空 GUID、重复定义。
- 没有高频反射、无意义协程和全场景逐帧扫描。

## 最终原则

```text
颜色是解锁分组。
形态在放置时固定。
钥匙只改变失效和生效。
一个功能一个组件。
万能方块只代玩家点击 B。
物体反应直接走类型安全接口。
交互图整套退役。
```
