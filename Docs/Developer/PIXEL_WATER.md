# 像素横截面水

正式关卡现已接入自动分区面片与流向气泡，见 [关卡水体](GAMEPLAY_PIXEL_WATER.md)。下文为TA独立预制体和沙盒的原始配置；正式玩法由统一配置生成，不需要逐格拖放这些预制体。

此功能提供三个可拖入场景的像素水预制体。每个预制体恰有两个视觉面片：后方的像素水图，和前方透明的屏幕折射板。中间保留 0.3 世界单位的 Z 深度间隔，角色按排序位于两者之间。

## 资源

| 预制体 | 后片图片 | 用途 |
|---|---|---|
| `PixelWater_Surface.prefab` | `Water_Surface.png` | 像素水面切片 |
| `PixelWater_Body.prefab` | `Water_Body.png` | 水体主体切片 |
| `PixelWater_Interior.prefab` | `Water_Interior.png` | 水体内部切片 |

资源目录为 `Assets/ta_source/Newwater`。切片来自原始 `0-5 (3).png`，纹理外框为 16×17 像素、Point 过滤和 16 PPU（Sprite 外框为 1×1.0625 世界单位）；三张水图最底下一行均为全透明像素。控制器的 `filterBoundsInsetsPixels=(0,1,0,0)` 会从后片可见边界裁去这行留白，将前片收紧到实际水图的 16×16 像素（1×1 世界单位），并向上居中半像素。每个预制体的子物体为 `PixelWaterBack` 与 `CrossSectionDistortion`；后者使用 `Shaders/PixelWaterDistortion.shader`。三种预制体分别使用独立材质 `Materials/PixelWater_Surface.mat`、`PixelWater_Body.mat` 和 `PixelWater_Interior.mat`，可以独立调折射和水色参数。根节点的 `PixelWaterFilterController` 会按后片 Sprite 的世界中心、旋转和尺寸同步前片，同时保留前片单独的 Z 间距；运行时更换 Sprite 或调整子片后，LateUpdate 会重新对齐。扰动板从透明面片后方的相机画面取样，并用随时间变化的程序噪波只偏移片元的屏幕 UV；网格和顶点保持固定，面片本身不绘制噪波图案。预制体不带水面顶板、交互触发器、碰撞波纹或反射相机。

## 渲染顺序

- 后片：Default 排序层，Order `-100`，局部 Z `+0.15`。
- 角色：建议放在 Default 排序层，Order `0` 或更高，Z 位于两片之间（默认根节点平面 Z `0`）。
- 扰动板：Water 层，WaterFrontMesh 排序层，Order `0`，局部 Z `-0.15`。

示例相机位于水片的 `-Z` 一侧并朝 `+Z` 看。正交相机下主要依靠排序层；透视相机下也保留明确的深度间隔。使用其它相机朝向时，需沿相机深度方向旋转/摆放整个水体，让角色处于后片与扰动板之间。

扰动板由 `WaterUnderlayRendererFeature` 在透明物体渲染完成后复制当前相机画面，再仅绘制 `PixelWaterRefraction` 通道。这样采样画面包含水后片和位于两片之间的角色；滤镜以 0.9 的默认覆盖率混合扭曲画面与原画面，并叠加很轻的水色。预制体根节点的 `WaterUnderlayCaptureRequester` 请求此通道。主相机需要有 `MainCamera` 标签，URP Renderer 需要配置既有的 `WaterUnderlayRendererFeature`。旧交互水使用的 `_WaterUnderlayTexture` 捕获和像素水的后透明 `_PixelWaterSceneColor` 捕获相互独立。前片按水图的非透明像素边界对齐，因此不会多露出图片底部的透明行。默认屏幕偏移强度为 `0.004`、流动速度为 `0.9`、强度时间变化幅度为 `0.75`、变化速度为 `0.9`、色调混合为 `0.1`；强度单位为归一化屏幕 UV。Inspector 可以编辑控制器字段，程序也可随时设置 `WaterTint`、`TintStrength`、`DistortionStrength`、`DistortionTimeVariation`、`DistortionChangeSpeed`、`NoiseScale`、`NoiseFlowSpeed`、`Opacity` 属性，或用 `SetFilter(...)` 一次更新全部参数。

## 沙盒

`Assets/_Project/Development/PixelWater/PixelWaterTest.unity` 展示三种切片及一张玩家精灵。选择一个预制体可以查看双子物体与 Z 位置；运行场景时玩家自动往返穿过水片，可观察折射采样；按方向键或 A/D 可以手动移动。沙盒不在正式场景流程中。具体关卡相机朝向、目标图形后端及不同角色排序层还需要接入时核对。
