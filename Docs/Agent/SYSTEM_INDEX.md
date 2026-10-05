# 系统索引

| 系统 | 源码 | 状态 |
|---|---|---|
| 启动/场景/UI/基础音频 | `Assets/_Project/Code/Systems/GameFlow` | 旧功能可用子集，清洁新场景 |
| 音频资源 | `Assets/_Project/Content/Audio` | 14 首 BGM、16 个 SFX 与同名 `.meta` 已导入；尚未绑定玩法／UI 事件，见 `Integration/AUDIO_HANDOVER.md` |
| 相机 Manager/跟随/2D3D | `Assets/_Project/Code/Systems/CameraModes` | 旧功能保留；PlayerInputDriver 可用 CameraModeSwitch 申请 2D/3D，Timeline 优先级更高 |
| 输入、触摸摇杆、平台UI预设 | `Assets/_Project/Code/Systems/InputAbstraction` | 旧功能保留 |
| 重绑定 | `Assets/_Project/Code/Systems/InputRebinding` | 旧功能保留 |
| 基础玩家 | `Assets/Player` | `PlayerController` 仅保留马能力，`PlayerInputDriver` 负责默认输入；不含旧玩法 |
| 基础工具 | `Assets/GJ_Tools/BasicTools` | 对象池、计时、等待、事件与编辑器帮助已恢复 |
| 成就 | `Assets/_Project/Code/Systems/Achievements` | 其他程序旧功能，空新catalog |
| Timeline演出/战斗 | `Assets/GJ_Tools/TimelineTools3D` | 旧功能保留；SampleScene 提供相机与玩家接线范本 |
| 方块瓦片绘制/烘焙/切片 | `Assets/_Project/Code/Systems/SurfaceTiles` | 旧工具保留，导入UI改为UI Toolkit，无素材 |
| 方块功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 全覆盖交互契约；`BlockAbilityHost`/Catalog 按颜色与角色自动启停动力、水体等模块，完整动力能力配置待施工 |
| 公共自动发现与 Inspector 基础 | `Assets/_Project/Code/Shared` | `ProjectDiscovery` 统一多态组件/标签/类型发现；项目自制组件使用 UI Toolkit Inspector、中文标签和 Foldout 分区 |
| 字幕管理器与触发展示 | `Assets/_Project/Code/Systems/Subtitles` | `SubtitleManager` 统一管理中间大字/下方小字、九宫格位置、扩展进出动画与打字机/故障/漂浮等效果；`SubtitleTrigger` 支持重复激发并调用 API，与相机系统解耦 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、玩家贴纸与拖拽同步、矩形多选与单块合并、组件模板、贴画、场景生成和本地存档；RGB 栏目直连 ColorBlock 预制体 |
| 可变色方块/颜色属性/HSV褪色 | `Assets/_Project/Code/Systems/ColorBlocks` | RGB 属性、钥匙演出、按层褪色、蓝水同步渐显、玩家游泳和颜色状态服务；不再提供颜色交互图或颜色工作台 |
| 物体交互管理器 | `Assets/_Project/Code/Systems/Interactions` | 以物体定义为边界的连连看交互图；颜色作为物体属性；三色方块自动同步并默认带接触/解锁图；支持触碰、停留、时间、延迟、颜色/物体条件、褪色/恢复、Timeline、方法调用与关卡编辑器双向同步 |
| 交互水面 | `Assets/ta_source/InteractiveWater` | TA 独立预制体保留独立波纹/反射；颜色管理器生成的蓝水实例使用共享低分辨率模拟和渐显，已在 Test Play Mode 验证 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
