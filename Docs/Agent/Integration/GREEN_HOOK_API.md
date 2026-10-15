# 绿色钩子接入 API

源码目录：`Assets/_Project/Code/Systems/GreenHook`。

## 玩家入口

`PlayerColorWheel.TryUseSelectedAbility()` 在选中绿色时调用
`GreenHookAbility.TryLaunch`。操作输入仍由 `PlayerInputDriver` 通过
`PlayerController.TryUseSelectedColorAbility()` 转发，不在输入层增加特殊按键。

```text
绿色选中
→ PlayerColorWheel.Tick 更新待机藤蔓头
→ 左键点击有效目标
→ GreenHookAbility.TryLaunch
→ 锁控制、伸藤
→ 命中目标：拉动、清理
→ 未命中：原速收回、解除限制
```

## 目标契约

目标实现 `Project.GreenHook.IGreenHookTarget`：

```csharp
GreenHookMotionMode GreenHookMotionMode { get; }
Transform GreenHookTransform { get; }
bool CanBeGreenHooked(GameObject actor);
```

当前实现：

- `ContactCubeMotion`：`PullTarget`，物块向玩家移动。
- `ContactCubeMotion` 到玩家的回收路径上检测到任何物理物体时立即切换为拉玩家；只有玩家也无法接近时才结束。
- 钩子视觉终点使用目标有效碰撞体的包围盒中心；接触和路径阻挡仍使用碰撞体表面与带厚度的路径检测。
- 目标接触玩家时立即结束拉动，并沿两者连线做最小分离，避免玩家和目标碰撞体重叠。
- `MechanismBaseFeature`：`PullPlayer`，玩家向基座移动。
- `ClimbableVineFeature`：`PullPlayer`，玩家向藤蔓移动。
- `BouncePlantFeature`：`PullPlayer`，玩家向弹性植物移动。

## 玩家运动接口

`PlayerController` 提供仅供 `GreenHookAbility` 使用的钩子运动状态：

```csharp
BeginGreenHookPull(Component source)
SetGreenHookPullVelocity(Component source, Vector2 velocity)
EndGreenHookPull(Component source)
```

钩子运行期间，`PlayerController.FixedUpdate` 使用钩子速度并且不执行普通重力、
移动和跳跃流程。控制锁同时阻止交互、染色和搬运。

## 目标运动

`ContactCubeMotion` 提供：

```csharp
BeginGreenHookPull()
SetGreenHookVelocity(Vector3 velocity)
EndGreenHookPull()
```

钩子期间 `ContactCubeMotion` 停止自身固定更新，`ContactCube` 停止接触反应。
结束时恢复 Rigidbody 重力、约束和接触反应。

## 房间边界

`GreenHookAbility.IsPathWithinRoom` 使用 `RoomLifecycleManager.ForScene` 和
`FindRegion`，从玩家位置到矫正后的落点以及目标点逐格检查。没有区域管理器的开发
场景允许运行；正式场景必须保持同一区域归属。

## 表现约束

- `InteractionLinkEffect` 只负责红、蓝预览，不再绘制绿色预览连线。
- 绿色待机头和正式钩子复用 `InteractionLink.mat` 的绿色藤蔓样式。
- 出藤点默认在玩家身体偏上位置；正式钩子默认 `5.22 格/秒`。
- 待机头一格、透明度 `50%`；正式钩子使用独立 `LineRenderer` 和绿色样式。
- 钩子结束使用材质溶解退场，不留下第二条预览线。
- 被拉动的 `ContactCube` 在落稳后会反复校验最近可用网格；房间归属、关卡占用、
  可搬运物占用和物理碰撞均通过后才吸附，不覆盖已有方块。
