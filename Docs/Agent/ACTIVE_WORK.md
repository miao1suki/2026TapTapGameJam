# 工作状态与交接（每次任务重读）

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

## Interactive Water 迁移包

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：codex/ta/water-platform-demo。
- 范围：仅导入 Assets/ta_source/InteractiveWater，包含独立预制体、脚本、材质、Shader Graph、网格、渲染纹理和 Renderer2D 配置；不修改现有场景、ProjectSettings 或 URP Pipeline Asset。
- 接口变更：新增 InteractiveWater 水面资源；需要在目标项目配置 WaterTopMesh/WaterFrontMesh Sorting Layers，并将 Renderer2D 加入 URP Renderer List、指定给水面相机。
- 验证：迁移包静态检查包含 50 个文件且无目标文件冲突；Unity 导入及运行待验证。
- 交接：水面资源及其接入配置随 `codex/ta/water-platform-demo` 分支提交推送；Unity 导入与运行待复验。


## Interactive Water 横截面透明修复

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：codex/ta/water-platform-demo。
- 范围：修复迁移后水面横截面的渲染配置；编辑 ProjectSettings/TagManager.asset、PC/Mobile URP Renderer 列表以及水面导入说明。
- 场景：暂不编辑场景，等待确认正在使用的相机。
- 接口：补充 WaterTopMesh/WaterFrontMesh Sorting Layers，并注册迁移包的 Renderer2D；保留现有默认 Universal Renderer。
- 状态：已完成资源与渲染配置的静态修复；Unity 导入、场景视觉效果和运行仍待确认。

- 补充范围：Level_01 使用常驻 Universal Renderer 主相机；调整导入水面的 FrontMesh Shader Graph 与材质，并为 Mobile RPAsset 启用 Opaque Texture，以在现有主相机下实现半透明横截面。


## Interactive Water 多实例与水下重着色

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：codex/ta/water-platform-demo。
- 范围：仅调整 Assets/ta_source/InteractiveWater 的运行时资源隔离、水下画面捕获和关联 URP Renderer Feature；不覆盖 Level_01 场景中现有改动。
- 场景：Level_01 由用户编辑，本任务不写该场景。
- 接口：每个水面持有自己的波纹/反射纹理与材质；水下捕获由 Universal Renderer 的独立 pass 提供。
- 状态：代码和 URP Renderer Feature 已写入；默认捕获除 Water/UI 外的可见物体。编译、运行与视觉效果仍待 Unity 验证。
- 交接：水面资源及其接入配置随 `codex/ta/water-platform-demo` 分支提交推送。


## 藤蔓 Trail 颜色贴图

- 日期：2026-10-09。
- 负责人：Codex。
- 分支：`codex/ta/water-platform-demo`。
- 范围：`Assets/ta_source/Particle/Grow/Vine_Trail_Color.png` 纯绿色深浅变化纹理及 Unity 导入设置；不修改场景。
- 接口变更：无；贴图采用双向 Repeat、双线性过滤和 mipmaps，Trail 负责藤蔓形状。
- 验证：确认 Unity 版本 `6000.3.12f1`、仓库远端正确、PNG 已生成并复制；Unity 导入效果待编辑器确认。
- 交接提交：随本次分支提交。

## Grow 粒子效果资源

- 日期：2026-10-09。
- 负责人：Codex（推送）；场景、预制体与材质由用户完成。
- 分支：`codex/ta/water-platform-demo`。
- 范围：推送 `Assets/ta_source/Particle/Grow` 下的场景、粒子预制体、材质与藤蔓颜色贴图及其 `.meta`；不包含叶片资源。
- 接口变更：无。
- 验证：Unity 版本与仓库远端已确认；Unity 编辑器导入及场景运行待验证。
- 交接提交：随本次分支提交。
## 工作区资源与渲染配置推送

- 日期：2026-10-09。
- 负责人：Codex（按用户要求整理并推送当前工作区改动）。
- 分支：`codex/ta/water-platform-demo`。
- 范围：`Assets/ta_source` 新增资源、PC Renderer 与 TagManager 配置、颜色材质、关卡编辑器 TileLibrary、Level_01 场景、`Assets/_Recovery` 场景，以及项目级 `.vscode` 配置。
- 接口变更：无代码接口变更；渲染配置加入水下捕获和 Stencil Pixel Effect Renderer Features。
- 验证：Unity `6000.3.12f1`；暂存差异与资源引用待提交前核对；Unity 导入及场景运行未验证。
- 交接提交：随本次分支提交。
