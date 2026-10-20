# story_start 开场 Timeline

资产：`Assets/_Project/Content/GameFlow/Resources/story_start.playable`

资产位于 `Resources` 下，运行时通过 `Resources.Load<PlayableAsset>("story_start")` 读取。没有有效运行存档时，`RunSessionCoordinator` 会先重置本轮运行时状态并完整播放该开场，结束后才提交初始运行存档并交还玩家。已有存档直接走 RGB 故障读档效果，不播放该开场。

新档入口直接挂在“开始游戏”点击链路：`StartMenuFlowBridge.BeforeStartTransition` 先让 `RunSessionCoordinator` 在菜单上播放 `GameplayEntryCircleCover`，完全遮黑后才开始 MainMenu → Gaming 运镜。从完全遮黑起等待 `2` 秒，再通过 `StoryStartEntryHandoff` 让本 Timeline 以“已遮黑”模式接手，因此不会等玩家出现在画面里才蒙屏，也不会重复播放一次闭圈。

Timeline 不绑定场景物体，使用一条 Playable 轨道运行 `Project.StoryStart.StoryStartTimelinePlayable`。脚本位于 `Assets/_Project/Code/StoryStart`，属于 `Assembly-CSharp`，可直接引用 Player、WordNote 与 Timeline。挂入任意接触 Timeline 触发器后会先按 `Player` Tag 检查玩家。

## 演出流程

1. 获取玩家控制锁并冻结玩家演出运动。
2. 新档入口已在点击开始时完成圆形闭合并完全遮黑；普通 Timeline 触发时则在本步自行闭全屏圆形黑幕。
3. 从完全遮黑起累计等待 `2` 秒；等待期间镜头倍率平滑变为 `1.4×`，随后文字通过常驻 `WordNote_UI` 居中打字机显示。
4. 每段文字显示 `2` 秒，上一段结束后间隔 `0.5` 秒；文字到时直接消失，不播放退场动画。
5. 全黑期间把玩家放到相对当前位置向左 `16` 格；玩家面向右侧并切换为 Run 精灵动画。
6. 圆形黑幕从中心向外消散，同时玩家走回画面中心；默认返程速度为 `2.6666667` 格/秒，黑幕散开与返程共用同一段时间。
7. 播放结尾分段文字，文字直接消失后交还相机和玩家操作。

## 可编辑参数

- 字体、字号、打字速度、每段持续时间、段间间隔。
- 圆形黑幕 Shader、颜色、中心点、层级、覆盖/揭开速度与边缘过扫。
- 开场/结尾文字数组。
- 镜头倍率、扩大时长、玩家左移格数、返程速度与镜头交还时长。

玩家位置始终以播放时当前位置为基准，没有写死世界坐标。若场景缺少玩家、`CameraControlManager`、`TimelineCamRig`、`WordNote_UI` 或字体，演出会报错并停止。
