# 工作状态与交接（每次任务重读）

## 音频资源协作规范

- 日期：2026-10-04。
- 负责人：Codex。
- 范围：新增 `.agents/skills/taptap-audio-worker/SKILL.md`；音频成员沿用通用 Git 分支流程，只交付游戏音频及 `.meta`，音频系统接线交给整合者；补充常见音频格式的二进制属性。
- 存储：当前使用普通 Git；大型或频繁修改的大文件先协调 LFS，不由分支工作者单独启用。
- 验证：skill 格式与 Git 补丁检查；不修改 Runtime、Scene 或现有未提交资源。

## 可变色方块与颜色工作台

- 日期：2026-10-03。
- 负责人：Codex。
- 分支：`codex/color-blocks`。
- 范围：新项目的 RGB 可变色方块、颜色钥匙、Timeline 相机演出、局部 HSV 褪色、UI Toolkit 工作台及交互图设计数据。
- 接口变更：新增 `ColorWorldManager`、`ColorBlock`、`HSVColorFadeManager` 与 EventMgr 颜色事件。混色规则暂未执行。
- 验证：Unity 6000.3.12f1 脚本与 Shader 导入无错误；UI Toolkit 工作台已在编辑器中打开检查；主菜单 Play Mode 冒烟通过，未见 RenderGraph 异常。带颜色方块的拾取与屏幕渐变场景仍需关卡内实测。
- 交接提交：`codex/color-blocks` 本次提交；仅整合本模块，不包含当前测试场景或他人的瓦片库脏数据。

## 基础迁移

- 工作分支：`codex/migrate-foundation`。
- 范围：筛选迁移基础系统、UI规范与git技能；不维护旧项目。
- 对话：用户已确认尚未开发，实际完成的是成就系统。
- 资源：不迁移旧美术、瓦片库、切片配方、烘焙产物、旧测试场景。
- 当前验证状态：基础迁移已完成；编译、46项EditMode、PlayMode基础流程与移动跳跃、Windows开发版构建通过。详见 `Docs/Agent/MIGRATION_REPORT.md`。

## 新任务记录格式

追加独立条目：日期、负责人、分支、功能/场景范围、接口变更、验证结果、交接提交。
不要删除/改写其他成员正在进行的工作。

## 基础工具与相机 Timeline 范本

- 日期：2026-10-02。
- 负责人：orpheus0829。
- 分支：`codex/orpheus0829/basic-tools`。
- 范围：恢复 `Assets/GJ_Tools/BasicTools`；在 `SampleScene` 增加相机管理、TimelineCamRig 与示例玩家组件；为 `CameraTimelineClip` Inspector 的 2D/3D 相关参数增加可折叠视觉分区；新增 Timeline 示例资产与 TimelineSettings。
- 接口变更：运行时接口无变更；Timeline 相机 Inspector 仅调整显示分组。
- 验证：补丁静态检查通过；Unity 全量编译和 PlayMode 验证未在本轮完成，交由整合者复验。
- 交接提交：本条目随当前任务提交。

## 玩家输入驱动与 2D / 3D 视角申请

- 日期：2026-10-03。
- 负责人：orpheus0829。
- 分支：`codex/orpheus0829/第一次搭建组件`。
- 范围：新增 `IPlayerDriver` 与 `PlayerInputDriver`；输入、动作绑定、交互触发和 CameraModeSwitch 从 PlayerController 移到 driver；PlayerController 只保留物理与动作能力。同步输入 fallback、玩家 Prefab、SampleScene 和 RoyTestScene 的序列化引用；玩家相关组件新增 UI Toolkit 中文 Inspector 与 Foldout 分区。
- 接口变更：`PlayerController` 提供 `SetMoveInput`、`SetSprintInput`、`RequestJump`、`ClearBufferedInput` 等马命令；`PlayerInputDriver` 作为 `GameplayAbility` requester，Timeline `Cutscene` 仍可压过。
- 后续调整：`PlayerController` 增加起步加速度、停止减速度和加速度响应曲线；曲线横轴为当前速度/目标速度，纵轴为加速度倍率。
- 后续调整：PlayerController Inspector 按“移动 / 跳跃 / 动作与控制”分区，只读调试信息按对应动作分别显示。
- 后续调整：跳跃细分调参：上升重力、松键重力、下落重力、最大下落速度；调试显示重力模式、跳跃键、离地时间、速度与缓冲。
- 后续调整：玩家 Capsule 在运行时使用零摩擦物理材质；横向贴墙时只停止水平接触，不再通过碰撞摩擦连带损失竖直速度。
- 验证：补丁静态检查通过；Unity 全量编译和 PlayMode 验证未在本轮完成。
- 交接提交：待提交。

## 2D 关卡编辑器原型

- 日期：2026-10-03。
- 负责人：orpheus0829。
- 分支：`codex/orpheus0829/第一次搭建组件`。
- 范围：`Assets/_Project/Development/LevelEditor/Orpheus0829` 的 2D 方格编辑器、方块栏目、贴画、组件模板和场景生成工具。
- 编辑器：可停靠 EditorWindow、选择/绘制/擦除、玩家参考与 XY 平面锁定、父物体选取；进入编辑模式时先聚焦玩家，再锁定正交视图。
- 栏目：支持缩放、新增、编辑、删除、缩略图、颜色与贴画；分为“自己编辑 / 直接使用预制体”，预制体模式不套颜色、贴画或组件模板。
- 组件模板：支持搜索添加组件、Foldout 折叠、默认组件保护、参数/状态/操作分区、最近事件、运行时 SerializedObject 清理和保存前校验。
- 类约束：`BlockFeature` 必须在声明首项填写中文 `DisplayName`；脚本名和 Unity 组件名不变，编辑器内容首行用粗体大号显示翻译。
- 方块功能：独立 `BlockFeatures` 系统提供 Feature 生命周期、能力查询、信号、命令、联动、表达、阶段调度、通道和编辑器校验。
- 动力组件：参数为“开关 / 信号源”角色与“单次 / 持续 / 脉冲”信号类型；开关提供交互开关接口，信号源放置后自动输出，支持格数传播范围和可视化调试。
- 待施工：动力容量、输入输出、消耗、Port/Link 供电连接、优先级、仲裁和完整表现。
- 验证：补丁静态检查、关卡编辑器与 BlockFeatures Runtime/Editor 代码编译通过；Unity 窗口交互和 PlayMode 仍需实机验证。
- 交接提交：待提交。

## 融合关卡编辑器迁移

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：`codex/orpheus0829/关卡编辑器收尾`。
- 范围：把 test 项目完成的关卡规划融合编辑器迁入 `Assets/_Project/Development/LevelEditor/Orpheus0829`，统一为一个菜单入口，并同步道具栏目、贴画预览、通行连通和存档能力。
- 数据规则：保留比赛项目现有 6 个方块栏目；道具栏目迁移为空栏目，不迁入 test 的临时道具、预制体或存档；内置示例地图随代码迁移，作为存档面板的只读默认项。
- 入口：`Tools/2026TapTap/关卡规划原型/打开融合编辑器`。仅隐藏旧关卡编辑器与旧“重建默认瓦片库”入口，其他系统的菜单入口保留。
- 存档：本地存档位于 `Library/PlanningEditorSaves`，可新建空存档、另存、读取、重命名和导出 JSON。
- 后续调整：移除手动“房间父物体”入口，改为按房间名/通道名自动收纳到 `__PlanningMapGenerated` 下的同名子物体。
- 后续调整：房间/通道详情聚焦改为寻找方块最密集区域，并按内容包围盒自动计算可见缩放。
- 验证：使用比赛项目 `6000.3.12` 的编译响应文件通过 Runtime 与主 Editor 静态编译；受影响 asmdef 中除既有相机编辑器缓存引用外均通过。
- 交接提交：待提交。

## 开场主题音乐导入

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：\codex/codex/taptap-music\。
- 范围：导入开场/主题音乐 \2026 GJ WN OP.mp3\ 到 \Assets/_Project/Content/Audio/Music/\。
- 接口变更：无（仅音频资源导入）。
- 验证：文件已复制到正确目录，Git 暂存并提交。
- 交接提交：\34cf18\。

## 音频资源整体替换（第二版）

- 日期：2026-10-09。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：替换 `Assets/_Project/Content/Audio/` 下的音乐与音效；移除旧版 14 个音频，导入新版 14 个 BGM、22 个 SFX（玩家动作、上水交互、上色流程、机关循环、拒绝提示）。
- 接口变更：无（资源替换）。旧 `sfx_ui_button_select`、`sfx_ui_notice_success_*`、`sfx_gp_water_enter_01`、`sfx_gp_water_splash_*` 已移除，接入方需同步更新引用。
- 文档：同步更新 `Docs/Agent/Integration/AUDIO_HANDOVER.md` 资源清单与接线表。
- 验证：文件按 Music / SFX 目录归类；Git 审查确认仅音频资源与交接文档变更；本机未运行 Unity 导入与试听。
- 交接提交：本条目随本次提交。

## 音频资源第三批（环境声与机关音效）

- 日期：2026-10-10。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：新增 4 个环境声（`amb_env_machine_hum_01`、`amb_env_plant_rustle_01`、`amb_env_water_drip_01`、`amb_env_wind_gust_01`，暂存于 `Music/`）与 6 个音效（开关门、植物生长、蒸汽喷发与循环）；`sfx_gp_paint_acquire_initial.wav` 为同路径修订。
- 接口变更：无。新增音频提交为 `ee2f0d3`，本次提交只补记文档。
- 文档：`Docs/Agent/Integration/AUDIO_HANDOVER.md` 增补环境声分区、蒸汽与开关门接线项。
- 验证：Ableton 导出目录与仓库 45 个音频逐文件 SHA256 一致；未在 Unity 中导入试听，`.meta` 待首次导入生成。
- 交接提交：本次文档提交。
