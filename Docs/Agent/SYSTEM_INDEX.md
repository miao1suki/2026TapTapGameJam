# 系统索引

正式水族已替换为TA像素水面/水中/水下自动分区与透明折射板；定向水流四向气泡、浮力柱向上连通水域气泡，继续共享蓝色HSV/透明交接与原玩法/区域重置。无需重建场景。见 [玩法水体API](Integration/GAMEPLAY_PIXEL_WATER_API.md)、[使用说明](../Developer/GAMEPLAY_PIXEL_WATER.md)。旧独立波纹水保留作资源和后备路径，不再默认生成。

Android 原生隐私：UnityPlayerActivity 前置独立进程授权、离线全文/在线入口、设置查看与撤回；最低 Android 7.1 不变，不启用 Bootstrap。见 [开发说明](../Developer/ANDROID_PRIVACY.md) 与 [接入 API](Integration/ANDROID_PRIVACY_API.md)。

物体美术新增 `MechanismArtworkPresentation`：门及按钮三色提示、曲柄进度正反帧、移动平台、三色接触方块/钥匙、梯子及静态岩浆；沿用原碰撞、格尺寸与集中颜色表现。安卓暂停只反转触摸上下滑动，不改变键鼠手柄。见 [物体美术](../Developer/OBJECT_ARTWORK.md) 与 [接入API](Integration/OBJECT_ARTWORK_API.md)。

藤蔓外观现默认使用TA192帧序列图集（ObjectArtwork），整根单SpriteRenderer、按生长长度适配、燃烧倒放及长成末段循环；仍走集中绿色HSV/共享材质透明交接，粒子仅作缺配置后备。弹性植物底部贴齐格子，不再为顶面对齐而悬空。水序列帧尚未交付，保留现有水效果。见 [OBJECT_ARTWORK.md](../Developer/OBJECT_ARTWORK.md) 和 [OBJECT_ARTWORK_API.md](Integration/OBJECT_ARTWORK_API.md)。

双平台测试：SharedGameUI 的 MobileGameplayControls 提供多指方向/跳跃/Sprint/交互/左右放下与暂停/重生/选色，ScreenSafeArea 与 Expand Canvas 适配横屏比例及安全区。输入仍走 GameInput，触摸边沿在下一帧统一发布；合并气泡柱使用碰撞体底部而非中心判断上浮。正式包仅 MainMenu/Gaming，应用名和 APK 为“形之彩”。见 [双平台操作与构建](../Developer/ANDROID_AND_PC_TEST_BUILDS.md)。

重生点/弹性植物美术：`ObjectArtwork`配置与`WorldObjectSpritePresentation`表现，三态重生点、真实弹跳触发的6帧动画、合并植物按格重复，接入既有绿色HSV/共享透明材质。使用见 [OBJECT_ARTWORK.md](../Developer/OBJECT_ARTWORK.md)，接入见 [OBJECT_ARTWORK_API.md](Integration/OBJECT_ARTWORK_API.md)。

网格美术试用版：`Code/Systems/GridArt`，独立美术资产、八邻格四象限自动边角、多格装饰、多层XY视差与持久化分块Mesh；关卡编辑器“网格美术”入口共享格尺寸、只读已应用的场景轮廓，不改玩法存档。独立测试在Development/GridArt/TileArtTrial。见 [操作与素材评估](../Developer/GRID_ART.md)、[接入API](Integration/GRID_ART_API.md)。

游戏UI美术：`Resources/UI/GameUiArtwork.asset`统一开始菜单、三色能力轮盘、暂停图标和设置背景，预裁切产物位于`Art/Generated/GameUI`。启动、能力门控与暂停操作仍由原组件负责；水中玩家使用裁剪/深度精灵Shader进入水下底图。素材接入与验证见 [GAME_UI_ARTWORK.md](../Developer/GAME_UI_ARTWORK.md)。

主角像素表现：`Assets/Player/PlayerSpritePresentation.cs`，7套动作35帧，读取现有玩家状态；独立1×1格外观、不新增碰撞体，死亡优先、演出冻结与复活恢复。两份玩家预制体已接线，裸PlayerController运行时自动补齐。接入/维护见 [PLAYER_SPRITE_PRESENTATION.md](../Developer/PLAYER_SPRITE_PRESENTATION.md)。

MVMap 标注图：Development/RoomLifecycleTests 的 MVMapImportTest 与共享 MVMapObjects.planning.json；按 map.mvmap (3).json 标注范围制作，紫色为三区房间、白墙/水域/转弯水流/上喷气泡/单向平台/三按钮一门，方向与模式可编辑。首开场景可恢复共享存档，不依赖 Library。历史73区临时推测版已替换，不改正式关卡。见 [转换 API](Integration/MVMAP_OBJECT_IMPORT_API.md)、[使用说明](../Developer/MVMAP_OBJECT_IMPORT.md)及[映射报告](../Developer/MVMAP_OBJECT_MAPPING.md)。

当前 UI 挂载、两套玩家配置与 TA 白块连线判定：[UI_PLAYER_AND_LINK_AUDIT.md](../Developer/UI_PLAYER_AND_LINK_AUDIT.md)。

- 独立曲柄与多平台：平台侧持久绑定、自动配套创建、同控制器平台合并/解除与重建引用。见 [机关 API](Integration/MECHANISM_SIGNAL_API.md) 与 [策划操作](../Developer/MECHANISMS_AND_JUMP.md)。

红块供能与平台：旧能源兼容替换为固定红色 ContactCube，任意已解锁红块可供能任意基座。移动平台信号驱动，曲柄通电制动/载人快降；参数模式可点击绑定，地图存稳定 ID + 相对格，Scene 显示青色路线与粉色绑定。新增无色单向平台和有材质无碰撞隐藏块。玩家1×1、普通上升4格，弹性植物3/8格及一次搬运4格跳跃。见 [机关信号 API](Integration/MECHANISM_SIGNAL_API.md)、[机关与跳跃](../Developer/MECHANISMS_AND_JUMP.md)。

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
| 音频资源 | `Assets/_Project/Content/Audio` | 20个音乐/环境声与44个SFX；60个文件登记为52个Cue。搬运、基座启停、平台移动、气泡/浮力、6格落地、燃烧、岩浆冷却/触碰及菜单确认已接；解谜/秘密与独立浅水步行待触发点，区域环境声/其余BGM待规划。见 `Integration/AUDIO_HANDOVER.md` |
| 相机 Manager/跟随/2D3D | `Assets/_Project/Code/Systems/CameraModes` | 旧功能保留；玩家驱动不再申请 2D/3D 切换，Timeline 优先级更高 |
| 输入、触摸摇杆、平台UI预设 | `Assets/_Project/Code/Systems/InputAbstraction` | 旧功能保留 |
| 重绑定 | `Assets/_Project/Code/Systems/InputRebinding` | 旧功能保留 |
| 基础玩家 | `Assets/Player` | `PlayerController` 仅保留马能力，`PlayerInputDriver` 负责默认输入；不含旧玩法 |
| TA 交互像素连线预览 | `Assets/ta_source/LinkEffect` | 仅红绿蓝染色选中时，在 PlayerInteractionSensor 圆形范围内只读预览可接色目标；红蓝整线淡入、绿线带四条噪波弯枝逐段生长；蓝线外围水纹有独立强度参数，Shader 按实际线长生成火星、叶花，不拉伸图案；具备溶解退场。Test 场景只保留玩家上的预览组件；场景接入见 [连接效果使用说明.md](../../Assets/ta_source/LinkEffect/连接效果使用说明.md) |
| TA 染色覆盖效果 | `Assets/ta_source/GivingColor` | 独立 URP 透明覆盖 Shader 读取原材质纹理 Alpha，按红色燃烧、蓝色四次叠染、绿色逐级分叉播放；材质 Inspector 可调速度、噪波和像素化。只在玩家染色成功后短暂显示，完成后渐隐并露出原玩法改色结果；不覆盖水与岩浆生成的深蓝固体块，不覆盖蒸汽和植物反应。开局能力、解锁及目标原颜色由原程序管理；Test 场景文件不作接线修改；见 [使用说明.md](../../Assets/ta_source/GivingColor/使用说明.md) |
| 掉落物与拾取飞行 | `Assets/_Project/Code/Shared/Runtime/DropItemBase.cs`、`Assets/_Project/Code/Systems/PickupPresentation` | 基类负责感知后的磁吸、收集生命周期；颜色钥匙触碰结算并播放 DOTween 四段飞行与镜头；Pick 场景保留测试物 |
| 基础工具 | `Assets/GJ_Tools/BasicTools` | 对象池、计时、等待、事件与编辑器帮助已恢复 |
| 成就 | `Assets/_Project/Code/Systems/Achievements` | 旧功能保留；固定目录自动同步到运行时目录；暂停收藏页通过 `AchievementCollectionPresenter` 以两列布局直接读取 `AchievementManager` 的目录、当前条件、解锁状态、块材质和达成图片 |
| Timeline演出/战斗 | `Assets/GJ_Tools/TimelineTools3D` | 旧功能保留；SampleScene 提供相机与玩家接线范本 |
| 方块瓦片绘制/烘焙/切片 | `Assets/_Project/Code/Systems/SurfaceTiles` | 旧工具保留，导入UI改为UI Toolkit，无素材 |
| 颜色物功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 提供固定形态、生命周期、信号、命令与调试；`BlockAbilityHost` 只按固定颜色组启停预制体已有组件；`CarryableFeature` 是能源方块和万能方块共用的独立搬运组件，能源由 `IColorObject` 门控，万能方块常态可搬 |
| 公共自动发现、掉落物生命周期与 Inspector 基础 | `Assets/_Project/Code/Shared` | `ProjectDiscovery` 统一多态组件/标签/类型发现；`DropItemBase` 统一掉落物出现、收集、停止动画和回收生命周期；项目自制组件使用 UI Toolkit Inspector、中文标签和 Foldout 分区 |
| 字幕管理器与触发展示 | `Assets/_Project/Code/Systems/Subtitles` | `SubtitleManager` 统一管理屏幕字幕；`DisplayBlockFeature` 提供世界空间显示方块文字、方块九宫格定位和玩家上下排序；`SubtitleTrigger` 支持重复激发 |
| 全局文字提示与字体 | `Assets/_Project/Code/Systems/WordNote`、`.agents/skills/taptap-typography/SKILL.md` | `WordNote_UI` 常驻 Canvas，支持九个位置队列、堆叠、七色描边和按 ID 清理；所有游戏内 UGUI/TMP 文本统一使用 `Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset`，开始菜单通过 `StartMenuTypography`，暂停界面通过 `EscMenuController` 的 SDF 镜像 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、玩家贴纸与拖拽同步、矩形多选与单块合并、组件模板、贴画、场景生成和本地存档；方块栏目只做地图墙壁，颜色玩法物体全部走道具栏目；图定义、交互按钮和交互目录同步已移除 |
| 固定颜色物体/钥匙/房间重置 | `Assets/_Project/Code/Systems/ColorBlocks` | `ColorObject`、`ColorRuntimeService`、钥匙解锁、房间重置和 HSV 渐变；`Content/ColorBlocks/Prefabs` 使用 `Red_*`、`Blue_*`、`Green_*` 道具预制体；Universal_Block 白色代理及直接接色链仍保留，通用旧交互图退役 |
| 颜色物玩法组件 | `Assets/_Project/Code/Systems/Interactions` | 旧交互图及无调用 AnchorPoint 已退役；固定功能组件位于 `BlockFeatures/Runtime/Features`，米黄色重生点使用 RespawnAnchorFeature，不依赖旧 AnchorPoint |
| 交互水面 | `Assets/ta_source/InteractiveWater`、`Assets/ta_source/Newwater/Prefabs` | TA 独立预制体保留独立波纹/反射资源；像素水提供三种双面片预制体和独立材质，透明物体后复制相机颜色、专用绘制折射滤镜，噪波只改片元屏幕采样 UV；每个实例可运行时调整滤镜色彩、折射强度和时间变化，前片自动贴合后片宽高/变换。场景和接入方式见 `Developer/PIXEL_WATER.md`、`Integration/PIXEL_WATER_API.md`；无颜色系统自动生成水体 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
