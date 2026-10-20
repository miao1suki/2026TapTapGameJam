# 成就逐条接线

## 数据源

- 成就资产：`Assets/_Project/Content/Data/Global/Achievements/Achievement_1001.asset` ～ `Achievement_1014.asset`
- 运行目录：`Assets/_Project/Code/Systems/Achievements/Runtime/Resources/AchievementCatalog.asset`
- 当前共 `14` 条，目录引用数量与资产数量均为 `14`。
- 成就属于全局本机数据，不随单轮存档清档删除。
- `AchievementManager.TriggerById` 对已解锁成就返回 `false`，不会重复显示或重复保存解锁结果。

## 逐条映射

| ID | 成就 | 条件 | 实际接线 |
|---|---|---|---|
| `1001` | 一失足成千古恨 | 摔落死亡 1 次 | `PlayerDeathAchievementRouter` ← `PlayerFallDeath` |
| `1002` | 这里怎么有块尖刺!? | 尖刺死亡 1 次 | `PlayerDeathAchievementRouter` ← `PlayerSpikeDeath` |
| `1003` | 一失足成万古恨 | 摔落死亡 10 次 | `PlayerDeathAchievementRouter` ← `PlayerFallDeath` |
| `1004` | 这里怎么还有尖刺!? | 尖刺死亡 10 次 | `PlayerDeathAchievementRouter` ← `PlayerSpikeDeath` |
| `1005` | 常回家看看 | 因收集不足回到第一关 | `LowCollectibleReturnHome`：低收集 `enter_boss`、旧 Boss 入场门槛或 Player Thinking 不足分支成功传回 `Start` 后发出 |
| `1006` | 结束了......吗？ | 达成假结局 | Player Thinking 判定进入不足分支时立即发出 `FakeEndingReached` |
| `1007` | 结束了 | 达成真结局 | 喷泉真结局 Timeline 初始化成功时立即发出 `TrueEndingReached` |
| `1008` | Power! | 使用颜色能力 1 次 | `PlayerColorWheel` 成功应用颜色，或绿色钩子成功发射后发出 `ColorAbilityUsed` |
| `1009` | 后备能源I | 单轮收集进度达到 5% | `CollectibleProgressService.RecordCollected` 按 `MeetsRatio(.05f)` 发出 `CollectibleProgress5` |
| `1010` | 后备能源II | 单轮收集进度达到 10% | `CollectibleProgressService.RecordCollected` 按 `MeetsRatio(.1f)` 发出 `CollectibleProgress10` |
| `1011` | 炙热的我 | 岩浆死亡 1 次 | `PlayerDeathAchievementRouter` ← `PlayerLavaDeath` |
| `1012` | 额...又是魂游 | 总死亡 50 次 | `PlayerDeathAchievementRouter` ← `PlayerDied` |
| `1013` | Oh please dont let me die | 总死亡 5 次 | `PlayerDeathAchievementRouter` ← `PlayerDied` |
| `1014` | 七分熟 | 岩浆死亡 10 次 | `PlayerDeathAchievementRouter` ← `PlayerLavaDeath` |

## 路由入口

```text
玩法入口
  -> GameplaySignalHub.Emit(AchievementSignalIds...)
  -> PlayerDeathAchievementRouter / GameplayAchievementRouter
  -> AchievementManager.TriggerById
  -> 全局 achievements.json
```

- 死亡路由：`Project/Achievements/Runtime/PlayerDeathAchievementRouter.cs`
- 非死亡玩法路由：`Project/Achievements/Runtime/GameplayAchievementRouter.cs`
- 信号 ID：`Project/InputAbstraction/Runtime/AchievementSignalIds.cs`

## 边界

- `1009`、`1010` 使用整数数量向上取整规则，不用浮点 `Ratio` 做边界判断。
- 临时结局收集品标记为不计进度，不会误触发 `1009`、`1010`。
- `1005` 只在玩家确实成功返回 `Start` 后触发，缺少锚点或传送失败时不记。
- 真假结局成就在分支确定时立即结算，不等待黑屏、传送或菜单切换；成就存档独立于单轮清档保留。
- 成就 Toast 使用最高 Sorting Layer、`sortingOrder=32767` 的独立 ScreenSpaceOverlay Canvas，显示在加载、结局和暂停黑幕之上；Toast 本体仍固定为屏幕左下角。
