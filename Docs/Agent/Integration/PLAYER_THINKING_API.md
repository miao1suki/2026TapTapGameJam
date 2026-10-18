# Player Thinking 演出

## 资源

- Timeline：`Assets/_Project/Content/Boss/Timelines/player_thinking.playable`
- 总控：`Assets/Boss_Logic/PlayerThinkingTimelinePlayable.cs`
- 播放链：`BossDefeatTimelinePresenter.playerThinkingTimeline`

`Boss_defeat.playable` 结束后由同一个 `PlayableDirector` 无缝切换播放 `player_thinking`。普通死亡与处决死亡共用。

## 收藏品进度 API

外部在进入演出前设置：

```csharp
PlayerThinkingTimelinePlayable.SetCollectibleProgress(
    currentCollectibleCount,
    maximumCollectibleCount);
```

规则：

- `current / maximum >= 0.6`：播放演出 2。
- `< 0.6`：播放演出 1。
- 尚未调用 API、最大值小于等于 0 或调用 `ClearCollectibleProgress()`：默认播放演出 2。
- 静态值在子系统注册时重置。

## 共同内容

- `PlayerTimelineTrack` 锁操作；Timeline 结束时停止导演并释放玩家锁和相机轨。
- `BossCameraPlayable` 使用 `PlayerFocus` 模式，聚焦玩家并保持正常视野大小。
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
TeleportAnchorFeature.TeleportObject(player, "Start");
```

传送到 `Start` 目标，再倒放黑屏并结束。

## 演出 2

六份 `Collectible.prefab` 依次从玩家身体出现，排列到头顶并持续呼吸律动。等待 `2` 秒后播放：

```text
终于
要结束了吗......
```

最后一句消失后等待 `2` 秒并收黑；画面完全变黑时立即销毁六份演出收集品，再保持黑屏 `1` 秒，通过：

```csharp
TeleportAnchorFeature.TeleportObject(player, "End");
```

传送到 `End` 目标，再倒放黑屏。画面完全恢复后停止 Timeline，释放操作与相机。

## 场景要求

- 至少放置一个 `Teleport_Block.prefab`，其 `teleportId` 填 `Start`。
- 至少放置一个 `Teleport_Block.prefab`，其 `teleportId` 填 `End`。
- 目标方块可以放在任意位置，默认无碰撞且不可见。
