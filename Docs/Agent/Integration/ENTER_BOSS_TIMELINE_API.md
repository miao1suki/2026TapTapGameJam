# enter_boss 收集品与六钥匙门槛 Timeline

## 资源

- Timeline：`Assets/_Project/Content/Boss/Timelines/enter_boss.playable`
- Playable：`Assets/Boss_Logic/EnterBossTimelinePlayable.cs`
- 进度来源：`CollectibleProgressService.GetWorldProgress()`
- 返回目标：同场景 `TeleportAnchorFeature`，默认 ID `Start`

该 Timeline 只负责入场条件不足时的提示和送回起点，不负责 Boss 出场、Boss 战开始或清档。条件统一由 `BossEntranceRequirements` 查询当前会话/读档恢复的进度，不新增存档字段。

## 分支行为

1. 读取本轮世界普通收集品进度，默认门槛为 `30%`，并检查红绿蓝每色两把、共 `6` 把颜色钥匙。
2. 两项均达到门槛时立即结束 Timeline，不锁玩家、不显示字幕、不传送、不修改进度。三种场景颜色恢复只代表三把；必须同时有三种玩家能力才是六把。
3. 任一条件未达到门槛时锁住玩家全部操作。
4. 以 Cubic `72` 号白字黑描边、居中下方打字机字幕显示：

```text
进入需收集至少30%的调色板，并集齐6把颜色钥匙（当前调色板11/35，钥匙5/6）
```

5. 第一句完整显示并停留 `1` 秒后，直接消失并显示：

```text
还是回去再收集一些吧
```

6. 第二句完整显示并停留 `1` 秒后，从屏幕中心收黑。
7. 全黑期间调用 `PlayerController.TryTeleportToStoryAnchor` 前往同场景 `Start`。
8. 黑幕从玩家位置倒放揭开，释放玩家控制并停止 Timeline。

提示中的门槛与进度动态生成，上述数量只是示例。失败返回不消费入口的 Once 机会：本次演出完成后重置所属 `ContactTimelineTrigger`，下次经过仍重新核对；不会因为第一次不足就绕过后续门槛。仅缺钥匙的返回不触发“低收集回家”成就。

未达标返回不会清档；收集品、颜色钥匙、能力、Boss 和重生进度全部保留。

## 触发配置

- 正式 Gaming 已接入：`Boss_box_aa7a24dc` 的 `ContactTimelineTrigger.timeline` 指向本资产，`BossCollectibleEntryGate.gateEnabled=0`，BoxCollider 为 `3×6×3`。
- 在 Timeline 触发器上直接选择 `enter_boss.playable`。
- 推荐 `ContactTimelineTrigger` 使用“只触发一次”和只匹配玩家。
- 关闭 `requestPlayerControlLockDuringTimeline`；Timeline 只在低进度分支自行申请控制锁。
- 不要在同一个触发器上同时挂启用的 `BossCollectibleEntryGate`，否则它会先拒绝播放，`enter_boss` 无法执行自己的字幕与返回演出。
- `Start` 必须与玩家处于同一场景。

## 可调参数

- 最低收集比例。
- Cubic 字体引用、字号、打字机速度、每句完整显示后的停留时间。
- 返回提示文本；首段条件提示由统一进度源动态生成，旧 `insufficientLine` 字段仅保留资产兼容、已隐藏。
- `Start` 传送 ID。
- 黑幕中心、收放速度、边缘柔度和全黑停留时间。
