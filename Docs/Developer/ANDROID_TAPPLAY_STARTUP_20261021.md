# TapPlay Android 启动闪退兼容修复

## 证据

用户提供 `形之彩-测试报告.zip`，未达标设备为 vivo S19 / V2364A / Android 16。日志时间为 2026-10-21 08:40:23.998（北京时间）。应用已启动 Unity 6000.3.12f1、IL2CPP ARM64，崩溃线程是 `UnityGfxDeviceW`，信号为 `SIGSEGV / SEGV_MAPERR`；原生栈顶为 `/vendor/lib64/hw/vulkan.adreno.so`，下一帧为 `qglinternal::vkCmdDrawIndexed`，随后进入 `libunity.so`。启动日志确认使用 Adreno Vulkan 驱动 `0676.76.1`。

因此，本报告中的直接崩溃点是 Vulkan 图形驱动绘制路径，不是“缺少隐私协议”。它尚不能证明究竟是驱动缺陷、TapPlay 环境与驱动组合，还是 Unity 提交的特定绘制触发；不据此擅自删除玩法、美术或 SDK。

## 1.0.2 兼容措施

- Android 显式关闭自动图形 API，仅保留 `OpenGLES3`，完全绕开已确认的 Vulkan 崩溃路径。仅重排优先级并保留 Vulkan 仍可能回到该路径，因此不采用。
- `MobileBuildSetup.BuildAndroid` 重申该配置；所有 Android 构建经过 `AndroidGraphicsCompatibilityBuildGuard`，错误恢复自动选择或 Vulkan 时停止构建。
- PC 图形 API、Unity 版本、URP 资源、SDK、权限及隐私入口不变。不切换到旧兼容渲染管线，不关闭游戏光照/遮蔽来掩盖故障。
- 配置专项覆盖发布配置、幂等配置且不影响 PC、拒绝 Vulkan fallback 和自动选择。

Unity 官方 API 文档说明可按平台明确设置图形 API 列表，自动选择应另行关闭：[SetGraphicsAPIs](https://docs.unity3d.com/cn/6000.0/ScriptReference/PlayerSettings.SetGraphicsAPIs.html)、[SetUseDefaultGraphicsAPIs](https://docs.unity3d.com/cn/6000.0/ScriptReference/PlayerSettings.SetUseDefaultGraphicsAPIs.html)。

## 验收边界

构建成功、配置测试或其他手机启动正常，只能验证本地修复接入，不能代替 vivo S19 / Android 16 / TapPlay 同环境复测。新版应再次提交 TapPlay，确认使用 OpenGL ES，检查启动、菜单、光照遮蔽、天气/水体与完整游玩；无同型号复测前不宣称该报告已获平台通过。
