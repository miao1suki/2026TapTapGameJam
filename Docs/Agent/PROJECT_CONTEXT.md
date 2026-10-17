# 项目上下文（持续更新）

项目：2026TapTap；远程：`https://github.com/miao1suki/2026TapTapGameJam.git`。
Unity 6000.3.12f1，URP、新 Input System。模板项目的设置保留，未导入旧项目设置。

场景美术统一16个有效美术像素对应1格。编码尺寸需扣除已验证的导出倍率（tail为4倍），不拉伸以迁就碰撞；24像素玩家外观为1.5格而物理仍1格。修改物体、关卡预览、贴花或背景前读 [美术换算约定](../Developer/ART_GRID_CONVENTIONS.md) 和 [工作台换算](../Developer/SCENE_ART_PIXEL_CONVERSION.md)。基座默认有重力，可实例关闭；MRoom2两基座保持无重力。新增颜色装饰仅归属颜色层，不是可交互颜色物体，不能授予能力或参与房间玩法重置。

当前是一份从旧项目筛选而来的基础框架，不是旧游戏整体复制。
正式关卡已采用策划分支 `codex/formal-level-progress` 的 `a86fc5a` 交付（10房间）：`Scenes/Playing/Gaming.unity`、同名 `Gaming.planning.json` 与 `Content/LevelMaps/正式关卡.json` 成套维护（均在 `Assets/_Project` 下）。物体、位置、实例参数和绑定以策划版为准，运行时和工具修复保留整合窗口最新版。Gaming 是正式打包游玩场景，不是临时测试关卡；后续正式关卡变动在此基线上继续。旧 Gaming 使用者必须先同步新版场景和两份存档，不可用旧版覆盖；仅当前整合窗口合并 main，其他成员只拉取和推送任务分支。完整约定见 [正式关卡交接](Integration/FORMAL_LEVEL_DELIVERY.md)。
当前唯一标准玩家为 `Assets/_Project/Content/Player/Player.prefab`（1×1、中心归零），各场景共用；旧RuntimePlayer/Player(暂定)已退役。同色第一把钥匙只解锁场景物体，第二把授予玩家颜色能力，两种状态独立、房间重置保留。接入必读 `Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。
保留：启动与多场景生命周期、场景导航、基础玩家控制、输入与重绑定、双端 UI 预设、相机 Manager/跟随/模式转换、成就、Timeline 演出/战斗、表面瓦片工具、方块功能组件契约、不规则精灵切片。
相机转换不再由 PlayerInputDriver 申请；Tab 的调色动作只控制染色轮盘，F 负责交互目标与搬运，Q/E 负责左右放下，左键负责已选颜色的染色，R / 手柄右摇杆对应“重生”，存活或死亡均可返回最近触碰的锚点并恢复相关房间/通道。门、按钮、已拾取钥匙及解锁进度保留。能源方块和万能方块共用 CarryableFeature；输入仍只通过 PlayerInputDriver 调用 PlayerController，Timeline 优先级不变。

门按钮默认一次按下保持，持续压住模式则在松开、R 或离区时重置，取消持续信号会关门但保留其他保持按钮进度。三色接触方块/可搬运方块禁止跨布置区域迁移，跨区放下被拒绝、越界回到作者位置；玩家离区取消携带并复原来源区。
Timeline 现提供独立对话轨道：轨道不绑定对象，直接向常驻 `WordNote_UI` 申请使用权，与原 API 消息共用同一画布、位置队列、堆叠和动画系统。Timeline 只负责申请和释放本次消息，不接管玩家、颜色系统或房间状态。

基础迁移时排除：绳/线、梯子、移动平台玩法、视差平台投影碰撞/深度吸附、2D小拼/总拼/3D合并、旧地图代理同步、旧关卡、旧测试脏数据、美术/音效素材、瓦片库与烘焙贴画、QQ bot 运维源码和私密配置。之后由音频成员在新项目单独交付的 `Assets/_Project/Content/Audio` 不属于“旧素材迁移”，已纳入当前仓库。
成就中的旧玩法信号名字保留仅为兼容枚举/协议，不代表玩法可用。不要重新引入对应实现。
例外：关卡编辑器开发目录中的 `TileLibrary/26TAPTAP.png` 是用户明确要求恢复的源图，只允许用于该开发工具，不扩散到正式美术目录。

## 目录约定

- `Assets/_Project/Code/Systems/<Feature>`：功能源码，Runtime/Editor/Tests 分开。
- `Assets/Player`：基础玩家及 Timeline 动作适配。
- `Assets/GJ_Tools/TimelineTools3D`：保留的旧 Timeline 工具（兼容路径）。
- `Assets/_Project/Scenes/Start/MainMenu.unity`：唯一正式启动场景，持有 `GameFlowController` 和开始菜单流程桥。
- `Assets/_Project/Scenes/Playing/Gaming.unity`：唯一正式玩法场景，持有玩家、玩法相机和共享暂停 UI。
- 正式场景序列固定为 `MainMenu -> Gaming`；Bootstrap、旧五个 Systems 场景、旧 `Flow` 目录、Level_01～03 和 Ending 均退役，不再恢复。
- `Assets/_Project/Development/<Feature>/<AuthorOrTask>`：独立实验；不默认加入正式构建。
- `Assets/_Project/Content`：正式流程的 prefab、catalog 和配置。
- `Assets/_Project/Content/Audio/Music` 与 `SFX`：游戏实际使用的音频及其 `.meta`；音频成员按 `.agents/skills/taptap-audio-worker/SKILL.md` 交付。
- `Assets/_Project/Art/ArtSource`：未来导入的原始美术。
- `Assets/_Project/Art/Generated`：工具产物；生成后检查并提交依赖。
- `Docs/Agent`：agent 的统一交流、约定和状态；每次重新读取。
- `Docs/Agent/Integration`：面向 agent 的接入说明。
- `Docs/Developer`：面向开发者/策划的使用说明。

Unity 模板的 `SampleScene` 暂保留为模板参考，不属于正式流程。
