# 运行与场景

正式入口及唯一构建场景是 `Assets/_Project/Scenes/Playing/Gaming.unity`。MainMenu 只是该世界的 UI 展示状态，不是加载的第二份场景。旧 `Scenes/Start/MainMenu.unity` 保留新美术编辑器预览并在运行时重定向，不进入构建。不再使用 Bootstrap、旧 Systems 场景、旧 Level_01～03 或 Ending。

场景采用同世界菜单：Gaming 首帧前自动安装纯 UI `Resources/UI/WorldMainMenu.prefab`，逻辑流程初始为 MainMenu。只使用始终启用的 Gaming Camera、AudioListener 和 EventSystem，不预载第二场景、不复制关卡或重复创建菜单。水体表现注销只停用并延迟销毁，不能在父节点禁用期间执行 SetParent。

自动流程根持有 `GameFlowController`，开始按钮通过 `StartMenuFlowBridge` 请求 Gaming 逻辑态。Gaming 的 Main Camera 挂 `CameraControlManager`、`CameraModeController`、`CameraFollowController` 和 `LevelSceneContext`；`LevelSceneContext` 仅首次按 `Player` Tag 绑定玩家，不在菜单过渡中重复 Snap。暂停与共享界面来自 `Content/UI/SharedGameUI.prefab`。

菜单过渡由流程控制器的 `WorldMenuPresentation` 协调：

- 进入 App：真实 Gaming 近景作为背景，左侧显示新动画 LOGO 与开始／制作名单／设置／退出美术按钮，不使用旧黑圈或滚动背景。
- 开始：菜单淡出，相机平滑缩放到正常随行，镜头租约保留至最终帧后才释放，结束后释放预览输入锁。
- 返回 MainMenu：先立即解除暂停，在当前世界反向运镜并淡入菜单；不切换成假背景。
- 再次开始：继续同一世界与玩家，保留房间与颜色进度；不重载、不截图，不触发关卡重置。
- 菜单预览采用独立控制锁租约，冻结玩家物理并抑制收集、玩法 UI 和输入，但不关闭世界美术、不清除其他系统的控制锁、不把全局时间设为零。
- 退出按钮仍通过 StartMenuFlowBridge / GameSceneLoader 请求，不直接退出、不播放旧菜单黑圈。

其他显式加载和开发者加载式复活仍由 `GameSceneLoader` / `GameTransitionOverlay` 管理。复活保留原地关卡与房间重置，不重新加载场景；默认仍为常规方式，见 [复活表现](PLAYER_RESPAWN.md)。

菜单展示时保证鼠标可见，PC 独立包有焦点时经 `DesktopCursorPolicy` 限制在当前游戏窗口；失焦释放，Editor／安卓不锁定。绝对指针与菜单点击保留。

流程根在初始化时安装并初始化`GameAudioService`，随流程根跨场景保留。进入Gaming切换到Atmosphere E Minor循环音乐，返回菜单沿用菜单音乐。不要依靠已退役的Audio系统场景启动音乐切换。

返回菜单、重载及场景切换会立即释放暂停，恢复`Time.timeScale = 1`与`AudioListener.pause = false`；普通继续游戏仍保留暂停收尾动画。卸载UI时同样清理，关闭动画被打断不会让下一次进入游戏冻结。

菜单 `Tools/2026TapTap/场景/场景导航与关卡入口` 显示实际存在的主菜单、项目场景、开发测试场景，不再列出旧系统与固定三关。新增场景后可点击“刷新列表”。

开始界面为纯 UI `Resources/UI/WorldMainMenu.prefab`（作者入口 `Content/UI/MainMenuUI.prefab` 同步新美术）；`SharedGameUI.prefab` 保存暂停/收藏品/设置/存档/HUD/提示。SharedGameUI 自带服务初始化及 EventSystem；场景必须仅有一个 EventSystem，菜单不得增加自己的设备。独立地图保持直接游玩，正式 Gaming 的隔离玩法审计可在流程初始化后显式调用仅 Editor 的 `EnterGameplayImmediatelyForDevelopment()`。

WordNote_UI 已保留在 SharedGameUI.prefab 内，文字提示通过 WordNoteUI API 调用。输入、音频、成就、相机等代码保留，但删除系统场景后不保证它们自动存在，各自按实际关卡需求放置。

旧“初始化基础流程 / 创建或修复标准场景骨架 / 重建共享 UI 场景”不再可用，不会重新生成已退役内容。删除前场景另备份于本机 `Library/RetiredStandardScenesBackup`，也可以从 Git 历史恢复。

2D 关卡编辑器雏形位于 `Assets/_Project/Development/LevelEditor/Orpheus0829`，通过
`Tools/2026TapTap/关卡编辑器/打开编辑器窗口` 打开。窗口可停靠、拖动和缩放；编辑模式开启后，
Scene 视图锁定为 XY 正交平面，点击醒目的编辑模式按钮退出后，会主动切回透视 3D Scene 视图。方块栏目可用 `−` / `+`
在 70%–150% 间调整卡片与预览大小。`玩家参考` 可以引用当前场景中的物体；切换“玩家”工具后会选中该物体，
使用 Scene 的 Move 工具即可自由拖动，默认会把玩家锁定到关卡所在的 `Z = 0` 平面。编辑器放置的白模方块
会按当前格子尺寸铺满。默认栏目为红、蓝、绿、黑、白五种纯色方块；纯色会序列化保存并在运行时重新应用，
栏目末尾的 `+ 新增` 卡片用于新增方块；选中某个方块栏目后，卡片图片右上方会临时出现“编辑 / 删除”按钮组，
按钮大小跟随栏目缩放；编辑在独立临时窗口中修改，删除直接移除该栏目。栏目的“自己编辑”模式可以设置可选基础预制体、
颜色、贴画和组件模板；“直接使用预制体”模式只保存预制体引用，放置时原样实例化，不套用颜色、贴画或栏目组件模板。
`自己编辑` 的“贴画”会打开独立的“栏目贴画编辑器”临时窗口；该窗口复制原有 SurfaceTiles 表面贴画流程。
关闭后结果保存回该栏目，之后放置的同栏目方块会自动带回贴画。打开时会隐藏原 Scene HUD；
开始绘制时会自动选择第一张瓦片并把 Scene 视角聚焦到临时预览方块。
绘制区可选择自动或固定某一面，固定后只在该面落笔并自动转到对应视角。
绘制完成后点“保存并关闭”，结果会写回栏目；直接关闭窗口也会执行同样的保存。
`组件与调试` 会打开组件模板窗口：可添加或删除组件，并用 Inspector 风格的字段调试；Transform、渲染器、
碰撞体、MeshFilter 和 SurfaceTileBlock 等默认组件不能删除。保存后生成独立模板预制体，之后新放置或
“替换为当前”时使用；已有场景实例不会被静默重建。
融合编辑器不再提供手动父物体选取。进入详情搭建时，房间或通道会按自己的名称自动收纳在
`__PlanningMapGenerated` 下，新放置的方块会自动成为当前房间或通道的子物体。
进入编辑模式时，编辑器会先对“玩家参考”对象执行一次 SceneView 聚焦，再切换并锁定为 XY 正交视图；未设置玩家时不改变当前视角。
默认瓦片源图位于 `Assets/_Project/Development/LevelEditor/Orpheus0829/TileLibrary`。缺失或需要重建时使用
`Tools/2026TapTap/关卡编辑器/重建默认瓦片库`，生成图集、预览材质和 `26TAPTAP_Palette.asset`。

新关卡自行创建场景并保存；不使用已退役的标准流程初始化工具覆盖关卡。

Gaming 的正常 Side2D 游玩镜头使用正交 size 8（垂直可见范围 16 格）；场景 Main Camera 与 CameraModeController.side2D.orthographicSize 保持一致，确保进入玩法后仍使用该视野。
