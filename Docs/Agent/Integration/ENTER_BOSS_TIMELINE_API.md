# enter_boss 低收集门槛 Timeline

## 资源

- Timeline：`Assets/_Project/Content/Boss/Timelines/enter_boss.playable`
- Playable：`Assets/Boss_Logic/EnterBossTimelinePlayable.cs`
- 进度来源：`CollectibleProgressService.GetWorldProgress()`
- 返回目标：同场景 `TeleportAnchorFeature`，默认 ID `Start`

该 Timeline 只负责低收集进度的提示和送回起点，不负责 Boss 出场、Boss 战开始或清档。

## 分支行为

1. 读取本轮世界普通收集品进度，默认门槛为 `30%`。
2. 达到门槛时立即结束 Timeline，不锁玩家、不显示字幕、不传送、不修改进度。
3. 未达到门槛时锁住玩家全部操作。
4. 以 Cubic `72` 号白字黑描边、居中下方打字机字幕显示：

```text
我身上的调色板好像还不足以激活喷泉....
```

5. 第一句完整显示并停留 `1` 秒后，直接消失并显示：

```text
还是回去再收集一些吧
```

6. 第二句完整显示并停留 `1` 秒后，从屏幕中心收黑。
7. 全黑期间调用 `PlayerController.TryTeleportToStoryAnchor` 前往同场景 `Start`。
8. 黑幕从玩家位置倒放揭开，释放玩家控制并停止 Timeline。

低进度返回不会清档；收集品、颜色钥匙、能力、Boss 和重生进度全部保留。

## 触发配置

- 在 Timeline 触发器上直接选择 `enter_boss.playable`。
- 推荐 `ContactTimelineTrigger` 使用“只触发一次”和只匹配玩家。
- 关闭 `requestPlayerControlLockDuringTimeline`；Timeline 只在低进度分支自行申请控制锁。
- 不要在同一个触发器上同时挂启用的 `BossCollectibleEntryGate`，否则它会先拒绝播放，`enter_boss` 无法执行自己的字幕与返回演出。
- `Start` 必须与玩家处于同一场景。

## 可调参数

- 最低收集比例。
- Cubic 字体引用、字号、打字机速度、每句完整显示后的停留时间。
- 两段字幕文本。
- `Start` 传送 ID。
- 黑幕中心、收放速度、边缘柔度和全黑停留时间。
