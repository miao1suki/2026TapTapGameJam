# 物体交互管理器交接目录

## 入口与目录

- Unity 编辑器入口：`Tools/2026TapTap/物体交互管理器`。
- 交互目录：`Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`。
- 物体定义：`Assets/_Project/Content/Interactions/Definitions`。
- 物体预制体：`Assets/_Project/Content/Interactions/Prefabs` 与 `Assets/_Project/Content/ColorBlocks/Prefabs`。
- 运行时核心：`Assets/_Project/Code/Systems/Interactions/Runtime`。
- 编辑器 GraphView：`Assets/_Project/Code/Systems/Interactions/Editor`。
- 使用规范：`.agents/skills/taptap-interaction-graph/SKILL.md` 及 `references/playmode-checklist.md`。

## 运行时职责

- `InteractionObjectDefinition` 保存一个具体物体的 ObjectId、基础颜色、节点和连线。
- `InteractionObject` 或 `ColorBlock` 是物体的事件来源；同一预制体只保留一个主要来源。
- `InteractionManager` 注册物体、编译图、派发 Enter/Exit/Stay/Touched/Manual/Time 触发并执行节点。
- `ColorRuntimeService` 只维护颜色解锁、颜色属性、水体视觉和颜色事件；颜色交互关系仍写在物体图中。
- `HSVColorFadeManager` 只维护颜色层渐变状态。

## 当前节点和接入模式

常用链路：

```text
玩家进入 → 需要玩家 → 调用方法(GameObject)
物体触碰 → 需要其他物体 → 受到其他物体颜色 → 调用方法
手动执行 → 玩家选中颜色 → 调用方法
手动执行 → 恢复(颜色 ID)
```

方法节点的“值”填写方法名，可选 GameObject 或字符串参数。没有连线的节点不会执行。

## 三色方块

- 红、蓝、绿方块定义已同步到交互目录，并在预制体上绑定对应功能组件。
- 蓝方块：水体、游泳减速、生命恢复、摔落免疫、蓝绿藤蔓入口。
- 绿方块：落差弹跳、红绿下一跳弹高、站立顶部屏蔽右键轮盘。
- 红/蓝方块：双方颜色条件通过后调用喷流组件；喷流起点、方向、距离、持续时间来自物体配置。
- 锚点预制体：`Assets/_Project/Content/Interactions/Prefabs/AnchorPoint.prefab`；蓝绿交互由 `VineGrowthEmitter` 按最近锚点逐段生长并在来源禁用时清理。

## 颜色钥匙链路

蓝色钥匙的正确运行日志顺序是：

```text
[Interactions] 触发物体图：蓝色钥匙 / PlayerEntered
[Interactions] 方法调用成功：蓝色钥匙.OnInteractionPlayerEntered
[ColorBlocks] Unlock 成功：blue
[ColorBlocks] 钥匙触发入口已解锁 blue：材质恢复/水体显现已开始
```

颜色定义类型必须带 Unity 的 `[Serializable]` 标记。`ColorCatalog.asset` 虽然是文本资产，也必须在运行时确认 `Find("red")`、`Find("green")`、`Find("blue")` 都返回定义；否则钥匙会只播放 Timeline 而不会解锁颜色。

钥匙入口在播放相机 Timeline 之前同步执行 `Unlock` 和 `SetColorFaded(false)`，Timeline 不能阻塞材质恢复和水体显现。

## 玩家操作

- 玩家 `PlayerColorWheel` 只保存当前选色，默认为空，不直接修改物体玩法状态。
- 按住右键显示环形轮盘，鼠标方向高亮颜色，松开右键确认并隐藏。
- 左键刷新交互目标并使用当前能力触发目标物体的 `Manual` 图。
- 交互距离统一使用 `PlayerInteractionSensor.ScanRadius` 与虚线圆；执行前再次检查目标有效、启用和距离。
- Console 会记录默认能力、能力切换、左键交互成功/失败和钥匙链路。

## 验收顺序

1. 清空 Console，让 Unity 完成脚本重载。
2. 运行测试关卡，确认颜色目录能找到三种颜色。
3. 收集蓝色钥匙，按日志确认 `PlayerEntered → InvokeMethod → Unlock → 材质/水体恢复`，不能只看镜头动画。
4. 测试按住右键轮盘、方向高亮、松开确认和左键 Manual 交互。
5. 测试蓝水、红蓝喷流、红绿弹跳、蓝绿锚点藤蔓及 Enter/Exit 清理。
6. 记录触发步骤、颜色饱和度、玩家状态、生成物数量和 Console 日志。

## 已知边界

- `Publisher socket is null` 来自 `com.merry-yellow.code-assist` 的编辑器 MQTT 插件，不属于运行时交互图。
- 轮盘是运行时 UGUI；交互管理器 GraphView 仍使用 UI Toolkit。
- 运行时/编辑器静态编译已验证为 0 错误；完整 PlayMode 事件矩阵仍需在 Unity 窗口内实测。
