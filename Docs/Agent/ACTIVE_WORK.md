# 工作状态与交接（每次任务重读）

## 当前功能与协作分支整合（进行中）

- 日期：2026-10-05；负责人：Codex；目标：整合本地已验收的颜色／关卡编辑进度、`Test` 场景、TA 水效果分支和音频分支至 `main` 并推送。
- 边界：保留当前窗口项目状态；未引用的 `ColorPickup_blue 1.playable` 副本和与本次功能无关的 `SampleScene` 删除不纳入提交。音频首次导入须补齐 Unity `.meta`，不代替音频成员做玩法接线。
- 验证／交接：待合并后补充。

## Test 开场与颜色初始外观修正

- 日期：2026-10-05；负责人：Codex；分支：`codex/color-editor-link`，本地验收前不推送。
- 范围：仅修正 `Test` 场景玩家 Director 的自动播放、RGB 方块未解锁时的白色基础材质、三色钥匙在 Game 中的初始颜色；不改其他成员的场景或 Timeline 资源。
- 接口：保持 `ColorBlock` 基础/当前颜色状态与 HSV 表现分离；编辑识别材质仅用于编辑态。未解锁时基础网格使用白色中性材质，解锁后由正式视觉接替；钥匙始终保持预制体的对应颜色材质。
- 验证/交接：Runtime 与 Editor 程序集顺序编译均 0 错误/0 警告；Unity Play Mode 中 `TestPlayer` 的 Director 保持 0 秒、未自动运镜，未解锁蓝块在运行时显示白色黑边基础网格，Console 0 错误/0 警告。三色钥匙预制体材质及其颜色值已静态核对；用户后来在 `Test` 场景重新放置并保存蓝色钥匙，且确认可见。本次随场景提交；其他颜色钥匙仍需实际关卡验收。

## 颜色解锁演出与编辑器预览修正

- 日期：2026-10-05；负责人：Codex；分支：`codex/color-editor-link`，本地验收前不推送。
- 范围：RGB 方块仅在编辑态使用识别材质，Game 中未解锁时显示白色基础材质；解锁后蓝色使用正式水效果、红绿在正式外观接入前隐藏；蓝水随解锁渐显；颜色钥匙触发拉远—恢复—更快返回的相机演出；关卡编辑器 RGB 预览与三色钥匙道具栏目。
- 接口：新增 `ICameraLensOverrideSource`，仅当前相机控制源可覆盖镜头大小，投影类型仍由模式权威决定；`TimelineCamRig` 提供镜头尺寸与单次返回时长；`InteractiveWater.SetReveal` 随 HSV 进度控制正面透明度和顶面展开。
- 验证/交接：相机 Runtime、主 Runtime/Editor、相机 EditorTests 程序集静态编译均 0 错误/0 警告；受本任务影响的已跟踪文件 `git diff --check` 通过。Unity Play Mode 已检查 Test 场景：RGB 编辑识别网格在运行时隐藏；蓝色解锁后只出现 TA 水效果；红绿解锁后没有出现调试材质。临时红色钥匙拾取测试触发了拉远再返回的 Timeline 镜头动画，Console 0 错误/0 警告。保留用户另建的 Timeline 和其他成员资源，不推送。

## Test 场景旧红绿方块替换

- 日期：2026-10-05；负责人：Codex；分支：`codex/color-editor-link`，本地验收前不推送。
- 范围：仅 `Assets/Scenes/Test.unity`，把 16 个旧“能源方块”实例原位替换为标准 `ColorBlock_red` 预制体，保留父级、名称、姿态和网格占位。14 个绿色实例已是标准 `ColorBlock_green`，不做无意义重建；白色房地基与其他场景保持原样。
- 验证/交接：场景序列化中标准红/绿预制体实例分别为 16/14，旧红色能源预制体实例为 0；Play Mode 解锁红绿后仍不显示编辑识别材质，碰撞和颜色状态由标准 `ColorBlock` 保留。一次性迁移脚本已移除；Test 场景仍为本地未跟踪文件，不推送。

## Test 场景玩家可见外形

- 日期：2026-10-04；负责人：Codex；分支：`codex/color-editor-link`，本地验收前不推送。
- 范围：仅为 `Assets/Scenes/Test.unity` 的 `TestPlayer` 增加无碰撞的测试外形与专用材质；不改玩家控制与正式玩家预制体。
- 验证/交接：`TestPlayer` 下新增 `Visual` 胶囊网格，局部中心/尺寸与原 CapsuleCollider 对齐，不含额外碰撞体；专用 URP Lit 琥珀色材质保存在 `Assets/Scenes/TestPlayerVisual.mat`。Unity 的 Game 视图已可见，场景已保存；一次性编辑脚本已移除。仅本地修改，不推送。

## 颜色方块正式化、水效果与多格合并

- 日期：2026-10-04；负责人：Codex；分支：`codex/color-editor-link`，未获本地验收前不推送。
- 范围：移除旧自由文字节点及对话式 UI 文案；RGB 编辑态/运行态材质与层；一次性修复当前场景中的颜色方块；多选合并编辑；玩家游泳状态；整合 TA 的独立水效果提交 `adce5df`，使蓝水覆盖整个单元方块。
- 场景：`RoyTestScene` 的 2031 个旧 RGB 基础方块已一次性换成颜色预制体，黑色/能源方块保持原样；`Assets/Scenes/Test.unity` 为用户未跟踪场景，未保存或改写。
- 接口：颜色状态仍由 ColorWorldManager 持有；玩家游泳由 PlayerController 持有；水视觉封装在颜色模块，不让交互图直接改 BlockRuntime 状态。
- 验证/交接：Unity 6000.3.12f1 导入和 Runtime/Editor C# 编译均通过；Inspector 确认蓝色预制体实例在 `Color_blue` 层且编辑态显示目标材质。Play Mode 通过主菜单→第一关流程。临时 5×3×2 蓝块在解锁并完成褪色恢复后生成 1 个 TA 水视觉实例；玩家进入水区为 `Swimming`、离开后恢复 `Normal`，涟漪更新无异常。临时测试脚本已移除。游泳手感和 Scene 框选/合并仍需在关卡中人工验收。仅本地分支，不推送。

## 颜色层褪色与交互图基础节点

- 日期：2026-10-04。
- 负责人：Codex；本地分支：`codex/color-editor-link`，按用户约定完成本地测试前不推送。
- 范围：ColorBlocks 的按层配置/调试、交互图基础节点与有限运行时执行；不改用户未提交的测试 Scene、瓦片库或颜色目录资产。
- 接口：复用现有 ColorWorldManager、HSVColorFadeManager 与 BlockFeatures 契约，不接管玩家状态机或 Timeline 所有权。
- 验证：`Assembly-CSharp` 与 `Assembly-CSharp-Editor` 静态编译均 0 错误/0 警告；Unity 6000.3.12f1 编辑器脚本重载未见编译异常。未改用户未提交 Scene、瓦片库或颜色目录；交互节点与局部褪色仍需在用户测试关卡 Play Mode 实测。
- 交接：此条为先前阶段记录；旧自由文字节点已由上方任务移除，策划需保存 Project 资源后测试新连线。

## 颜色方块与关卡编辑器联动

- 日期：2026-10-04。
- 负责人：Codex。
- 本地分支：`codex/color-editor-link`，用户确认完成前不推送。
- 范围：关卡编辑器 RGB 栏目改为颜色系统的直接预制体；覆盖 Scene 绘制与规划图场景生成，保留能源方块及其他栏目。
- 场景：不编辑用户当前未提交的测试 Scene；只改栏目资源、必要的编辑器逻辑与说明。
- 验证：Unity 6000.3.12f1 导入及 Runtime/Editor 脚本编译通过；RGB 栏目引用的 prefab GUID、根物体、`BaseColorTypeId` 与颜色目录 ID 静态核对通过；Git 补丁检查通过。未在用户未保存的测试场景中执行放置或 Play Mode，待本地验收。
- 交接：此条为先前阶段记录；`RoyTestScene` 的旧 RGB 方块已由上方任务完成一次性转换，其他普通方块不自动转换。

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
