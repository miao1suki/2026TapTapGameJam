# 同场景开始菜单

正式构建只有 `Assets/_Project/Scenes/Playing/Gaming.unity`。启动即进入这个世界，自动安装共享纯 UI `Assets/_Project/Resources/UI/WorldMainMenu.prefab`，初始逻辑状态为 MainMenu。菜单、玩家、关卡、相机始终属于同一 Gaming，不加载一份 MainMenu 来承载 UI。

旧 `Assets/_Project/Scenes/Start/MainMenu.unity` 只保留为编辑器兼容入口：磁盘里已是新美术、没有旧蓝色底板或旧按钮布局；运行时重定向到 Gaming。它不加入正式构建。已有打开的旧内存场景需重新加载，不能以旧内存覆盖新文件。

## 进入与返回

- 启动：Gaming 的真实 Camera、AudioListener、EventSystem 从第一帧保持启用，近景构图配合左侧新菜单，不播放旧黑圈或加载黑幕。
- 开始：菜单图片淡出／向左离开，相机从近景平滑拉远到正常玩家机位，默认 1.35 秒；结束后恢复玩家物理和玩法输入。
- 返回：立即清理暂停，以当前世界位置反向近景运镜并淡入菜单，默认 1.15 秒。
- 再次开始：继续同一玩家、同一关卡与进度，不卸载、重载、截图交接，也不调用房间重置或 `ResetProgress`。玩法 R 和死亡重生仍按原职责处理。

`GameFlowController` 是流程唯一所有者；MainMenu/Gaming 两个 ID 代表展示状态，catalog 的两个 ScenePath 都指向 Gaming。构建列表及 `MobileBuildSetup` 都只收录 Gaming。没有复制世界或重复注册房间。

`WorldMenuPresentation` 只提供相机状态并持有 `CameraControlManager` 控制租约。进入过程通过 `Retarget` 缓动到标准机位，在最终 LateUpdate 后才释放；返回请求反向租约。严禁在过渡中调用 `LevelSceneContext.Activate`、重新绑定并 Snap 玩家机位或直接写 Camera。初始激活只做一次。

## 预览隔离

- `GameplayPresentationGate` 在菜单期间抑制收集、吸附、接触演出、玩法按键和自动／手动复活，时间与世界美术动画仍正常运行。
- 玩家独立控制锁、刚体 Kinematic、Collider 和输入驱动在预览时保存／冻结，结束按原状态恢复，不清除其他控制者的锁。
- 恢复玩家 Collider 后才发布 `GameplayRestored`，确保收集品保持实体穿过。
- 只隐藏世界的屏幕 HUD，不隐藏同场景菜单；不禁用世界 Camera、Listener、EventSystem 或 BlockFeature。
- 设置中的触控布局有独立编辑预览，按键可见，但禁止发送后台玩家操作；位置和大小沿现有持久化业务。

相机过渡异常恢复同一个世界的可点击菜单，清理 Tween 和本次租约；不通过卸载世界来恢复。`LastTransitionFailure` 记录原因，正常重试会清除。

## 美术与编辑态

35 张新源图位于 `Assets/_Project/Resources/26TAPTAP/UI/LOGO` 和 `ButtonIcon`，共享引用为 `Resources/UI/GameUiArtwork.asset`。四按钮按开始、制作名单、设置、退出竖排；01 为正常、02 为选中／按下。使用完整帧整数倍放大和离线计算的透明留白，不拉伸、逐帧读像素或覆盖第二层文字；27 帧 LOGO 默认 80ms／帧。

图片导入为 Single Sprite、Full Rect、16 PPU、Point、无 Mipmap／压缩、最大 512；Android 和 Standalone 均 RGBA32。源图、meta 和共享配置一起交付。旧暂停所用切片不删除。

`Content/UI/MainMenuUI.prefab` 同步新布局，旧蓝底、旧标题／状态占位已从资产清除，不再只在 Play 中替换。主菜单文字与设置业务继续复用统一 Cubic_11 SDF、设置及制作名单控制器，音乐从逻辑状态显式路由。

显式素材导入：`Project.Player.Editor.WorldMenuArtworkSetup.Build()`。
隔离迁移入口：`Project.Player.Editor.SingleSceneMenuSetup.Build()`，只允许 FlowRetirementProbe，生成纯 UI Prefab、更新作者 UI Prefab 和兼容 MainMenu；不打开／保存正式 Gaming、任何 planning 或玩家资源。

## 验证入口

只在隔离工程运行：

- `Project.Player.Editor.WorldMenuFusionVerification.Run()`：Gaming 真实入口、逐帧唯一世界相机／Listener、真实运镜中间值、两次入退、同世界与进度、零场景加载／截图交接、不同实际 GameView 宽高比、设置／名单、收集过滤及预览隔离。
- `Project.Player.Editor.WorldMenuFailureVerification.Run()`：触控布局真实按键与工具栏、相机临时引用失效、同世界可点击恢复和重试。
- `GameFlowMenuEntryTests`、`GameFlowRoundTripTests`：单景启动、设备、音频、暂停清理与重复进入。

其他玩法审计显式打开正式 Gaming 后，等待 `flow.IsInitialized && !flow.IsTransitioning`，再调用 `flow.EnterGameplayImmediatelyForDevelopment()`。该接口仅在 Editor 编译，正式包不存在；独立 MRoom 等测试地图不自动叠加主菜单。

2026-10-18 隔离实跑：`WorldMenuFusionVerification` 完成 165 项检查、843 个逐帧设备样本，两向运镜均检查到多个真实镜头中间值，同世界两次入退及零额外场景加载通过；新菜单和返回菜单截图已目视确认。日志位于 `Library/RoomPhysics-20261018/menu-fusion-1.log`，截图位于隔离工程 `Library/WorldMenuFusionVerification`。异常恢复及触控布局验证结果由整合交付记录补充；旧多场景结果不能替代这次单场景验收。

团队交付关卡仍须 Scene、同名 planning.json、meta 与新增依赖成套；本功能不重建或改变作者关卡布局。
