# 像素水预制体接入

## 可用资源

- `Assets/ta_source/Newwater/Prefabs/PixelWater_Surface.prefab`
- `Assets/ta_source/Newwater/Prefabs/PixelWater_Body.prefab`
- `Assets/ta_source/Newwater/Prefabs/PixelWater_Interior.prefab`

每个根节点有 `WaterUnderlayCaptureRequester`，且恰有两个视觉子物体：`PixelWaterBack`（SpriteRenderer）与 `CrossSectionDistortion`（MeshRenderer）。不要给这个横截面方案加回 `InteractiveWater` 顶面、波纹模拟或反射相机。三个预制体都使用同一透明屏幕折射材质，图片切片不同。前片完整采样相机底图，仅通过片元屏幕 UV 偏移扭曲后方画面；默认折射覆盖率为 1，不混入水色面片；没有顶点位移，也不显示动画噪波纹理。

## 排序契约

默认相机在 `-Z` 一侧朝 `+Z` 观察。根节点 Z 为 `0` 时，后片局部 Z 为 `+0.15`，扰动板局部 Z 为 `-0.15`。角色放在两者之间；角色 SpriteRenderer 建议使用 Default 排序层、Order `0` 或更高。后片是 Default / `-100`；前片使用 `WaterFrontMesh` / `0` 并处于 Water Layer。保持两个子片同宽同高（默认 1×1.0625），根节点缩放可按关卡所需调整。

## 渲染依赖

主相机需带 `MainCamera` 标签，且其 URP Renderer 中已安装 `WaterUnderlayRendererFeature`。该 Feature 在场景中有完整交互水体或启用的 `WaterUnderlayCaptureRequester` 时，为主相机捕获水板之前的底图。底图捕获层排除 Water 与 UI；需要折射的角色和场景物体需处于捕获层内。折射强度用归一化屏幕 UV 表示；调节 `PixelWaterCrossSection.mat` 的 `_DistortionStrength` 可改变片元偏移幅度。

三种变体的沙盒示例在 `Assets/_Project/Development/PixelWater/PixelWaterTest.unity`，详细说明见 [PIXEL_WATER.md](../../Developer/PIXEL_WATER.md)。
