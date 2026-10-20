# 手机触控布局与 TapTap 胶囊

- 触控区域继续使用 Screen.safeArea，额外保留右上角 248×156 UI 单位给 TapTap 胶囊；控制按钮的外扩触控范围也不得进入。旧自定义布局保留，仅在越界时做最小位移。
- 重生常驻于玩法触控 HUD，默认在左上暂停右侧，复用原有重生按钮与输入动作，不新增另一份重生逻辑。旧右上重生坐标自动迁移到新默认位置，其余自定义位置与尺寸不清空。
- 设置/存档/收集页面和世界地图关闭按钮通过 MobileOverlaySafeArea 避让胶囊，手机最小尺寸 124×124；PC 布局不变。安全区跟随窗口/横竖屏变更，不改场景或预制体。
- 手机初始发送奔跑输入，原有行走图标现在是切换按钮：点击进入行走，再次点击恢复奔跑。不使用独立奔跑图标。暂停/菜单会释放虚拟输入，返回后恢复所选状态；失焦同样释放，重新聚焦恢复。玩家与 Boss 原有疾跑禁用规则仍由 PlayerController 决定。
- 世界地图原已有独立入口，现手机放大到 148×100，并保留 Cubic_11 SDF 像素文字“地图”。未解锁不出现；收集门槛沿用最新 main 的5%，进入/击败 Boss、Boss 战和 Boss 房限制仍沿用 WorldTravelMap 原有规则，不另建入口也不绕过门槛。

## 验证范围

新增 EditMode 回归覆盖各屏幕比例下默认布局不遮挡胶囊、左上重生位置、旧布局迁移、自定义冲突位置夹取、默认奔跑与两次切换、失焦输入释放与恢复。未修改地图、正式场景、存档进度、曲柄或交互候选。

本说明为子任务交接；实际 Unity 编译/测试执行结果由整合任务记录，不将未运行的测试声称通过。

整合验证：7个相关 Runtime/Editor/EditorTests 程序集编译零错误；静音隔离 Unity 执行 `Project.Player.Editor.MobileCrankContextVerification.Run`，18条手机曲柄断言全部通过。手机无按键每0.1秒刷新候选，实际按下仍即时刷新，不改变制动、锁色、距离或朝向规则。不操作主 Unity，未做安卓真机测试或重新打包。

最新main整合后，`Project.GameFlow.Tests.MobileControlLayoutTests` 与 `Project.InputAbstraction.Tests.InputAbstractionTests` 共32项EditMode测试通过，0失败、0跳过。
