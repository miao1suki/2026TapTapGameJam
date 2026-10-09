# 像素化接入

```csharp
using Project.Pixelization;

PixelizationControl.SetOutlineEnabled(true);  // 开描边
PixelizationControl.SetOutlineEnabled(false); // 关描边；像素化不关闭
bool enabled = PixelizationControl.OutlineEnabled;
```

默认 false，SubsystemRegistration 重置。当前全 Game 摄像机统一开关，不是单物体开关，不改变颜色解锁状态或 HSV 参数。需要像素化的 Renderer 使用 `renderingLayerMask |= 128u`，不要为了水/藤蔓改掉普通颜色 Layer。

SelectiveHsvRendererFeature 提交独立 LearningPixelPass（事件 BeforeRenderingPostProcessing）再提交 HSV Pass（下一事件值）。中间纹理按源尺寸生成，三阶段 source / original / mask 依赖显式声明；相机颜色先替换为像素化结果，HSV 再读取新的 activeColorTexture。描边 mask 与对象选择编码在浮点块网格 Alpha，源相机 Alpha 在最终合成保留。

出处为用户的 URP学习 ScreenPixelOutline Shader/HLSL；不是拷贝整个工程，也不修改源项目。新 Shader 名称 Hidden/2026TapTap/LearningPixelOutline，PC/Mobile Feature 均显式引用以保留构建资源。保留源许可。

完整深度/法线 Sobel 针对实体几何；透明粒子/水可能缺乏可靠深度/法线，不应承诺其描边与不透明模型一致。可扩展亮度/RGB 权重或独立效果深度，需再做 GPU 验证。当前描边为遮罩内侧效果，不向背景扩张。
