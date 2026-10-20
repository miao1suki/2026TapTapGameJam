# Android TapTap 网络签名错误排查

截图中的 `TapSDK: 网络签名错误。详见 ErrLog` 来自原生 SDK。`ErrLog` 是 Android 日志标签，不是游戏设置里的菜单，也不能单凭这句话判定 APK 证书错误；网络请求签名、应用配置和登录证书校验需要看实际错误码区分。

## 当前核验结果

- 发布 APK `形之彩-0.2.1-20261020-SDK整合/Android/形之彩.apk` 的签名通过 `apksigner verify --print-certs`，证书 MD5 为 `2D9E5B4A9D63EB90D63EA84789C0C333`。
- 项目包名 `com.miao1suki.shapeofcolor`、SDK Client ID 与当前应用交接一致；SDK 仍只在 Android 隐私授权后由主线程初始化，Release 日志保持关闭。
- 本次没有连接可授权的 ADB 设备，尚未取得截图所对应的原生错误码，不能声称已修复服务端鉴权。
- 用户已确认该错误发生于 TapTap 内直接运行，即 TapPlay 沙箱路径，不是独立安装 APK。截图文件名的 `com.taptap` 不是游戏 APK 包名的证据。
- 旧后台交接记录曾记载证书列表为空，这只是需要负责人重新核对的历史状态，不代表当前后台仍为空。不要未经授权修改凭据、替换证书或关闭 SDK 来掩盖错误。

## TapPlay 与 SDK 兼容边界

[TapPlay 官方 v4 说明](https://developer.taptap.cn/docs/sdk/tap-play/features/)明确：它是运行 APK 的沙箱环境，APK 设置勾选 TapPlay 授权即可，零代码接入。它不是需要导出 WebGL 和更换 SDK 的 Tap 小游戏；不能因为出现右上角胶囊便把 Android SDK 切成小游戏 SDK，也不能把应用 Client ID 改成 TapTap 宿主的 ID。

当前项目初始化所需的 Client ID、Client Token、CN 区域、主线程调用已提供，未发现官方要求额外传入一个“TapPlay 签名”参数。官方 [4.10.9 Android 更新说明](https://developer.taptap.cn/docs/sdk/start/release-notes/android/)新增初始化回调；4.11.0 条目只记载登录/排行榜 WebView 缓存优化，没有可据此认定会修复本错误的条目，因此本轮不升级 SDK。

核查当前实际依赖的 `tap-core/common-4.10.9.aar` 字节码：Gatekeeper 网络错误响应的 `msg` 与 `description` 从服务端响应读取；`GatekeeperRepository.showErrorToast` 显示 `msg`，并通过 `TapInitializer` 记录详细描述；`invalid_client` 会转成初始化失败回调。客户端并没有可通过搜一个固定中文字符串就确定根因的错误映射。需取到 `ErrLog` / `TapInitializer` 的实际响应错误码及描述，才可区分凭据、包名/签名和宿主兼容问题。上述字节码证据不是该手机已抓到 `invalid_client` 的证据。

## 怎样提供日志

1. 手机启用开发者选项与 USB 调试，USB 连接本机，手机上允许本机调试。无需开启 Unity Development Build，也无需清除数据或重装游戏。
2. 先在手机重现该提示，保持连接；在工程根目录运行：

   ```powershell
   powershell -ExecutionPolicy Bypass -File Ops/Android/Export-TapTapDiagnostics.ps1
   ```

3. 脚本会给出 `Library/AndroidDiagnostics/TapTap-时间.txt` 路径。它只读取日志，不清空 logcat、不启动/停止/重装游戏；日志在被 Git 忽略的 Library 中。脚本不打印原始日志，并对 URL、令牌、账号和长字符串做尽力脱敏。发送前仍须人工检查，不能把凭据发到聊天或提交 Git。
4. 同时记录：发生时间、手机型号/安卓版本、是 TapTap 内测试还是独立 APK、所用游戏版本。USB 授权失败/未连接时脚本直接报错，不会伪造空日志为成功。

如果出现初始化 `1001`，按官方说明核对**同一后台应用**的 Client ID、Client Token、包名及证书。Token 仅由负责人在本地安全比对，不提供到聊天。若日志是登录 `signature not match`，再针对最终安装包证书配置排查；不要把两种签名问题混为一谈。截图本身不足以区分这些路径。

官方参考：[Unity 初始化错误码](https://developer.taptap.cn/docs/sdk/integration-guides/unity/)、[Android 初始化与包名/签名配置](https://developer.taptap.cn/docs/sdk/integration-guides/android/)、[登录签名常见问题](https://developer.taptap.cn/docs/sdk/taptap-login/faq/)。
