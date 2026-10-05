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
| 方块功能组件契约 | `Assets/_Project/Code/Systems/BlockFeatures` | `BlockFeature`/`BlockRuntime` 全覆盖交互契约；动力组件含开关/信号源与单次/持续/脉冲调试，完整动力网络待施工 |
| 融合关卡编辑器 | `Assets/_Project/Development/LevelEditor/Orpheus0829` | 唯一编辑器入口；世界图/详情/装配图、方块/道具栏目、矩形多选与单块合并、组件模板、贴画、场景生成和本地存档；RGB 栏目直连 ColorBlock 预制体 |
| 可变色方块/颜色工作台/HSV褪色 | `Assets/_Project/Code/Systems/ColorBlocks` | RGB 类型、钥匙拉远／恢复／返回演出、按层褪色调试、蓝水同步渐显与玩家游泳、交互图基础节点；九种完整交互及混色执行待设计 |
| 交互水面 | `Assets/ta_source/InteractiveWater` | TA 独立预制体保留独立波纹/反射；颜色管理器生成的蓝水实例使用共享低分辨率模拟和渐显，已在 Test Play Mode 验证 |

接入说明：[基础系统 API](Integration/FOUNDATION_API.md)。
开发者：[运行与场景](../Developer/GAME_FLOW.md)、[贴画与切片](../Developer/SURFACE_TILES.md)、[旧成就与Timeline](../Developer/LEGACY_SYSTEMS.md)。
颜色系统：[Agent 接入 API](Integration/COLOR_BLOCKS_API.md)、[制作说明](../Developer/COLOR_BLOCKS.md)。
迁移记录：[MIGRATION_REPORT.md](MIGRATION_REPORT.md)。
各模块随源码的 README 是旧实现参考；与这里冲突时以本项目最新说明为准，不复原被排除的场景与玩法。
