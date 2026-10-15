# 绿色钩子接入 API

源码：`Assets/_Project/Code/Systems/GreenHook`。2026-10-15 更新：成功挂接后不自动脱钩，覆盖旧的接触结束、停滞结束和超时结束规则。

## 输入与控制所有权

绿色能力仍由 `PlayerColorWheel → GreenHookAbility.TryLaunch` 发射，距离沿用 `PlayerInteractionSensor.ScanRadius`，吸附四向。未命中才原速收回。伸出阶段按线段检测目标，避免低帧率直接跨过小物体。

已挂接时 `PlayerInputDriver` 先通过 `PlayerController.UpdateGreenHookInput(move, release)` 分发输入，再处理普通控制锁。现有 Jump 或 Interact 动作主动脱钩；键鼠、手柄、触摸都沿用原动作映射，不增设硬编码按键。Respawn 仍可返回重生点。

`PlayerController.CanDriveGreenHook` 只在钩子持有唯一控制锁时成立；叠加 Timeline 等控制锁后暂停运动和主动操作，不绕过高优先级控制。暂停时不处理脱钩输入。死亡、重生、离区、目标销毁/禁用会取消连接并释放同一锁句柄。

## 目标与运动契约

`IGreenHookTarget` 定义目标资格、根 Transform 和模式：

| 模式 | 当前对象 | 行为 |
| --- | --- | --- |
| PullTarget | 红/蓝 ContactCube、机关基座 | 每物理步优先尝试拉物，物体受阻则拉玩家；下个物理步仍优先检查物体 |
| Swing | 绿色 ContactCube（含一体生长部分）、ClimbableVineFeature、BouncePlantFeature | 点击位置作为锚点并随目标变换；保留重力与切向输入并限制最大绳长，不把锚点拉走 |
| PullPlayer | 保留扩展契约 | 直接拉玩家，接触/受阻后保持挂接 |

需要实际移动的对象实现 `IGreenHookMotion`：

```csharp
Vector3 GreenHookPosition { get; }
void BeginGreenHookPull();
void SetGreenHookVelocity(Vector3 velocity);
void EndGreenHookPull(bool snapToGrid, Vector3 separationDirection, float separationDistance);
```

`ContactCubeMotion` 接管自身刚体并暂停接触反应，入钩时恢复玩家碰撞对；`GreenHookBodyMotion` 为基座接管刚体，结束后恢复原运动类型、重力、约束。基座没有迁移成 ContactCube，不改变信号契约。旧场景通过 MechanismBaseFeature.OnAttach 自动补齐运动组件，无需全量重建。

只有一个对象收到非零拉动速度；拉玩家时目标被冻结，重新可拉时先停玩家再动目标。碰到彼此后速度归零，但保持连接，直到主动结束。主动脱钩不执行跨房间搜索或位置吸附；荡绳脱钩保留当前动量。

## 物理、边界与性能

钩子以执行顺序 -50 在 PlayerController 固定更新前提交速度。`GreenHookPhysics.FreeDistance` 对下一步实体包围盒作无分配扫掠，忽略平行地面支撑、触发器、自身与拉动对端，尊重碰撞矩阵。不再用渲染帧 Transform 位移/累计停滞时间推断受阻。

可推动收集品不直接视作墙；最多八层推挤检查，收集品顶住墙时仍会阻挡。缓冲区饱和或超过深度上限保守停止。钩子线可以穿墙，玩家与被拉物仍受物理实体阻挡。

启动与每个挂接物理步检查 `RoomLifecycleManager.FindRegion`，沿路径以不大于四分之一格采样，始终限定于发射所属区域。没有区域管理器的独立开发场景可测试。禁止将“绳索可穿墙”误写成角色/物品穿墙。

目标/玩家碰撞体在启动时缓存，不每个物理步重新分配层级数组。新增加/移除实体碰撞体的目标需重新挂接以刷新缓存；已有生长体修改同一碰撞体尺寸无需刷新。

## 验证入口

`Project.GreenHook.Editor.GreenHookVerification.Run` 仅允许隔离的 FlowRetirementProbe 项目，使用真实 Physics.Simulate 验证拉物优先、受阻回退/重评估、插值、长时间不脱钩、控制锁、荡绳、基座、收集品推挤、四向尖刺与房间边界。不要在成员当前 Scene 内运行此入口。
