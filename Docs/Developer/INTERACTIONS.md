# 颜色物体与直接组件交互

交互图已退役。当前颜色玩法以固定颜色物体和具体功能组件为核心，不再使用 `InteractionManager`、GraphView、物体定义或 Catalog。

## 规则

1. 红、蓝、绿物体开局是白色失效状态。
2. 拾取对应颜色钥匙后，该颜色组物体恢复原色并启用自身组件。
3. 离开房间后统一失效、清除钥匙效果并清理生成物。
4. 物体放下时形态固定，不在颜色之间变化，也不在同颜色内切换形态。
5. 一个功能只由一个组件负责。
6. 组件之间直接使用类型安全接口，不做反射方法调用。

输入分工：

```text
Tab / F      开关染色轮盘（点击式：再按/选色关闭；长按式：松开/选色关闭）
E            交互按钮、曲柄等 IInteractionTarget
鼠标右键      长按搬运能源方块
鼠标左键     对已选颜色执行染色
```

## 新颜色物体

新预制体使用：

- `ColorObject`：固定 `BaseColorTypeId`、激活状态和视觉切换。
- `BlockAbilityHost`：当前作为过渡期薄宿主，只启停预制体上已经存在的 `BlockFeature`。
- `BlockRuntime`：单物体内的信号、命令、状态和调试总线。

宿主不查 Catalog、不添加新组件、不根据当前颜色切换形态。

## 万能方块

`UniversalColorBlock` 是固定场景代理。

```text
玩家选择已解锁颜色 A
→ 左键点击屏幕上的万能方块
→ 目标在交互圈内才响应
→ 万能方块自身显示 A 的纯色
→ 万能方块向左右上下四个紧邻目标广播 A
→ 每个目标执行自己的固定逻辑
```

它不能搬运，不拥有玩法能力，不参与颜色物体之间的固定反应。

## 颜色应用接口

```csharp
IColorObject
IColorApplicationTarget
IRoomColorResettable
```

直接点击 B 和点击万能方块代理时，最终都调用 B 的 `ApplyColor`。

## 已保留玩法组件

- `ColorRuntimeService`
- `ColorCatalog`
- `HSVColorFadeManager`
- `ColorKeyPickup`
- `InteractiveWater`
- `PlayerController`
- `PlayerHealth`
- `PlayerFallDamage`（当前只记录坠落高度，不结算伤害或免疫）
- `PlayerColorWheel`
- `PlayerInteractionSensor`
- `AnchorPoint`

这些组件不能因为删除图而误删；后续按固定形态逐步拆分或改名。旧喷流、下一跳、弹跳和藤蔓生长触发组件已经删除。

## 房间重置

`ColorRuntimeService.ResetForRoom()` 清除当前房间解锁状态，通知所有 `IColorObject` 失效，并发布房间重置事件。生成平台、蒸汽、藤蔓、喷流和玩家临时状态应由各自的拥有者组件清理。

具体开发约束见：

`Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`
