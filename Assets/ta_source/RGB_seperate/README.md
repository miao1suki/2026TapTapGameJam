# 全屏 RGB 故障效果

Unity 6000.3.12f1 / URP 17.3.0。效果在游戏相机的透明物体和颜色 HSV Pass 之后、URP 后处理之前执行，覆盖整个相机画面。强度为 0 时不入队渲染 Pass。PC 和 Mobile Renderer 已接入 `RGB Glitch` Renderer Feature；程序直接调用 API 时不需要在场景里放控制器，也可使用下方的场景预设体。

## 正式接入（2026-10-18）

唯一标准玩家 `Assets/_Project/Content/Player/Player.prefab` 根节点已绑定一个 `RgbGlitchSceneDriver`，并直接引用同物体的 `PlayerController`。使用标准玩家的 Gaming、MRoom 与测试场景自动继承，不需要向玩法 Scene 再放 Rig，本轮未采用 TA 分支的 `Test.unity` 改动。正常强度为 0；死亡采用 TA 强预设（暗角 0.65、速率 15、幅度 0.018、色差 10、跳动 0.85），复活在 1.2 秒内恢复。

死亡通过 `PlayerHealth.Died`、`ControlLockChanged` 与 `IsDead` 兜底检测；恢复只监听 `PlayerController.RespawnCompleted`。加载复活的 `PerformRespawn` 会提前发送 `CameraFollowRelocated` 和复活血量，这时遮罩尚未退出，不能用这两个事件提前清空效果。存活时按 R 也会发送 `RespawnCompleted`，不需要额外写一套 R 键监听。驱动禁用或离场立即清空死亡、惊恐与参数覆盖。

同一时刻只有一个主驱动：标准玩家上的驱动优先于可选场景 Rig。被抢占的 Rig 与独立 Preview 不会再覆盖参数，禁用它们也不能清掉主玩家的效果。仍建议不要在玩法场景额外放演示 Rig。Boss 的弱预设只提供显式调用入口，不扫描 Boss、不在其出现或场景加载时擅自启用。

```csharp
using TapTap.TA.RgbGlitch;

// Boss 专属流程在 Assets/Boss_Logic 中显式调用；返回 false 表示当前没有主驱动。
RgbGlitchEffect.BeginBossFear(1f); // TA 弱预设渐入，基线 0.4
RgbGlitchEffect.EndBossFear(1f);   // 战斗结束渐退至玩家原有基线（默认 0）
```

Boss 惊恐期间死亡仍使用强死亡参数；死亡层淡出完成后自动恢复弱惊恐参数。结束战斗不会中断尚未完成的死亡层。可以在标准玩家 Inspector 的“Boss 显式调用预设（默认关闭）”调节六个弱预设参数。没有修改玩家玩法状态、相机 Transform、输入或加载转场，也没有增加第二个加载遮罩。

现有 Boss 右移战阶段已正式接通：`Assets/Boss_Logic/BossRightMoveExecutor.cs` 在 tracking 开关和原有启动延迟都放行、实际首次接管右移后发出 `MotionStateChanged(true)`；停战、死亡、禁用时发出 `false`。它在 Awake 确保同物体有一个 `BossRgbFearPresentation`，该桥接按真实运动阶段调用同一个玩家驱动，不改场景、Boss 运动数值或 Timeline。多个执行器重叠时，最后一个停止才结束基线；新玩家替换时重新交给当前驱动。不是检测场景里存在 Boss 就开启效果。

## TA 独立演示与可选场景 Rig

`RgbGlitchSceneRig.prefab` 保留给没有标准玩家的独立演示使用。Hierarchy 中展开 **RGB Glitch (Full Screen) → Full Screen Effect**，选中子物体，在 Inspector 的 **触发与目标**、**画面参数** 中调节中文滑块。默认惊恐强度为 0：进入 Play 后正常画面不显示故障；玩家死亡瞬间将整屏抖动与 RGB 分离拉到最大并保持，复活后用 1.2 秒 Lerp 平滑消减回原本强度。Inspector 的 **运行时测试** 可手动播放死亡强化与复活恢复，只有当前主驱动可以执行。

控制脚本优先使用显式玩家引用和同物体的玩家，独立演示 Rig 留空时才限频查找场景玩家。正式死亡与复活完成事件接线见上节，不再使用 `CameraFollowRelocated` 启动恢复。死亡效果一直保持到复活完成，不做自动循环。

子物体是**控制器**，不是被处理的画面目标；它不需要放在相机、Canvas 或 Global Volume 之下。渲染范围仍是当前使用 PC/Mobile Renderer 的游戏 Base Camera 全屏，任何父子级外、由该相机渲染的物体也会被处理；Screen Space Overlay UI 不在此 Pass 内。编辑模式下可见控制预设体层级和参数，但真正的全屏画面变化只在 Play 模式显示。

## 死亡与 Boss 双预设

`预设版本` 文件夹另有两套**可选**场景控制预设体和各自独立演示场景：`RgbGlitchDeathPreset.prefab`／`RgbGlitchDeathDemo.unity` 复现当前 Test 的强烈死亡效果；`RgbGlitchBossFearPreset.prefab`／`RgbGlitchBossFearDemo.unity` 保留较轻的色差与抽动，适合 Boss 战持续惊恐。两个场景在编辑模式可见四色测试图案，Play 后死亡场景仅演示一次“正常 → 死亡保持 → 复活 Lerp”，Boss 场景持续显示轻效果。不要把两个控制预设体同时启用，也不要在已有原始控制器的 Test 场景重复挂载；程序接入提示见 [预设版本/程序AI接入提示.md](预设版本/程序AI接入提示.md)。

## 给程序的接口

命名空间：`TapTap.TA.RgbGlitch`，静态入口：`RgbGlitchEffect`。参数 `seconds` 是过渡秒数，使用非缩放时间；传 0 立即生效。

```csharp
using TapTap.TA.RgbGlitch;

RgbGlitchEffect.SetFear(0.45f, 1.0f); // Boss 战惊恐基线，0～1
RgbGlitchEffect.PlayDeath(0f);        // 死亡：瞬间加强到最大并保持
RgbGlitchEffect.PlayRespawn(1.2f);    // 复活：Lerp 渐退到当前惊恐基线
RgbGlitchEffect.SetFear(0f, 1.0f);    // Boss 战结束
RgbGlitchEffect.ResetEffect();         // 离开玩法时立即清空
RgbGlitchEffect.SetParameters(0.65f, 15f, 0.012f, 7f, 0.7f); // 暗角、速率、整屏抖动、色差像素、色差跳动
RgbGlitchEffect.UseRendererDefaults(); // 取消参数覆盖，回到 Renderer Asset 数值
```

不传秒数时，低层 `PlayDeath()` 默认立即生效，`PlayRespawn()` 默认在 1.2 秒内渐退。两者可单独传入过渡时间；死亡后不自动消减，必须调用 `PlayRespawn()` 或 `ResetEffect()`。正式标准玩家已统一完成死亡、正常复活、慢加载复活和存活 R 重置接线，业务不要再重复订阅；尤其不要用 `PlayerHealth.Revived` 抢先结束慢加载死亡表现。Boss 正式流程请使用上节的 `BeginBossFear` / `EndBossFear`，由同一个驱动统一切换强弱预设。下面的低层接口保留给 TA 独立预览或明确管理整个生命周期的调用者。

惊恐与死亡是独立的两层强度，最终取较大值。死亡层在复活时按非缩放时间进行 `Mathf.Lerp` 平滑恢复，回到当前惊恐强度，不会抹掉 Boss 战的持续状态。重新进入游戏或切场景时可调用 `ResetEffect()`，避免保留上一段演出强度。

## 可调参数

在 Test 场景选中 `RGB Glitch (Full Screen) → Full Screen Effect`，或在独立的 `RgbGlitchPreview.unity` 选中 `RGB Glitch Preview`，Inspector 的 **画面参数** 中有五个中文滑块。Play 中调节会立即驱动全屏效果；Play 前也可设置并保存到预设体或场景。程序可用五参数 `SetParameters` 设置同样五项；原四参数调用仍可用，色差跳动默认 0.7。`UseRendererDefaults` 清除参数覆盖。

默认值仍可在 `Assets/Settings/PC_Renderer.asset` 和 `Assets/Settings/Mobile_Renderer.asset` 的 `RGB Glitch` Feature Inspector 分别调整：

| 字段 | 作用 |
| --- | --- |
| `Vignette Darkness` | 画面边缘的黑度，0～1 |
| `Shake Rate` | 抽动画面重新取样的频率，1～40 次/秒 |
| `Shake Strength` | 全屏与局部横向抽动幅度，按屏幕宽度比例计算 |
| `Color Separation Pixels` | 红蓝通道在强度 1 时的水平偏移像素数 |
| `Color Separation Jitter` | 红、绿、蓝各通道独立偏移的跳动幅度，0～1；0 为固定色差 |

暗角黑度、整屏抖动幅度、色差和通道跳动均受 `RgbGlitchEffect.Intensity` 缩放；抖动速率同时控制整屏与色彩通道重新取样的频率。把 `Shake Strength` 调成 0，色差仍可独立跳动；把 `Color Separation Jitter` 调成 0，则恢复固定色差。强度为 0 时画面完全不变。PC 和 Mobile 的默认值可分别调优。

## 测试场景

打开同目录 `RgbGlitchPreview.unity`，Scene 视图中立即可见已保存的四色测试图案。选中 `RGB Glitch Preview` 可在 Inspector 的 **演示** 中调惊恐强度和自动循环，在 **画面参数** 中调五个滑块。直接 Play 后，全屏效果每 6 秒循环：前 2 秒惊恐基线、2～4 秒死亡强化、4～6 秒复活恢复。关闭自动循环可单独查看持续惊恐。此场景专用于演示，可保持非零惊恐强度；Test 场景的正常游玩基线则为 0。独立预览场景不加入正式构建。

## 专项验证

静默隔离工程可调用 `TapTap.TA.RgbGlitch.Editor.RgbGlitchIntegrationAudit.Run`（需 `-noaudio`，仅允许 `Library/FlowRetirementProbe` 或本轮 `Library/TAIntegration-20261018` 工程）。脚本实例化真实标准玩家，检查唯一驱动、普通/血量死亡、存活 R、慢加载的复活回调边界、Boss 强弱预设恢复、重复 Rig/Preview 所有权和退出清理，报告输出 `Library/RgbGlitchIntegrationAudit.txt`。测试用可控 Tick 检查非缩放时间状态机，不代替实际 GPU 渲染与安卓实机截图；PC/Mobile RendererFeature 与 Overlay UI 范围仍需整合者单独验证。

Boss 专项入口 `Project.BossLogic.Editor.BossRgbFearAudit.Run` 位于 `Assets/Boss_Logic/Editor`，在同样静默隔离环境中覆盖实际 tracking/延迟门、右移开始/停止、玩家死亡复活、多个执行器、Boss 死亡与禁用。报告为 `Library/BossRgbFearAudit.txt`。
