# 拾取飞行动画接入

`Project.Pickups.PickupFlightAnimator` 位于 `Assets/_Project/Code/Systems/PickupPresentation/Runtime`。它只播放表现，不检测拾取资格，不发放奖励，不修改背包，也不销毁拾取物。DOTween 是运行依赖。正式掉落物继承 `Project.Items.DropItemBase`；基类的收集动画钩子通过 `IDropItemCollectionAnimation` 自动调用同物体的飞行动画，无须子类直接调用 `Play`。

把组件放在拾取物根对象上，`visual` 可以指向外观子对象；若外观就在根对象上，也可指向根 Transform。`pickupCollider` 指向拾取触发器。独立测试物确认拾取成立后可以直接调用：

```csharp
bool started = pickupFlightAnimator.Play(player.transform, () =>
{
    // 表现完成后的结算、销毁或对象池回收由正式拾取系统决定。
});
```

`Play` 返回 `false` 表示正在播放、目标无效或外观不可见。开始后组件会关闭配置的拾取碰撞体，防止重复触发。完成后外观移动到玩家中心并隐藏，执行一次完成回调；根对象仍存在。若根对象在播放中被禁用，DOTween 动画被终止且不执行完成回调；正式逻辑应自行处理取消场景。颜色钥匙由 `DropItemBase` 在玩家感知圈内先磁吸，碰撞玩家后立即解锁颜色，再并行播放飞行与镜头演出，两者结束后回收。

轨迹分为四段：短促地飞向远离玩家的随机落点、快速到玩家头顶、短暂减速向中心靠近、再次快速收至中心。反弹距离与高度每次播放都随机变化；飞离与飞往均使用双控制点的三次贝塞尔曲线，飞往头顶的第一控制点还保留向外的惯性，使转向成为可见的回旋弧线。各段另有独立的随机控制点，所以不同物体不会沿同一条固定路线。头顶与最终中心仍精确跟随玩家移动。时长、头顶高度、反弹和路径随机范围可在中文 Inspector 中配置。缓动在减速段外避免连续归零；若实际帧率下降，应使用 Unity Profiler 分辨渲染、脚本或 Editor 开销，不能仅凭轨迹的视觉停顿判定为性能问题。

`Assets/_Project/Development/PickupFlight/PickupFlightTestTrigger.cs` 仅供 `Assets/Scenes/Pick.unity` 测试。它在玩家碰到测试球时调用动画，不应被正式拾取逻辑依赖。测试预制体 `PickupFlight_Test.prefab` 同样不代表正式拾取物定义。
