# 全屏 RGB 故障效果

Unity 6000.3.12f1 / URP 17.3.0。效果在游戏相机的透明物体和颜色 HSV Pass 之后、URP 后处理之前执行，覆盖整个相机画面。强度为 0 时不入队渲染 Pass。PC 和 Mobile Renderer 已接入 `RGB Glitch` Renderer Feature；程序直接调用 API 时不需要在场景里放控制器，也可使用下方的场景预设体。

## 场景挂载与 Test 测试

`Assets/Scenes/Test.unity` 已增量挂载 `RgbGlitchSceneRig.prefab`，不改原有玩家、相机、玩法代码。Hierarchy 中展开 **RGB Glitch (Full Screen) → Full Screen Effect**，选中子物体，在 Inspector 的 **触发与目标**、**画面参数** 中调节中文滑块。默认惊恐强度为 0：进入 Play 后正常画面不显示故障；玩家死亡瞬间将整屏抖动与 RGB 分离拉到最大并保持，复活后用 1.2 秒 Lerp 平滑消减回原本强度。父物体或子物体组件的启停控制是否监听这些触发；Inspector 的 **运行时测试** 可手动播放死亡强化与复活恢复。

控制脚本自动查找场景玩家；`PlayerHealth.Died`、`PlayerController.ControlLockChanged` 和 `IsDead` 状态检查共同覆盖受伤与摔死，`RequestRespawn()` 完成时的 `CameraFollowRelocated` 事件触发恢复，因此存活时按 R 重生也可覆盖。死亡效果一直保持到重生，不做自动循环；复活过程才执行 Lerp 渐退。禁用控制物体或离开场景会清空强度和参数覆盖。以后需要在其他场景以同样方式控制，可拖入 `RgbGlitchSceneRig.prefab`；同一时间只启用一个场景控制预设体。

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

不传秒数时，`PlayDeath()` 默认立即生效，`PlayRespawn()` 默认在 1.2 秒内渐退。两者可单独传入过渡时间；死亡后不自动消减，必须调用 `PlayRespawn()` 或 `ResetEffect()`。Test 场景预设体已自动接入死亡与重生；其他场景如果不放预设体，可由程序监听 `PlayerHealth.Died`、`PlayerHealth.Revived` 等事件调用 API。`PlayerController.RequestRespawn()` 在玩家存活时也能执行，但此时 `PlayerHealth.Revived` 不会触发，手动重生需额外调用 `PlayRespawn()`。正式 Gaming 场景未改动。

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
