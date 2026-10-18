# 真实关卡融合开始菜单

## 入口与表现

唯一正式入口仍是 `Assets/_Project/Scenes/Start/MainMenu.unity`，唯一正式游玩内容仍是 `Assets/_Project/Scenes/Playing/Gaming.unity` 与配套策划存档。菜单加载真实 Gaming 作为动态背景，不复制一份布景、不修改关卡布局，也不播放录制视频。

`GameFlowController` 负责唯一流程；`WorldMenuPresentation` 只持有预览隔离与共享相机控制租约。进入时菜单图片淡出并向左离场，相机从菜单近景平滑退到标准玩家跟随；从暂停返回时在当前真实关卡中反向运镜并还原菜单。菜单路径不使用旧黑圈、圆形遮罩或黑色排布参考底图；Boss 慢复活等其他加载接口保留。

首次开始直接释放已预载世界，不重载场景。再次从主菜单开始沿用旧流程的重载关卡语义：捕获上一帧作为临时底图，先卸载旧 Gaming，再加载新 Gaming，完成相机交接后淡出底图并退镜。不会让两个带相同房间 ID 的 Gaming 同时注册。没有新增 `ResetProgress`，也不改变颜色钥匙第一次解锁场景颜色、第二次解锁玩家能力的阶段。

## 预览隔离与相机边界

- 菜单期间 `Time.timeScale=1`、音频不暂停，因此 LOGO、水、藤蔓等视觉动画可继续运行。
- `GameplayPresentationGate` 是嵌套、幂等释放的共享租约，预览时阻止拾取物磁吸／收集以及接触 Timeline；`GameUiRouter` 和手机版常驻按键不接受预览玩法操作。
- 标准玩家只取得本流程自己的 `AcquireControlLock` 租约，保存并临时冻结刚体、禁用玩家碰撞和输入／能力驱动，释放时不清除其他控制者的锁。不要关闭 `BlockFeatures` 来冻结画面，否则会触发美术 Detach。
- `RequestRespawn` 在展示期间同时拒绝自动／手动复活；死亡后返回菜单不会另开慢复活黑幕。收集品的实体穿过不依赖当前拾取资格，最后一个展示租约释放时在玩家Collider恢复之后发布`GameplayRestored`，一次性重施碰撞过滤，避免开始游戏后收集品变墙。
- 稳定状态仅真实 Gaming 的相机、AudioListener、EventSystem 启用。MainMenu 原有设备保持禁用，其 Canvas 复用真实世界 EventSystem；进入游戏后仅隐藏 Canvas，不销毁菜单。卸载／加载间隙由一个临时 AudioListener 承接，恢复真实世界 Listener 前同步关闭，不能留下无 Listener 的数十帧警告。
- 相机位置、镜头与插值全部通过 `CameraControlManager`，不增加第二个直接写 Camera Transform 的脚本。`LevelSceneContext` 绑定自己场景的相机和玩家，避免重载时抓到旧对象。

`WorldMenuPresentation` 可在流程对象中调整：进入运镜默认 1.35 秒、返回 1.15 秒、近景镜头比例 0.58、菜单构图玩家屏幕位置 (0.72, 0.28)。这些是 UI 展示参数，不是存档或关卡物体参数。

### 加载失败与触控布局

融合过渡使用同一个受保护的嵌套协程执行边界；空／不存在的 Gaming 路径、异步加载结果无效，以及加载后缺少 `CameraControlManager` 等异常统一返回可交互菜单。先保持隔离与临时 Listener，卸载失败关卡，再清除 Tween、持帧底图、相机租约和预览租约，恢复 MainMenu 相机／EventSystem／唯一 Listener、UI透明度和交互。`IsTransitioning` 恢复空闲，`LastTransitionFailure` 记录原因；修复配置后可再次开始，不会保留旧世界引用或卡在截图上。恢复分支不会因为错误 Catalog 把 Gaming 指到 MainMenu 而卸载唯一菜单。

主菜单的设置页仍可打开触控布局编辑器。`LayoutEditing` 的可视预览优先于玩法隔离：布局编辑器显示所有实际按键，普通手机 HUD 则继续被预览 Gate 隐藏。布局预览里的 `VirtualInputButton`／`VirtualDirectionButton` 已禁用，只编辑布局，不会控制后台玩家；放大等工具栏按钮依旧可用。

## 美术资源与像素

源图在 `Assets/_Project/Resources/26TAPTAP/UI`：`ButtonIcon` 的四功能 01／02 图片和 `LOGO` 的 27 帧。01 为正常，02 为悬停／选中／按下；图片已含文字，不再叠一层运行时文字。四按钮按开始、制作名单、设置、退出排列，图案左侧对齐，每张图按自己的完整帧尺寸放大 4 倍。四张源图本身同宽但设置／退出左侧有较多透明像素，因此导入入口离线计算 01／02 不透明区域并集的左侧留白，配置到 `worldMenuButtonLeftPadding`；运行时用独立子图平移留白，保留统一且较宽的按钮命中区，不在每帧读像素、不拉伸或裁掉选中边缘。LOGO 默认 80ms／帧，保留美术完整帧。

共享引用在 `Assets/_Project/Resources/UI/GameUiArtwork.asset`。单独更新这批菜单图片使用显式入口：

```csharp
Project.Player.Editor.WorldMenuArtworkSetup.Build();
```

该入口只调整 35 张源图导入设置并保存共享配置，不打开／重存 Scene 或玩家、关卡预制体。导入为 Single Sprite、Full Rect、16 PPU、Point、无 Mipmap、无压缩、最大 512；Android／Standalone 使用 RGBA32。所有源图、对应 `.meta` 与共享配置必须一并提交。旧 UI 切片仍供暂停等既有界面使用，不删除。

Canvas 延用安全区和等比适配，设置、制作名单复用现有业务。主菜单音乐由逻辑 `ActiveSceneChanged(MainMenu)` 显式切回 `MusicMenuOpening`，不能再依赖常驻菜单物体的 `OnEnable`。

## 专项验证

隔离工程专用入口（不要在团队主编辑器中运行）：

```csharp
Project.Player.Editor.WorldMenuFusionVerification.Run();
```

覆盖唯一 MainMenu 实际入口自动预载、首次开始不重载、独立控制锁不被抹除、预览不吸取／收集钥匙、世界动画继续、设置／名单、暂停返回清理时间与音频、第二次重载无重复房间注册、新相机跟随新玩家，以及每阶段唯一启用 Camera／Listener／EventSystem。截图输出隔离工程 `Library/WorldMenuFusionVerification`。宽高比检查通过实际 GameView 固定分辨率 1280×720、1600×720、1024×768，不把 `Screen.SetResolution` 请求值当成实际截图尺寸；临时尺寸结束时恢复。

PlayMode 的 `GameFlowMenuEntryTests`、`GameFlowRoundTripTests` 保留音频、暂停与回程回归。专项不保存作者 Scene／存档，不重置颜色会话，不替策划改关卡构图。团队关卡变动仍需同时提交场景与对应存档，不能只交 Scene。

独立错误／布局专项入口为 `Project.Player.Editor.WorldMenuFailureVerification.Run()`，同样只允许隔离工程：测试主菜单 Gate 下布局的实际按键可见且禁用玩法输入、放大工具可用；真实首轮之后，二次开始空路径（包含 GPU 持帧和旧景卸载）、不存在路径抛异常、加载场景缺相机管理器三种故障的清理；再修复配置并重试健康流程。故障测试会故意打印带恢复说明的错误，不修改项目 Catalog 资产或作者场景。

2026-10-18 隔离 GPU 实跑 `MenuFlow-5`：167 项检查通过，1076 次过渡帧采样均恰好一个启用的 AudioListener；首次、二次开始及返回、真实 GPU 截帧交接、恢复后的收集品过滤和死亡返回菜单等待均通过。16:9、20:9、4:3 的实际分辨率截图，以及设置／名单截图已人工复看。记录在主工程 `Library/MenuFusion-20261018/`，截图在隔离工程 `Library/WorldMenuFusionVerification`。完整EditMode 232/232、GameFlow PlayMode 4/4通过；终版错误／布局专项`MenuFailure-3`62项通过。这些是编辑器 GPU 回归，不等同于安卓实机手感或新包验收。
