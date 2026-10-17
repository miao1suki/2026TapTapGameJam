# 系统索引

Timeline触发器编辑器标记使用 `UI/棍母.png`，道具栏/关卡画布/非Play的SceneView可见，画布独立顶层与半透明放置预览；游戏中图片、占位模型和阴影不可见。资源与meta必须一起交付，见 [CONTACT_TIMELINE_API](Integration/CONTACT_TIMELINE_API.md)。普通藤蔓生长可分步托起顶部重力方块/堆叠并承载玩家，路径受阻停止，搬运/绿绳/燃烧/房间重置交还物理控制，见 [藤蔓托举](../Developer/VINE_GROWTH_LIFT.md)。

新增独立绿色进阶关：`Assets/Scenes/MRoom3.unity`及同名planning/mvmap。仅新区域，4平台/3无重力基座、充能前进断电回位，6门/13保持按钮，尖刺朝通道。新图不用覆盖旧MRoom2或正式Gaming；交付必须带可编辑存档/meta，见 [MROOM3_IMPORT](../Developer/MROOM3_IMPORT.md)。

三色连线：红蓝不再按1/3条截断，预热线条池、复用淡出状态、批量提交顶点；绿色待机绳尖跟鼠标实际位置且不超染色范围，挂接后仍跟目标。PC独立包有焦点时使用DesktopCursorPolicy限制鼠标在游戏窗口，失焦释放，Editor/安卓不锁定。见[连线说明](../../Assets/ta_source/LinkEffect/连接效果使用说明.md)和[绿绳API](Integration/GREEN_HOOK_API.md)。

植物障碍：同房间上下左右真实接触连通燃烧，斜角/孔洞不连通；一次组燃烧一次音效，沿用R/死亡/离区复原。见 [玩法说明](../Developer/GAMEPLAY_REFINEMENTS.md#植物障碍连通燃烧)。主菜单已接新OP `bmg_op`，Gaming保留atmosphere；Boss曲通过`AudioCueIds.MusicBoss`显式调用，见 [音频交接](Integration/AUDIO_HANDOVER.md)。

平台路线：支持四向、双侧独立范围/中心及首程方向，预览/运行共用MovingPlatformPath。信号驱动可选充能往返断电停留（旧默认），或充能至首程端点并保持、断电返回作者位置；遇障等待后续恢复，曲柄进度逻辑不变。长虚线先裁剪视口。见 [机关API](Integration/MECHANISM_SIGNAL_API.md)、[配置说明](../Developer/MECHANISMS_AND_JUMP.md#移动路线与中心)。

关卡/美术适配：16有效像素/格、正式2D预览、左右靠墙梯、基座重力及新图用途见 [ART_GRID_REFINEMENT_API](Integration/ART_GRID_REFINEMENT_API.md)。工作台静态三色装饰按颜色目录Layer分批，初始褪色、场景颜色解锁后恢复；不要误当动画或加ColorObject。告示牌复用字幕激发道具。片段导入/区域导出入口在关卡编辑器右侧三视图按钮下方。

正式打包关卡：`Assets/_Project/Scenes/Playing/Gaming.unity`，配套同名规划文件及 `Assets/_Project/Content/LevelMaps/正式关卡.json`；采用策划 `a86fc5a` 的10房间基线，物体/位置/实例参数/绑定以策划版为准，代码与工具修复保留整合最新版。旧 Gaming 使用者先同步新版，不用旧场景/缓存覆盖；其他成员仅拉取和推送分支，由当前整合窗口合并 main。必读 [正式关卡交接](Integration/FORMAL_LEVEL_DELIVERY.md)。

命名MVMap独立导入图：`Assets/Scenes/MRoom2.unity` + 同名planning存档/原始mvmap文件。第二版24分类完整重导入，六平台、三基座、两曲柄、三门七保持按钮及六重生点；四个基座消费者使用断电回位，托红块平台与左竖平台共用基座。路线补齐网页占格缺口，左下平台补4格水，两排尖刺均朝上，侧梯按邻墙选左右。保留Scene/meta、默认锁色、单测试区域，不替代Gaming或加入构建。见 [地图说明](../Developer/MROOM2_IMPORT.md)、[导入API](Integration/MVMAP_OBJECT_IMPORT_API.md)。

自动同步删除记忆已按规划文档版本隔离；重导入/队友更新不能沿用旧版 `(ID,格)` 推断删除，实例重建先重新查找。MRoom2开窗误删已从完整第二版成套恢复（188项、456墙格、61水格）；仅重新加载Scene/重开窗口，不能保存旧内存覆盖。专项 `PlanningAutoSyncAudit` 和链路说明见 [协作API](Integration/PLANNING_COLLABORATION_API.md)。

开发者复活方式：标准玩家默认常规，可用 `PlayerController.ConfigureRespawn(LoadingTransition, seconds)` 切换慢速收黑/加载背景/原路退场，沿用既有区域及重生点恢复，不换场景、不清进度；支持Inspector调试、重复请求和中断清理。见 [接入API](Integration/PLAYER_RESPAWN_API.md)、[复活表现](../Developer/PLAYER_RESPAWN.md)。

颜色双阶段解锁与唯一标准玩家：见 [接入约定](Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md)。场景颜色和玩家能力分别查询；所有玩法场景共用 `Content/Player/Player.prefab`，新建空地图在原点生成并绑定玩家。

移动端按键与转角：双侧跳跃、情境动作、多指独立持有与世界指针、设置内拖动/缩放/持久化布局；真实绿块外缘保护交接、横爬跳跃脱手，颜色能力持续选色。普通藤蔓及单格/竖向绿块补齐双侧向上露顶交接，遮挡顶部不提前翻顶，见 [CONTACT_CUBES_API](Integration/CONTACT_CUBES_API.md)。绿绳合法点击立即挂接，默认发射5格/维持8格、连续超距0.4秒才释放，见 [GREEN_HOOK_API](Integration/GREEN_HOOK_API.md)。移动端说明见 [MOBILE_CONTROLS.md](../Developer/MOBILE_CONTROLS.md)。

编辑器响应与攀爬衔接：预览缓存/分层容器/视口裁剪、合并持久化与连续编辑延后落盘；蹬跳上升期间不被攀爬状态重新接管，适用于藤蔓、梯子与绿色接触方块。重生点首次激活/返回已激活点两种提示声已接线，见 [ROOM_LIFECYCLE_API](Integration/ROOM_LIFECYCLE_API.md) 和 [AUDIO_HANDOVER](Integration/AUDIO_HANDOVER.md)。

编辑器远景：关卡画布与美术工作台共用有线数预算的自适应参考网格，实际1格命中和存档不变；房间/通道缓存精确矩形覆盖及孔洞轮廓，装配物体复用房间原点；美术瓦片使用保序的缓存批次，平移/缩放不逐瓦片重写顶点。接入维护见 [GRID_ART_API](Integration/GRID_ART_API.md)。

画布边缘拖拽：世界/详情/装配共用定时自动平移，静止按住和拖过边界仍持续，释放/取消/失焦即停止；补偿抓取起点、不增加中间保存事务。使用与接入见 [关卡协作](Integration/PLANNING_COLLABORATION_API.md)，专项验证为 PlanningEdgePanAudit。

绿钩子最新规则：自由指针瞄准，与红蓝共用出线点，合法点击立即挂接并接管运动，出线仅视觉。发射5格，挂接维持额外3格/连续超距0.4秒宽限。红蓝接触方块/基座每物理步四向优先拉物，受阻转拉玩家并重评估；绿色接触方块/生长体、藤蔓、弹性植物为保留动量的荡绳锚点，连接中直接点击切换挂点。成功挂接不因受阻/接触/等待脱钩，现有跳跃/交互主动结束。见 [GREEN_HOOK_API](Integration/GREEN_HOOK_API.md)。无色收集品与四向尖刺已加入道具栏目，见 [使用说明](../Developer/WORLD_ITEMS.md)、[接入](Integration/WORLD_ITEMS_API.md)。

无色尖刺接触链（2026-10-17）：NeutralSpike + 子碰撞体 SpikeContactRelay，按合并实际占格生成方向触发区，保留空洞并支持房间重置；统一玩家死亡/复活保护，重生安全查询排除尖刺且不误跳过同地图地面。真实物理回归 HazardContactAudit 仅在静音隔离工程运行，见 WORLD_ITEMS_API。

关卡协作：共享项目存档、Scene 同名规划文件、片段追加导入/区域导出、水与物品同格分层、整房间内容移动。使用 [LEVEL_EDITOR_COLLABORATION.md](../Developer/LEVEL_EDITOR_COLLABORATION.md)，接入 [PLANNING_COLLABORATION_API.md](Integration/PLANNING_COLLABORATION_API.md)。

玩家搬运体积约束：头顶方块按实际实体扫掠限制玩家物理位移，支持长块、冲刺与反向离开；搬运提示统一字体且不继承合并拉伸。Pick 已补 WordNote_UI，Test/Gaming 沿用各自现有组件。见 [搬运 API](Integration/PLAYER_CARRY_API.md)、[使用说明](../Developer/PLAYER_CARRY.md)。

门按钮三色美术按创建/绑定顺序红、绿、蓝；门内白色图案按按钮数量显示、按各按钮独立点亮，全部按下后保留可配置0.8秒展示再开门，有Timeline时先到门附近。规划存档保存按钮创建次序，重建不按Hierarchy排序；按钮仍为无色踩踏机关。见 [门使用说明](../Developer/DOORS.md)、[接入API](Integration/DOOR_API.md)。

正式水族已替换为TA像素水面/水中/水下自动分区与透明折射板；定向水流四向气泡、浮力柱向上连通水域气泡，继续共享蓝色HSV/透明交接与原玩法/区域重置。无需重建场景。见 [玩法水体API](Integration/GAMEPLAY_PIXEL_WATER_API.md)、[使用说明](../Developer/GAMEPLAY_PIXEL_WATER.md)。旧独立波纹水保留作资源和后备路径，不再默认生成。

Android 原生隐私：UnityPlayerActivity 前置独立进程授权、离线全文、设置查看与撤回；同意/不同意严格短文案。仅主动点击时系统浏览器打开 https://meowgame.cloud/shape-of-color/privacy/ ，网页版从唯一正文源生成，只有政策与主页相同的ICP备案；最低 Android 7.1 不变，不启用 Bootstrap。见 [开发说明](../Developer/ANDROID_PRIVACY.md) 与 [接入 API](Integration/ANDROID_PRIVACY_API.md)。用户只允许这一网页例外，游戏仍禁止非 TapTap SDK 联网，不增加 INTERNET，不使用 WebView；必须阅读 [离线规则](../Developer/OFFLINE_BUILD.md)，不得使用开发构建或恢复网络权限。

物体美术新增 `MechanismArtworkPresentation`：门及按钮三色提示、曲柄进度正反帧、移动平台、三色接触方块/钥匙、梯子及静态岩浆；沿用原碰撞、格尺寸与集中颜色表现。安卓暂停只反转触摸上下滑动，不改变键鼠手柄。见 [物体美术](../Developer/OBJECT_ARTWORK.md) 与 [接入API](Integration/OBJECT_ARTWORK_API.md)。

藤蔓外观使用TA192帧序列图集（ObjectArtwork），整根单SpriteRenderer、按生长长度适配、燃烧倒放及长成末段循环；仍走集中绿色HSV/共享材质透明交接，不再回退实时粒子。构建前检查序列帧和材质完整性，缺配置阻止出包。移动端额外像素化参数与PC一致，烘焙精灵不进入实时粒子像素遮罩。弹性植物底部贴齐格子。水序列帧尚未交付，保留现有水效果。见 [OBJECT_ARTWORK.md](../Developer/OBJECT_ARTWORK.md) 和 [OBJECT_ARTWORK_API.md](Integration/OBJECT_ARTWORK_API.md)。

双平台测试：SharedGameUI 的 MobileGameplayControls 提供多指方向/跳跃/Sprint/交互/左右放下与暂停/重生/选色，ScreenSafeArea 与 Expand Canvas 适配横屏比例及安全区。输入仍走 GameInput，触摸边沿在下一帧统一发布；合并气泡柱使用碰撞体底部而非中心判断上浮。正式包仅 MainMenu/Gaming，应用名和 APK 为“形之彩”。见 [双平台操作与构建](../Developer/ANDROID_AND_PC_TEST_BUILDS.md)。

重生点/弹性植物美术：`ObjectArtwork`配置与`WorldObjectSpritePresentation`表现，三态重生点、真实弹跳触发的6帧动画、合并植物按格重复，接入既有绿色HSV/共享透明材质。使用见 [OBJECT_ARTWORK.md](../Developer/OBJECT_ARTWORK.md)，接入见 [OBJECT_ARTWORK_API.md](Integration/OBJECT_ARTWORK_API.md)。

关卡美术工作台：`Code/Systems/GridArt`，UI Toolkit双侧栏、共享多图集素材库、直接读取当前关卡／房间／通道Solid白块并可联动铺绘，无需Hierarchy选择；自动保存美术／延后实时预览默认开启，显式“保存场景”交付Scene。支持Scene绘制、自动边角、多格装饰、同格多层透明叠图、多层XY视差预设／预览及每关独立持久化Mesh。自动底图不覆盖手工装饰；空闲不扫描、笔画不重建侧栏、未变生成网格复用。不改白块共享材质／玩法存档，跨Scene需独立副本；预留同UV法线与URP 3D受光，不代表2D Light已接入。独立示例在Development/GridArt/TileArtTrial。见 [操作与素材评估](../Developer/GRID_ART.md)、[接入API](Integration/GRID_ART_API.md)。

游戏UI美术：`Resources/UI/GameUiArtwork.asset`统一开始菜单、三色能力轮盘、暂停图标和设置背景，预裁切产物位于`Art/Generated/GameUI`。启动、能力门控与暂停操作仍由原组件负责；水中玩家使用裁剪/深度精灵Shader进入水下底图。素材接入与验证见 [GAME_UI_ARTWORK.md](../Developer/GAME_UI_ARTWORK.md)。

主角像素表现：`Assets/Player/PlayerSpritePresentation.cs`，7套动作35帧，读取现有玩家状态；按24源像素自然显示1.5格、物理仍1×1格，不新增碰撞体，死亡优先、演出冻结与复活恢复。唯一标准Player.prefab已接线，裸PlayerController运行时自动补齐。最新比例见 [美术约定](../Developer/ART_GRID_CONVENTIONS.md)。

MVMap 标注图：Development/RoomLifecycleTests 的 MVMapImportTest 与共享 MVMapObjects.planning.json；按 map.mvmap (3).json 标注范围制作，紫色为三区房间、白墙/水域/转弯水流/上喷气泡/单向平台/三按钮一门，方向与模式可编辑。首开场景可恢复共享存档，不依赖 Library。历史73区临时推测版已替换，不改正式关卡。见 [转换 API](Integration/MVMAP_OBJECT_IMPORT_API.md)、[使用说明](../Developer/MVMAP_OBJECT_IMPORT.md)及[映射报告](../Developer/MVMAP_OBJECT_MAPPING.md)。

当前 UI 挂载、标准玩家配置与 TA 白块连线判定：[UI_PLAYER_AND_LINK_AUDIT.md](../Developer/UI_PLAYER_AND_LINK_AUDIT.md)。

- 独立曲柄与多平台：平台侧持久绑定、自动配套创建、同控制器平台合并/解除与重建引用。见 [机关 API](Integration/MECHANISM_SIGNAL_API.md) 与 [策划操作](../Developer/MECHANISMS_AND_JUMP.md)。

红块供能与平台：旧能源兼容替换为固定红色 ContactCube，任意已解锁红块可供能任意基座。移动平台信号驱动，曲柄通电制动/载人快降；参数模式可点击绑定，地图存稳定 ID + 相对格，Scene 显示青色路线与粉色绑定。新增无色单向平台和有材质无碰撞隐藏块。玩家1×1、普通上升4格，弹性植物8格及一次搬运4格跳跃，摔死门槛10格，气泡柱出水上升3格。见 [机关信号 API](Integration/MECHANISM_SIGNAL_API.md)、[机关与跳跃](../Developer/MECHANISMS_AND_JUMP.md)。

独立三色接触方块：`Code/Systems/ContactCubes`，道具栏目三个 ContactCube 预制体，不是玩家染色目标；绿块四态长度反应、红蓝水柱、既有颜色目标转发。动态重力、落稳锁定、红蓝空中玩家碰撞隔离和喷出保护；绿块始终实体，母体及伸长部分侧面可攀爬、横向伸长部分上下攀附已接入。搬运已复用 CarryableFeature/玩家现有输入，通过 ICarryMotionParticipant 挂起刚体与反应、完整实体校验放下；黏住待接。API：[CONTACT_CUBES_API.md](Integration/CONTACT_CUBES_API.md)，策划：[CONTACT_CUBES.md](../Developer/CONTACT_CUBES.md)。

区域重生与重置：`Code/Systems/Rooms`，最近触碰锚点优先、存活 R / 死亡重生，按世界图格子分区恢复。三色/可搬运方块限于布置区域，跨区放下拒绝、越界回位，玩家离区取消携带。门、保持按钮、钥匙、解锁保留；持续按钮重置并重评估门。接入 [ROOM_LIFECYCLE_API.md](Integration/ROOM_LIFECYCLE_API.md)，使用 [ROOM_RESPAWN_AND_RESET.md](../Developer/ROOM_RESPAWN_AND_RESET.md)。两个独立测试 Scene 在 Development/RoomLifecycleTests。

最近整合说明：[2026-10-09 关卡编辑、门与颜色表现](../Developer/UPDATE_2026-10-09_EDITOR_AND_MECHANISMS.md)。

程序交接入口：[项目设置与程序交接](../Developer/PROJECT_SETUP.md)，含当前染色反馈、蒸汽喷发与目标连接预览模块的检索字段、挂载方式、触发规则和逻辑边界。

世界图编辑：独立通道占格、整体多选移动、区域规划与装配归属；见 [WORLD_PLANNING_API.md](Integration/WORLD_PLANNING_API.md)。统一 1:1 单元格、旧存档展开/备份和矩形创建/擦除；MVMap v1 已支持布局试导入，不转换灰盒/锁规则，见 [MVMap 转换评估](../Developer/MVMAP_IMPORT_ASSESSMENT.md)。

规划道具参数、存档资源引用与藤蔓链接：[PLANNING_REFERENCES_AND_VINES.md](../Developer/PLANNING_REFERENCES_AND_VINES.md)。参数资产使用 GlobalObjectId，旧失效 ID 不覆盖 prefab 默认值；母根与节段记录序列化所有权并在增量同步完成后校验。当前本地修复待实际关卡测试，未推送。

颜色表现：`ColorAppearanceManager` 集中管理基础方块、水、藤蔓的透明交接与共享运行时材质；`HSVColorFadeManager.SetHsv` 只控制全屏饱和度/白化，不控制物体和材质。水体采用 URP Unlit 透明网格 Shader，深度为实例材质参数。接入见 [COLOR_BLOCKS_API.md](Integration/COLOR_BLOCKS_API.md)；隔离水体 GPU 渐显/重置检查通过，完整 Game 与打包待验收，2026-10-10 已整合至 main。

定向水流泡泡表现：`Assets/ta_source/Paopao/Scripts/Runtime/DirectionalWaterBubbleStream.cs` 与同目录资源提供向上、向左、向右三种视觉预制体，泡泡从源头小尺寸沿方向移动并逐渐增大；长度 1–10 格、流向排数 0.2–3 排/格、每个位置 1–6 颗及交错程度 0–1、流宽 0–4 格、流速 0.1–5 格/秒均可调；不施力、不接管水流玩法。沙盒在 `Assets/ta_source/Paopao/TestScene/WaterBubblesTest.unity`，使用说明见 [DIRECTIONAL_WATER_BUBBLES.md](../Developer/DIRECTIONAL_WATER_BUBBLES.md)。

| 系统 | 源码 | 状态 |
|---|---|---|
| 非颜色门与绑定按钮 | `Assets/_Project/Code/Systems/Mechanisms` | 道具门 1×2、自动/追加专属按钮、玩家踩踏按钮、全部按下后开门、Timeline 镜头；不使用 E、不接收颜色交互。见 Integration/DOOR_API.md 与 Developer/DOORS.md |
| 无色梯子、横门与门提示 | `Assets/_Project/Code/Systems/Mechanisms` | 正面/侧面梯子复用攀爬；独立横门 2×1；每门唯一提示按总数/已按数量更新，新绑定上限三个。见 Integration/NEUTRAL_MECHANISMS_API.md 与 Developer/NEUTRAL_MECHANISMS.md；本地开发待验收 |
| 选择性像素化 | `Assets/_Project/Code/Systems/Pixelization`、`Assets/ta_source/PixelEffect` | 当前 PC 启用 TA Stencil 路径及其像素参数，关闭重复内置像素化；HSV 保持开启且在其后执行。内置路径的 Rendering Layer 128/描边 API 不控制 TA Shader。见 Integration/PIXELIZATION_API.md |
| 启动/场景/UI/基础音频 | `Assets/_Project/Code/Systems/GameFlow` | 正式序列固定为 `Assets/_Project/Scenes/Start/MainMenu.unity` → `Assets/_Project/Scenes/Playing/Gaming.unity`；MainMenu 持 `GameFlowController`/`StartMenuFlowBridge`，Gaming 持玩家、相机管理和 `SharedGameUI` 暂停界面。Bootstrap、旧 Systems、旧三关和 Ending 不再使用 |
| 音频资源 | `Assets/_Project/Content/Audio` | 22个音乐/环境声与46个SFX；63份音频登记为55个Cue。新OP接主菜单，Boss曲仅登记待触发点；两种重生点激活、搬运、基座启停、平台移动、气泡/浮力、6格落地、燃烧、岩浆冷却/触碰及菜单确认已接。解谜/秘密与独立浅水步行待触发点，区域环境声/其余BGM待规划。见 `Integration/AUDIO_HANDOVER.md` |
| 相机 Manager/跟随/2D3D | `Assets/_Project/Code/Systems/CameraModes` | 旧功能保留；玩家驱动不再申请 2D/3D 切换，Timeline 优先级更高 |
| 输入、触摸摇杆、平台UI预设 | `Assets/_Project/Code/Systems/InputAbstraction` | 旧功能保留 |
| 重绑定 | `Assets/_Project/Code/Systems/InputRebinding` | 旧功能保留 |
| 基础玩家 | `Assets/Player` | `PlayerController` 仅保留马能力，`PlayerInputDriver` 负责默认输入；不含旧玩法 |
| TA 交互像素连线预览 | `Assets/ta_source/LinkEffect` | 仅红绿蓝染色选中时，在 PlayerInteractionSensor 圆形范围内只读预览可接色目标；红蓝整线淡入、绿线带四条噪波弯枝逐段生长；蓝线外围水纹有独立强度参数，Shader 按实际线长生成火星、叶花，不拉伸图案；具备溶解退场。Test 场景只保留玩家上的预览组件；场景接入见 [连接效果使用说明.md](../../Assets/ta_source/LinkEffect/连接效果使用说明.md) |
| TA 染色覆盖效果 | `Assets/ta_source/GivingColor` | 独立 URP 透明覆盖 Shader 读取原材质纹理 Alpha，按红色燃烧、蓝色四次叠染、绿色逐级分叉播放；材质 Inspector 可调速度、噪波和像素化。只在玩家染色成功后短暂显示，完成后渐隐并露出原玩法改色结果；不覆盖水与岩浆生成的深蓝固体块，不覆盖蒸汽和植物反应。开局能力、解锁及目标原颜色由原程序管理；Test 场景文件不作接线修改；见 [使用说明.md](../../Assets/ta_source/GivingColor/使用说明.md) |
| 掉落物与拾取飞行 | `Assets/_Project/Code/Shared/Runtime/DropItemBase.cs`、`Assets/_Project/Code/Systems/PickupPresentation` | 基类负责感知后的磁吸、收集生命周期；颜色钥匙触碰结算并播放 DOTween 四段飞行与镜头；Pick 场景保留测试物 |
| 基础工具 | `Assets/GJ_Tools/BasicTools` | 对象池、计时、等待、事件与编辑器帮助已恢复 |
| 成就 | `Assets/_Project/Code/Systems/Achievements` | 旧功能保留；固定目录自动同步到运行时目录；暂停收藏页通过 `AchievementCollectionPresenter` 以两列布局直接读取 `AchievementManager` 的目录、当前条件、解锁状态、块材质和达成图片 |
| Timeline演出/战斗 | `Assets/GJ_Tools/TimelineTools3D` | 旧功能保留；SampleScene 提供相机与玩家接线范本；新增对话轨道，不需要绑定对象，直接向常驻 `WordNote_UI` 申请同一画布中的消息。使用见 [对话轨道](../Developer/TIMELINE_DIALOGUE.md)，接入见 [API](Integration/TIMELINE_DIALOGUE_API.md) |
| 玩家 / Boss Timeline 轨道 | `Assets/GJ_Tools/TimelineTools3D/AboutTimeline` | `PlayerTimelineTrack` 与 `BossTimelineTrack` 支持显式绑定或按 Tag 查找角色，驱动位移、跳跃、玩家输入锁和 Boss 追击状态。使用见 [玩家与 Boss 轨道](../Developer/TIMELINE_ACTOR_TRACKS.md)，接入见 [API](Integration/TIMELINE_ACTOR_TRACKS_API.md) |
| 通用接触触发道具 | `Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/ContactTimelineTrigger.cs` | 独立 MonoBehaviour，挂在有 Collider 的道具上后自动隐藏 Renderer，通过 Trigger/Collision 接触播放绑定 Timeline；支持玩家、任意碰撞体或 Tag 筛选，不依赖颜色系统。使用见 [接触触发](../Developer/CONTACT_TIMELINE.md)，接入见 [API](Integration/CONTACT_TIMELINE_API.md) |
| Boss 控制器与右移马夫 | `Assets/Boss_Logic/BossController.cs`、`BossAnimationDriver.cs`、`BossRightMoveExecutor.cs` | Controller 默认关闭自动目标追击，只响应 Timeline 和马夫指令，提供运动、跳跃、受击和死亡能力；Timeline 期间保持正常世界交互，`BossRightMoveExecutor` 启动后配置固定世界 Y、切穿地形并下发右移命令。`BossIntro.playable` 由多条 `BossTimelineTrack` 加 2D 相机轨组成，每个 Boss Clip 只做一个动作。选中怪物 Clip 时 Scene 视图显示轨迹。Renderer 使用 `BossTop`、排序值 `32767` 和 Renderer Priority `32767`。 |
| Boss 追击寻路（保留未挂载） | `Assets/Boss_Logic/Navigation` | 有向节点图 + A*，未来可用于其他 Boss/关卡；当前 Boss 预制体不挂载，不参与默认长廊战斗。使用见 [Boss 追击寻路](../Developer/BOSS_PATHFINDING.md)，接入见 [API](Integration/BOSS_PATHFINDING_API.md) |
| 方块瓦片绘制/烘焙/切片 | `Assets/_Project/Code/Systems/SurfaceTiles` | 旧工具保留，导入UI改为UI Toolkit，无素材 |
| 颜色物功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 提供固定形态、生命周期、信号、命令与调试；`BlockAbilityHost` 只按固定颜色组启停预制体已有组件；`CarryableFeature` 是能源方块和万能方块共用的独立搬运组件，能源由 `IColorObject` 门控，万能方块常态可搬 |
| 公共自动发现、掉落物生命周期与 Inspector 基础 | `Assets/_Project/Code/Shared` | `ProjectDiscovery` 统一多态组件/标签/类型发现；`DropItemBase` 统一掉落物出现、收集、停止动画和回收生命周期；项目自制组件使用 UI Toolkit Inspector、中文标签和 Foldout 分区 |
| 字幕管理器与触发展示 | `Assets/_Project/Code/Systems/Subtitles` | `SubtitleManager` 统一管理屏幕字幕；`DisplayBlockFeature` 提供世界空间显示方块文字、方块九宫格定位和玩家上下排序；`SubtitleTrigger` 支持重复激发 |
| 全局文字提示与字体 | `Assets/_Project/Code/Systems/WordNote`、`.agents/skills/taptap-typography/SKILL.md` | `WordNote_UI` 常驻 Canvas，支持九个位置队列、堆叠、七色描边和按 ID 清理；所有游戏内 UGUI/TMP 文本统一使用 `Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset`，开始菜单通过 `StartMenuTypography`，暂停界面通过 `EscMenuController` 的 SDF 镜像 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、玩家贴纸与拖拽同步、多选与保留孔洞的不规则合并、组件模板、贴画、场景生成和共享存档；水与墙/道具同格，水层独立编辑、前景半透明；方块栏目只做地图墙壁，颜色玩法物体全部走道具栏目；详见 Integration/PLANNING_COLLABORATION_API.md |
| 固定颜色物体/钥匙/房间重置 | `Assets/_Project/Code/Systems/ColorBlocks` | `ColorObject`、`ColorRuntimeService`、钥匙解锁、房间重置和 HSV 渐变；`Content/ColorBlocks/Prefabs` 使用 `Red_*`、`Blue_*`、`Green_*` 道具预制体；Universal_Block 白色代理及直接接色链仍保留，通用旧交互图退役 |
| 颜色物玩法组件 | `Assets/_Project/Code/Systems/Interactions` | 旧交互图及无调用 AnchorPoint 已退役；固定功能组件位于 `BlockFeatures/Runtime/Features`，米黄色重生点使用 RespawnAnchorFeature，不依赖旧 AnchorPoint |
| 绿色钩子 | `Assets/_Project/Code/Systems/GreenHook` | 自由瞄准、命中后接管、四向拉取/受阻回退、切向荡绳与连续换点；成功挂接由玩家主动结束，房间和控制锁保持隔离。见 [GREEN_HOOK_API.md](Integration/GREEN_HOOK_API.md) |
| 交互水面 | `Assets/ta_source/InteractiveWater`、`Assets/ta_source/Newwater/Prefabs` | TA 独立预制体保留独立波纹/反射资源；像素水提供三种双面片预制体和独立材质，透明物体后复制相机颜色、专用绘制折射滤镜，噪波只改片元屏幕采样 UV；每个实例可运行时调整滤镜色彩、折射强度和时间变化，前片自动贴合后片宽高/变换。场景和接入方式见 `Developer/PIXEL_WATER.md`、`Integration/PIXEL_WATER_API.md`；无颜色系统自动生成水体 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
