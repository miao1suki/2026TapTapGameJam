# 项目上下文（持续更新）

2026-10-20 最新关卡以用户指定 xianyu66web `ac01381` 为准：25房间、24区域、4261物体，Gaming全部玩法与两份编辑存档采用作者版本；TA美术独立保留，双方程序修复及另一对话连通空间遮蔽/地图滚轮/UI修正接入。以下旧地图整合数量不再代表最新关卡。

当前正式发布地图：策划ab757fc的23房布局，补main最新1caaf67 Boss/竖井/结局后25区域4199条规划；TA美术与93fd945死亡溶解保留，旧28区摆放不恢复。TapTap SDK已接入，版本0.2.1、包名com.miao1suki.shapeofcolor。以下地图数量是历史记录，当前以FORMAL_LEVEL_DELIVERY顶部为准。

2026-10-20包名最终确认：用户明确恢复并长期使用 **com.miao1suki.shapeofcolor**。ProjectSettings与MobileBuildSetup已恢复原包名；下文com.xingzhicai.game仅为历史构建状态，不得再据此改回。TapTap应用976206的14条成就已全部发布，交接见 `Docs/Agent/Integration/TAPTAP_ACHIEVEMENT_BACKEND_HANDOVER_2026-10-20.md`；SDK代码由另一程序负责。

项目：2026TapTap；远程：`https://github.com/miao1suki/2026TapTapGameJam.git`。
Unity 6000.3.12f1，URP、新 Input System。模板项目的设置保留，未导入旧项目设置。

2026-10-20正式合图更新：Gaming已纳入最终R1/R2/R3、用户最新MRoom2/MRoom3及Boss，4343物体、28区域、35普通收集品；实际Scene回采快照与可独立导入片段位于Content/LevelMaps/AuthoredSections，旧R planning仍不可直接覆盖最终图。Start/End及结局均同场景。版本0.2.1、Android com.miao1suki.shapeofcolor；原房间21用户移走部分保留。以下未合图房间数量与“不进入正式构建”为历史状态，详见FORMAL_LEVEL_DELIVERY与COMBINED_WORLD_0_2_1.md。

当前正式图以FORMAL_LEVEL_DELIVERY顶部2026-10-20远端基线为准：xianyu66web 5e88e7c、21房间、2604原物体，补新出生位置的隐形Start后2605条。保留main玩法、美术和单Gaming菜单修复；下文20/17房间等是历史记录，不可据此回退地图。R2最新Scene为d3fac6a，R1/R2/R3最终手调planning仍待精确回采，不进入正式构建。TA新美术f7cbb62仅备份不合，现有main美术保留。各策划工作场景最终导入同一Gaming；Boss门槛/结局/地图不引入跨场景加载。

场景美术统一16个有效美术像素对应1格。编码尺寸需扣除已验证的导出倍率（tail为4倍），不拉伸以迁就碰撞；24像素玩家外观为1.5格而物理仍1格。修改物体、关卡预览、贴花或背景前读 [美术换算约定](../Developer/ART_GRID_CONVENTIONS.md) 和 [工作台换算](../Developer/SCENE_ART_PIXEL_CONVERSION.md)。基座默认有重力，可实例关闭；MRoom2第二版三基座保持无重力。新增颜色装饰仅归属颜色层，不是可交互颜色物体，不能授予能力或参与房间玩法重置。

当前是一份从旧项目筛选而来的基础框架，不是旧游戏整体复制。
关卡自动同步的删除会话缓存必须与采集时文档版本绑定；重导入或共享存档更新不能沿用旧版ID/格推断删除。生成实例重建先重新识别，避免墙、水和机关引用再次误删。MRoom2第二版已从完整副本恢复；打开旧内存场景时先重新加载，不保存覆盖。详见 `Integration/PLANNING_COLLABORATION_API.md`。
正式关卡已增量整合策划分支 `codex/level/main-20261017-03` 的 `eb76949` 交付（17房间，房间12已有内容，新增13～17）：`Scenes/Playing/Gaming.unity`、同名 `Gaming.planning.json` 与 `Content/LevelMaps/正式关卡.json` 成套维护（均在 `Assets/_Project` 下）。保留main原有修复、房间9的平台去重及独立单格16格路线，不全量重建。Gaming 是正式打包游玩场景，不是临时测试关卡；后续变动在整合main继续。旧 Gaming 使用者须同步新版场景和两份存档，不可用策划原始旧文件覆盖本地修复；仅当前整合窗口合并 main，其他成员只拉取和推送任务分支。完整约定见 [正式关卡交接](Integration/FORMAL_LEVEL_DELIVERY.md)。
当前唯一标准玩家为 `Assets/_Project/Content/Player/Player.prefab`（1×1、中心归零），各场景共用；旧RuntimePlayer/Player(暂定)已退役。同色第一把钥匙只解锁场景物体，第二把授予玩家颜色能力，两种状态独立、房间重置保留。接入必读 `Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。
保留：启动与多场景生命周期、场景导航、基础玩家控制、输入与重绑定、双端 UI 预设、相机 Manager/跟随/模式转换、成就、Timeline 演出/战斗、表面瓦片工具、方块功能组件契约、不规则精灵切片。
相机转换不再由 PlayerInputDriver 申请；Tab 的调色动作只控制染色轮盘，F 负责交互目标与搬运，Q/E 负责左右放下，左键负责已选颜色的染色，R / 手柄右摇杆对应“重生”，存活或死亡均可返回最近触碰的锚点并恢复相关房间/通道。电脑端由 `DesktopControlHintOverlay` 在 `WordNote_UI` 同一 Canvas 左上角显示当前可操作按键，宽度不超过安全区 `1/6`，按状态和运行时映射刷新；手机不显示。门、按钮、已拾取钥匙及解锁进度保留。能源方块和万能方块共用 CarryableFeature；输入仍只通过 PlayerInputDriver 调用 PlayerController，Timeline 优先级不变。

门按钮默认一次按下保持，持续压住模式则在松开、R 或离区时重置，取消持续信号会关门但保留其他保持按钮进度。三色接触方块/可搬运方块禁止跨布置区域迁移，跨区放下被拒绝、越界回到作者位置；玩家离区取消携带并复原来源区。
Timeline 现提供独立对话轨道：轨道不绑定对象，直接向常驻 `WordNote_UI` 申请使用权，与原 API 消息共用同一画布、位置队列、堆叠和动画系统。Timeline 只负责申请和释放本次消息，不接管玩家、颜色系统或房间状态。
Boss 入场 Timeline 只保留 `PlayerTimelineTrack` 和 Boss 专用 `BossAppearCinematicPlayable`，旧相机/Boss 走跳/对话轨已从轨道列表移除。玩家先被锁 1 秒，故障字幕与问号打字机播放期间相机固定在玩家初始位置；Boss 在玩家当前位置 `X-7 / Y+13` 生成，未开启追迹，由 `BossController` 固定 X/Z 并以可调加速度下移伪装掉落，直到 Y 与玩家完全一致后锁定演示位置，播放“不好！”并开启追击、血条、监控与 `BossCameraSize`。Boss 自然死亡与处决死亡共用 `BossDeathCinematicPlayable`：自然死亡先传送玩家到最近已激活重生点并关闭 Boss 接触击杀，再从 Boss 故障闪烁进入两句 Boss 故障字幕、玩家闪烁、六钥匙竖列、玩家打字机字幕、钥匙吸入、最终 Boss 故障字幕、死亡动画与删除 Boss；处决从开局即接管 Boss，镜头进度一半时以 `10` 格/秒右移，任意正伤害直接进入同一故障闪烁节点。Boss 删除前会先启动独立场景级 Player Thinking Director，随后进入真假结局；普通外部删除不触发。Boss 死亡六钥匙、Player Thinking 六份收集品与喷泉结局四份调色盘都必须通过 `MechanismArtworkPresentation.PrepareCinematicClone` 使用当前最新物体美术，禁止直接禁用美术准备链后显示预制体旧占位图。
Boss死亡表现新增可调RGB溶解，临时碎片保持死亡世界位姿，不随Boss移动；保留最新演出美术准备入口。
`BossAnimationDriver.PlayDeathAnimationNow()` 在死亡演出最后阶段启动三阶段 RGB 网格溶解：完整原画面、红绿蓝方块横向分解、方块逐渐消失；速度和横向分散可在 Inspector 调整。独立预览场景 `Assets/ta_source/Boss_Die/BossDeathPreview.unity` 在 Play Mode 自动循环。
重生点组件可直接标记为 Boss 关卡重生点，并保存复活后的 Boss 血量与相对 X。复活链在房间重置前建立 Boss 待应用请求，房间重建完成后再让 Boss 归位；归位前先销毁 Boss 左边缘到 Boss 当前 X 范围内相交的普通/重力岩浆。Boss 由 Timeline 后生成，不建立启动时引用：控制器启用时自注册，重生点先保存待应用状态，Boss 出现后自动应用并回读血量/坐标。Boss 存活期间通过玩家疾跑租约禁用疾跑，Boss 死亡或禁用后恢复。关卡参数窗口生成通过稳定 ID 连接的规划标记格，标记格只允许 X 拖动。实际 Scene 附属标记无实体，删除后自动补建。
Boss 战复活另有兜底：存在活动 Boss 且当前没有已解锁的 Boss 出生点时，只在玩家死亡所属房间内选择 X 最小的启用 Boss 出生点；不能因最终 Gaming 含有 Boss 就把其他房间的复活劫持过去。无房间管理器的独立旧工作场景保留同场景兜底。剧情 Start/End 返回绑定专用普通重生兜底，不再被旧 Boss 点覆盖。
低收集 Boss 入口使用 `Content/Boss/Timelines/enter_boss.playable`：正式 Gaming 的 `Boss_box_aa7a24dc` 已注入该 Timeline，同物体 `BossCollectibleEntryGate.gateEnabled=false`；低于 5% 时锁玩家并播放两段打字机字幕，收黑传送到同场景 Start 后揭幕，达到 5% 时 Timeline 无副作用直接结束。该触发器不能同时启用 `BossCollectibleEntryGate`，否则会在 Timeline 播放前拒绝。
Boss HUD 使用 `BossDisplayName` 在分段血条左侧显示 Cubic_11 SDF 名字；同 Canvas 的 `BossMonitorUI` 通过独立正交相机和 RenderTexture 在屏幕左上角显示约 `23.3%×35%` 屏幕的实时 Boss 窗口，从中心展开、倒放退场，并在死亡延迟 5 秒后与血条同步隐藏。监控相机渲染除 UI 外全部图层，窗口比例、纹理尺寸和渲染分辨率均按 Canvas 实际尺寸自适应。
Boss 从中心向玩家连接火线、水线、藤蔓线三套现有样式；三份独立运行时材质分别设置 `_Style=1/2/3`。距离越近越粗越明显，最近达到普通线条 1.5 倍。连线随血条激活生成，藤蔓沿线生长，Boss 死亡时按交互线同样的噪波溶解退场。Boss 接触玩家会直接触发玩家死亡。

`Assets/_Project/Content/GameFlow/Resources/story_start.playable` 是独立开场演出资产。新档点击“开始游戏”时先在菜单上通过 `GameplayEntryCircleCover` 闭合全屏圆形黑幕，完全遮黑后才启动场景运镜；从全黑起等待 `2` 秒，再由 `StoryStartEntryHandoff` 以已遮黑模式接入 `story_start`。随后以 `1.4×` 镜头显示中央打字机分段文字，台词到时直接消失；玩家以当前位置为基准左移 16 格，圆形黑幕从中心散开时以 Run 动画走回画面中心，返程速度 `2.6666667` 格/秒。没有有效运行存档时由 `RunSessionCoordinator` 自动播放，已有存档不播放。

基础迁移时排除：绳/线、梯子、移动平台玩法、视差平台投影碰撞/深度吸附、2D小拼/总拼/3D合并、旧地图代理同步、旧关卡、旧测试脏数据、美术/音效素材、瓦片库与烘焙贴画、QQ bot 运维源码和私密配置。之后由音频成员在新项目单独交付的 `Assets/_Project/Content/Audio` 不属于“旧素材迁移”，已纳入当前仓库。
成就中的旧玩法信号名字保留仅为兼容枚举/协议，不代表玩法可用。不要重新引入对应实现。
TapTap Unity 成就 SDK 固定使用官方 NPM 当前可用的 Core/Login/Achievement `4.10.9`，配置与运行桥位于成就系统 Runtime；后台 Client ID、Client Token 和成就 ID 未确认时配置关闭并保持安全待机。Android 只允许 TapTap 必需的 `INTERNET`、`ACCESS_NETWORK_STATE`，且原生隐私同意前不得启动 Unity/TapSDK。
暂停界面的存档子页已封存为只读展示：不再提供手动保存、读取、删除或槽位列表，只显示 Cubic 字体“待开发内容，请关注后续消息”；旧页面脚本备份在 `Docs/SavePanelController.Legacy.cs.txt`。
单轮运行存档已由 `RunSaveService` 落到 `Saves/run.json`，由 `RunSessionCoordinator` 在开始游戏时自动新建或恢复当前轮；读取已有存档会先将标准玩家的 RGB 故障死亡层拉满，再在故障遮屏期间恢复当前重生点、收集品、颜色钥匙/能力和 Boss 进度，并按玩家预制体的复活淡出时长揭示。读档不经过通用加载蒙屏或“加载中”流程。
暂停轮盘存档页提供单一“删除当前存档”按钮：无确认和额外特效，直接调用 `RunSessionCoordinator` 删除本轮运行文件、重置本轮收集/颜色/Boss/重生点并将玩家恢复到本轮初始位置，然后返回开始界面；成就、设置、按键映射和触控布局不删除。
例外：关卡编辑器开发目录中的 `TileLibrary/26TAPTAP.png` 是用户明确要求恢复的源图，只允许用于该开发工具，不扩散到正式美术目录。

## 目录约定

- `Assets/_Project/Code/Systems/<Feature>`：功能源码，Runtime/Editor/Tests 分开。
- `Assets/Player`：基础玩家及 Timeline 动作适配。
- `Assets/GJ_Tools/TimelineTools3D`：保留的旧 Timeline 工具（兼容路径）。
- `Assets/_Project/Scenes/Start/MainMenu.unity`：仅旧编辑器兼容入口，新美术界面预览，运行重定向 Gaming，不进入正式构建。
- `Assets/_Project/Scenes/Playing/Gaming.unity`：唯一正式启动与玩法场景，持有玩家、玩法相机和共享暂停 UI；启动前帧自动安装共享 `Resources/UI/WorldMainMenu.prefab`。
- 正式流程为同一 Gaming 世界内的 MainMenu/Gaming 两个逻辑态。开始、返回、再次开始都不加载或卸载场景，不重置进度；相机通过同一 Manager 租约平滑入退。Bootstrap、旧五个 Systems 场景、旧 `Flow` 目录、Level_01～03 和 Ending 均退役，不再恢复。
- `Assets/_Project/Development/<Feature>/<AuthorOrTask>`：独立实验；不默认加入正式构建。
- `Assets/_Project/Content`：正式流程的 prefab、catalog 和配置。
- `Assets/_Project/Content/Audio/Music` 与 `SFX`：游戏实际使用的音频及其 `.meta`；音频成员按 `.agents/skills/taptap-audio-worker/SKILL.md` 交付。
- `Assets/_Project/Art/ArtSource`：未来导入的原始美术。
- `Assets/_Project/Art/Generated`：工具产物；生成后检查并提交依赖。
- `Docs/Agent`：agent 的统一交流、约定和状态；每次重新读取。
- `Docs/Agent/Integration`：面向 agent 的接入说明。
- `Docs/Developer`：面向开发者/策划的使用说明。

Unity 模板的 `SampleScene` 暂保留为模板参考，不属于正式流程。
