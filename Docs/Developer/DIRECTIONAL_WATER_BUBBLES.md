# 定向水流泡泡表现

本功能提供三种独立泡泡流预制体，用于标记定向水流的可视方向。泡泡只负责表现，不施加力、不创建碰撞体，也不替代水流物体现有的玩法组件。

## 资源

| 方向 | 预制体 |
|---|---|
| 向上 | `Assets/ta_source/Paopao/Prefabs/DirectionalBubble_Up.prefab` |
| 向左 | `Assets/ta_source/Paopao/Prefabs/DirectionalBubble_Left.prefab` |
| 向右 | `Assets/ta_source/Paopao/Prefabs/DirectionalBubble_Right.prefab` |

运行脚本位于 `Assets/ta_source/Paopao/Scripts/Runtime/DirectionalWaterBubbleStream.cs`。六张小图来自 `Assets/ta_source/Paopao/0-5 (3).png` 里的空心泡泡序列，按原像素裁出并作为 Point 过滤、16 PPU、无压缩的 Sprite 导入。原图不修改；生成预设只依赖 `Assets/ta_source/Paopao/Sprites` 下的六张裁切图。

## 调整与运行

在预制体根节点的 `DirectionalWaterBubbleStream` 中调整：

- **特效长度（格）**：1–10 的线性 Inspector 滑杆；运行时代码可读写 `LengthInCells`。
- **流动方向**：Up、Left 或 Right；运行时代码可读写 `Direction`。
- **每格世界单位**：将网格长度换算到本关的世界单位，默认 1。
- **泡泡密度（个/格）**：0.2–1.5 的线性滑杆；运行时代码可读写 `DensityPerCell`。泡泡数量随长度和密度变化，受预制体的 16 个渲染器池限制。
- **泡泡流宽度（格）**：0–4 的线性滑杆；运行时代码可读写 `StreamWidthInCells`。泡泡在主流线两侧随机分布，该数值表示总宽度，0 会收拢成一条线。
- **流动速度（格/秒）**：0.1–5 的线性滑杆；运行时代码可读写 `SpeedInCellsPerSecond`。各泡泡仍保留轻微的独立速度随机差异。
- **尺寸倍率和排序顺序**：用于微调画面与现有水面/物体的绘制顺序。

粒子使用固定子物体池循环移动，不在运行时反复实例化。每个泡泡按它当前所在的流向进度选择六张由小到大的 Sprite 并逐步缩放；泡泡的初始位置、速度、横向分布、透明度和尺寸有可复现的随机差异。随机种子相同会得到相同的初始分布。长度为 1 格时至少使用 2 个泡泡；长度和密度提高时逐步启用更多泡泡，最多 16 个。

将预制体根节点放在水流的源头，泡泡会沿其配置方向排布并持续流动。`sortingOrder` 可用于调整泡泡与水体、玩家的显示关系。组件在选中时会在 Scene 视图绘制长度与方向线。

## 独立测试场景

`Assets/ta_source/Paopao/TestScene/WaterBubblesTest.unity` 并列展示上、左、右三种预制体，各自设置为 6 格；点击 Play 可观察泡泡从小到大沿流向循环的效果。此场景不属于正式场景流程，也没有加入 Build Settings。
