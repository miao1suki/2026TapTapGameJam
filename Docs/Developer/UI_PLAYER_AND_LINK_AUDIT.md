# UI、玩家与交互连线接线说明

核对日期：2026-10-13。正式流程为 `Start/MainMenu → Playing/Gaming`，不恢复 Bootstrap 或旧常驻场景。

## 游戏 UI 放在哪里

| 内容 | 资源/代码 | 挂载方式 |
|---|---|---|
| 开始菜单、菜单内设置/存档 | `Content/UI/MainMenuUI.prefab`；`Code/Systems/StartMenu/Runtime` | 放在 MainMenu 场景根部。菜单根的 `StartMenuController` 管理页面，`StartMenuFlowBridge` 连接流程；`GameFlowController` 放独立 `__GameFlow` 根，不挂玩家或菜单子页面。正式 MainMenu 已有完整界面，不再重复放 prefab。 |
| ESC 暂停、设置、收藏、存档、游戏 HUD | `Content/UI/SharedGameUI.prefab`；`Code/Systems/GameFlow/Runtime` | 整个 prefab 放在玩法场景根部，不挂玩家或相机。保留它的 `GameSystemSceneRoot(kind=UI)`、`GameUiRouter` 和内部引用；只挂 `EscMenuController` 不等于完成暂停接线。Gaming 已放好。 |
| 搬运/染色等短文字提示 | `Code/Systems/WordNote/Runtime/WordNoteUI.cs` | 挂独立 Canvas；SharedGameUI 已包含 `WordNote_UI`，不要再放第二份。该组件不会自行创建全局实例；玩家只消费它的服务。 |
| 染色轮盘、选色范围圈 | `Assets/Player/PlayerColorWheel.cs`；`Development/PickWheel/PickWheelPrototype.cs` | 两者挂同一个玩家根，前者管理选色/接色，后者是 PSD 外观。后者的图片、像素字体和 playerWheel 引用可配置；轮盘 Canvas 运行时生成，不要手放第二套旧轮盘。输入由 PlayerInputDriver 分发。 |
| 屏幕字幕 | `Code/Systems/Subtitles/Runtime/SubtitleManager.cs` | 全局管理器，可独立挂场景系统根；访问 Instance 时可自动创建并常驻，自建 Canvas。SubtitleTrigger 挂触发区域；DisplayBlockFeature 是世界空间文字，挂展示物，不挂玩家。 |
| 手机虚拟摇杆/按钮、双端布局 | `Code/Systems/InputAbstraction/Runtime` | 独立 PlatformInputUI Canvas。菜单 `GameObject/2026TapTap/Input/创建双平台输入 UI` 生成，PlatformUILayoutController 管理 Desktop/Mobile 预设，VirtualJoystick/按钮挂对应控件；不挂玩家实体。当前正式场景未完成这套移动端 UI 接线。 |
| 加载进度 | `GameLoadingScreenAdapter` | 挂独立加载 UI，配置 canvasRoot、进度条和文字；通过 GameSceneLoader 注册。预留接入接口不代表现有正式场景已经放置了完整加载页面。 |

上述相对资源路径均以 `Assets/_Project/` 为根，明确标注 `Assets/Player` 的例外除外。设置、存档、收藏是菜单子页面，不应逐个复制到玩家上。运行时 UI 使用 UGUI；策划编辑器使用 UI Toolkit，二者不是同一套挂载。

### 场景核对

- MainMenu：1 Camera、1 AudioListener、1 EventSystem；菜单及流程根，不需要玩家轮盘。
- Gaming：1 Camera、1 AudioListener、1 EventSystem、1 GameUiRouter、1 WordNoteUI、1 玩家连线；共享暂停界面已存在。
- Test：测试玩家、轮盘、连线、独立 WordNoteUI；没有共享暂停界面和预置 EventSystem。
- Pick：使用 RuntimePlayer；本地保存布局单独保留，没有 GameUiRouter、WordNoteUI 或预置 EventSystem，不向它自动新增 UI。
- 在 Test/Pick 接暂停时，放 SharedGameUI 根；检查原独立 WordNote_UI 和 EventSystem，保证各只有一份。轮盘旧内置视图有 EventSystem 兜底，但不能替代完整暂停系统配置。

## 两个玩家的差异

两份资源都位于 `Assets/_Project/Content/Player`，都使用 `Assets/Player/PlayerController.cs` 与 `PlayerInputDriver.cs`，不是两套输入或状态机。

| 项目 | RuntimePlayer | Player(暂定) |
|---|---|---|
| 当前使用 | Pick；旧导航的玩家创建入口 | Gaming；新版 Test 实例 |
| 外形 | 根节点一格 Mesh | Visual 子物体，保存的外形仍是高胶囊 |
| 保存的 CapsuleCollider | 半径0.5、高1、中心0 | 半径0.38、高1.8、中心Y=0.9 |
| 颜色与生命组件 | prefab 已显式挂 PlayerColorWheel、PlayerHealth、PlayerFallDamage | prefab 未显式挂前两者，PlayerController.Awake 会补齐；未挂 PlayerFallDamage |
| 拾取演出/旧 Timeline | Director 无默认资产 | Director 保存了测试 Timeline，playOnAwake 关闭，另有 TimelineActorHost |
| TA 连线 | prefab 没有；Pick 实例额外挂了 | prefab 已有 InteractionLinkEffect、材质与颜色目录 |
| 轮盘外观 | PickWheelPrototype | 同一 PickWheelPrototype |

重要：新共享控制器 Awake 会把两者实体半径/高度统一成一格，也会统一 Rigidbody 的约束、插值和连续碰撞，因此不能只看 prefab 内保存的 Rigidbody 数值判断运行差异。但该代码没有统一 CapsuleCollider.center，也不会同步缩放 Visual：Player(暂定) 的中心偏移和旧外形仍可能导致与 RuntimePlayer 的站立表现不同。此次未擅自改正式 Gaming 玩家资产；Test 冲突处理保留了本地一格尺寸/零中心，使用 prefab override，不丢弃远程抽出 prefab 的结构。

后续建议只保留一套标准玩家配置作为正式来源，把 TA 连线和 Timeline 适配作为它的组件；不要在同一玩法场景同时放这两份玩家。场景相机绑定唯一玩家，Tag 使用 Player。

## 白色方块为什么被连线

`Assets/ta_source/LinkEffect/InteractionLinkEffect.cs` 按 `PlayerInteractionSensor.ScanRadius` 扫描 Collider，并查父级 `IColorApplicationTarget`，不是根据材质、HSV 颜色或 Layer 名字来认定目标。

`CanColorReact` 明确允许 `UniversalColorBlock`。它是白色万能代理，接收已解锁的所选颜色并向四邻广播，因此白色不等于不可交互。Gaming/Test 各有5个该 prefab；核对到的实例包括 `Prop [63,28]`、`Prop [74,28]`、`Prop [79,28]`、`Prop [90,28]`，资源来源为 `Content/ColorBlocks/Prefabs/Universal_Block.prefab`。

普通墙块没有此接口，不会只因为白色而连线。仅有 F 交互/搬运接口的物体也不会被当成染色连线目标。已锁定的功能物经 CanApplyColor 的激活检查，不会单凭白色外观被纳入。

目前万能块 CanApplyColor 只要求所选颜色已解锁，不要求四邻必有可反应物。因此连线表示它能接色，不保证广播后一定有机关或植物发生反应。万能块连线保留，不作为异常处理。

## 连到空气的排查

代码目前取 Collider 最近点而不是可见模型锚点，没有前景遮挡检查；隐藏外观或较大的 Trigger 也可能成为合法目标。线尾会被裁切到玩家交互球内，可能离开原始碰撞表面；目标消失后的溶解仍使用旧世界坐标，可能短暂留下残线。这些是已确认的代码行为，但不能仅凭截图断定哪一项导致 Pick 通道右侧和第二房间异常。

本次通过临时连线日志确认真实目标后完成修复；诊断代码已在用户确认测试正常后移除，不保留持续输出与诊断菜单。

2026-10-13 Pick 复现日志确认：房间1的四格宽水体视觉范围 X=183～187，但其 Water Visual 的旧22单位宽 Trigger 随父级缩放为88，覆盖 X=183～271，通道/第二房间也被认作靠近水体，生成玩家附近竖直空线。公共修复已让方块模式同步3D/2D水面碰撞尺寸、关闭残留鼠标预览 Collider；连线排除 InteractiveWater 的内部表现碰撞体，保留玩法水体根 Collider。该修复覆盖使用同组件的 Pick/Test/Gaming 及后续场景，不需要重摆合并物。红色移动平台与独立曲柄平台没有同类巨型子 Trigger，且不在连线接色名单中。藤蔓节段隐藏识别模型属于共享母根粒子显示策略，不按 Renderer.enabled 一刀切排除。

## 验证边界

依据合并后的源代码、prefab、真实 Unity 场景导入和隔离检查；检查不向用户当前窗口保存 Scene。完整画面、输入手感、音频试听和打包仍需人工验收。旧文档中“万能方块已退役”与当前资源/代码不符，以本说明及现行颜色规范为准。
