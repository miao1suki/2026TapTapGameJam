# 星空全景天空盒

`StarPanoramaSkybox.shader` 是用户提供的 Godot `shader_type sky` 星空 shader 的 Unity URP 版本。材质 `StarPanorama.mat` 同时用于独立预览和正式 Gaming 场景。

## 预览与调节

打开 `StarPanoramaPreview.unity` 并进入 Play Mode，即可查看动态天空。场景相机上的 `StarPanoramaTimeDriver` 使用 Unity `Time.time` 推送全局 shader 时间；Inspector 可启用 `Use Unscaled Time`，让暂停期间也继续流转。星点会缓慢旋转并闪烁，星云噪声会平滑漂移，星云旋转与流动速度可分别调节。选择 `StarPanorama.mat`，Inspector 中中文属性名前缀标明所属类别，可直接调节；保存材质后，预览场景和 Gaming 场景会使用同一组参数。默认时间倍率为 1，流动效果开箱可见。

相机只有背景天空，没有关卡物体。旋转预览相机的 Transform 可以查看全景不同方向；正式 Gaming 的相机清除模式保持 Skybox。

## 参数分组

- **时间**：时间总倍率。
- **星点**：密度、亮度、旋转速度、尺寸与随机尺寸、距离感及四种颜色。
- **星点光晕**：柔光的强度和范围。
- **十字光芒**：强度、长度、粗细和出现比例。
- **闪烁**：闪烁频率和随机显隐比例。
- **星云**：强度、全景旋转速度与噪声流动速度。
- **银河形态**：叶片状银河的宽度、倾斜、噪声不规则度、断裂程度、长度、边缘柔化、中心位置和弯曲。
- **空间与星云颜色**：空间上下渐变及三种星云色。

属性默认值沿用 Godot 源码。星云通过三层 3D 噪声形成；星体、十字光芒、星尘和星云颜色混合逻辑对应原 shader。

## 使用位置

- 测试：`Assets/ta_source/star/StarPanoramaPreview.unity`
- 正式场景：`Assets/_Project/Scenes/Playing/Gaming.unity`
- 共用材质：`Assets/ta_source/star/StarPanorama.mat`
