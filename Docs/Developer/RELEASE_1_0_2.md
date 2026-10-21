# 1.0.2正式测试交付

## 来源与保留

- 基于main `e1419f2`，接收TA `83611de`（含`4582ebb`）的最新Gaming美术、生成网格与10个天气区域。
- 保留main关卡及三份3010条规划、10487格框架碰撞、玩家出生位置和10格摔落、1542→107组白块合并、MRoom2/MRoom3无遮蔽、30%+6钥匙Boss入口及60%结局、18秒曲间间隔等修复。
- 场景按YAML对象合并，旧关卡/内嵌规划/光照配置不回退；不接收TA的动态字体缓存或个人IDE设置。
- GPU加载核对通过：框架10487格不变，前景墙面9877格；美术差异候选为32格有墙面无框架、642格有框架无墙面，未自动补墙。旧核对报告数字对应上一版，不代表这版新美术。

## Android兼容修复

TapPlay日志确认vivo S19 / Android16崩溃于Unity图形线程的Adreno Vulkan绘制路径，不是协议缺包。Android显式仅OpenGLES3，构建入口配置及prebuild守卫防止恢复Vulkan或自动选择；3项Editor配置测试通过。详细证据见[启动排查](ANDROID_TAPPLAY_STARTUP_20261021.md)。同型号TapPlay复测尚未完成，不宣称平台测试已经通过。

## 发布配置与进度

- 双端版本1.0.2，Android versionCode10002，包名仍为`com.miao1suki.shapeofcolor`，非Development。
- 图标使用`Resources/正式ICON.png`，SHA256与用户桌面正式ICON一致；构建入口主动设置默认/PC/Android图标。
- 本地游戏与成就两类存档（含各自.bak）均已检查清理；游戏存档原本为空，成就文件已移到`Library/Release102ProgressBackup-20261021-103320`。PC启动检查后再清理新生成的成就文件到`Library/Release102ProgressBackup-20261021-103657`。不清除设置或TapTap服务端成就，不清手机数据。
- 产物目录：`Builds/形之彩-1.0.2-20261021-正式测试`；PC上传包`形之彩.7z`直接包含玩家文件，无额外外层目录和DoNotShip目录。
- 双端构建成功、均0错误；PC 7z完整性通过，D3D11启动日志无异常。APK版本1.0.2/10002、ARM64、debuggable=false、隐私Activity为启动入口且UnityActivity不导出；网络权限仅INTERNET/ACCESS_NETWORK_STATE，离线政策存在。实际EXE与APK提取图标均为正式ICON。
- 安卓签名MD5：`2d9e5b4a9d63eb90d63ea84789c0c333`。
- APK SHA256：`F6EEAD0954334CAC8A4E789D891AA97D91A677117207B05A65931AD605852BB0`。
- PC 7z SHA256：`6E433DA318BC9CD23A2A8620F9B33AE01D13B2CB09DD824409A03F1AA7F8E011`。
