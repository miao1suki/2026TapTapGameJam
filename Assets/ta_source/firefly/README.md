# 萤火虫范围预设

通过 `Tools/2026TapTap/关卡美术/创建萤火虫区域` 创建，或将 `FireflyField.prefab` 拖入场景。选中物体显示活动矩形，中文 UI Toolkit Inspector 可调整范围、数量（最多 128）、游动、驱散、呼吸及补光。

这是中性暖色美术，不声明红、绿、蓝解锁归属，默认使用 Default Layer，不注册颜色物体或接入 HSV/交互连线。默认不生成实时点光；每只萤火虫由一个硬方像素与柔和光晕组成。硬点统一为单格世界尺寸的 1/16，中心按世界像素网格对齐，父级缩放和旋转不会改变其大小或方形方向。光晕是柔和照明，不按单格贴图拉伸。

区域、速度和光晕半径以格为单位；默认一格为 1 世界单位，父级的 `IGridCellSizeProvider` 可提供其他格尺寸。三个固定种子不依赖临时 instanceID，也不改变 Unity 全局随机状态。编辑态静止缓存，只在参数或变换改变时更新；移动不重抽布局。

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
| `Realtime Light Count`、`Light Intensity`、`Light Range`、`Illuminated Layers` | 最多 4 盏小范围无阴影点光，仅对受 URP 实时灯影响的材质补光；默认关闭（0），需要时手动开启。点光复用，关闭后清理；移动端建议保持关闭。 |

萤火虫共用一个动态网格和两份材质，不为每个光点创建对象。点光只在 Play 模式生成，编辑时由叠加光晕展示静态预览。它是视觉效果，不添加碰撞或玩法交互。

## 场景与协作

创建入口支持 Undo/Redo、Prefab 覆盖记录和场景标脏；运行状态、Prefab 编辑模式和预览场景不能创建。对象位于 Scene 独立根，不在关卡生成根下，“应用到场景”不会重建它。它不写入 planning；关卡交付仍必须同时提交 Scene、对应 planning 文件及新增依赖。生成网格和补光对象均为临时资源，不写入 Scene。不引入 TA 示例场景或测试对象，不自动放入正式关卡。

## 程序接口与验证

`FireflyField.Configure(areaCells, count, layoutSeed, motionSeed, pulseSeed)` 配置区域；`SetThreat(Transform)` 指定靠近者，传 null 后按设置恢复玩家 Tag 查找。它只改变视觉游动，不操控玩家、机关、碰撞、解锁或重置状态。

`FireflyRegionWindow.CreateRegion` 为编辑器创建入口。`FireflyIntegrationAudit.Run` 仅允许在静音 `Library/FlowRetirementProbe` 执行（真实 GPU，不使用 `-nographics`）；检查原生硬像素、静态缓存、固定种子、128/4预算、补光复用/清理、UI Toolkit、Undo/Redo、Scene 保存重载。报告为 `Library/FireflyIntegrationAudit.txt`，GPU截图为 `Library/Firefly_HardPixel_Cell1.png` 与 `Cell2.png`。
