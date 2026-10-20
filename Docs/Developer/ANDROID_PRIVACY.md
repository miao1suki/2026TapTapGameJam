# Android 隐私授权

## 玩家流程

安卓首次安装、清除应用数据或内置政策正文变更后，先显示原生隐私页面。页面包含完整离线正文和仅标为“同意”/“不同意”的按钮，另有主动点击的“查看《形之彩隐私政策》网页版”。未同意不启动 Unity；不同意或返回退出。打开或关闭网页均不代表同意。已同意且正文版本未变时正常进入游戏。

开始菜单与暂停菜单的设置均提供“隐私政策”。安卓点击后打开系统浏览器中的固定政策页，返回后仍保留原生离线全文、返回游戏与撤回授权入口；系统恢复原生页不反复打开浏览器。没有浏览器时提示并保留离线阅读。撤回会停止游戏，下次启动重新授权。桌面端在游戏内离线阅读同一正文，不增加强制启动弹窗。

## 政策来源

- 自建在线政策：https://meowgame.cloud/shape-of-color/privacy/ ，页底备案号与网站主页一致：豫ICP备2026018798号。
- 既有正文来源：https://www.taptap.cn/app/976206/common-privacy 。本轮仅更换载体/样式，不改写条款。
- 唯一正文源：`Assets/_Project/Resources/Privacy/privacy-policy.txt`，内容为既有 HTML，来自上述游戏自己的政策页，不是 TapTap 平台通用隐私政策。Android 构建时复制为生成工程 androidlib 的 `assets/privacy-policy.html`；PC 从 Resources 离线加载。不要维护两份正文。
- 同意版本为离线正文的 SHA-256，保存在应用不参与备份的私有目录；正文更新自动要求重新同意。更新在线政策时必须同步更新离线正文。

网站由 `Ops/PrivacyPolicy/build-site.mjs` 从唯一正文源生成，只增加响应式样式与备案页脚；`node --test Ops/PrivacyPolicy/build-site.test.mjs` 检查正文逐字一致。模板不含脚本、统计、外部字体/图片、登录、导航或其他内容；部署到现有 Nginx 静态目录，不改主页和其他服务。生成/更新及正文一致性维护见 `Ops/PrivacyPolicy/README.md`。没有新建 GitHub Pages。

政策现有注册、实名认证、TapSDK 等条款来自原文；发布前需要负责人按实际 TapTap 成就 SDK 的数据处理行为再次核对，不能把技术入口完成视为合规认证。原生隐私入口仍严格位于 Unity 和 TapSDK 初始化之前。

## 实现边界

- 最低 Android 7.1 / API 25 不变，Unity 6000.3.12f1 不变。
- Android Application Entry 使用传统 Activity，替代 GameActivity，以接入 UnityPlayerActivity 前置授权。
- `PrivacyActivity` 是独立 `:privacy` 进程中的唯一桌面入口，不引用 Unity、WebView 或网络 SDK；正文仍离线显示并移除原文 URLSpan，只通过受控点击调用 `PrivacyConsent.openPolicyWebsite` 打开固定 HTTPS 系统浏览器页面。设置查看以显式 Intent 标志发起，恢复状态不重开。浏览不会保存授权或启动 Unity。
- `PrivacyApplication.attachBaseContext` 在主进程组件初始化前验证同意记录，拦截系统直接恢复 Unity 活动的路径；无有效同意则打开原生入口并停止该主进程。不是只依赖桌面启动按钮。
- 构建钩子关闭 Unity 活动的导出和 Launcher，阻止未审计的自动初始化 Provider，复制现有 Cubic_11 原始字体到生成的 Android 工程，不在仓库额外复制字体。
- 新增 SDK、Application、其他进程、Activity/Provider 或入口变更，都必须重新审计。TapTap `4.10.9` 的 `TapTapKitInitProvider` 是当前唯一白名单 Provider，必须保持 authority `${applicationId}.TapTapKitInitProvider` 且 `exported=false`；不要扩大白名单。不要在隐私进程初始化 SDK，不要使用 Unity 内弹窗替代前置授权。

## 验收

安卓真机仍需验证：首次离线阅读、不同意/返回、同意、重启、设置阅读、撤回、最近任务恢复、清除数据、正文版本更新及 Android 7.1 恢复路径，并用网络/隐私检测工具检查同意前行为。原生 Java 编译及编辑器测试不能替代真机隐私检测。

历史隐私入口验证包位于 `Library/PrivacyGateChecks/Android/形之彩.apk`，最低 API25，含 INTERNET 和开发资源；它不是当前版本。当前构建规则保持非 Development，只允许 TapTap 必需的 `INTERNET`、`ACCESS_NETWORK_STATE`，其余网络/LAN/Wi-Fi/蓝牙权限继续移除，详见 [比赛离线版本](OFFLINE_BUILD.md)。本次没有重打 APK，不能以源码检查代替新包或真机验收。

只在隔离工程进行构建验证，不修改策划当前场景或升级编辑器。
