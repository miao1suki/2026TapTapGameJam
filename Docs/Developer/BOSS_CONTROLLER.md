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

## 受击、无敌与接触伤害

Inspector 的“受击、无敌与接触伤害”分区集中放置生存和闪烁参数：

- Boss 不再吃方块，也不会隐藏、停用或销毁任何普通物体、机关或岩浆。
- 碰到玩家时仍会直接触发玩家死亡。
- Boss 当前最大和初始血量均为 9。
- 穿行碰撞高度、仅移动中结算接触伤害等原有运动和伤害参数保持不变。
- 受击后进入无敌并持续闪烁，期间不结算后续伤害。
- 碰岩浆时先扣一次“岩浆单次伤害”，默认 1，然后进入默认 4 秒无敌。
- 岩浆不会因 Boss 接触而隐藏或停用；只有右移马夫或 execution 状态下的接触才结算伤害。
- 普通受伤没有硬直和击退，也不会取消跳跃；右移不会停止，只是在无敌期间按“无敌时右移速度倍率”减速，当前默认 0.7，4 秒内等效移动 7 格。
- 整个 Boss 对象按“无敌闪烁间隔”显隐；进入无敌时会自动收集自身及当前全部子 Renderer，包括后续加入的手部动画，不需要手工配置。
- 血量归零后右移立即停止，并调用 `defeatPresentationRequested`。Boss 预制体已把该事件绑定到 `BossDefeatTimelinePresenter`，由它播放独立的 `Boss_defeat.playable`。
- Boss 存活期间禁用玩家疾跑，死亡或禁用后释放；每次玩家复活时，先完成房间重建，再销毁 Boss 左边缘到 Boss 当前中心 X 范围内的普通/重力岩浆，最后让 Boss 按重生点配置归位。

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

## Boss 死亡 Timeline

`Assets/_Project/Content/Boss/Timelines/Boss_defeat.playable` 是独立的击败演出，只在 `BossController.EnterDeath` 发出 `defeatPresentationRequested` 时播放。直接 `Destroy` Boss 不会触发该事件，因此不会误播。

当前总时长为 `5` 秒：

- `0–1` 秒：镜头从玩家当前机位平滑 lerp 到 `Boss` Tag 对应角色。
- `1` 秒：`BossDeathAnimationTriggerPlayable` 调用 `BossAnimationDriver.PlayDeathAnimationNow()`，确保以后接入死亡动画时，镜头已经到位才开始播。
- `1–3` 秒：停留在 Boss 死亡位置。当前没有死亡动画，因此固定观看 `2` 秒；后续接入 Animator/死亡轨道时可直接延长这一段。
- `3–4` 秒：额外保持 `1` 秒。
- `4–5` 秒：镜头曲线平滑回到演出开始时的玩家机位。
- `0–5` 秒：`PlayerTimelineTrack` 按 `Player` Tag 锁住玩家移动、跳跃、交互、搬运和染色。
- `4` 秒：`BossDeathCameraRestorePlayable` 调用 `BossCameraSize.RequestDisable()`，让 Boss 战视野倍率与镜头回归同步收束。

Timeline 不保存玩家或 Boss 的世界坐标。去程和回程以播放前实际相机状态为起点，Boss 机位通过 `Boss` Tag 解析，因此换场景位置后无需硬编码坐标。

`BossAnimationDriver.waitForPresentationDeathTrigger` 在 Boss 预制体上默认开启：进入 Death 时只切换状态和停止马夫，不立即触发 Animator 死亡 Trigger；真正动画由死亡 Timeline 的第 1 秒 Playable 触发。没有 Animator 时调用安全跳过，Timeline 仍固定观察 2 秒。

## Boss 入场 Timeline

`Assets/_Project/Content/Boss/Timelines/Boss_appear.playable` 不再使用 `BossIntroTrack` / `BossIntroClip`，也不再包含镜头轨。资产只保留一条覆盖全程的 `PlayerTimelineTrack` 和一条 Boss 专用 `BossAppearCinematicPlayable`。

- `0–1` 秒：锁住玩家操作，镜头固定在玩家播放开始位置加 `playerCameraOffset=(0,0,-10)`。
- `1` 秒后：底部以故障动画显示“别急着走啊，你以为我能让你如愿?”，随后以打字机形式显示“？”。
- 问号结束后：在玩家当前位置 `X-7 / Y+13` 生成 Boss；Boss 不开启追迹，由 Playable 以可调加速度假下落到玩家同高。
- 落地后：固定机位停留 `1` 秒，再以打字机形式显示“不好！”。文字结束后交还玩家与相机权限，开启追击、血条、监控和 `BossCameraSize`。
- 整个过程中没有左移、没有 Boss 跟随和没有回程运镜；`BossAppearCameraGuard` 在最高 `LateUpdate` 执行顺序锁住实际 Camera Transform 与镜头参数。

所有空间偏移均为激发器局部值，时长、字号、打字速度、故障参数、下落速度和停留时间均序列化在 Playable 资产中。Boss 根物体使用 `3×3×3` 缩放，外观和碰撞同步放大；血条 Canvas 用 `1/3` 缩放抵消父物体比例。

## Boss 存活期视野

`BossCameraSize` 挂在 Boss 预制体根物体上，默认不提交申请。Timeline 最后一帧调用 `RequestEnable()` 后：

- 向 `CameraControlManager` 申请玩家玩法正交视角倍率，默认 `2` 倍。
- 扩大和恢复时间由组件的“平滑时间（秒）”统一控制，默认 `1` 秒。
- `CameraControlManager` 只在当前相机源优先级低于 `Cutscene` 时叠加倍率；入场、死亡演出等 Timeline 相机接管期间不会覆盖演出镜头。
- 玩家死亡、复活和相机重新跟回玩家都不会撤销请求，因此复活后仍保持 Boss 战视野。
- 组件不再在 `Died` 时自动恢复；死亡 Timeline 在返回镜头开始时调用 `RequestDisable()`。对象被外部销毁或场景卸载时仍在 `OnDisable` 立即清理请求。

## 后续扩展

Boss 专属的阶段、陷阱受击、路线和胜利条件应继续放在 `Assets/Boss_Logic`，不要把 Boss 逻辑写进颜色 Feature 或共享 BlockFeature Host。
