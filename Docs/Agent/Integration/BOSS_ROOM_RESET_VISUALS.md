# Boss 清障后的房间可见性恢复

## 所有权与修复边界

Boss 清障仍由 `BossController` 的碰撞队列发起，`TemporaryObjectHideState` 只临时关闭实体碰撞、强制隐藏 Renderer 并挂起 Rigidbody。原对象、作者位姿和共享材质不销毁；不调用全局颜色重置，也不强制开启全部 Renderer。

`RoomResetObject.Persistent` 表示门、保持按钮、锚点和已结算物品的进度不回滚，不代表 Boss 的临时隐藏永久保留。房间重置先释放机关的临时隐藏租约，再跳过持久对象的作者姿态及逻辑重置。已收集或正在收集的 `DropItemBase` 仍完全交还掉落物生命周期，不恢复其收集碰撞。

门在隐藏期间仍可能达成按钮条件。释放隐藏时调用 `DoorController.RefreshBlockingState()`，只根据当前 `DoorState` 重申阻挡，不调用 `ResetDoor()`、不清按钮、不开新演出、不改门轴动画。这样不会把隐藏前的“关门碰撞”快照错误恢复到现已开启的门上。

每次 `RoomResetObject.RestorePose()` 推进只读 `ResetRevision`。Boss 的预算化隐藏队列记录入队时的版本；碰撞入队后若发生 R、死亡或离区复位，该旧任务失效。新的真实碰撞仍可重新排队，不全局清除另一个房间的有效工作。

## 已定位的问题

1. 旧 `RestorePose()` 先因门的 `Persistent` 返回，根本未执行 `TemporaryObjectHideState.Restore()`。因此门对象和材质还在，但 `forceRenderingOff=true` 永远留下，看起来像门或材质消失。
2. Boss 的隐藏工作在物理碰撞时入队、`LateUpdate` 按预算执行；旧队列没有重置世代校验，能在房间已经恢复后再次隐藏刚恢复的门或平台。

不通过修改场景、重建规划图、重置解锁、清空门进度或材质替换掩盖问题。

## 验证入口

`Project.BossLogic.Editor.BossRoomResetVisualAudit.Run`，仅静音 `Library/FlowRetirementProbe` 隔离工程允许执行。测试源码按 Boss 专属约定位于 `Assets/Boss_Logic/Editor`。

使用真实门、按钮、红色移动平台和标准玩家 Prefab，以及实际 Boss 隐藏队列。覆盖锁色、中间渐显、完成渐显、平台运动、隐藏期间开门、真实 `RequestRespawn()`、连续 8 轮、离区、死亡、旧排队工作失效、附加 Scene 卸载。断言对象激活、Renderer 门控、`forceRenderingOff`、Sprite、材质、Shader、门碰撞及按钮进度；测试不保存 Scene 或 planning。

执行结果由整合者填写到本次 `ACTIVE_WORK`，不能把源码中断言数量当成已执行结果。关卡交付遵循 Scene 权威规则，提交 Scene/`.meta`、Scene 引用的作者资产与新增依赖；历史 planning 不再同步。此修复不改变布局和作者数据。
