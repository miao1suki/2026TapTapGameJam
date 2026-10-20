# SDK 与策划新版地图整合

## 内容归属

- main 基线 `0a89c2b`：TapTap SDK 4.10.9、Boss Timeline 覆盖、现有玩法/存档/编辑器修复全部保留。
- 策划 `origin/codex/codex/gaming-camera-size-8` 的 `ab757fc`：采用其正式 Gaming 的 23 个房间、3441 条规划记录及原生场景对象，不从旧合图重建。原生物体的世界位置、旋转、缩放、占格和稳定 ID 逐项保持。
- main 补充 Boss 原作者区域、竖井连接、出生处 Start；不带回旧 R1/R2/R3/MRoom2/MRoom3 的摆放。最终 25 个区域、4194 条规划记录，三份正式规划一致。
- TA `f7cbb62` 的 GridArt、天气和既有修复保留，不用策划旧美术覆盖；TA `b374c99` 的 Boss RGB 死亡溶解合入，保留 main 最新死亡演出和美术准备链。
- 保留 `enter_boss`、camera_down、camera_close、Boss 出场/处决的触发参数；新增可放置的 `FountainEndingTrigger.prefab`，接通同场景 End 后喷泉结局。

启动仍只有 Gaming，菜单及运镜使用 main。版本 0.2.1，Android 包名固定 `com.miao1suki.shapeofcolor`。同步后从磁盘重新打开场景，不用旧内存或旧 planning 覆盖。

## SDK 配置与构建

用户提供的本地 Client Token 已放入忽略的 Resources/TapTap 配置。验证 Token 非空且不含 Server Secret；配置与生成的 meta 均不得提交或分享。Android 只在原生隐私同意后初始化，PC 不自动启用 SDK 网络桥。

构建入口仍为 `MobileBuildSetup.BuildWindows` / `BuildAndroid`，通过 `TAPTAP_TEST_BUILD_ROOT` 指定输出。Windows 成功后自动生成根目录 `形之彩.7z`，内部直接是 Player 文件，不套 Windows 文件夹，排除 `*DoNotShip*` / `*DontShip*`。需安装 7-Zip，或用 `TAPTAP_7ZIP` 指定可执行程序。上一次同名 archive 替换时留 `.previous`，不把旧压缩包内容增量混入新包。

本机未配置独立发布 keystore，沿用既有 Android 调试证书；这不是 Development 构建。TapTap 填写的签名须从最终 APK 的证书 MD5 提取，不能填 APK 文件 MD5；未来更新必须保留同一证书。正式发布前由负责人决定是否启用并备份专用发布密钥。

## 验证范围

- 隔离 Unity 6000.3.12f1 编译、地图原生对象不变检查、唯一玩家/Listener、Start 和 Boss/喷泉接线检查通过。
- TapTapAchievementIntegrationTests、AndroidPrivacyTests、ContestOfflineTests 共 20 项通过。
- 双端构建、APK Manifest/签名和 PC 压缩包验收结果见本次交付目录；不将静态检查或启动冒烟表述为全关通关、安卓真机登录或成就上报已验收。

## 快速推送

团队规则见 `.agents/skills/taptap-fast-push/SKILL.md`。实际 Git 推送与合图、验证、构建分开计时。当前 hook 只执行规划配对守卫，没有要求每次推送启动 Unity 或构建全平台。半小时不是常规小改动 Git push 的必需耗时；没有队员端日志，不能断言是代理检测或网络造成。超时应报告正在等待的具体步骤，不扩大无关全项目检查。
