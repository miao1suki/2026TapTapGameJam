# 像素横截面水

此功能提供三个可拖入场景的像素水预制体。每个预制体恰有两个视觉面片：后方的像素水图，和前方透明的屏幕折射板。中间保留 0.3 世界单位的 Z 深度间隔，角色按排序位于两者之间。

## 资源

| 预制体 | 后片图片 | 用途 |
|---|---|---|
| `PixelWater_Surface.prefab` | `Water_Surface.png` | 像素水面切片 |
| `PixelWater_Body.prefab` | `Water_Body.png` | 水体主体切片 |
| `PixelWater_Interior.prefab` | `Water_Interior.png` | 水体内部切片 |

资源目录为 `Assets/ta_source/Newwater`。切片来自原始 `0-5 (3).png`，保留 16×17 像素、Point 过滤和 16 PPU，因此默认尺寸为 1×1.0625 世界单位。每个预制体的子物体为 `PixelWaterBack` 与 `CrossSectionDistortion`；后者使用 `Shaders/PixelWaterDistortion.shader` 和 `Materials/PixelWaterCrossSection.mat`。它从相机捕获图中采样后方内容，并用随时间变化的程序噪波只偏移片元的屏幕 UV，轻微折射角色和场景；网格和顶点保持固定，面片本身不绘制水纹图案。预制体不带水面顶板、交互触发器、碰撞波纹或反射相机。

## 渲染顺序

- 后片：Default 排序层，Order `-100`，局部 Z `+0.15`。
- 角色：建议放在 Default 排序层，Order `0` 或更高，Z 位于两片之间（默认根节点平面 Z `0`）。
- 扰动板：Water 层，WaterFrontMesh 排序层，Order `0`，局部 Z `-0.15`。

示例相机位于水片的 `-Z` 一侧并朝 `+Z` 看。正交相机下主要依靠排序层；透视相机下也保留明确的深度间隔。使用其它相机朝向时，需沿相机深度方向旋转/摆放整个水体，让角色处于后片与扰动板之间。

扰动板使用 `WaterUnderlayRendererFeature` 捕获当前主相机底图，再由横截面材质进行透明屏幕折射。它完整采样并显示被扰动后的底图，透明度覆盖默认是 `1`，不会在水片上叠加实色；预制体根节点的 `WaterUnderlayCaptureRequester` 负责在没有完整 `InteractiveWater` 组件时请求这次捕获。主相机需要有 `MainCamera` 标签，URP Renderer 需要配置既有的 `WaterUnderlayRendererFeature`；底图排除 Water 与 UI 层，避免重复捕获水板。材质默认屏幕偏移强度为 `0.004`、噪波尺度为 `6`、动画速度为 `0.25`；强度单位为归一化屏幕 UV。

## 沙盒

`Assets/_Project/Development/PixelWater/PixelWaterTest.unity` 展示三种切片及一张玩家精灵。选择一个预制体可以查看双子物体与 Z 位置；运行场景时玩家自动往返穿过水片，可观察折射采样；按方向键或 A/D 可以手动移动。沙盒不在正式场景流程中。具体关卡相机朝向、目标图形后端及不同角色排序层还需要接入时核对。
