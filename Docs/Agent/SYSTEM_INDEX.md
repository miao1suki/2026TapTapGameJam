# 系统索引

| 系统 | 源码 | 状态 |
|---|---|---|
| 启动/场景/UI/基础音频 | `Assets/_Project/Code/Systems/GameFlow` | 旧功能可用子集，清洁新场景 |
| 音频资源 | `Assets/_Project/Content/Audio` | 14 首 BGM、16 个 SFX 与同名 `.meta` 已导入；尚未绑定玩法／UI 事件，见 `Integration/AUDIO_HANDOVER.md` |
| 相机 Manager/跟随/2D3D | `Assets/_Project/Code/Systems/CameraModes` | 旧功能保留；玩家驱动不再申请 2D/3D 切换，Timeline 优先级更高 |
| 输入、触摸摇杆、平台UI预设 | `Assets/_Project/Code/Systems/InputAbstraction` | 旧功能保留 |
| 重绑定 | `Assets/_Project/Code/Systems/InputRebinding` | 旧功能保留 |
| 基础玩家 | `Assets/Player` | `PlayerController` 仅保留马能力，`PlayerInputDriver` 负责默认输入；不含旧玩法 |
| 拾取飞行动画 | `Assets/_Project/Code/Systems/PickupPresentation` | DOTween 四段随机弯曲飞行表现（先向外反弹）；Pick 场景有测试物，正式拾取判定和奖励逻辑尚未接入 |
| 基础工具 | `Assets/GJ_Tools/BasicTools` | 对象池、计时、等待、事件与编辑器帮助已恢复 |
| 成就 | `Assets/_Project/Code/Systems/Achievements` | 其他程序旧功能，空新catalog |
| Timeline演出/战斗 | `Assets/GJ_Tools/TimelineTools3D` | 旧功能保留；SampleScene 提供相机与玩家接线范本 |
| 方块瓦片绘制/烘焙/切片 | `Assets/_Project/Code/Systems/SurfaceTiles` | 旧工具保留，导入UI改为UI Toolkit，无素材 |
| 颜色物功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 提供固定形态、生命周期、信号、命令与调试；`BlockFeatureAttribute` 声明颜色组、类别和交互类型；`BlockAbilityHost` 只按固定颜色组启停预制体已有组件；`Runtime/Features` 已接入红蓝绿固定功能组件，跨色反应暂留接口 |
| 公共自动发现与 Inspector 基础 | `Assets/_Project/Code/Shared` | `ProjectDiscovery` 统一多态组件/标签/类型发现；项目自制组件使用 UI Toolkit Inspector、中文标签和 Foldout 分区 |
| 字幕管理器与触发展示 | `Assets/_Project/Code/Systems/Subtitles` | `SubtitleManager` 统一管理屏幕字幕；`DisplayBlockFeature` 提供世界空间显示方块文字、方块九宫格定位和玩家上下排序；`SubtitleTrigger` 支持重复激发 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、玩家贴纸与拖拽同步、矩形多选与单块合并、组件模板、贴画、场景生成和本地存档；方块栏目只做地图墙壁，颜色玩法物体全部走道具栏目；图定义、交互按钮和交互目录同步已移除 |
| 固定颜色物体/钥匙/房间重置 | `Assets/_Project/Code/Systems/ColorBlocks` | `ColorObject`、`ColorRuntimeService`、钥匙解锁、房间重置和 HSV 渐变；`Content/ColorBlocks/Prefabs` 使用 `Red_*`、`Blue_*`、`Green_*` 道具预制体；万能方块和染色应用链已按最新定稿退役 |
| 颜色物玩法组件 | `Assets/_Project/Code/Systems/Interactions` | 旧交互图和旧功能组件已删除；仅保留必要的锚点等非图玩法代码。当前固定功能组件位于 `BlockFeatures/Runtime/Features`，玩家血条 HUD 已移除 |
| 交互水面 | `Assets/ta_source/InteractiveWater` | TA 独立预制体保留独立波纹/反射资源；当前已无颜色系统自动生成水体，后续由具体水源、水流和气泡柱组件按固定形态接入 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
