# TapTap Unity 成就 SDK 接入

## 依赖与版本

项目固定 TapSDK 官方 NPM 当前可解析版本 `4.10.9`：

```text
com.taptap.sdk.core
com.taptap.sdk.login
com.taptap.sdk.achievement
```

同时固定 `com.unity.nuget.newtonsoft-json:3.2.1` 与
`com.google.external-dependency-manager:1.2.179`。不自动升级 Unity、URP 或
TapSDK；后续升级必须单独验证 Android Gradle 依赖和最终 APK。

官方 v4 文档当前写 `4.11.0`，但官方 NPM 尚未发布该版本，因此工程使用
`4.10.9`。该版本已包含统一初始化回调、Android 成就上报与并发重复上报修复。

## 配置

运行时资源：

```text
Resources/TapTap/TapTapAchievementSettings
```

字段：

```text
enabled                         总开关
clientId                        TapTap 开发者后台 Client ID
backendAchievementIdsConfirmed  后台成就 ID 已逐项确认
autoLoginWithBasicInfo          无账号时尝试 basic_info 无感登录
enableSdkToast                  默认关闭，避免与项目 Toast 重复
enableSdkLog                    Release 保持关闭
```

Client Token 不写入可提交的配置资产，而是读取本地、被 `.gitignore` 排除的
`TapTapAchievementSecrets.json`。Server Secret 不得进入客户端；Client Public Key
只用于 TapTap PC 启动器场景，当前 Android 接入不使用。

本地 Token 缺失或后台 ID 未确认时，`TapTapAchievementBridge` 不初始化、不登录、不发请求。

## 数据流

```text
本地玩法
  -> AchievementManager.TriggerById
  -> 本地 achievements.json
  -> AchievementUnlocked
  -> TapTapAchievementBridge
  -> TapTapAchievement.Unlock(platformAchievementId)
```

登录成功后桥接器会重新扫描本地已经解锁的成就并补报。上报成功按 TapTap
`unionId` 或 `openId` 写入独立报告记录；失败不写入，下一次启动会重试。平台 ID
只能取 `AchievementSO.PlatformAchievementId`。

## 生命周期与网络

- Android 原生 `PrivacyActivity` 明确同意后才启动 Unity；Unity 启动后桥接器才初始化 TapSDK。
- TapSDK AAR 的唯一自动初始化项 `TapTapKitInitProvider` 必须保持固定 authority 与 `exported=false`，其他 Provider 会被隐私构建守卫阻断。
- 桌面 Editor/Windows 不自动建立 TapSDK 桥，避免扩大当前 PC 网络面。
- Android 只允许 `INTERNET` 与 `ACCESS_NETWORK_STATE`；Wi-Fi、蓝牙、网络状态修改等权限继续被构建守卫移除。
- EDM4U 的编辑期分析关闭，不保留分析 cookie。
- 登录取消或失败只关闭平台上报，本地成就和本地 Toast 不受影响。

## 待回填

1. 在 TapTap 开发者后台确认包名 `com.miao1suki.shapeofcolor`、签名和成就服务已开通。
2. Client ID 已填；Client Token 放在本地 secrets 文件。
3. 将后台生成的最终成就 ID 逐项写入 14 个 `AchievementSO.platformAchievementId`。
4. `backendAchievementIdsConfirmed` 与 `enabled` 已开启。
5. 使用非 Development Android 包验证登录、解锁、补报、断网重试与最终 APK 权限。
