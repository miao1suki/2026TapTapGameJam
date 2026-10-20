# 比赛离线版本

比赛期间禁止非 TapTap SDK 的游戏运行时联网，包括局域网发现、遥测和广告。当前已接入 TapTap Unity Core/Login/Achievement SDK，按官方要求只保留 `INTERNET` 与 `ACCESS_NETWORK_STATE`；SDK 只在 Android 原生隐私入口明确同意、Unity 启动后初始化，其他平台不发请求。2026-10-17 用户明确要求的唯一网页例外：玩家主动点击隐私政策时，由系统浏览器打开固定 HTTPS 政策页；浏览器自行联网，不向游戏申请 INTERNET。Git、Package Manager、编辑器协作工具不属于游戏运行时，不因此删除。

## 构建规则

- `MobileBuildSetup.BuildAndroid` / `BuildWindows` 使用 `BuildOptions.None`，关闭 Development、Profiler、Script Debugging 和 Deep Profiling。仍可用本机调试证书签名；调试签名不等于开发构建，也不会单独开启联网。
- `ContestOfflineBuildGuard` 拒绝带上述调试选项的任何构建，检查 Unity Analytics、Ads、Diagnostics、Crash Reporting、Performance Reporting 的开关和初始化配置。`submitAnalytics` 必须关闭。
- Android 在最高优先级 launcher Manifest 显式保留 TapTap 必需的 `INTERNET`、`ACCESS_NETWORK_STATE`，并对网络状态修改、Wi-Fi、局域网及蓝牙相关权限写入移除规则；关闭 `debuggable` 和明文流量。不能只修改低优先级 androidlib 的 Manifest。
- 初次原生授权保留内置全文，提供一个明确点击的固定政策网页入口；安卓设置点击隐私政策时打开系统浏览器，返回后仍可在原生页离线阅读和撤回同意。网页没有自动请求、WebView 或任意 URL 参数。PC 保持游戏内离线阅读。此例外不改变非开发构建和网络权限移除守卫。
- 现有内置政策是既有正文，没有按本次技术变更擅自重写法律条款；发布前负责人仍须核对其内容是否符合实际数据处理行为。

TapTap 成就 SDK 固定为 `4.10.9`，依赖官方 Core、Login、Newtonsoft.Json 与 EDM4U；EDM4U 编辑期分析明确关闭。当前只初始化 Android；客户端 ID 与后台 ID 确认已写入配置资产，Client Token 只保存在被 `.gitignore` 排除的本地 Resources 文件，缺失时保持安全待机、不产生网络请求。Server Secret 不得进入客户端。原生隐私入口未同意时 Unity 和 TapSDK 均不会启动。最终 AAR 合并有 TapTapKitInitProvider 和其间接依赖 ThemisProvider：仅允许固定 authority、exported=false、同意门保护的默认主进程；不得在 :privacy 初始化。最终合并 Manifest 会再经构建守卫检查，避免间接 AAR 绕过源文件扫描。不得为 SDK 之外的请求开放通路。

## 验证

2026-10-20：EDM4U 1.2.179 改为项目内嵌包，持久化禁用不使用的 `Google.IOSResolver.dll` 编辑器导入，避免未安装 iOS Build Support 时报告缺少 `UnityEditor.iOS.Extensions.Xcode`。Android 的 `Google.JarResolver.dll` 及导入配置保留原样；未升级 SDK，也未关闭全局程序集引用检查。具体覆盖见 `Packages/com.google.external-dependency-manager/PROJECT_OVERRIDES.md`。

运行 `ContestOfflineTests`、`AndroidPrivacyTests` 和 `MobileLayoutPointerTests`。最终 Android 包必须再用 aapt 检查权限与 Manifest，确认只有 `INTERNET`、`ACCESS_NETWORK_STATE` 两项网络权限，无网络状态修改、Wi-Fi、局域网或蓝牙权限，无导出的 Unity 入口且 `debuggable=false`；真机还需验证授权前无网络、同意后才初始化 SDK、最近任务恢复、菜单往返和断网游玩。PC 需检查非开发构建、服务开关及运行时网络行为。编辑器测试和 Manifest 验证不是完整网络抓包或比赛合规认证。

旧 APK 不会因修改源码而更新。2026-10-16 贴墙放置修复旧包实际包含 INTERNET、`debuggable=true`，未发现 Wi-Fi/附近设备/蓝牙权限。此前构建脚本使用 Development；未找到项目自写扫描代码。手机提示的具体触发源未经真机日志确认，开发调试连接是可疑来源而非已证实结论。Unity 官方说明：[Android Manifest](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/introducing/manifest)。

本次不重新打包、不合并；已发布的旧包不作为离线版本继续分发。后续构建必须重新验收最终产物。
