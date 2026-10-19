# story_start 开场 Timeline

资产：`Assets/_Project/Content/GameFlow/story_start.playable`

Timeline 不绑定场景物体，使用一条 Playable 轨道运行 `Project.StoryStart.StoryStartTimelinePlayable`。脚本位于 `Assets/_Project/Code/StoryStart`，属于 `Assembly-CSharp`，可直接引用 Player、WordNote 与 Timeline。挂入任意接触 Timeline 触发器后会先按 `Player` Tag 检查玩家。

## 演出流程

1. 获取玩家控制锁并冻结玩家演出运动。
2. 四块黑幕从屏幕边缘向中心扩张并完全覆盖。
3. 镜头倍率平滑变为 `1.4×`，文字通过常驻 `WordNote_UI` 居中打字机显示。
4. 每段文字显示 `2` 秒，上一段结束后间隔 `0.5` 秒。
5. 玩家相对当前位置向左移动 `16` 格，镜头保持原机位。
6. 黑幕反向缩回四边；玩家以 `8` 格/秒走回原位，镜头倍率同步回到 `1×`。
7. 播放结尾分段文字，随后交还相机和玩家操作。

## 可编辑参数

- 字体、字号、打字速度、每段持续时间、段间间隔。
- 开场/结尾文字数组。
- 黑幕颜色、层级、覆盖/揭开速度与边缘过扫。
- 镜头倍率、扩大时长、玩家左移格数、返程速度与镜头交还时长。

玩家位置始终以播放时当前位置为基准，没有写死世界坐标。若场景缺少玩家、`CameraControlManager`、`TimelineCamRig`、`WordNote_UI` 或字体，演出会报错并停止。
