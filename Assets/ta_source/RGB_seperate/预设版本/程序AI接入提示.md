# RGB 故障双预设：给程序 AI 的接入提示

> 2026-10-18 正式接入更新：标准 `Assets/_Project/Content/Player/Player.prefab` 已有唯一生命周期驱动，下面的独立场景预设仅保留作为 TA 演示。正式 Boss 流程直接调用 `RgbGlitchEffect.BeginBossFear(seconds)` / `EndBossFear(seconds)`，不再同时挂两个预设，不扫描 Boss 自动开启。驱动已统一监听死亡和 `RespawnCompleted`（含存活 R、慢加载）；不要额外监听 `Revived` 或 `CameraFollowRelocated` 提前恢复。死亡强预设会在 Boss 弱预设之上自动接管，复活淡出后回到 Boss 弱预设。历史示例未改正式 Gameplay Scene，详细行为以上级 README“正式接入”一节为准。

可直接把下段作为接入任务说明：

> 在 Unity 6000.3.12f1 项目中复用现有效果 Shader，不重写玩家死亡状态或相机逻辑。标准玩家已绑定唯一驱动，不再往正式场景放这两套演示预设。先确认玩法相机使用已配置 `RGB Glitch` Renderer Feature 的 PC/Mobile Renderer。死亡会自动触发；Boss 阶段用同一驱动的显式 `BeginBossFear` / `EndBossFear` 切换。现有右移马夫已经通过 `Assets/Boss_Logic/BossRgbFearPresentation` 按真实运动阶段调用，其他 Boss 专属调用也只能放 `Assets/Boss_Logic`。

## 资源与行为

| 用途 | 预设体 | 独立演示场景 | 默认表现 |
| --- | --- | --- | --- |
| 死亡／复活 | `RgbGlitchDeathPreset.prefab` | `RgbGlitchDeathDemo.unity` | 正常强度 0；死亡瞬间强抖动、明显 RGB 分离与暗角，保持到复活；复活用 1.2 秒 Lerp 消减。数值与当前 Test 场景的 `RgbGlitchSceneRig.prefab` 相同。 |
| Boss 惊恐 | `RgbGlitchBossFearPreset.prefab` | `RgbGlitchBossFearDemo.unity` | 惊恐基线 0.4；较轻的色差、通道跳动与小幅抽动，长时间显示但尽量不妨碍辨认画面。 |

死亡演示场景中，编辑模式即可看到四色测试图案；Play 后 0～2 秒正常、2 秒模拟死亡、4 秒模拟复活，之后渐退且不自动循环。场景专用的 `RgbGlitchDeathDemo` 只调用预设体控制器的触发方法，不在预设体中，也不要带入关卡。Boss 演示场景在 Play 后持续展示较轻的效果。两个演示场景都不加入正式构建，且不需要玩家对象。

## 接线约束

- 本轮正式集成未采用 TA 分支的 `Assets/Scenes/Test.unity` 变更；标准玩家即可继承完整效果。独立演示场景保留原有 TA Rig，不加入正式构建。
- 标准玩家监听死亡、`RespawnCompleted` 与死亡状态兜底；不需要修改 `PlayerController`、`PlayerHealth`、相机或输入源码。Boss 期间死亡会自动使用强死亡参数，复活完成并淡出后恢复弱惊恐参数，不需要切换两个控制器。
- `RgbGlitchBossFearPreset` 仅用于 TA 无玩家演示，仍保留其 0.4 authored 基线。正式玩家默认基线 0，只在实际入战时由 `BeginBossFear(1f)` 渐入，停战 `EndBossFear(1f)` 渐退；不要把演示预设直接挂到正式玩家或正式场景。
- 低层 `PlayDeath()`、`PlayRespawn()`、`SetFear(level, seconds)`、`SetParameters(...)` 与 `ResetEffect()` 保留给独立演示或明确拥有完整生命周期的调用者。正式业务不要重复订阅死亡，不要用 `ResetEffect()` 结束 Boss 阶段（这会抹掉死亡层）；使用 `EndBossFear` 只结束惊恐基线。
- 效果作用于游戏 Base Camera 的全屏画面，不是特定子物体；Screen Space Overlay UI 不经过此 Pass。编辑模式只预览场景几何，实际全屏故障需要 Play。
