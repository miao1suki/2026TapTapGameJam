# Android 隐私入口接入

维护前读取项目持续更新约定及 [开发说明](../../Developer/ANDROID_PRIVACY.md)。原生实现位于 `Assets/Plugins/Android/PrivacyGate.androidlib`。

## 游戏侧 API

`Project.Privacy.AndroidPrivacyPolicy.ShowPolicy(TMP_FontAsset font)`：安卓打开原生离线阅读/撤回页，其他平台用传入的 Cubic_11 SDF 在游戏内离线阅读。仅在用户主动请求时调用，不调用外部浏览器。现有 SettingsPanelController 已接入，无需改场景或预制体。`IsShowing` 用于阻止桌面阅读模态层下的暂停导航。

Unity 不负责首次授权，不得将同意按钮、政策加载或 SDK 初始化搬到 Awake/运行时场景内。不要改用 PlayerPrefs 作为原生授权记录。

## 原生与构建

- `PrivacyConsent`：离线正文哈希、AtomicFile 持久化、查看/撤回；跨进程直接读取，不用 SharedPreferences 缓存。
- `PrivacyActivity`：独立进程原生阅读与同意，明确同意并落盘后才启动 UnityPlayerActivity。
- `PrivacyApplication`：attachBaseContext 早期校验；含 Android 7.1 的自身进程识别路径，不查询其他应用列表。
- `AndroidPrivacyBuildGuard`：Activity 入口检查、生成 Manifest 的 Unity Launcher 移除/非导出设置、自动初始化 Provider 审计、原始字体及唯一政策正文复制。只改生成目录。
- 正文源为 `Assets/_Project/Resources/Privacy/privacy-policy.txt`，不得恢复外部跳转。`ContestOfflineBuildGuard` 禁止开发构建/遥测并在最高优先级 launcher 移除网络权限；比赛规则见 [离线构建](../../Developer/OFFLINE_BUILD.md)。
- JNI 调用由 consumer-rules 保留。不使用 AppComponentFactory，不提高最低系统版本。

在线政策和内置正文必须保持一致。任何 SDK、自动初始化组件或进程结构变更均需重新检查最终 APK 的合并 Manifest 及授权前行为。真机验收项见开发说明。

## 同批玩法回归

弹性植物精确弹跳的上升阶段由 PlayerController 保护，邻近竖向/横向攀爬不能覆盖向上速度；到顶后恢复正常攀爬判定。不可简单禁用场景藤蔓。

ColorKeyPickup.IsCollectionVisualComplete 表示需要隐藏的收集视觉已结束。MechanismArtworkPresentation 必须遵守此状态；物体的完整拾取生命周期仍等待演出/表现完成，不能为解决可见性提前销毁 GameObject。重置时恢复该状态。
