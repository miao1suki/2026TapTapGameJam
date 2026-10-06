# 物体交互 API

## 运行时

- `InteractionObjectDefinition`：一个具体物体的身份、预制体、基础颜色和交互图数据。
- `InteractionObject`：通用预制体组件，实现 `IInteractionObjectSource`。
- `ColorBlock`：三色方块的物体交互来源。它收集接触事件，并提供 `OnInteractionPlayerEntered`、`OnInteractionPlayerLeft`、`OnInteractionObjectTouched` 和 `OnInteractionObjectStay` 给图的“调用方法”节点。
- `PlayerHealth` / `PlayerFallDamage`：玩家生命、落差记录和摔落伤害；水体通过图调用的进入/离开链统一设置恢复、速度倍率和免疫状态。
- `GreenBouncePad`：由绿方块图的 `RegisterBounceActor` / `UnregisterBounceActor` 管理监听，落地事件按序列化阈值和曲线计算弹高。
- `BlockSprayEmitter`：红蓝方块图的 `ApplySpray` / `ApplyPlayerInteraction` 入口；距离、方向、持续时间和起点偏移都是预制体参数。
- `VineGrowthEmitter` 与 `AnchorPoint`：蓝方块图的 `GrowVineTowardAnchors` 入口；生成藤蔓的协程和段对象由发起方拥有，禁用/销毁时清理。
- `NextJumpBounceEmitter`：红绿物体触碰图的 `ArmNextJump` 入口，玩家下一次跳跃后自动清除倍率。
- `InteractionManager.Trigger(GameObject, InteractionNodeKind, GameObject, GameObject)`：脚本或 Timeline 的手动入口。实际执行仍由目标定义中的连线决定。
- `ColorRuntimeService`：颜色解锁、颜色属性、水体视觉和颜色事件的底层服务；不拥有交互图。
- `HSVColorFadeManager`：颜色层渐变服务，供图中的褪色/恢复节点调用。
- `ColorBlock.TryOpenColorWheel(GameObject)`：右键颜色轮盘的统一入口。绿色方块在玩家站立于顶部时返回 `false`，避免站立状态与颜色交互同时发生；蓝色大方块会先记录碰撞点再进入 `Manual` 图链。

颜色解锁的顺序是：`ColorRuntimeService.Unlock(typeId)` 先把该颜色的基础褪色状态设为恢复，再派发同色方块定义的 `Manual` 根节点。颜色钥匙自己的 `PlayableDirector` 只承担相机 Timeline，不是材质或水体恢复的唯一来源。启动时颜色目录默认全部褪色；方块注册和锁回操作会再次同步对应状态。

实现新的物体类型时，优先挂 `InteractionObject`，通过交互管理器设置 `InteractionObjectDefinition`；如果物体已有 `ColorBlock`，不要叠加第二个来源组件，直接绑定定义到 `ColorBlock`。

## 数据路径

- 目录：`Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`
- 定义：`Assets/_Project/Content/Interactions/Definitions/*.asset`
- 编辑器入口：`Project.Interactions.Editor.InteractionManagerWindow`

`InteractionObjectDefinition` 与 `InteractionObjectCatalog` 分别位于独立脚本文件中；不要把两个 ScriptableObject 类型重新合并到同一个 `.cs` 文件，否则 Unity 可能把普通图节点当作脚本组件并报 `ExtensionOfNativeClass`。

## 编辑器同步

`InteractionEditorService.SynchronizeWithLevelEditor()` 在打开窗口和编辑器启动时运行：

1. 扫描 `ColorBlock_red/green/blue.prefab`，确保各自有唯一物体定义并绑定回预制体。
2. 扫描 `LevelEditorPalette.Entries` 和 `PropEntries`，把栏目引用同步到定义。
3. 将交互目录中的其他预制体补回关卡编辑器道具栏目。

同步还会从 `ColorKeyPickup.ColorTypeId` 回填钥匙定义的基础颜色，并为已有定义补齐 `Restore` 节点及其颜色值。当前三把钥匙资产必须分别包含 `baseColorTypeId: red/blue/green` 与相同颜色的 `kind: Restore` 节点，避免窗口里只能看到 Timeline 而看不到颜色恢复链。

## 物体事件

`InteractionObject` 和 `ColorBlock` 会把玩家进入、玩家离开、物体触碰和物体停留转发给管理器。对象类型由 `InteractionObjectDefinition.ObjectId` 判断；颜色通过 `CurrentColorTypeId` 读取。调用方法节点支持无参数、`GameObject` 参数和字符串参数；褪色/恢复节点调用 `HSVColorFadeManager`，Timeline 节点播放 `PlayableDirector`。

节点条件 `RequireOtherColor` 专门读取触碰对象的颜色属性；它与 `RequireObjectId` 可以串联，先限制具体物体再限制颜色。每个组合图都必须保留对应的离开链和生成物清理入口。
