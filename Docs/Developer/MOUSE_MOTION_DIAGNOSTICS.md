# 鼠标快速移动卡顿：定位与采样

绿色绳尖的视觉插值、整帧停顿是两个不同问题。协程与DOTween本身仍在主线程调度；不能只把代码搬进协程就宣称不会阻塞。

## 主机采样（手动启用，本地保存）

1. 正常进入要测试的场景并开始Play，不需要打开规划工具。
2. 菜单选择 `Tools → 诊断 → 录制鼠标卡顿（10秒）`，工具将聚焦Game窗口。
3. 前2秒保持鼠标静止，随后持续快速转圈到结束；不需要点击/挂钩。可分开测试未选色、红、蓝、绿。
4. Console完成提示对应 `Library/MouseMotionDiagnostics/时间戳.data / .csv / .txt`。提供时间戳即可由整合者继续分析，不会上传、录音或修改鼠标设置。

`.csv`覆盖采样期间渲染帧耗时、墙钟帧间隔、窗口焦点与InputSystem收到的鼠标状态事件数；事件已可能合并，不是硬件回报率。`.data`是Unity Profiler的当前历史缓冲，保留时长取决于编辑器设置，不保证包含完整10秒。采样期间临时启用Profile Editor，以展开原本被折叠的EditorLoop（默认Player采样不能区分这部分原生/扩展耗时）；不启用Deep Profile。结束/退出Play/脚本重载时移除监听，恢复原Profiler启用状态与Profile Editor选项，不永久改用户设置。开启Profiler本身有额外开销，先比较同一采样内的静止与移动片段，不用这次录制直接给性能提升下结论。

2026-10-18首份主机样本`20261018-133528`已确认：CSV 479帧、p95 107.63ms；Profiler 477帧，典型110ms慢帧中约101ms属于未展开的EditorLoop，绿绳绘制的慢帧均值0.057ms、最大0.113ms，InputUpdate慢帧均值0.257ms。此份采样未开启Profile Editor，不能仅凭EditorLoop就宣称根因是某个插件或已知引擎问题；需使用更新后的入口展开这部分。详细数据仅在本机Library，不提交原始Profiler文件。

更新入口已在静音隔离项目的DX11实际渲染循环完成42项校验，含EditorLoop确有子样本、四种原Profiler状态恢复、计时停止/退出Play/重复操作与新增CSV列。`-nographics`下的batch没有有效Profiler帧，不能用它验证采样完整性。报告：`Library/FlowRetirementProbe/Library/MouseDiagnosticsAudit.txt`；实际主机的补录结果见下一节，不以工具自测冒充根因已修复。

## 补录结论与下一步

2026-10-18补录`20261018-135055`已经展开EditorLoop，CSV 1094帧（中位5.38ms、p95 17.97ms、最大111.47ms）；Profiler 1092帧，其中36帧超过50ms。用户确认本地鼠标，CSV中Game与Unity焦点始终为真。静止片段约5ms，快速移动时重复出现约110ms慢帧。两次录制操作节奏不同，不能把整段p95下降当作修复成果。

- 最重消息帧：`Application.Message` 100.781ms，其中自身74.524ms、338条`Application.Message.WM_00245`共23.085ms。此标记对应Windows `WM_POINTERUPDATE (0x0245)`；余量在原生消息泵自身，不是每条消息都调用一次绿线或物理逻辑。不要将子样本23ms误写成100ms。
- 36慢帧平均：窗口消息83.627ms，InputUpdate 0.392ms，GreenHookVisualDriver 0.060ms，EditorApplication.update全部已采样回调合计0.144ms。已确认主要阻塞位于Windows原生窗口消息阶段；不能靠给绳线增加协程/DOTween、改变FixedUpdate或减少C#事件来消除这段耗时。慢帧后的多次物理追帧是后果，不是本次100ms阻塞的起点。
- 原始文件及离线`-analysis.txt`、`-native.csv`仅在`Library/MouseMotionDiagnostics`，不提交Profiler文件。读入与统计由静音隔离Unity完成，未操作作者正在运行的场景。

官方[UUM-142550（Editor）](https://issuetracker.unity.com/issues/19661/play-mode-framerate-drops-significantly-when-moving-the-mouse-cursor-with-high-polling-rate)与[UUM-147395（Player）](https://issuetracker.unity.com/issues/23505/windows-player-framerate-drops-significantly-when-moving-the-mouse-cursor-with-high-polling-rate)与本机症状高度相近，查询时6.3仍Open；Editor条目说明7.0a2也只是缓解。此处已定位原生消息处理瓶颈，但未以硬件Hz对照证明本机与官方缺陷完全相同，不宣称项目已根治，也不擅自升级引擎。

只读设备树显示`ML903 4K DOCK`及`MpandaKeyboard`，不能据设备名推断当时设置的回报率。雷神[ML903官方驱动页](https://www.thunderobot.com/driver/download/?id=209)列有ML903与ML903 4K两种驱动，应先确认用户实物型号、选择对应版本。经用户自行操作，在驱动中临时把回报率降至1000Hz，再视情况对照500Hz，保持DPI不变；可恢复原值。无需继续录同条件Profiler。不得自动下载/安装驱动、刷固件、修改USB/注册表或全局鼠标设置。

当前版本也有[ForwardRawInput](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Windows.Input.ForwardRawInput.html)官方批量转发接口，但示例会接管进程的RawInput窗口注册，不是普通设置开关。本次最重消息为WM_POINTERUPDATE，不能直接证明RawInput接管可以解决；没有进行原生接管试验。`CursorLockMode.Locked`没有官方减少消息数量的承诺，且会改变绝对瞄准/点击行为，不作为默认修复。未来若专项尝试，必须先在隔离环境验证所有输入、清理与恢复路径，再由用户明确选择，不用未测的兼容方案替代当前输入。

## 已验证与未验证边界

### 2026-10-18 Windows 独立包对照

用户后续授权降低回报率，并提供本机ML903驱动2.0.3.5（有效签名）。仅通过驱动正常GUI操作，当前识别为 **ML903 有线**，回报率500赫兹，经返回主页/重新进入设备页读回确认；DPI、消抖、Motion Sync和固件均未改。原回报率未读出，不能宣称原先是4000Hz；无线4K配置尚未改动。此为明确用户授权的本次硬件对照，不是允许后续任务自动下载/安装或修改设备。

独立测试包：`Builds/形之彩-20261018-MRoom3鼠标测试/形之彩-MRoom3-Windows.zip`，79.55MiB；解压后运行形之彩.exe，或直接运行同级Windows目录中的程序。第一场景为当前保存的MRoom3，附带MainMenu/Gaming供返回菜单后的原流程；未修改主项目正式构建列表和作者Scene/planning，也未额外写入颜色解锁。Unity6000.3.12f1、Windows64/Mono、D3D11、默认1280×720可调窗口、BuildOptions.None；离线构建守卫通过，0错误/2个既有Shader警告。

实际Player静音batch启动与日志检查通过，未见运行异常；没有物理鼠标快速旋转的包内帧时间数据，因此仍需用户实测，不宣称独立包已根治Windows消息泵卡顿。本轮无合并/推送，不升级引擎、不加入原生输入接管。日志为Library/MRoom3-WindowsBuild-20261018.log、MRoom3-WindowsPlayerSmoke.log。

- 绿绳每渲染帧在最终相机之后绘制，待机尖端直接用当前指针，只有内部绳身小幅缓动。透视深度偏移保持同一屏幕投影。无需每次转向创建协程/Tween。
- 关卡规划窗口和旧放置窗口只负责创作。即使隐藏在最大化Game后面，其EditorApplication.update也可能继续运行；Play期间现在暂停栏目、AssetPreview、作者画布和编辑平面刷新，返回Edit恢复。性能标记为`PlanningEditor.Tick`、`LevelEditor.Tick`。
- 静音隔离`GreenHookSceneProfile.RunNative`使用实际MRoom3、4K/DX12、每渲染帧64条虚拟Mouse事件，覆盖InputSystem事件合并、真实输入驱动与UI动作。它不移动桌面鼠标，也不经过Windows的硬件消息泵/真实Editor鼠标重绘，因此不能证明主机实录问题已解决。
- Unity有相似的Windows鼠标移动/高回报率卡顿记录：[UUM-1484](https://issuetracker.unity3d.com/issues/high-polling-rate-mice-are-causing-performance-issues)、[UUM-147395](https://issuetracker.unity.com/issues/23505/windows-player-framerate-drops-significantly-when-moving-the-mouse-cursor-with-high-polling-rate)。这是排查线索，不是本项目根因结论；不擅自升级引擎、关闭输入事件、修改设备驱动或安装原生钩子。

不要用`InputSystem.pollingFrequency`冒充鼠标硬件回报率设置，也不要为减少事件而丢弃点击/键盘输入。不要用删除美术效果、自动断绳或放宽实体碰撞来掩盖卡顿。

关卡交付仍必须成套携带Scene、可编辑planning.json、meta及新增依赖；这些运行时/诊断修复不需要重建策划场景。
