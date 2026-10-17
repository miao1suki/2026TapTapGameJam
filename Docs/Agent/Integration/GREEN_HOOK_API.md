# 绿色钩子接入 API

源码：`Assets/_Project/Code/Systems/GreenHook`。2026-10-17 更新：复查受阻回退与持续连接，补齐挂接后的超距检查；保留自由瞄准、四向拉取及连续换点。

## 输入与控制所有权

绿色能力仍由 `PlayerColorWheel → GreenHookAbility.TryLaunch` 发射，距离沿用 `PlayerInteractionSensor.ScanRadius`。待机预览与发射自由朝向指针，不吸附四向；端点保存真实点击位置的局部坐标，移动中的目标与玩家会更新绳子端点。出线点共用 `InteractionLinkEffect.SourceWorldPosition`（现有标准玩家为位置上移0.3格），无该组件时才使用后备偏移。红蓝表现不改。

合法点击通过真实目标、发射范围、当前区域与能力资格检查后，当帧挂接并取得唯一控制句柄，立即开始拉取/荡绳，不等待绳头飞行。出线使用与换点相同的视觉插值（默认0.1秒），不参与命中与物理时序；无效点击不取得控制锁。不要恢复旧四向射线/高起点造成实际目标漏接、先锁人再等待命中的路径。旧伸出/收回字段与内部状态仅保留兼容，不是当前正常点击流程。

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

只有一个对象收到非零拉动速度；拉玩家时目标被冻结，重新可拉时先停玩家再动目标。双方受阻或碰到彼此后速度归零，但保持连接，不积累超时、不自动结束；解除障碍后不必重发。主动脱钩不执行跨房间搜索或位置吸附；荡绳主动/超距脱钩保留碰撞求解后的当前动量，但叠加高优先级控制锁时不注入速度。

拉取运动每步只提交一个水平或竖直速度，不沿绳子斜向施力；选定主轴后持续到该轴接近对齐再切换，避免45度附近抖动和贴地微小高度差被当成向地下拉。荡绳不套此四向限制：输入沿切线加速，保留初始动量、重力、轻阻尼与速度上限，绳长为最大约束而非瞬移定位。读取碰撞求解后的刚体速度，松开时也交还该实际速度。

挂接中再次点击合法目标或同一物体上的新位置即可切点，不重开轮盘、不重新锁人、不瞬移玩家；新绳长从当前玩家位置计算，保留切向/向内惯性，去除拉长绳子的向外分量。绳尖默认0.1秒平滑衔接，连续换点从当前可见端点继续。无效、圈外、UI或受额外控制锁的点击保留原连接。

## 物理、边界与性能

发射/换点范围仍是传感器 ScanRadius（标准玩家5格），不扩大可点击范围。挂接后才增加 `attachedRangeSlackCells`（默认3格），实际锚点距离连续超出该维持半径满 `attachedOutOfRangeGraceSeconds`（默认0.4秒）才释放；回到半径内清零计时，暂停不推进计时。标准玩家即发射5格、维持8格，并容忍短暂越过8格。距离按真实局部锚点变换后的坐标，不用鼠标、目标中心或插值绳尖；Update/FixedUpdate均检查，共用同一个连续超距计时。0.001世界单位另防数值误差。此冗余不放宽房间/通道边界、实体碰撞或目标资格；死亡/重生、离区及目标销毁/禁用仍清理。接触、双方不能移动或等待很久都不自动断开。

钩子以执行顺序 -50 在 PlayerController 固定更新前提交速度。`GreenHookPhysics.FreeDistance` 对下一步实体包围盒作无分配扫掠，忽略平行地面支撑、触发器、自身与拉动对端，尊重碰撞矩阵。不再用渲染帧 Transform 位移/累计停滞时间推断受阻。

可推动收集品不直接视作墙；最多八层推挤检查，收集品顶住墙时仍会阻挡。缓冲区饱和或超过深度上限保守停止。钩子线可以穿墙，玩家与被拉物仍受物理实体阻挡。

启动与每个挂接物理步检查 `RoomLifecycleManager.FindRegion`，沿路径以不大于四分之一格采样，始终限定于发射所属区域。没有区域管理器的独立开发场景可测试。禁止将“绳索可穿墙”误写成角色/物品穿墙。

目标/玩家碰撞体在启动时缓存，不每个物理步重新分配层级数组。新增加/移除实体碰撞体的目标需重新挂接以刷新缓存；已有生长体修改同一碰撞体尺寸无需刷新。

## 验证入口

`Project.GreenHook.Editor.GreenHookVerification.Run` 仅允许隔离的 FlowRetirementProbe 项目且强制要求 -noaudio，使用真实 Physics.Simulate 验证拉物优先、受阻回退/重评估、插值、长时间不脱钩、控制锁、荡绳、基座、收集品推挤、四向尖刺与房间边界；另覆盖真实屏幕偏角发射、八象限、共享起点、单轴速度、换点输入和动量，不只绕过发射调用私有挂接方法。新增红/蓝块与基座四方向双方受阻650物理步、解除障碍自动继续、跳跃/交互主动结束、双方移动超距（渲染/物理入口）、最大范围荡绳及藤蔓/弹性植物锚点验证。基座按作者实际重力/刚体设置检查交还，不假定所有基座静止时均为运动学。

`Project.GreenHook.Editor.GreenHookLiveVerification.Run` 在隔离空场景运行真实Update/FixedUpdate与PlayerInputDriver，不手工调用物理步，强制要求 -noaudio；验证键鼠/手柄/触摸输入源下的拉物、控制锁、主动脱钩和连续荡绳换点，另经真实屏幕点击覆盖双方受阻、移走障碍后拉人/恢复拉物、6格维持、短暂超距与持续超距释放。物理验证另检查合法点击同帧接管、范围内重置超距计时及两种更新入口一致。不要在成员当前Scene内运行验证入口，模拟设备输入不代替真实手柄/安卓或实机手感验收。
