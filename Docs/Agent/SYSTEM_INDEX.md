# 系统索引

独立三色接触方块：`Code/Systems/ContactCubes`，道具栏目三个 ContactCube 预制体，不是玩家染色目标；绿块四态长度反应、红蓝水柱、既有颜色目标转发。动态重力、落稳锁定、空中玩家碰撞隔离、喷出保护和横向绿块上下攀附已接入；搬运/黏住待接。API：[CONTACT_CUBES_API.md](Integration/CONTACT_CUBES_API.md)，策划：[CONTACT_CUBES.md](../Developer/CONTACT_CUBES.md)。

区域重生与重置：`Code/Systems/Rooms`，最近触碰锚点优先、存活 R / 死亡重生，按世界图格子分区恢复。三色/可搬运方块限于布置区域，跨区放下拒绝、越界回位，玩家离区取消携带。门、保持按钮、钥匙、解锁保留；持续按钮重置并重评估门。接入 [ROOM_LIFECYCLE_API.md](Integration/ROOM_LIFECYCLE_API.md)，使用 [ROOM_RESPAWN_AND_RESET.md](../Developer/ROOM_RESPAWN_AND_RESET.md)。两个独立测试 Scene 在 Development/RoomLifecycleTests。

最近整合说明：[2026-10-09 关卡编辑、门与颜色表现](../Developer/UPDATE_2026-10-09_EDITOR_AND_MECHANISMS.md)。

世界图编辑：独立通道占格、整体多选移动、区域规划与装配归属；见 [WORLD_PLANNING_API.md](Integration/WORLD_PLANNING_API.md)。统一 1:1 单元格、旧存档展开/备份和矩形创建/擦除；MVMap v1 已支持布局试导入，不转换灰盒/锁规则，见 [MVMap 转换评估](../Developer/MVMAP_IMPORT_ASSESSMENT.md)。

规划道具参数、存档资源引用与藤蔓链接：[PLANNING_REFERENCES_AND_VINES.md](../Developer/PLANNING_REFERENCES_AND_VINES.md)。参数资产使用 GlobalObjectId，旧失效 ID 不覆盖 prefab 默认值；母根与节段记录序列化所有权并在增量同步完成后校验。当前本地修复待实际关卡测试，未推送。

颜色表现：`ColorAppearanceManager` 集中管理基础方块、水、藤蔓的透明交接与共享运行时材质；`HSVColorFadeManager.SetHsv` 只控制全屏饱和度/白化，不控制物体和材质。水体采用 URP Unlit 透明网格 Shader，深度为实例材质参数。接入见 [COLOR_BLOCKS_API.md](Integration/COLOR_BLOCKS_API.md)；隔离水体 GPU 渐显/重置检查通过，完整 Game 与打包待验收，2026-10-10 已整合至 main。

| 系统 | 源码 | 状态 |
|---|---|---|
| 非颜色门与绑定按钮 | `Assets/_Project/Code/Systems/Mechanisms` | 道具门 1×2、自动/追加专属按钮、玩家踩踏按钮、全部按下后开门、Timeline 镜头；不使用 E、不接收颜色交互。见 Integration/DOOR_API.md 与 Developer/DOORS.md |
| 无色梯子、横门与门提示 | `Assets/_Project/Code/Systems/Mechanisms` | 正面/侧面梯子复用攀爬；独立横门 2×1；每门唯一提示按总数/已按数量更新，新绑定上限三个。见 Integration/NEUTRAL_MECHANISMS_API.md 与 Developer/NEUTRAL_MECHANISMS.md；本地开发待验收 |
| 学习项目选择性像素化 | `Assets/_Project/Code/Systems/Pixelization` | Rendering Layer 128 / PixelEffect 选择性像素化；描边 API 默认关闭；像素化先于 HSV。见 Integration/PIXELIZATION_API.md |
| 启动/场景/UI/基础音频 | `Assets/_Project/Code/Systems/GameFlow` | 旧标准流程整体退役：五个 Systems 场景、旧三关及 Ending 已移除，catalog 清空、初始化菜单撤下。MainMenu 为独立 UI 预览，不自动载入旧流程。功能代码与 Content/UI 中的开始/暂停 prefab 保留；新关卡流程尚未接线 |
| 音频资源 | `Assets/_Project/Content/Audio` | 14 首 BGM、16 个 SFX 与同名 `.meta` 已导入；尚未绑定玩法／UI 事件，见 `Integration/AUDIO_HANDOVER.md` |
| 相机 Manager/跟随/2D3D | `Assets/_Project/Code/Systems/CameraModes` | 旧功能保留；玩家驱动不再申请 2D/3D 切换，Timeline 优先级更高 |
| 输入、触摸摇杆、平台UI预设 | `Assets/_Project/Code/Systems/InputAbstraction` | 旧功能保留 |
| 重绑定 | `Assets/_Project/Code/Systems/InputRebinding` | 旧功能保留 |
| 基础玩家 | `Assets/Player` | `PlayerController` 仅保留马能力，`PlayerInputDriver` 负责默认输入；不含旧玩法 |
| TA 交互像素连线预览 | `Assets/ta_source/LinkEffect` | 只读扫描玩家检测圈内的 F 交互、实际接色功能和可搬运物；按当前选色与目标状态绘制透明像素噪波线。Test 场景只保留玩家上的预览组件，接入说明见目录 README |
| 掉落物与拾取飞行 | `Assets/_Project/Code/Shared/Runtime/DropItemBase.cs`、`Assets/_Project/Code/Systems/PickupPresentation` | 基类负责感知后的磁吸、收集生命周期；颜色钥匙触碰结算并播放 DOTween 四段飞行与镜头；Pick 场景保留测试物 |
| 基础工具 | `Assets/GJ_Tools/BasicTools` | 对象池、计时、等待、事件与编辑器帮助已恢复 |
| 成就 | `Assets/_Project/Code/Systems/Achievements` | 旧功能保留；固定目录自动同步到运行时目录；暂停收藏页通过 `AchievementCollectionPresenter` 以两列布局直接读取 `AchievementManager` 的目录、当前条件、解锁状态、块材质和达成图片 |
| Timeline演出/战斗 | `Assets/GJ_Tools/TimelineTools3D` | 旧功能保留；SampleScene 提供相机与玩家接线范本 |
| 方块瓦片绘制/烘焙/切片 | `Assets/_Project/Code/Systems/SurfaceTiles` | 旧工具保留，导入UI改为UI Toolkit，无素材 |
| 颜色物功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 提供固定形态、生命周期、信号、命令与调试；`BlockAbilityHost` 只按固定颜色组启停预制体已有组件；`CarryableFeature` 是能源方块和万能方块共用的独立搬运组件，能源由 `IColorObject` 门控，万能方块常态可搬 |
| 公共自动发现、掉落物生命周期与 Inspector 基础 | `Assets/_Project/Code/Shared` | `ProjectDiscovery` 统一多态组件/标签/类型发现；`DropItemBase` 统一掉落物出现、收集、停止动画和回收生命周期；项目自制组件使用 UI Toolkit Inspector、中文标签和 Foldout 分区 |
| 字幕管理器与触发展示 | `Assets/_Project/Code/Systems/Subtitles` | `SubtitleManager` 统一管理屏幕字幕；`DisplayBlockFeature` 提供世界空间显示方块文字、方块九宫格定位和玩家上下排序；`SubtitleTrigger` 支持重复激发 |
| 全局文字提示 | `Assets/_Project/Code/Systems/WordNote` | `Systems_UI` 中与 `GameUI` 同级的 `WordNote_UI` 常驻 Canvas；总队列与九个位置队列支持同位置纵向堆叠、退场后 DOTween 补位、七色加白黑字体/描边、驻留/定时/瞬时模式、按 ID 强制退场和全屏清理 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、玩家贴纸与拖拽同步、矩形多选与单块合并、组件模板、贴画、场景生成和本地存档；方块栏目只做地图墙壁，颜色玩法物体全部走道具栏目；图定义、交互按钮和交互目录同步已移除 |
| 固定颜色物体/钥匙/房间重置 | `Assets/_Project/Code/Systems/ColorBlocks` | `ColorObject`、`ColorRuntimeService`、钥匙解锁、房间重置和 HSV 渐变；`Content/ColorBlocks/Prefabs` 使用 `Red_*`、`Blue_*`、`Green_*` 道具预制体；万能方块和染色应用链已按最新定稿退役 |
| 颜色物玩法组件 | `Assets/_Project/Code/Systems/Interactions` | 旧交互图和旧功能组件已删除；仅保留必要的锚点等非图玩法代码。当前固定功能组件位于 `BlockFeatures/Runtime/Features`，玩家血条 HUD 已移除 |
| 交互水面 | `Assets/ta_source/InteractiveWater` | TA 独立预制体保留独立波纹/反射资源；当前已无颜色系统自动生成水体，后续由具体水源、水流和气泡柱组件按固定形态接入 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
