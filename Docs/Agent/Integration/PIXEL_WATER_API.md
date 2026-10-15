# 像素水预制体接入

关卡玩法自动接线见 [GAMEPLAY_PIXEL_WATER_API.md](GAMEPLAY_PIXEL_WATER_API.md)。下文保留独立TA预制体用法；正式水族使用blue Layer，专属折射LightMode现已不再限定Water Layer。

## 可用资源

- `Assets/ta_source/Newwater/Prefabs/PixelWater_Surface.prefab`
- `Assets/ta_source/Newwater/Prefabs/PixelWater_Body.prefab`
- `Assets/ta_source/Newwater/Prefabs/PixelWater_Interior.prefab`

每个根节点有 `WaterUnderlayCaptureRequester` 和 `PixelWaterFilterController`，且恰有两个视觉子物体：`PixelWaterBack`（SpriteRenderer）与 `CrossSectionDistortion`（MeshRenderer）。不要给这个横截面方案加回 `InteractiveWater` 顶面、波纹模拟或反射相机。三个预制体各自引用独立材质（`PixelWater_Surface.mat`、`PixelWater_Body.mat`、`PixelWater_Interior.mat`），实例之间仍可单独移动；控制器按后片 Sprite 的世界中心、旋转和宽高同步前片，并保留独立的 Z 间隔。更换 Sprite 或运行时调整后片子物体时会在 LateUpdate 自动重新对齐；前片网格保持固定。折射通道在透明物体绘制完成后复制相机颜色为 `_PixelWaterSceneColor`，只通过片元屏幕 UV 偏移采样并扰动后方画面，再混入少量水色；当前默认覆盖率为 0.9。没有顶点位移，也不显示动画噪波纹理。

## 排序契约

默认相机在 `-Z` 一侧朝 `+Z` 观察。根节点 Z 为 `0` 时，后片局部 Z 为 `+0.15`，扰动板局部 Z 为 `-0.15`。角色放在两者之间；角色 SpriteRenderer 建议使用 Default 排序层、Order `0` 或更高。后片是 Default / `-100`；前片使用 `WaterFrontMesh` / `0` 并处于 Water Layer。水图 Sprite 的纹理外框为 1×1.0625 世界单位，但底部一行全透明；三个预制体的 `filterBoundsInsetsPixels` 均去掉这 1 像素透明边，故前片实际为 1×1 并上移半像素，严格贴合水图可见像素范围。根节点缩放可按关卡所需调整，控制器会同步前片。

## 渲染依赖

主相机需带 `MainCamera` 标签，且其 URP Renderer 中已安装 `WaterUnderlayRendererFeature`。该 Feature 保留旧交互水在透明物体前的捕获，并另在透明物体后运行像素水折射通道。后者采样捕获时已经画出的水后片、角色和场景；滤镜面片属于 Water 层，只由专用 LightMode 绘制，避免被普通透明通道重复画出。折射强度用归一化屏幕 UV 表示；可在控制器 Inspector 中编辑颜色、色调比例、基础折射强度、噪波尺度、流动速度、强度变化幅度/速度与覆盖率；运行时通过 `WaterTint`、`TintStrength`、`DistortionStrength`、`DistortionTimeVariation`、`DistortionChangeSpeed`、`NoiseScale`、`NoiseFlowSpeed`、`Opacity` 属性即时调整，或调用 `SetFilter(...)` 一次设置全组。Shader 用独立时间噪波改变折射强度，不是静态幅度；默认变化幅度为 0.75，变化速度为 0.9。

三种变体的沙盒示例在 `Assets/_Project/Development/PixelWater/PixelWaterTest.unity`，详细说明见 [PIXEL_WATER.md](../../Developer/PIXEL_WATER.md)。
