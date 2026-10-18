# RGB 故障双预设：给程序 AI 的接入提示

可直接把下段作为接入任务说明：

> 在 Unity 6000.3.12f1 项目中，复用 `Assets/ta_source/RGB_seperate/预设版本` 的两个全屏 RGB 故障预设体，不重写效果 Shader、玩家死亡状态或相机逻辑。普通死亡选 `RgbGlitchDeathPreset.prefab`；Boss 惊恐阶段选 `RgbGlitchBossFearPreset.prefab`。同一时刻只启用一个 `RgbGlitchSceneDriver`，不要把两个预设体叠加。先确认玩法相机使用已配置 `RGB Glitch` Renderer Feature 的 PC/Mobile Renderer。父物体只是场景开关，子物体 `Full Screen Effect` 的 Inspector 是五个画面参数及过渡时长的调节入口；全屏效果不要求成为相机或 Canvas 的子物体。

## 资源与行为

| 用途 | 预设体 | 独立演示场景 | 默认表现 |
| --- | --- | --- | --- |
| 死亡／复活 | `RgbGlitchDeathPreset.prefab` | `RgbGlitchDeathDemo.unity` | 正常强度 0；死亡瞬间强抖动、明显 RGB 分离与暗角，保持到复活；复活用 1.2 秒 Lerp 消减。数值与当前 Test 场景的 `RgbGlitchSceneRig.prefab` 相同。 |
| Boss 惊恐 | `RgbGlitchBossFearPreset.prefab` | `RgbGlitchBossFearDemo.unity` | 惊恐基线 0.4；较轻的色差、通道跳动与小幅抽动，长时间显示但尽量不妨碍辨认画面。 |

死亡演示场景中，编辑模式即可看到四色测试图案；Play 后 0～2 秒正常、2 秒模拟死亡、4 秒模拟复活，之后渐退且不自动循环。场景专用的 `RgbGlitchDeathDemo` 只调用预设体控制器的触发方法，不在预设体中，也不要带入关卡。Boss 演示场景在 Play 后持续展示较轻的效果。两个演示场景都不加入正式构建，且不需要玩家对象。

## 接线约束

- `Assets/Scenes/Test.unity` 已有原始 `RgbGlitchSceneRig.prefab`，功能与死亡预设相同；在这个场景测试时不要再拖入一份死亡预设。两个新预设为以后其他场景选用，不替换或修改 Test 原有接线。
- 死亡预设自动查找 `PlayerController`，通过现有玩家死亡与重生事件驱动；不需要修改 `PlayerController`、`PlayerHealth`、相机或输入源码。Boss 预设也有相同触发能力，但其参数整体较轻；若 Boss 战死亡也必须使用强烈死亡表现，应由程序在阶段切换时选择死亡预设，而不是同时启用两个控制器。
- Boss 预设启用后默认立即显示 0.4 惊恐强度。若要渐入，在场景实例把 `惊恐强度` 设为 0，再由 Boss 事件调用 `RgbGlitchEffect.SetFear(0.4f, 1f)`；结束时调用 `RgbGlitchEffect.SetFear(0f, 1f)`，待渐退完成后再禁用预设体。直接禁用会立即清空效果。
- 无场景预设体时可直接使用 `TapTap.TA.RgbGlitch.RgbGlitchEffect` 的 `PlayDeath()`、`PlayRespawn()`、`SetFear(level, seconds)`、`SetParameters(...)` 与 `ResetEffect()`；完整 API 和参数范围见上级 `README.md`。死亡层与惊恐基线取较大值，复活只清除死亡层，不会清除 Boss 惊恐。
- 效果作用于游戏 Base Camera 的全屏画面，不是特定子物体；Screen Space Overlay UI 不经过此 Pass。编辑模式只预览场景几何，实际全屏故障需要 Play。
