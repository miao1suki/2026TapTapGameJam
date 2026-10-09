---
name: taptap-drop-items
description: 2026TapTap 可拾取掉落物开发规范，覆盖 DropItemBase、出现与收集动画、对象池、DOTween 接入和颜色钥匙拾取。适用于新增或修改掉落物、拾取物、钥匙、收集品及启停动画时。
---

# 掉落物开发入口

所有可拾取掉落物统一继承：

`Project.Items.DropItemBase`

基类位置：

`Assets/_Project/Code/Shared/Runtime/DropItemBase.cs`

## 不可违反的结论

- 掉落物的出现动画只在 `OnEnable` 流程触发。
- 拾取必须通过 `TryBeginCollection` 进入收集阶段，不能在子类直接 `Destroy` 或 `SetActive(false)`。
- 收集表现完成后，由基类调用 `RecycleItem`。
- `PlayAppearAnimation`、`PlayCollectAnimation`、`StopItemAnimations` 是统一动画钩子；以后接入 DOTween 时只改这里。
- `PlayCollectAnimation` 必须保证 `onComplete` 只调用一次。
- `OnDisable` 只停止动画和清理，不能等待消失动画。
- 派生类不得提前关闭 Renderer，否则收集动画不可见。
- 对象池掉落物必须重写 `RecycleItem` 走对象池归还；普通掉落物保留默认销毁。
- 对象复用时必须通过 `ResetItemState` 恢复 Collider、Renderer、Transform、材质和逻辑状态。
- 拾取玩法效果可以立即生效，但视觉回收必须等待表现结束。
- 新增或修改自制掉落物组件时，必须提供中文 UI Toolkit Inspector、Foldout、条件显隐和合理调试状态。

## 标准流程

```text
OnEnable
→ ResetItemState
→ PlayAppearAnimation

感知范围
→ TryBeginAttraction
→ PlayAttractAnimation
→ 追向 CollectionTarget 并缩小
→ 真正碰到玩家
→ CompleteAttraction
→ TryBeginCollection
→ OnCollectionStarted
→ PlayCollectAnimation
→ 解锁、销毁和其他拾取表现
→ OnCollectionFinished
→ RecycleItem
```

## 磁吸拾取动画

颜色钥匙以后采用磁吸拾取表现：

- 玩家进入掉落物的感知范围后，由子类调用 `TryBeginAttraction(player)`。
- 基类通过 `CollectionActor` / `CollectionTarget` 把玩家目标交给磁吸动画。
- `PlayAttractAnimation` 在基类中实现“逐渐缩小并追随玩家”。
- 磁吸阶段不结算玩法，也不销毁物体；钥匙仍保留碰撞。
- 真正碰到玩家后才调用 `CompleteAttraction`，再通过 `TryBeginCollection` 进入拾取结算。
- `PlayCollectAnimation` 只处理碰触后的拾取表现，不负责磁吸追随。
- 碰到玩家进入拾取动画时，通过 `IPlayerControlLockTarget.AcquireControlLock` 获取控制锁句柄；动画结束或对象禁用时释放同一个句柄。
- 掉落物不得直接改玩家输入字段、移动状态或逐个关闭玩家操作，控制锁统一由玩家端实现。
- 子类不得再各自写追随或缩小 Tween。
- 不要等玩家已经实际碰撞后才开始追随，那会导致追随动画没有可播放距离。

玩家侧的感知范围统一放在 `PlayerController`：

- `PickupSenseRadiusBlocks`：Inspector 中的“拾取感知半径（格）”，按关卡网格格数填写。
- `PickupSenseRadiusWorld`：根据 `GridCellSizeUtility` 解析出的格子世界尺寸计算出的实际半径。
- `IsWithinPickupSenseRange(worldPosition)`：在 XY 平面上判断掉落物是否处于玩家感知范围。
- 掉落物不得自己维护另一套“拾取感知半径”，也不要把该范围与 `PlayerInteractionSensor.ScanRadius` 混用。

## 子类职责

子类只负责具体玩法，例如：

- 钥匙解锁颜色。
- 关闭拾取碰撞。
- 增加货币、物品或存档数据。
- 发起专用演出。

子类不得重新实现通用的出现、消失、Tween 生命周期、对象池归还或 Renderer 清理流程。

## DOTween 接入约束

项目已导入 DOTween 免费版核心。

- `DropItemBase` 统一实现出现、磁吸和停止动画；收集阶段通过 `IDropItemCollectionAnimation` 接入 `PickupFlightAnimator`。
- Tween 必须记录目标或 Tween ID。
- `OnDisable`、对象池归还、重新启用前必须 Kill 或复位旧 Tween。
- 如果使用暂停或不受 TimeScale 影响的演出，必须显式选择更新方式。
- 子类可以协调额外的专用演出，但通用视觉 Tween 仍在基类中。

## 颜色钥匙参考实现

`Assets/_Project/Code/Systems/ColorBlocks/Runtime/ColorKeyPickup.cs`

颜色钥匙必须保持：

- 碰到玩家后立即关闭 Collider。
- 钥匙进入感知范围后先播放磁吸，不提前解锁或销毁。
- 真正碰到玩家后才解锁颜色并隐藏钥匙。
- Renderer 在碰到玩家后保持可见以播放飞行动画，动画结束时隐藏；实际 GameObject 等镜头演出结束后回收。
- 镜头 Timeline、颜色渐显和基类收集动画可以并行。
- 两边完成后才真正销毁对象。

颜色钥匙的结算点是玩家物理碰撞，不是进入感知范围。

## 完成检查

- Runtime 和 Editor 编译 0 错误。
- `git diff --check` 通过。
- 没有掉落物子类直接 `Destroy`、`SetActive(false)` 或提前隐藏 Renderer。
- 没有在 `OnDisable` 中等待动画。
- 对象池复用不会保留上一次收集状态。
- DOTween 只使用已纳入项目的免费版核心，不提交本机 Pro 资源。
- 拾取感知范围使用玩家上的网格格数配置，不使用掉落物各自的独立半径。
