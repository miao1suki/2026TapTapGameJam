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
```

`PlayerHealth.Died` 可以触发 `PlayDeath`，`PlayerHealth.Revived` 可以触发 `PlayRespawn`。`PlayerController.RequestRespawn()` 在玩家存活时也能执行，但此时 `PlayerHealth.Revived` 不会触发；若希望手动重生同样播放恢复效果，应在调用 `RequestRespawn()` 的流程里调用 `PlayRespawn()`。目前未修改玩家或正式场景的事件接线，由程序按演出时机接入。

惊恐与死亡是独立的两层强度，最终取较大值。死亡层恢复时会回到当前惊恐强度，不会抹掉 Boss 战的持续状态。重新进入游戏或切场景时可调用 `ResetEffect()`，避免保留上一段演出强度。

## 可调参数

在 `Assets/Settings/PC_Renderer.asset` 和 `Assets/Settings/Mobile_Renderer.asset` 的 `RGB Glitch` Feature Inspector 分别调整：

| 字段 | 作用 |
| --- | --- |
| `Vignette Darkness` | 画面边缘的黑度，0～1 |
| `Shake Rate` | 抽动画面重新取样的频率，1～40 次/秒 |
| `Shake Strength` | 全屏与局部横向抽动幅度，按屏幕宽度比例计算 |
| `Color Separation Pixels` | 红蓝通道在强度 1 时的水平偏移像素数 |

这些数值与 `RgbGlitchEffect.Intensity` 相乘；0 强度时画面完全不变。PC 和 Mobile 参数可分别调优。

## 测试场景

打开同目录 `RgbGlitchPreview.unity`，直接 Play。场景运行时生成四色条纹以观察色彩偏移；每 6 秒自动循环：前 2 秒惊恐基线、2～4 秒死亡强化、4～6 秒复活恢复。选中 `RGB Glitch Preview` 可在 Inspector 调整 `Fear Level`，或关闭 `Automatic Cycle` 单独查看持续惊恐。其他四项参数在当前画质使用的 Renderer Asset 中调整，Play 时修改即可观察结果。此场景不加入正式构建。
