# 喷泉结局 Timeline

资产：`Assets/_Project/Content/GameFlow/fountain_ending.playable`

该 Timeline 只包含一个 Playable 轨道，运行 `Project.Ending.FountainEndingTimelinePlayable`。请把 Director 和 `ContactTimelineTrigger` 放在喷泉旁边，并使用一次性/进入播放模式。脚本以触发器的局部坐标作为表演坐标系，运行时查找同一 Scene 中距离触发器最近的 `FountainArtwork`，不写死地图世界坐标。

## 演出流程

1. 锁住玩家全部控制并接管 Rigidbody 表现运动。
2. 玩家以 Run 动画缓慢走向喷泉，在距离喷泉中心 `2` 格处停止。
3. 相机全程跟随玩家，不做双锚点构图、不横移到喷泉，也不扩大视野。
4. 四个调色盘从玩家身体中出现，依次升到头顶排列，再逐个飞到喷泉顶部并进入喷泉内部；每份演出副本都通过 `MechanismArtworkPresentation.PrepareCinematicClone` 使用当前最新物体美术，隐藏预制体遗留占位 Renderer。
5. 等待 `1.5` 秒，以打字机形式显示 `真的结束了?`，到时直接消失。
6. 以故障风格显示 `是的，结束了，`，到时直接消失。
7. 瞬间全屏黑屏；结束分支先停止镜头平滑回程，再调用 `RunSessionCoordinator.CompleteCurrentRunAndReturnToMenu()` 清档并回开始界面。
8. 开始界面状态完成后继续保持 `1` 秒全黑，避免露出从喷泉返回出生点的镜头。
9. 黑屏解除后立即播放 RGB 故障特效；故障短暂停留后淡出，Timeline 结束。

## 成就边界

Timeline 初始化成功、确认进入喷泉真结局分支时立即发出 `TrueEndingReached`，让 `1007` 成就提示在演出开始时就弹出；黑屏节点只负责清档和返回主菜单，不再等待黑幕阶段结算成就。

## 可调参数

- 行走速度、停止距离、相机释放时长。
- 调色盘预制体、数量、头顶间距/弧高、出场/投掷/进入时长、喷泉内缩放。
- 字体、字号、每段字幕时长、段间间隔、打字机速度、故障参数。
- 黑屏颜色/层级、主菜单等待、故障停留和故障消退时长。
