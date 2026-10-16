# 绿色钩子接入 API

源码：`Assets/_Project/Code/Systems/GreenHook`。2026-10-16 更新：自由瞄准与四向拉取分离，成功挂接后可直接换点；继续保留主动脱钩规则。

## 输入与控制所有权

绿色能力仍由 `PlayerColorWheel → GreenHookAbility.TryLaunch` 发射，距离沿用 `PlayerInteractionSensor.ScanRadius`。待机预览与发射自由朝向指针，不吸附四向；端点保存真实点击位置的局部坐标，移动中的目标与玩家会更新绳子端点。出线点共用 `InteractionLinkEffect.SourceWorldPosition`（现有标准玩家为位置上移0.3格），无该组件时才使用后备偏移。红蓝表现不改。

伸出阶段不持有玩家控制锁、不清速度；确认命中才取得一个控制句柄并接管运动。未命中原速收回，失效、超范围、取消选色或离区取消伸出。伸出阶段按线段检测目标，避免低帧率跨过小物体。不要恢复“发射即锁人”的旧实现，旧四向射线与0.75格起点组合曾使实际点击目标漏接并冻结玩家。

已挂接时 `PlayerInputDriver` 先通过 `PlayerController.UpdateGreenHookInput(move, release)` 分发输入，再处理普通控制锁；同时刷新世界指针并将 PointerPrimary 转发至现有选色入口以切换目标。现有 Jump 或 Interact 动作主动脱钩，同帧脱钩优先于换点；键鼠、手柄、触摸沿用原动作映射，不增设硬编码按键。Respawn 仍可返回重生点。

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

拉取运动每步只提交一个水平或竖直速度，不沿绳子斜向施力；选定主轴后持续到该轴接近对齐再切换，避免45度附近抖动和贴地微小高度差被当成向地下拉。荡绳不套此四向限制：输入沿切线加速，保留初始动量、重力、轻阻尼与速度上限，绳长为最大约束而非瞬移定位。读取碰撞求解后的刚体速度，松开时也交还该实际速度。

挂接中再次点击合法目标或同一物体上的新位置即可切点，不重开轮盘、不重新锁人、不瞬移玩家；新绳长从当前玩家位置计算，保留切向/向内惯性，去除拉长绳子的向外分量。绳尖默认0.1秒平滑衔接，连续换点从当前可见端点继续。无效、圈外、UI或受额外控制锁的点击保留原连接。

## 物理、边界与性能

钩子以执行顺序 -50 在 PlayerController 固定更新前提交速度。`GreenHookPhysics.FreeDistance` 对下一步实体包围盒作无分配扫掠，忽略平行地面支撑、触发器、自身与拉动对端，尊重碰撞矩阵。不再用渲染帧 Transform 位移/累计停滞时间推断受阻。

可推动收集品不直接视作墙；最多八层推挤检查，收集品顶住墙时仍会阻挡。缓冲区饱和或超过深度上限保守停止。钩子线可以穿墙，玩家与被拉物仍受物理实体阻挡。

启动与每个挂接物理步检查 `RoomLifecycleManager.FindRegion`，沿路径以不大于四分之一格采样，始终限定于发射所属区域。没有区域管理器的独立开发场景可测试。禁止将“绳索可穿墙”误写成角色/物品穿墙。

目标/玩家碰撞体在启动时缓存，不每个物理步重新分配层级数组。新增加/移除实体碰撞体的目标需重新挂接以刷新缓存；已有生长体修改同一碰撞体尺寸无需刷新。

## 验证入口

`Project.GreenHook.Editor.GreenHookVerification.Run` 仅允许隔离的 FlowRetirementProbe 项目，使用真实 Physics.Simulate 验证拉物优先、受阻回退/重评估、插值、长时间不脱钩、控制锁、荡绳、基座、收集品推挤、四向尖刺与房间边界；另覆盖真实屏幕偏角发射、八象限、共享起点、单轴速度、换点输入和动量，不只绕过发射调用私有挂接方法。

`Project.GreenHook.Editor.GreenHookLiveVerification.Run` 在隔离空场景运行真实Update/FixedUpdate与PlayerInputDriver，不手工调用物理步；验证键鼠/手柄/触摸输入源下的拉物、控制锁、主动脱钩和连续荡绳换点。不要在成员当前Scene内运行验证入口，自动测试不代替实机手感验收。
