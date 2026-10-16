# Boss 控制器接入 API

## 源码

- `Assets/Boss_Logic/BossController.cs`
- `Assets/Boss_Logic/BossAnimationDriver.cs`

## 状态

`BossStateId`：

- `Chase`
- `Hit`
- `Death`
- `Jump`

`BossController` 实现 `IDamageable`，可通过 Timeline HitBox 或外部陷阱调用 `TakeDamage` / `ReceiveHit`。

检查器中的目标标签使用 Unity `TagField`；`trackingEnabled` 控制是否执行目标查找和追击，关闭后不阻断受击、跳跃与死亡状态。

## 分离边界

- `BossController` 只写 Rigidbody、目标、状态和受击数值。
- 动画、Timeline 和动作实现只听事件，不反向驱动控制状态。
- `BossAnimationDriver` 可以完全不配置 Animator；控制器仍然工作。
- `RequestJumpTo(Vector3 worldTargetPosition, float arcHeight = -1, float duration = -1)` 请求一次成弧跳跃；`RequestJump(Vector2 direction, float distance, ...)` 按方向请求。
- Jump 状态会临时接管 Rigidbody，落地后恢复原刚体设置并回到 Chase；受击和死亡会安全取消 Jump。

## Boss 入场 Timeline 接口

- Timeline 资源：`Assets/_Project/Content/Boss/Timelines/BossIntro.playable`
- 轨道：`BossIntroTrack`
- 片段：`BossIntroClip`
- 片段参数：`bossPrefab`、`useExistingBoss`、`bossTag`、`usePlayerPositionAsSpawn`、`playerTag`、生成偏移、落地偏移、镜头参数、站立时长、跳跃弧高/时长、结束后是否开启追击。
- `usePlayerPositionAsSpawn=false` 时，`spawnOffset` 是固定世界坐标；Boss 入场当前使用编辑模式玩家位置 `(-9.4, -32.1, 0)` 作为固定出生点，不跟随运行时玩家。
- 不要求轨道绑定；Timeline 在运行时按 Tag 复用已有 Boss，找不到时按 `bossPrefab` 生成。

## 事件

- `StateChanged(previous, next)`
- `Damaged(amount, remainingHealth)`
- `Died`
- `BossAnimationDriver.StateForwarded(previous, next)`

## 预制体

`Assets/_Project/Content/Boss/Boss(暂定).prefab` 已挂：

- `Rigidbody`
- `BossController`
- `BossAnimationDriver`
