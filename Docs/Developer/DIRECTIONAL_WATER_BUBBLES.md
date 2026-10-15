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
- **泡泡密度（排/格）**：0.2–3 的线性滑杆；运行时代码可读写 `DensityPerCell`，控制沿流向每格的泡泡排数。
- **每个流向位置泡泡数**：1–6 的整数滑杆；运行时代码可读写 `BubblesPerFlowSlice`。上流时，同一高度可放多颗泡泡；左、右流时对应同一横向位置。
- **交错程度**：0–1 的线性滑杆；运行时代码可读写 `InterweaveAmount`，控制同一排泡泡的前后错位和横向扰动。
- **泡泡流宽度（格）**：0–4 的线性滑杆；运行时代码可读写 `StreamWidthInCells`。泡泡在主流线两侧随机分布，该数值表示总宽度，0 会收拢成一条线。
- **流动速度（格/秒）**：0.1–5 的线性滑杆；运行时代码可读写 `SpeedInCellsPerSecond`。各泡泡仍保留轻微的独立速度随机差异。
- **尺寸倍率和排序顺序**：用于微调画面与现有水面/物体的绘制顺序。

泡泡按“流向排数 × 每个流向位置泡泡数”成组排列。同一排的泡泡横向分开并按交错程度轻微错位，让同一高度不再只有一颗；小尺寸仍集中在源头，随流向逐步变大。泡泡的位置、速度、透明度和尺寸带可复现的随机差异，随机种子相同会得到相同的初始分布。

预制体复用已有 SpriteRenderer；运行时需要超过预制体池容量时，只在初始化或调参时扩展一次，不会每帧创建对象。支持的最大池为 31 排 × 6 颗，共 186 个泡泡。

将预制体根节点放在水流的源头，泡泡会沿其配置方向排布并持续流动。`sortingOrder` 可用于调整泡泡与水体、玩家的显示关系。组件在选中时会在 Scene 视图绘制长度与方向线。

## 独立测试场景

`Assets/ta_source/Paopao/TestScene/WaterBubblesTest.unity` 并列展示上、左、右三种预制体，各自设置为 6 格；点击 Play 可观察泡泡从小到大沿流向循环的效果。此场景不属于正式场景流程，也没有加入 Build Settings。
