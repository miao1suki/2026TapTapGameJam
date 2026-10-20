# Player Thinking 演出

## 资源

- Timeline：`Assets/_Project/Content/Boss/Timelines/player_thinking.playable`
- 总控：`Assets/Boss_Logic/PlayerThinkingTimelinePlayable.cs`
- 播放链：`BossDefeatTimelinePresenter.playerThinkingTimeline`

`Boss_defeat.playable` 结束后由同一个 `PlayableDirector` 无缝切换播放 `player_thinking`。普通死亡与处决死亡共用。

## 收藏品进度 API

BossDefeatTimelinePresenter在进入演出前自动从普通成就收集品进度服务读取全会话世界的真实数量。最终Boss与迁移关卡统一合入Gaming，不新增跨场景加载架构。直接播放演出时也会读取真实进度；开发者仍可显式提供测试数据：

```csharp
PlayerThinkingTimelinePlayable.SetCollectibleProgress(
    currentCollectibleCount,
    maximumCollectibleCount);
```

规则：

- `current / maximum >= 0.6`：播放演出 2。
- `< 0.6`：播放演出 1。
- 判定进入 `< 0.6` 的演出1分支时立即发出 `FakeEndingReached`，成就提示先于后续字幕、传送和黑幕出现。
- 最大值小于等于0不再默认成功，播放演出1；`ClearCollectibleProgress()`清除测试覆盖，下次直接播放时读取场景进度。
- 静态值在子系统注册时重置。
- 临时生成的六份演出收集品在启用前显式标记`SetExcludedFromProgress(true)`，不增加全局/房间统计的分母或奖励。
- `HasSufficientCollectibles(current, maximum, threshold=.1f)`提供相同的纯判断；恰好10%进入成功分支。

## Boss入口与传送地图状态

见[收集进度与Boss流程](COLLECTIBLE_BOSS_PROGRESS_API.md)。Boss入口的5%门槛与结局的10%门槛是两个判断，不可混用；`BossCollectibleEntryGate`只挂在明确的Boss入场Timeline触发器，不影响其他触发器。

## 共同内容

- `PlayerTimelineTrack` 锁操作；Timeline 结束时停止导演并释放玩家锁和相机轨。
- `BossCameraPlayable` 使用 `PlayerFocus` 模式，聚焦玩家并保持正常视野大小；本地偏移为 `(0, 4, -10)`，镜头相对原版整体上移 4 格。
- 全部文字通过 `WordNoteUI.RequestTimeline` 进入同一消息队列。
- 字体为 `Cubic_11 SDF`，字号默认 `72`，白字黑描边，居中下方。
- 每句打字机完成后停留 `1.2` 秒，直接消失，间隔 `1` 秒后进入下一句。

## 演出 1

初始思考结束后追加：

```text
......
可恶......
还是达不到吗......
还是回去继续收集画板吧
```

随后使用 `GameTransitionOverlay` 收黑，完全黑屏保持 `1` 秒，期间通过：

```csharp
TeleportAnchorFeature.TryGetAnchor("Start", out var start);
controller.TryTeleportToStoryAnchor(start);
```

传送到 `Start` 目标，再倒放黑屏并结束。

## 演出 2

六份 `Collectible.prefab` 依次从玩家身体出现，排列到头顶并持续呼吸律动。每份演出副本在关闭玩法组件后调用 `MechanismArtworkPresentation.PrepareCinematicClone`，显示当前最新收集品 Sprite，不显示预制体遗留占位图。等待 `2` 秒后播放：

```text
终于
要结束了吗......
```

最后一句消失后等待 `2` 秒并收黑；画面完全变黑时立即销毁六份演出收集品，再保持黑屏 `1` 秒，通过：

```csharp
TeleportAnchorFeature.TryGetAnchor("End", out var end);
controller.TryTeleportToStoryAnchor(end);
```

传送到 `End` 目标，再倒放黑屏。画面完全恢复后停止 Timeline，释放操作与相机。

## 场景要求

- 至少放置一个 `Teleport_Block.prefab`，其 `teleportId` 填 `Start`。
- 至少放置一个 `Teleport_Block.prefab`，其 `teleportId` 填 `End`。
- 目标方块可以放在任意位置，默认无碰撞且不可见。
- Start/End必须与当前玩家处于同一场景；独立Boss开发测试缺少锚点时只警告并正常揭幕，不自动加载Gaming。
- 锚点返回通过玩家控制器处理运动、房间、相机及会话复活绑定；回Start后再次死亡不会返回旧Boss重生点。
