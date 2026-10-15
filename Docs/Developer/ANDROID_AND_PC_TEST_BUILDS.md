# 安卓与 PC 测试包

## 场景和名称

唯一启动场景是 `Assets/_Project/Scenes/Start/MainMenu.unity`，开始游戏进入 `Assets/_Project/Scenes/Playing/Gaming.unity`。测试地图不加入正式流程。应用名称为 **形之彩**，安卓文件名为 `形之彩.apk`，Windows 主程序为 `形之彩.exe`。

本轮产物位于 `Builds/形之彩-20261014/`。安卓为 ARM64、IL2CPP 开发测试包，使用本机调试签名，不是商店发布包。手机要求 Android 7.1/API25 及以上；实际兼容性和性能仍须在设备上验收。Windows 需连同 `_Data`、UnityPlayer.dll 等整个目录分发，不能单独复制 exe。

## 触摸控制

安卓显示左下四方向、右下跳跃/冲刺/交互/左右放下/选色，上方暂停和重生。交互复用电脑 F 的搬运和机关语义；左右放下复用 Q/E，不另造搬运入口。冲刺复用现有 Sprint 能力，不新增冲刺机制。移动和动作可多指同时操作，方向合成后归一化，相反方向互相抵消。

电脑继续使用现有键位。两个平台继续支持手柄。暂停轮盘支持直接触摸选项和竖向拖动，返回/设置/制作名单沿用已有页面。暂停期间隐藏玩法触摸按钮；禁用、松开、滑出、切后台和失焦时释放按键。

`SharedGameUI.prefab` 的 `Mobile Gameplay Canvas` 保存新 HUD，字体引用为 Cubic_11 SDF。`MobileGameplayControls.previewOnDesktop` 可在隔离测试中预览；正常电脑运行不显示手机按钮。旧双摇杆原型的手机组关闭，避免重复控制。

## 尺寸与安全区

游戏采用左右横屏自动旋转，不启用竖屏。Canvas 使用 Expand 的参考尺寸适配：主菜单与触摸 HUD 为 1280×720，暂停保持原 1920×1080。内容通过 `ScreenSafeArea` 按 `Screen.safeArea` 设置归一化锚点，更新时清除偏移；背景继续全屏覆盖且各菜单页共享同一横移背景。未改变暂停扇形素材的支点、间距、选中动画和 PC 键位。

检查了 1280×720、1920×1080、2400×1080、2560×1600、2048×1536 的触摸按钮尺寸与模拟安全区，设备刘海、手势条与实际点击手感需在手机上确认。

## 气泡柱

`BuoyancyColumnFeature` 以作者 Collider 的世界包围盒底部判断上浮范围，不再用合并长柱的中心。玩家在蓝色水体内的垂直浸没超过自身一半时启用浮力；离开后解除。两根实际合并柱的上下段均使用真实玩家 Rigidbody 验证上浮，而不是仅检查浮力接口值。

## 构建与验证

构建脚本：`Assets/Player/Editor/MobileBuildSetup.cs`。使用 Unity 6000.3.12f1，不升级项目。构建时设置 `TAPTAP_TEST_BUILD_ROOT` 为明确输出目录，以 `-executeMethod MobileBuildSetup.BuildWindows` / `BuildAndroid` 调用；安卓启动 Unity 时带 `-buildTarget Android`。脚本明确只收录上述两个场景，检查 BuildReport 结果并生成报告。`Prepare` 为本轮 UI 资产配置脚本，已经应用，不应为普通功能开发反复执行。

本轮使用 `Library/FlowRetirementProbe` 隔离快照，避免对已打开的主项目切换平台或调用批处理；不将探针脚本放入正式包。源码修正和 UI 资产已同步回当前项目。构建日志为 `Library/WindowsTestBuild.log`、`Library/AndroidTestBuild.log`。输入/UI 与原系统编辑测试：`Library/MobileEditMode.xml`；真实气泡上浮：`Library/BubbleMotion.log`；真实移动/冲刺/跳跃多指输入与暂停释放：`Library/TouchPlay.log`；主菜单三次往返：`Library/MobileMenuRoundTrip.log`。

## 输入实现注意事项

UI Toolkit/UGUI 指针回调可能晚于 `PlayerInputDriver`（执行顺序 -100）。`VirtualInputState` 将触摸按下/释放边沿发布到下一帧，避免玩家驱动漏掉跳跃/交互/放下的一次触发；边沿不跨两帧重复消费。持续输入独立维护。`UnityInputSource` 提供触摸主指针位置及 Primary 动作，玩家仍只读 GameInput；`GameInput.IsPointerOverUi()` 在动作时射线检查 UI，避免手机操作按钮误染场景物体。
