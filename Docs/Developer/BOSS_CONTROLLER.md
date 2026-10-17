# Boss 控制器

## 组件

`BossController` 只负责 Boss 的逻辑和运动状态：

- `Chase`：追击目标。
- `Hit`：保留的兼容状态；自动受伤流程不再进入该状态。
- `Death`：死亡。
- `Jump`：按 Boss 自身重力与速度能力完成一次物理跳跃。

当前 `BossController` 默认关闭 `enableAutonomousChase`，常规运行时不会自己追击或右移，只作为 Timeline 和外部马夫可调用的运动能力。入场 Timeline 结束后通过 `SetTrackingEnabled(true)` 开启运动门。

右移由独立组件 `BossRightMoveExecutor` 负责。它是“马夫”，等待 Boss 运动开启后，只调用 `BossController.SetExternalMove(1)`、`SetExternalHeight(接管瞬间记录的 Boss 当前 Y)` 和 `SetPassThroughTerrain(true)`，不直接写 Rigidbody。速度由 `BossController.moveSpeed` 提供，长廊默认值为 `2.5`。不再在 Inspector 手填移动高度。Timeline 演出期间 Collider 保持正常世界交互；开始右移后切为 Trigger，可以穿过地形，同时保留后续道具通过 Trigger 或直接调用 `TakeDamage` / `ReceiveHit` 造成伤害的入口。Renderer 使用项目最高排序层 `BossTop`、排序值 `32767` 和 Renderer Priority `32767`。

它不引用动画、Timeline 或具体伤害特效，因此没有动画资源也能正常追击、受击、跳跃和死亡。

`BossAnimationDriver` 是可选表现层，订阅控制器状态并转发 Animator 参数或 UnityEvent。没有 Animator 时不会影响控制器。

## 使用

1. 在 `Boss(暂定)` 预制体上使用 `BossController`。
2. 目标留空时按 `Player` Unity Tag 查找玩家。
3. “目标标签”使用 Unity TagField 下拉，不需要手输字符串。
4. “启用自动目标追击”默认关闭；常规长廊不需要打开。
5. `BossRightMoveExecutor` 的“等待 Boss 运动开启”开启后，Timeline 的整个 PlayableGraph 停止并调用 `SetTrackingEnabled(true)` 才会开始右移。
6. 右移马夫首次接管时自动记录 Boss 当前世界 Y，之后始终沿该高度右移；时间轴演出期间仍使用正常重力。
7. 需要动画时，把 `Animator` 和参数名填到 `BossAnimationDriver`。
8. 跳跃通过 `RequestJump` 或 `RequestJumpTo` 请求；只有目标在最大跳高、最大垂直速度和水平速度能力内才会执行，进入 Jump 后由 Rigidbody 按配置时长完成物理弹道，向下跳不会被起跳点的地面判定提前取消。
9. 需要接 Timeline/动作时，订阅 `StateChanged`、`hitStarted`、`jumpStarted`、`deathStarted` 或直接使用 `StateForwarded`。

## 碰撞隐藏与无敌调试

Inspector 的“碰撞隐藏、受击与无敌”分区集中放置生存、清障和闪烁参数：

- Boss 开始由右移马夫接管后，碰到非玩家、非 Boss 物体会隐藏，不再销毁对象。
- Boss 当前最大和初始血量均为 9。
- 有房间生命周期组件的物体按原有生命周期隐藏，R 或离区恢复；其他对象直接停用，减少销毁造成的卡顿。
- 碰撞回调只登记唯一目标，不直接停用对象；`LateUpdate` 按“每帧最多隐藏”预算逐个处理，避免物理回调中同时移除大量 Collider 造成峰值。
- 实际隐藏使用 `TemporaryObjectHideState`：只关闭 Renderer、Collider，并临时把 3D Rigidbody 切为 Kinematic/关闭重力，不 `SetActive(false)` 整棵对象，避免触发大量 `OnDisable`、UI/美术刷新和房间生命周期回调。
- 重复碰撞通过实例 ID 去重；同一物体的多个子 Collider 不会重复排队。穿过地形开关只在状态真的改变时写入 Collider。
- 碰岩浆时先扣一次“岩浆单次伤害”，默认 1，然后进入默认 4 秒无敌。
- 无敌期间任何伤害都不再结算，但岩浆照样会被隐藏。
- 普通受伤没有硬直和击退，也不会取消跳跃；右移不会停止，只是在无敌期间按“无敌时右移速度倍率”减速，当前默认 0.7，4 秒内等效移动 7 格。
- 整个 Boss 对象按“无敌闪烁间隔”显隐；进入无敌时会自动收集自身及当前全部子 Renderer，包括后续加入的手部动画，不需要手工配置。
- 如果不想在入场阶段隐藏脚下地图，保留“仅外部移动时隐藏”开启。
- 血量归零后右移立即停止；“死亡演出请求（接口）”可在后续接线时让桥接组件绑定玩家并播放 Timeline，控制器本身不直接引用 Timeline 资源。

## Boss 血条

血条位于 Boss 预制体的 `Boss Health Canvas` 子物体，渲染在玩家屏幕中心上方。Boss 刚出生时血条隐藏且无法受伤；需要由后续 Timeline、剧情桥接或调试按钮调用：

```csharp
BossController.ActivateHealthSystem();
```

开启后：

- 血条从 0 在默认 0.35 秒内快速涨满。
- 当前 9 点血自动分成 9 段，中间生成 8 条棱角分明的纯黑竖直分隔线。
- 血条外框以外全透明；底层拖尾血皮为纯白，当前血层为纯色肉红 `#B23E3E`。
- 外框为硬边双层纯色结构：1px 黑外缘、项目暗红 `#523B42` 框体，以及左上/右下 `#F65332` 短角部强调。
- 受伤时肉红血层快速下降，纯白血皮较慢跟随。
- 若在 Inspector 修改最大血量后重新生成并激活 Boss，段数会按新血量自动重算。
- 长度、高度、屏幕位置、外框、颜色、分隔线和两层 Lerp 速度都在 `BossHealthBarUI` 的独立 Inspector 分区中调试。
- 血条不再引用任何图片材质；外框、血量和分隔线全部为无 Sprite 的纯色硬边 `Image`，不创建底板或图片描边。

## Boss 入场 Timeline

`Assets/_Project/Content/Boss/Timelines/Boss_appear.playable` 不再使用 `BossIntroTrack` / `BossIntroClip`。当前总时长为 `6.55` 秒，由五条 `BossTimelineTrack`、一条 2D 相机轨、一条对话轨和一条末尾 Playable 轨组成：

- 生成/发呆：生成 Boss 到当前场景编辑态玩家位置 `(-9.07, -33.33, 0)`，保持 `2.1333333` 秒。
- 并行移动输入：与跳跃同时从 `4` 秒持续到 `5.4` 秒，只向 `BossController.SetWalkInput` 写入向右输入；Walk 速度为控制器参数 `3` 格/秒。
- 关闭运动：确认 Boss 运动门处于关闭状态。
- 跳跃输入：与移动 Track 同时从 `4` 秒持续到 `5.4` 秒，只向 `BossController.RequestJump()` 发送一次请求；弹道、重力、惯性、碰撞和落点均由控制器与物理决定。
- 实际结果：向右移动约 `1.4` 秒后停在 X 约 `-4.87`，再受重力落入场景中世界 X `-7…-2`、碰撞顶面 Y `-41` 的弹性植物组。
- 落地发呆：从 `6` 秒保持 `0.55` 秒，覆盖 Boss 在约 `6.02` 秒首次接触植物后的半秒停顿，Timeline 于 `6.55` 秒结束。
- 开启运动与血条：在 `6.54` 秒打开运动门并激活血条，随后释放玩家控制。
- 对话：从 `1.0166667` 秒开始，展示时长延长为 `2.7916667` 秒，在 `3.8083333` 秒消失；字号为 `84`，启用加粗，WordNote 显示时长参数为 `5` 秒。
- 台词 Clip 在 `3.8083333` 秒释放，并播放 `0.18` 秒退场动画，完整淡出到 `3.9883333` 秒；Boss 移动与跳跃输入从 `4` 秒开始，确保文字完全消失后才行动。
- 血条激活：在时间轴最后一刻调用 `BossController.ActivateHealthSystem()`，血条初始化后才开放受伤。
- Boss 入场触发器在场景实例上开启“播放期间申请玩家控制锁”；Timeline 开始即锁定移动、跳跃、交互、搬运和染色，到总时长末尾由 `ContactTimelineTrigger` 释放。

每个 Boss Clip 只能有一个动作。面板只显示当前动作的数据，以及 `spawnBoss` / `bossPrefab`；移动动作只显示 `moveInput`，跳跃不显示目标或速度。Boss Timeline 是操作输入层，不保存“最终走到哪里、跳多高、落在哪”等结果数据；这些都由 `BossController`、刚体、重力和场景碰撞决定。Actor Timeline 层不得直接写 Boss 的 Rigidbody 或 Transform。相机轨只允许 2D 正交运镜，负责跟随 Boss。

移动输入 Track 调用 `BossController.SetWalkInput`，跳跃 Track 调用 `BossController.RequestJump()`。两条 Track 处于同一时间窗口，因此 Boss 可以边向右移动边受重力跳跃。Timeline 不传送、不纠偏，也不在片段结束时改回位置。

相机轨结束后会把 `TimelineCamRig.target` 恢复为 `CameraControlManager.FocusTarget`，并释放 Timeline 相机控制权，让玩法相机重新跟随玩家。

相机 Clip 使用“锚点 Tag = Boss”显式跟随 Boss，不再由 Boss 行为 Clip 偷偷改写相机目标。释放发生在相机 Clip 到达自然末尾时，不依赖 `OnGraphStop`：当前 PlayableDirector 使用 Hold 模式，播放到末帧后只会停住，不会销毁 Graph，因此旧实现一直等不到 Graph 停止回调，控制权会留在 Timeline。

2D 相机轨的目标高度为 `-0.3`，进入 Boss 机位时正交视野扩大 `1.15` 倍。正交镜头只使用固定平面 Yaw，完全忽略角色 Transform 的旋转，避免角色转身或镜头移动时露出方块顶面和 3D 侧壁。

Boss 根物体使用 `3×3×3` 缩放，外观和碰撞同步放大。入场时保持本地 `1×1×1` BoxCollider；右移马夫接管后临时把本地高度拉到 `12`，在世界空间覆盖约 `3×36×3` 格，保证经过路线上下高低处都会被吞噬，停止穿行时恢复作者碰撞。血条 Canvas 用 `1/3` 缩放抵消父物体比例。

## 后续扩展

Boss 专属的阶段、陷阱受击、路线和胜利条件应继续放在 `Assets/Boss_Logic`，不要把 Boss 逻辑写进颜色 Feature 或共享 BlockFeature Host。
