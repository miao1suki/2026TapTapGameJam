# 全屏 RGB 故障效果

Unity 6000.3.12f1 / URP 17.3.0。效果在游戏相机的透明物体和颜色 HSV Pass 之后、URP 后处理之前执行，覆盖整个相机画面。强度为 0 时不入队渲染 Pass。PC 和 Mobile Renderer 已接入 `RGB Glitch` Renderer Feature；不需要在场景里放控制器。

## 给程序的接口

命名空间：`TapTap.TA.RgbGlitch`，静态入口：`RgbGlitchEffect`。参数 `seconds` 是过渡秒数，使用非缩放时间；传 0 立即生效。

```csharp
using TapTap.TA.RgbGlitch;

RgbGlitchEffect.SetFear(0.45f, 1.0f); // Boss 战惊恐基线，0～1
RgbGlitchEffect.PlayDeath(0.25f);     // 死亡：加强到最大
RgbGlitchEffect.PlayRespawn(0.7f);    // 复活：回到当前惊恐基线
RgbGlitchEffect.SetFear(0f, 1.0f);    // Boss 战结束
RgbGlitchEffect.ResetEffect();         // 离开玩法时立即清空
RgbGlitchEffect.SetParameters(0.65f, 15f, 0.012f, 7f); // 暗角、速率、幅度、色差像素
RgbGlitchEffect.UseRendererDefaults(); // 取消参数覆盖，回到 Renderer Asset 数值
```

`PlayerHealth.Died` 可以触发 `PlayDeath`，`PlayerHealth.Revived` 可以触发 `PlayRespawn`。`PlayerController.RequestRespawn()` 在玩家存活时也能执行，但此时 `PlayerHealth.Revived` 不会触发；若希望手动重生同样播放恢复效果，应在调用 `RequestRespawn()` 的流程里调用 `PlayRespawn()`。目前未修改玩家或正式场景的事件接线，由程序按演出时机接入。

惊恐与死亡是独立的两层强度，最终取较大值。死亡层恢复时会回到当前惊恐强度，不会抹掉 Boss 战的持续状态。重新进入游戏或切场景时可调用 `ResetEffect()`，避免保留上一段演出强度。

## 可调参数

打开 `RgbGlitchPreview.unity`，在 Hierarchy 选中 `RGB Glitch Preview`，Inspector 的 **画面参数** 中有四个中文滑块。测试场景 Play 后它们会立即驱动全屏效果；也可在 Play 前设置并保存到场景。程序可用 `SetParameters` 设置同样四项，`UseRendererDefaults` 清除覆盖。离开预览场景时会自动清除预览覆盖。

默认值仍可在 `Assets/Settings/PC_Renderer.asset` 和 `Assets/Settings/Mobile_Renderer.asset` 的 `RGB Glitch` Feature Inspector 分别调整：

| 字段 | 作用 |
| --- | --- |
| `Vignette Darkness` | 画面边缘的黑度，0～1 |
| `Shake Rate` | 抽动画面重新取样的频率，1～40 次/秒 |
| `Shake Strength` | 全屏与局部横向抽动幅度，按屏幕宽度比例计算 |
| `Color Separation Pixels` | 红蓝通道在强度 1 时的水平偏移像素数 |

暗角黑度、抖动幅度和色差受 `RgbGlitchEffect.Intensity` 缩放，抖动速率控制重新取样的频率；0 强度时画面完全不变。PC 和 Mobile 的默认值可分别调优。

## 测试场景

打开同目录 `RgbGlitchPreview.unity`，Scene 视图中立即可见已保存的四色测试图案。选中 `RGB Glitch Preview` 可在 Inspector 的 **演示** 中调惊恐强度和自动循环，在 **画面参数** 中调四个滑块。直接 Play 后，全屏效果每 6 秒循环：前 2 秒惊恐基线、2～4 秒死亡强化、4～6 秒复活恢复。关闭自动循环可单独查看持续惊恐。此场景不加入正式构建。
