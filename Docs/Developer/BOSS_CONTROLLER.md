# Boss 控制器

## 组件

`BossController` 只负责 Boss 的逻辑和运动状态：

- `Chase`：追击目标。
- `Hit`：受击硬直。
- `Death`：死亡。
- `Jump`：按请求目标完成一次可调弧线跳跃。

它不引用动画、Timeline 或具体伤害特效，因此没有动画资源也能正常追击、受击、跳跃和死亡。

`BossAnimationDriver` 是可选表现层，订阅控制器状态并转发 Animator 参数或 UnityEvent。没有 Animator 时不会影响控制器。

## 使用

1. 在 `Boss(暂定)` 预制体上使用 `BossController`。
2. 目标留空时按 `Player` Unity Tag 查找玩家。
3. “目标标签”使用 Unity TagField 下拉，不需要手输字符串。
4. “启用追击”关闭时保留物理和受击/死亡状态，但停止追踪和主动位移。
5. 需要动画时，把 `Animator` 和参数名填到 `BossAnimationDriver`。
6. 跳跃通过 `RequestJump` 或 `RequestJumpTo` 请求，进入 Jump 后由 Rigidbody 完成弧线并在落地时回到 Chase。
7. 需要接 Timeline/动作时，订阅 `StateChanged`、`hitStarted`、`jumpStarted`、`deathStarted` 或直接使用 `StateForwarded`。

## Boss 入场 Timeline

`Assets/_Project/Content/Boss/Timelines/BossIntro.playable` 是 Boss 入场序列资源。它使用 `BossIntroTrack`，在片段开始时生成或瞬移 Boss、关闭追击、移动镜头到入场点，站立指定秒数后调用 Boss 的 Jump 状态，最后重新开启追击。

各段参数都在 `BossIntroClip` 中：Boss 预制体、是否以运行时玩家位置为生成点、固定生成偏移、落地偏移、镜头距离/高度/时长、站立时长、跳跃弧高/时长和结束追击开关。当前 Boss 入场使用编辑模式玩家摆放位置作为固定世界坐标，保持 `z=0`，再向右下小跳；运行时玩家移动不会改变该出生点。Timeline 本身不直接写 Animator；动画仍由 `BossAnimationDriver` 订阅 Jump 事件处理。

## 后续扩展

Boss 专属的阶段、陷阱受击、路线和胜利条件应继续放在 `Assets/Boss_Logic`，不要把 Boss 逻辑写进颜色 Feature 或共享 BlockFeature Host。
