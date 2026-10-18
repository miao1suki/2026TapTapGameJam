# 萤火虫范围预设

将 `FireflyField.prefab` 拖到场景中，选中物体即可在 Scene 视图看到活动矩形。`Area Size` 控制范围，`Firefly Count` 控制数量（最多 128）。每只萤火虫由两个相邻的小方像素和一圈柔和光晕组成；默认像素边长为 0.018 单位。`Assets/Scenes/Test.unity` 的参照物常用 1 单位格子，默认光晕半径 0.22 单位，设计为小于格子的局部亮光。

## 调参

| Inspector 字段 | 作用 |
| --- | --- |
| `Flight Speed`、`Flow Speed` | 常态移速、轨迹噪声变化速度；路径不会走直线。 |
| `Flight Precision` | 转向跟随目标的快慢，越高越灵敏。 |
| `Wander Strength`、`Flock Strength` | 平滑游走与邻近个体的分离、对齐、聚拢强度。 |
| `Layout Seed`、`Motion Seed`、`Pulse Seed` | 分别固定初始分布、飞行差异与明暗节奏，方便复现同一组效果。 |
| `Threat`、`Find Player By Tag` | 可直接指定靠近者；未指定时运行中每秒查找一次 `Player` 标签。程序也可调用 `SetThreat(Transform)`。 |
| `Detection Radius`、`Flee Speed`、`Calm Down Seconds` | 近距离驱散的半径、加速移速与平复时间；受惊移速至少是常态的 1.1 倍。逃逸仍限制在活动框内。 |
| `Glow Radius`、`Glow Intensity` | 叠加光晕的范围和强度，可用于不受实时灯影响的画面素材。 |
| `Minimum/Maximum Pulse Size`、`Pulse Speed` | 光晕的呼吸范围与频率，各只萤火虫的相位、节奏不同。 |
| `Realtime Light Count`、`Light Intensity`、`Light Range`、`Illuminated Layers` | 最多 4 盏小范围无阴影点光，仅对受 URP 实时灯影响的材质补光；默认只启用 2 盏。设灯数为 0 可关闭。 |

萤火虫共用一个动态网格和两份材质，不为每个光点创建对象。点光只在 Play 模式生成，编辑时由叠加光晕展示静态预览。它是视觉效果，不添加碰撞或玩法交互。

## 独立预览

打开 `FireflyPreview.unity`。三个方块各为 1 单位宽，红色测试物体会在 Play 模式穿过活动框；预设体的 `Threat` 已指向它。可在 Game 视图观察游动、驱散、呼吸和暗色方块上的局部补光。此 Scene 不属于正式构建，也不改动 `Test.unity` 或 `Gaming.unity`。
