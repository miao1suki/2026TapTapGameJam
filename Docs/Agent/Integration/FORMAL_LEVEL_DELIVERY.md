# 正式关卡交接与同步

## 当前交付基线

2026-10-20 全关卡合图：4343 个实际作者物体、28 个区域；采用最新 Gaming、R1/R2/R3 的最终 Scene、用户保存的 MRoom2/MRoom3 及 Boss。只平移各关，按已确认接口补通道，不从陈旧规划重建作者场景。MRoom3 新增三格保留。原 Gaming 房间21已按用户保存规划移到暂存位置，未删除。正式 Scene、同名规划、正式关卡 JSON 同步；最终场景回采资源及六份可导入片段位于 `Assets/_Project/Content/LevelMaps/AuthoredSections`，必须整套提交，禁止用旧 R 规划覆盖。详见 `Docs/Developer/COMBINED_WORLD_0_2_1.md`。

2026-10-20 远端优先更新：接收 xianyu66web `5e88e7c` 的 21 房间、2604 原始物体，所有作者参数/位姿保留，另补隐形 Start 后 2605 条。Scene、同名规划、正式命名存档三份规划一致；保留 main 原有 Gaming_Art 层级和美术资源，没有合入 TA `f7cbb62` 新 Gaming。R2 接收作者 `d3fac6a`，R1/R3 仍为最终版本，三关手调规划回采限制不变，不能用陈旧 JSON 重建。详见 `Docs/Developer/REMOTE_LEVEL_RECEIPT_2026-10-20.md`。以下 20 房间数量为历史基线。

2026-10-19 用户明确采用策划MVMap17（b2d0e18）：20房间、1406原物体，增量补新玩家出生位置的隐形Start后1407条。保留main的代码、美术、菜单、字体、物理和Boss门槛修复；R1/R3及TA预览继续独立，不加入正式构建。全部成员先同步最新main并从磁盘重开Gaming，禁止旧内存覆盖。详见MVMAP17_ROOM_SPLIT.md及Docs/Developer/BRANCH_INTEGRATION_2026-10-19.md；以下条目为历史基线，不再表示当前地图数量。

2026-10-18 收集/Boss接线增量：保留20房间地图全部837条原物体及精确位姿，只新增正式玩家出生处的隐形`Start`锚点（现838条）。Gaming同名planning与正式命名存档同步；Boss独立工作场景的原入场触发器仅补30%门槛模板，位置/Timeline不变。所有工作场景最终导入同一Gaming，Start/End不做跨场景加载。详见`Docs/Developer/BOSS_START_AUTHORING_PATCH.md`。同步后关闭旧编辑窗口，从磁盘重新打开新版；不得把旧内存Scene/planning保存覆盖。

2026-10-18 紧急双端包更新：当前地图采用 `origin/codex/codex/gaming-camera-size-8` 的 `56126c3`（20房间，837物体，5743作者占格），仅接入其Gaming Scene、同名planning与命名正式存档及Map14说明。新版菜单、同场景开始/返回及相机动画采用本地整合实现，不从地图分支覆盖代码/UI。相机普通视野size=8沿用地图交付。三份JSON一致，相关meta GUID保持；7个未绑定按钮与部分信号保持策划原样、见MVMAP14_LAYOUT.md。所有作者之后同步此新版Gaming与存档；正式合并仍仅由当前用户整合窗口执行。交付Scene务必同时提交同名planning和命名正式存档，不能只传Scene。

2026-10-18 正式关卡增量合入策划分支 `codex/level/main-20261017-03` 的 `eb76949` 交付（17房间，承接此前12房间基线）。房间12已有实际内容，新增13～17房间。三方共同基线已包含此前main确认的修复，代码、美术和运行时仍采用最新main，不从策划分支回退功能。

整合窗口保留用户确认的房间9局部格(50,9)平台去重：独立`box_356e9c89`及其16格路线不变，原批量`box_a778178f`拆成48～49与51两段，各自原4格参数不变。通过三方增量合并保留既有Scene对象/GUID和两份存档，不全量重建。今后同步应取整合后的main，而不是用策划原始旧文件覆盖已修复部分。

必须成套维护以下文件及各自 `.meta`，保留现有 GUID：

- `Assets/_Project/Scenes/Playing/Gaming.unity`：正式打包、游玩及后续关卡修改的唯一玩法场景。
- `Assets/_Project/Scenes/Playing/Gaming.planning.json`：场景同名可编辑规划数据。
- `Assets/_Project/Content/LevelMaps/正式关卡.json`：正式关卡的命名共享存档。

场景内嵌规划、同名规划和命名存档的布局必须一致。不能只交付生成后的 Scene，不能把唯一存档留在 Library。新增栏目、预制体和美术依赖也须一起提交。

正式启动/构建只使用 `Gaming`，同一场景自动叠加纯 UI 菜单，在逻辑 MainMenu/Gaming 状态间平滑运镜，不加载／卸载世界或重置进度。旧 MainMenu 仅为编辑器兼容入口，不加入构建；不启用旧 Bootstrap、旧三关或测试场景。标准玩家仍唯一使用 `Assets/_Project/Content/Player/Player.prefab`；不复制或恢复旧玩家。关卡布局以策划交付为准，不因运行时接线检查而全量重建地图或重置出生点。

## 旧版本成员先同步

所有仍使用旧 Gaming 的成员，必须先取得包含本次整合的最新 `origin/main`，使用上述新版场景和两份存档，再继续正式制作。不能从旧 Scene、旧 JSON、EditorPrefs 或 Library 缓存恢复旧图后覆盖新版。

1. 重新读取根 AGENTS、PROJECT_CONTEXT、WORKING_AGREEMENTS、ACTIVE_WORK、SYSTEM_INDEX 和本文件。
2. 核对唯一远程为 `https://github.com/miao1suki/2026TapTapGameJam.git`，运行 `git fetch origin --prune`。
3. 干净的新任务从最新 `origin/main` 创建自己的 `codex/<成员>/<任务>` 分支。已有分支落后或分叉时，把未交付改动保存在自己的分支并联系当前整合者；不自行合并、rebase、强推或删除他人文件。
4. 使用新 Gaming 及配套存档。Unity 若仍打开旧版场景/关卡编辑器，先保存需要保留的个人内容到独立场景或片段，关闭旧编辑窗口，再从磁盘重新打开新版；不要把旧内存场景保存回正式路径。出现外部更新保护时，先保留冲突备份，再重开新版，不绕过保护。
5. 新版正式关卡已经包含自己的布局，不能用模板场景、测试图或“全量重建”代替同步。

本次“本地正式关卡丢弃、采用策划版”仅是当前整合者收到的一次性指令，不是授权其他成员丢弃本地工作或日后自动覆盖。

## 制作、交付与合并权限

指定关卡策划在自己的分支继续修改新版 Gaming；功能程序默认在独立测试场景验证，需接入正式关卡时保留现有布局、物体参数和绑定关系。多人制作片段时使用编辑器“协作与片段”交付，不并行手工合并大 JSON。

编辑器保存/应用会维护场景同名规划文件。交付前同时更新命名的“正式关卡”共享存档，核对三份布局一致，显式暂存 Scene、两份 JSON、各自 meta 和必要依赖。运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`；该检查保证配对文件存在，不代替三份布局一致性和游戏验收。

其他成员与 agent 只拉取进度、制作并推送自己的分支，交接分支、SHA、修改文件和验证结果。**只有用户当前整合窗口负责合并到 main 并推送 main。** 本文不授权其他人合并、直接推 main、重写历史或自动恢复旧关卡；后续每次合并仍等待用户在整合窗口确认。

策划原交付未执行 Play Mode 或打包；整合验证结果记录在 ACTIVE_WORK 当前条目，不把静态检查表述为完整关卡通关或真机验收。
