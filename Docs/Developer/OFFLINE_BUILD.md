# 比赛离线版本

比赛期间禁止非 TapTap SDK 的运行时联网，包括局域网发现、遥测、广告和外部网页跳转。当前项目没有接入 TapTap SDK，因此现阶段所有测试包均为完全离线版本。Git、Package Manager、编辑器协作工具不属于游戏运行时，不因此删除。

## 构建规则

- `MobileBuildSetup.BuildAndroid` / `BuildWindows` 使用 `BuildOptions.None`，关闭 Development、Profiler、Script Debugging 和 Deep Profiling。仍可用本机调试证书签名；调试签名不等于开发构建，也不会单独开启联网。
- `ContestOfflineBuildGuard` 拒绝带上述调试选项的任何构建，检查 Unity Analytics、Ads、Diagnostics、Crash Reporting、Performance Reporting 的开关和初始化配置。`submitAnalytics` 必须关闭。
- Android 在最高优先级 launcher Manifest 写入移除规则：INTERNET、网络状态、Wi-Fi、局域网及蓝牙相关权限；关闭 `debuggable` 和明文流量。不能只修改低优先级 androidlib 的 Manifest。
- 初次授权和设置中的隐私政策均使用内置全文，不提供浏览器跳转或可点击外链。PC 也在游戏内离线阅读。
- 现有内置政策是既有正文，没有按本次技术变更擅自重写法律条款；发布前负责人仍须核对其内容是否符合实际数据处理行为。

TapTap 云存档、成就等 SDK 是用户允许的例外，但尚未接入。接入时必须单独审核域名、权限、自动初始化及同意前行为，明确修改离线守卫和相关测试；不得为 SDK 之外的请求开放通路。当前不提前申请 INTERNET。

## 验证

运行 `ContestOfflineTests`、`AndroidPrivacyTests` 和 `MobileLayoutPointerTests`。最终 Android 包必须再用 aapt 检查权限与 Manifest，确认无网络权限、无导出的 Unity 入口且 `debuggable=false`；真机冷启动、最近任务恢复、授权、菜单往返和断网游玩还需设备验收。PC 需检查非开发构建、服务开关及运行时网络行为。编辑器测试和 Manifest 验证不是完整网络抓包或比赛合规认证。

旧 APK 不会因修改源码而更新。2026-10-16 贴墙放置修复旧包实际包含 INTERNET、`debuggable=true`，未发现 Wi-Fi/附近设备/蓝牙权限。此前构建脚本使用 Development；未找到项目自写扫描代码。手机提示的具体触发源未经真机日志确认，开发调试连接是可疑来源而非已证实结论。Unity 官方说明：[Android Manifest](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/introducing/manifest)。

本次不重新打包、不合并；已发布的旧包不作为离线版本继续分发。后续构建必须重新验收最终产物。
