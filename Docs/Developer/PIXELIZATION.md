# 选择性像素化

算法来自本机 `D:/.unity/URP_Learning_Git/URP学习/Assets/Rendering/ScreenPixel`，保留整数像素块、基于输入纹理尺寸的网格、fwidth 边界处理、块级归约及点采样放大；源 MIT 许可位于 Pixelization/ThirdPartyLicense.txt。

当前链路固定为：场景（包括透明效果）→ 学习项目像素化 / 可选描边 → 颜色层 HSV → URP 常规后处理。HSV 不再内置另一个像素采样，也不在像素化之前执行。

只对 Renderer 的 Rendering Layer 位 7（128）或普通 PixelEffect Layer 12 生效。水与藤蔓自动设置位 128，仍保留各自蓝/绿普通 Layer。为了不处理背景，合成限定在深度测试后的对象遮罩中；块中心落在透明孔洞外时选该块最近的有效对象像素，避免细藤蔓采到背景。这个遮罩范围适配不同于源项目整屏处理，轮廓不向对象外扩张。

描边默认关闭，每次进入 Play Mode 重置为关闭。像素化仍启用；描边关闭时跳过 Sobel 检测及深度/法线输入要求。运行时 HSV 管理器 Inspector 的“像素描边”开关用于检查；正式玩法调用接口见 Agent API。

像素块大小在 PC / Mobile Renderer 的 Selective HSV Color Fade Feature 调整，默认 15 屏幕像素。旧 TA 02StencilPixelEffect 已停用，不重复像素化。旧 01visibleStencil 保留，但新处理不依赖其 stencil。

初次接入仅通过 C# 静态编译，Shader GPU 编译、Game 图像、分辨率切换、透明水波/藤蔓轮廓、开启描边后的遮罩与性能、移动端打包仍需验收；不承诺屏幕空间算法能完全消除相机运动中的格点跳变。
