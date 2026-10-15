# Android 隐私授权

## 玩家流程

安卓首次安装、清除应用数据或内置政策正文变更后，先显示原生隐私页面。页面包含完整离线正文、在线政策入口，以及同意/不同意按钮。未同意不启动 Unity；不同意或返回退出。已同意且正文版本未变时正常进入游戏。

开始菜单与暂停菜单的设置均提供“隐私政策”。安卓可阅读政策及撤回同意；撤回会停止游戏，下次启动重新授权。桌面端打开在线政策，不增加强制启动弹窗。

## 政策来源

- 在线政策：https://www.taptap.cn/app/976206/common-privacy
- 离线正文：`Assets/Plugins/Android/PrivacyGate.androidlib/assets/privacy-policy.html`，来自上述游戏自己的政策页，不是 TapTap 平台通用隐私政策。
- 同意版本为离线正文的 SHA-256，保存在应用不参与备份的私有目录；正文更新自动要求重新同意。更新在线政策时必须同步更新离线正文。

TapTap [公开上架指南](https://developer.taptap.cn/docs/store/release/policy/policy-guide/)要求政策可访问、内外一致、与实际行为及主体一致，禁止使用论坛帖作为政策载体；未发现要求把游戏自身的 common-privacy 页改为自建网站。此前飞书文档当前无法完整读取，因此不能据此确认其中是否还有额外要求。当前未新建 GitHub Pages。

政策现有注册、实名认证、TapSDK 等条款来自原文；发布前需要负责人按实际 SDK/数据处理行为核对，不能把技术入口完成视为合规认证。本次未改写这些法律事实，也未接入 TapSDK。

## 实现边界

- 最低 Android 7.1 / API 25 不变，Unity 6000.3.12f1 不变。
- Android Application Entry 使用传统 Activity，替代 GameActivity，以接入 UnityPlayerActivity 前置授权。
- `PrivacyActivity` 是独立 `:privacy` 进程中的唯一桌面入口，不引用 Unity、WebView 或网络 SDK；正文离线显示，只有主动点击在线入口才调用外部浏览器。
- `PrivacyApplication.attachBaseContext` 在主进程组件初始化前验证同意记录，拦截系统直接恢复 Unity 活动的路径；无有效同意则打开原生入口并停止该主进程。不是只依赖桌面启动按钮。
- 构建钩子关闭 Unity 活动的导出和 Launcher，阻止未审计的自动初始化 Provider，复制现有 Cubic_11 原始字体到生成的 Android 工程，不在仓库额外复制字体。
- 新增 SDK、Application、其他进程、Activity/Provider 或入口变更，都必须重新审计。不要在隐私进程初始化 SDK，不要使用 Unity 内弹窗替代前置授权。

## 验收

安卓真机仍需验证：首次离线阅读、不同意/返回、同意、重启、设置阅读、撤回、最近任务恢复、清除数据、正文版本更新及 Android 7.1 恢复路径，并用网络/隐私检测工具检查同意前行为。原生 Java 编译及编辑器测试不能替代真机隐私检测。

本轮隔离构建已成功，包名 `com.miao1suki.shapeofcolor`、应用名“形之彩”，唯一 Launcher 为 PrivacyActivity，UnityPlayerActivity 非导出，合并 Application 正确；APK包含离线正文和字体，最低 API25，仅声明 INTERNET。验证产物位于 `Library/PrivacyGateChecks/Android/形之彩.apk`，是含调试资源的开发验证包，不是最终发布包。目前未连接安卓真机。

只在隔离工程进行构建验证，不修改策划当前场景或升级编辑器。
