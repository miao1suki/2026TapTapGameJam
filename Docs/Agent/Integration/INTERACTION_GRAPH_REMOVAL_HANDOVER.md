# 交互图架构移除交接说明

## 文档目的

当前项目的物体交互图可以表达复杂逻辑，但运行时会为物体注册、编译图、创建协程、遍历节点和反射调用。关卡中物体数量增加后，整套玩法全部经过图执行会产生不必要的 CPU、GC、协程和调试开销。

本文件只说明如何安全移除或逐步旁路交互图，不代表本次提交已经删除代码。实施时必须先完成迁移和 PlayMode 验收，再删除旧入口与资产。

## 当前实现清单

### 运行时代码

- `Assets/_Project/Code/Systems/Interactions/Runtime/InteractionManager.cs`
  - 全局注册表、图编译缓存、触发分发、节点遍历、延迟协程和反射方法调用。
- `InteractionObject.cs`
  - 通用物体来源，负责 Collider 事件转成 Enter/Exit/Stay/Touched/Manual。
- `InteractionGraph.cs`
  - `InteractionNodeKind`、节点和连线数据结构。
- `InteractionObjectDefinition.cs`、`InteractionObjectCatalog.cs`
  - 物体定义和 Resources 目录资产。
- `Assets/_Project/Code/Systems/Interactions/Runtime/*.cs`
  - 喷流、弹跳、藤蔓、锚点、生命 HUD 等玩法组件。它们本身可以保留，不应因为移除图而删除。

### 编辑器和资产

- 编辑器入口与同步：`Assets/_Project/Code/Systems/Interactions/Editor`。
- 交互定义：`Assets/_Project/Content/Interactions/Definitions`。
- 交互目录：`Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`。
- GraphView 窗口和关卡编辑器中的“打开交互管理器”按钮。
- `ColorBlock`、钥匙和锚点预制体上的 `interactionDefinition` / `InteractionObject` 引用。

### 不属于交互图、不能误删的服务

- `ColorRuntimeService`：颜色解锁、颜色属性、水体视觉和颜色事件。
- `HSVColorFadeManager`：颜色层渐变。
- `BlockWaterFeature`、`PlayerHealth`、`PlayerFallDamage`、`PlayerColorWheel` 等运行时能力组件。
- `ColorCatalog.asset` 和 `ColorTypeDefinition`。颜色属性仍是物体基础数据。

## 推荐迁移目标

采用“静态事件路由 + 物体组件直接调用”的结构：

1. 物体只注册必要的事件接口，不注册完整图。
2. Enter/Exit/Stay 由专用组件用 `HashSet` 或状态字段去重。
3. 颜色和物体类型在组件配置或轻量配置资产中作为条件字段。
4. 复杂组合交互由一个拥有者组件直接维护状态机，不创建每次触发的通用图协程。
5. 仍需策划编辑的少量规则可以保留为静态配置表，运行时预编译成委托或枚举分支；不要继续使用反射查找方法名。

推荐的运行时接口示例：

```text
IPlayerContactReceiver.Enter/Exit/Stay(player)
IObjectContactReceiver.Touch/Stay(other)
IColorInteractionHandler.Apply(otherColor, contactPoint)
```

接口调用必须是类型安全的直接调用；不要让组件通过 `InteractionManager.Trigger` 重新绕回图。

## 分阶段移除顺序

### 阶段 0：冻结和盘点

- 从最新 `origin/main` 建立独立分支。
- 记录所有定义、预制体、场景和编辑器入口的引用。
- 导出当前交互定义资产备份，保留回滚点。
- 禁止先删除 `InteractionManager`，否则 Unity 会把大量资产引用变成 Missing Script。

### 阶段 1：先旁路运行时图

- 为每个物体补充直接运行时组件或静态配置。
- 保留 `InteractionObject` 事件，但让它调用新的类型安全路由；同一事件只能有一个最终拥有者。
- 把红蓝喷流、红绿弹跳、蓝绿藤蔓、蓝水效果和钥匙解锁逐个迁移。
- 每迁移一个物体，记录“图路径”和“新路径”的事件矩阵，确认两者结果一致。
- 此阶段暂时保留图和 GraphView，便于回滚和对照。

### 阶段 2：移除图执行依赖

- 删除所有运行时脚本中的 `InteractionManager.Register/Raise/Trigger` 调用。
- 删除 `InteractionObject` 的图来源接口实现，或将其降级为仅提供普通事件的轻量组件。
- 删除 `GetCompiledGraph`、节点队列、延迟协程和反射 `InvokeMethod` 路径。
- 如果仍需要延迟，使用拥有者组件保存一个可取消的协程/计时器，并在 `OnDisable`、销毁和场景切换时取消。
- 删除 `InteractionObjectDefinition` 在预制体上的序列化引用前，先确认没有运行时加载代码读取它。

### 阶段 3：清理编辑器和资产

- 移除 `Tools/2026TapTap/物体交互管理器` 菜单、GraphView 窗口和节点编辑按钮。
- 清理关卡编辑器栏目中的旧交互管理器入口和“打开交互图”按钮。
- 从 `InteractionObjectCatalog.asset` 移除已迁移定义，确认没有 `{fileID: 0}`、空 GUID 或重复定义。
- 迁移完成后再删除 Definitions 资产、`.meta`、目录资产和对应编辑器同步代码。
- 不能直接删除整个 `Assets/_Project/Code/Systems/Interactions/Runtime`；其中的玩法组件可能已被预制体直接使用。

### 阶段 4：清理程序集和文档

- 从 asmdef、响应文件、Editor 自动扫描器和测试中移除交互图类型引用。
- 使用 `rg` 检查以下残留：

```text
InteractionManager
InteractionObjectDefinition
InteractionObjectCatalog
InteractionNodeKind
InteractionGraph
GraphView
InvokeMethod
```

- 更新 `SYSTEM_INDEX.md`、`INTERACTIONS_API.md`、`Docs/Developer/INTERACTIONS.md` 和本 skill。
- 文档中明确新的静态路由入口、组件所有权、颜色条件和清理规则。

## 三色方块迁移重点

### 蓝方块

- 直接挂载水体/游泳组件，Enter 时进入水体状态，Exit 时清除。
- 蓝色属性仍由 `ColorBlock` 和 `ColorRuntimeService` 管理。
- 蓝绿接触由蓝方块专用组件读取对方颜色和锚点服务，不经过通用图。

### 绿方块

- 直接挂载落差弹跳组件和下一跳弹高组件。
- 玩家 Enter/Exit 使用接触集合去重；离开必须清除玩家引用和倍率。
- 站在绿方块顶部时轮盘屏蔽应由 `ColorBlock.CanUseColorWheel` 保留。

### 红/蓝方块

- 直接调用喷流组件，条件为另一物体的颜色属性和物体类型。
- 喷流必须有重复触发保护、所有者和 `OnDisable` 清理。
- 起点、方向、距离、持续时间保留在方块配置，不硬编码在全局服务。

### 颜色钥匙

- 保留 `ColorKeyPickup` 的直接 Enter 入口。
- `ColorRuntimeService.Unlock` 先完成颜色目录查找、材质恢复和水体恢复，再播放相机 Timeline。
- `ColorTypeDefinition` 必须有 `[Serializable]`；删除交互图后仍需在运行时确认 `Find("red")`、`Find("green")`、`Find("blue")` 非空。
- 钥匙销毁、重复进入和场景切换不能再次触发解锁或遗留协程。

## 性能注意事项

- 不要在每帧为每个物体启动 `StartCoroutine` 或扫描整张图。
- 不要用字符串方法名和反射作为高频路径。
- Stay 事件应改为拥有者组件中的定时采样或固定更新，不要每个 Collider 每帧都派发通用事件。
- 多 Collider 只按物体实例去重；进入/离开必须按 Actor 去重。
- 生成物必须有上限、所有者和统一清理入口。
- 配置资产可在加载时编译为数组、枚举或委托，运行时只做索引和条件判断。

## 验证清单

### 静态检查

- Runtime/Editor 编译 0 错误。
- `git diff --check` 通过。
- 没有旧图类型、反射方法名或 GraphView 入口残留。
- 没有 Missing Script、空 GUID、重复定义和无效预制体引用。
- 所有直接使用的玩法组件仍挂在对应预制体上。

### PlayMode

- 钥匙：解锁、材质恢复、水体显现、Timeline、重复进入和销毁。
- 蓝方块：进入/离开水体、减速、生命、摔落免疫。
- 绿方块：低落差不弹、高落差弹起、离开清理、下一跳只消费一次。
- 红蓝：目标颜色命中、非目标颜色不命中、重复触碰和禁用清理。
- 蓝绿：最近锚点生长、无锚点停止、来源禁用清理、玩家可站立。
- 玩家轮盘：选色只改玩家能力，左键才触发目标组件；圆外目标不可交互。
- 场景切换：无残留协程、事件订阅、临时材质、生成物或玩家状态。

### 证据

记录测试场景、触发步骤、预期/实际结果、物体数量、玩家状态、颜色饱和度和 Console。不要以 Timeline 播放作为交互成功的唯一证据。

## 回滚策略

在阶段 1 和阶段 2 之间保留图资产和旧入口分支。若新路由出现行为差异，先恢复对应物体的图绑定，不要恢复全局颜色管理器。确认所有事件矩阵通过后，再删除定义、目录和 GraphView 文件。
