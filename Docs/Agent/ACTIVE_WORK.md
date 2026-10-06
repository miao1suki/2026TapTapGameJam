# 工作状态与交接（每次任务重读）

## 物体交互管理器与颜色属性迁移

- 日期：2026-10-05；负责人：Codex；分支：`codex/interaction-manager`。
- 范围：新增物体级 `InteractionObjectDefinition`、`InteractionObjectCatalog`、`InteractionObject` 与 `InteractionManager`；ColorBlock 的接触事件和解锁恢复改由物体图入口执行；新增 UI Toolkit 交互管理器和 GraphView；三色方块自动同步到目录和关卡编辑器，方块/道具栏目均提供入口。
- 节点：玩家进入/离开、物体触碰/停留、经过时间、手动、对象/颜色条件、延迟、褪色、恢复、Timeline、方法调用、设置颜色和日志。
- 边界：移除 `ColorWorldManager`、`ColorInteractionRunner`、颜色交互图、颜色工作台和褪色调试入口；保留改名后的 `ColorRuntimeService` 作为颜色解锁/水体视觉底层服务，以及 `HSVColorFadeManager` 作为渐变服务。
- 资源：目录位于 `Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`，物体定义位于 `Assets/_Project/Content/Interactions/Definitions`。
- 验证：Unity `6000.3.12f1` Runtime/Editor 静态编译 0 错误；`git diff --check` 通过；红/蓝/绿定义、目录引用、三色预制体绑定和关卡栏目引用已静态核对。交互图窗口增删节点/连线、三色方块 PlayMode 接触和窗口视觉仍需在编辑器内点击验收。尚未推送或合并。
- 追加修复：按 Unity 单一 ScriptableObject 类型拆分 `InteractionObjectDefinition` 与 `InteractionObjectCatalog` 脚本并修正资产 GUID，解决交互窗口打开时 `InteractionGraphNode` / `InteractionObjectCatalog` 脚本映射错误；补齐 Unity 6000 GraphView 操作兼容写法、静态调度调用和通用物体非玩家触碰分流。使用当前响应文件（补入新增 Runtime 文件并排除已删除旧颜色脚本）静态编译 Runtime/Editor 均 0 错误。Unity 当前实例尚未重新刷新脚本，需重载后清空旧 Console 历史并验收窗口。
- 钥匙褪色修复：`ColorRuntimeService` 启动时初始化颜色为褪色，注册方块时按解锁状态同步；`Unlock` 先建立基础恢复状态，再触发同色物体图的 `Manual` 节点。三把钥匙定义已补齐基础颜色和对应 `Restore` 节点值，避免只播放相机 Timeline 而不恢复材质或水体。`ColorKeyPickup` 增加物体图的玩家进入入口及兼容接触方法，避免钥匙默认图在运行时输出“未找到可调用方法”。已完成静态资产核对和 Runtime/Editor 0 错误编译；Unity Console、蓝色钥匙收集和水体渐显仍待编辑器内 PlayMode 验收。本次本地提交为 `d48509a`（尚未合并 `main`）；推送因当前主机无法连接 `github.com:443` 待网络恢复后重试。

## 字幕道具慢显隐与缩略图缓存清理

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：把 `ScreenNoteTool` 预制体及关卡编辑器栏目覆盖中的淡入淡出时长从 0.8 秒调整为 1 秒，保留已有 3 秒停留和文字覆盖；字幕淡入淡出同时驱动 CanvasGroup 和 TMP 文本 alpha；清除栏目中会覆盖预制体空 `inCurve/outCurve` 的旧数据；修正栏目缩略图只清外层缓存、未清渲染服务内部缓存导致颜色可能继续显示为白色的问题。
- 验证：Runtime 与 Editor 程序集静态编译均 0 错误；预制体和栏目覆盖时长已核对为 1/1/3；空曲线覆盖残留检查为 0；文字序列化未修改；Git 补丁检查通过。
- 交接提交：待提交。

## 道具组件覆盖面板对齐字幕 Inspector

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：修正关卡编辑器“道具组件数值”中 `SubtitleTrigger` 仍走旧通用字段和无翻译结构的问题。
- 行为：覆盖面板复用字幕组件的“字幕内容 / 字幕样式 / 触发设置”布局、九宫格位置、扩展动画细节、重复激发开关和预览入口；`AnimationCurve` 也纳入覆盖值序列化。
- 验证：Assembly-CSharp-Editor 静态编译 0 错误；待完成 Git 补丁检查和 Unity 窗口交互复验。
- 交接提交：待提交。

## 字幕位置、动画与重复激发扩展

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：扩展 `SubtitleAnchor` 为九宫格位置加自定义位置；扩展字幕动画为淡入横滑、弹性弹出、回弹、旋转、打字机、故障抖动、漂浮和脉冲；`SubtitleTrigger` Inspector 增加“允许重复激发”开关。
- 行为：打字机由 `charactersPerSecond` 控制逐字速度；新动画细节参数集中在“动画细节”折叠区；重复激发开关复用原有 `playOnce` 运行时语义，不改变已验证过的触发逻辑。
- 验证：Runtime 与 Editor 程序集静态编译均 0 错误、0 警告；Git 补丁检查通过；Unity 窗口与 PlayMode 交互仍待复验。
- 交接提交：待提交。

## 主角默认跳跃高度翻倍

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：把主角默认跳跃高度提高到原来的两倍；同步脚本默认值、`RuntimePlayer` 预制体和现有测试场景中的序列化值。
- 行为：只把起跳速度从 `7` 调整为 `7 × √2 ≈ 9.899`，上升重力、松键重力、下落重力、最大下落速度、土狼时间、跳跃缓冲和横向手感保持不变。
- 验证：Runtime 与 Editor 程序集静态编译均 0 错误；玩家预制体和 `SampleScene`、`RoyTestScene`、`Test` 中 4 处 `jumpSpeed` 均已同步为 `9.899`；Git 补丁检查通过。未运行 PlayMode 跳跃实测。
- 交接提交：待提交。

## 纯色栏目缩略图恢复

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：修复受管纯色栏目使用“直接预制体”模式时，缩略图只显示预制体当前白色材质的问题。
- 行为：预览生成仍使用正式预制体结构，但受管颜色栏目会把栏目颜色通过 `MaterialPropertyBlock` 叠加到预览 renderer，不修改预制体资产和游戏运行材质。
- 验证：使用比赛项目 Unity 6000.3.12 的 `Assembly-CSharp-Editor.rsp` 完成静态编译，0 错误；Git 补丁检查通过。
- 交接提交：待提交。

## 网格玩家贴纸与拖拽同步

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：在装配图和房间/通道详情图中增加玩家最高层贴纸，显示玩家缩略图与“玩家”文字；贴纸位置与场景玩家 Transform 双向同步，可直接拖拽移动玩家。
- 行为：玩家贴纸只做覆盖层，不参与方块/道具占位、覆盖、删除、擦除和生成；进入装配图、房间详情或通道详情后常态显示，玩家位置超出当前视口时贴纸钉在对应边缘而不会消失。设置玩家参考时自动贴到 XY 平面并刷新网格显示；聚焦玩家同时聚焦 SceneView 和网格画面。
- 撤回：每次拖动结束时写入一条自定义 Undo 记录；撤回/重做时仅当当前指定玩家仍是该记录绑定的玩家对象时才恢复对应位置，玩家引用已变化则跳过。
- 验证：使用比赛项目 Unity 6000.3.12 的 `Assembly-CSharp-Editor.rsp` 完成静态编译，0 错误、0 警告；Git 补丁检查通过。窗口内的实际拖拽、缩略图异步刷新和 Scene/网格双向同步仍待 Unity 交互复验。
- 交接提交：待提交。

## 道具组件覆盖排除 Transform

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：修正道具组件数值编辑把预览物体的 `Transform` 位置、旋转和缩放保存为覆盖值的问题；生成道具时不再应用任何 `Transform` 覆盖。
- 行为：道具组件编辑窗口只列出和控制实际功能组件，不再显示 `Transform`；已有栏目里的 `Transform` 覆盖会作为无效数据忽略，普通放置与规划生成都在应用其他覆盖后最后写入格子位置。
- 验证：栏目资产已无 `UnityEngine.Transform` 覆盖和预览用的 `10000` 坐标；使用比赛项目 Unity 6000.3.12 的 `Assembly-CSharp-Editor.rsp` 完成静态编译，0 错误；Git 补丁检查通过。
- 交接提交：待提交。

## 规划方块场景引用重新解析

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：修正详情图/装配图选中规划方块后仍可能使用旧 `sceneBlock` 缓存的问题；每次读取选中项时按房间、场景格、方块/道具类型和栏目名称重新解析当前场景实例。
- 行为：手动“定位方块”会选中目标并在 SceneView 中执行一次主动聚焦；场景生成后的自动视角保持本次任务既有行为不变。
- 验证：使用比赛项目 Unity 6000.3.12 的 `Assembly-CSharp-Editor.rsp` 完成静态编译，0 错误；Git 补丁检查通过。
- 交接提交：待提交。

## 生成场景保持编辑视角

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：生成当前房间、当前通道或整套规划场景后，不再自动对生成根节点执行 SceneView 取景；保留生成后的对象选中和场景重绘。
- 边界：不改变手动“聚焦玩家”“定位方块”和进入编辑模式时的主动聚焦行为。
- 验证：使用比赛项目 Unity 6000.3.12 的 `Assembly-CSharp-Editor.rsp` 完成静态编译，0 错误；Git 补丁检查通过。
- 交接提交：待提交。

## 公共自动发现与自制组件 Inspector 整理

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：新增项目级多态发现服务，统一场景组件、具体实现类型与发现标签查询；为项目自制可见组件补齐 UI Toolkit Inspector、中文标签、Foldout 分区、条件显隐与只读调试信息。
- 技能约束：以 `.agents/skills/taptap-editor-ui/SKILL.md` 原有编辑器工具、UI Toolkit、工作流和验收规则为优先；在其后追加补充条款，规定日后新增或修改自制组件时必须提供自定义 Inspector；除脚本名和类名外，标题、字段、分组、枚举、提示、按钮与状态信息全部中文，并统一使用 UI Toolkit、Foldout、条件显隐和运行时只读分区。
- 能力匹配收口：每个 `BlockFeature` 只声明一个 `DefaultColorId`，移除 `RoleTags` 多标签匹配；`BlockAbilityHost` 只按颜色 ID 精确启停组件，不再显示独立“方块角色”维度。
- 运行时状态显示：`BlockAbilityHost` Inspector 只读显示当前颜色、已启用组件和已禁用组件；不再提供手动颜色或启停入口，`BlockFeature` 在编辑模式下强制禁用，只允许运行时按玩法颜色切换。
- Play Mode 测试：`ColorWorldManager` Inspector 提供仅本次运行有效的颜色解锁开关，可逐色解锁、逐色锁回以及全部解锁／全部锁上；测试项全局检测颜色目录，后续新增颜色无需改代码，且不写入场景、目录或存档。
- 验证补充：`SetUnlockedForCurrentSession`、`SetAllUnlockedForCurrentSession` 与 Inspector 测试开关通过 Runtime/Editor 静态编译。
- 游泳纵向规则：水中未按跳跃键时缓慢下沉，按住跳跃键时缓慢上浮；新增上浮速度、下潜速度和垂直游泳加速度参数，水平游泳保持原速。
- 边界：不改变玩法数值、组件启停规则、场景序列化内容或第三方 TA/Timeline Inspector；不提交、不推送。
- 接口：`ProjectDiscovery` 负责返回 `Component` 多态实例与编辑器类型实现；Inspector 辅助层只影响编辑器展示。
- 验证：项目内非测试脚本的 `FindFirstObjectByType`、`FindObjectsByType` 与 `TypeCache.GetTypesDerivedFrom` 已全部收口到 `ProjectDiscovery`。替换时逐处保留原查询的未激活对象包含规则和返回顺序：默认排除未激活对象，原使用 `Include` 的位置显式传 `true`；批量查找与组件查找不再额外排序。使用比赛项目 `6000.3.12` 的现有响应文件补齐新源码，Runtime 与 Editor 静态编译 0 错误；SurfaceTiles/Achievements Editor 全量源码编译 0 错误（仅手动合并程序集时出现既有跨 asmdef 类型重复警告）；`git diff --check` 通过。Unity 当前实例尚未触发脚本刷新，完整编辑器内编译需在编辑器获得焦点后复核。

## 绿色藤蔓攀爬组件

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：新增 `BlockVineFeature`，只实现绿色方块范围内的纵向攀爬与横向脱离；不实现枝条生长、叶片落脚点、根系连接、正式视觉、音效和跳跃脱离。纵向规则为按上方向向上爬、无输入自然下滑、按下方向快速下滑，松键时清除向上残留速度。
- 接口：组件提供 `IBlockClimbSource`；`ColorBlock` 在进入/离开接触时转发给攀爬源；`PlayerController` 新增 `Climbing` 状态、`EnterClimb/ExitClimb` 和攀爬速度/加速度参数。
- 验证：使用比赛项目 `6000.3.12` 响应文件完成 Runtime 与 Editor 静态编译，0 错误；组件内已留 TODO 注释，Editor Inspector 由通用 `BlockFeatureEditor` 自动提供。

## 字幕管理器与触发道具组件

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：新增 `SubtitleManager`、`SubtitleTrigger`、字幕样式/请求数据与 UI Toolkit Inspector；支持中间大字、下方小字、字体、字号、颜色、位置、停留时间和进出动画。
- 接口：`ShowLarge`、`ShowBottom`、`Show(SubtitleCue)`、`Hide`、`HideAll`；`SubtitleTrigger` 只调用管理器，默认隐藏自身及子 Renderer，保留 Collider。
- 解耦：字幕使用独立的 `ScreenSpaceOverlay` 画布；字幕端实现 `ISubtitleService` 并由 `SubtitleServiceRegistry` 提供服务，相机端实现共享的 `ICameraViewportSource`，双方只依赖接口且都可独立不存在。
- 验证：使用比赛项目 `6000.3.12` 响应文件完成 Runtime 与 Editor 静态编译，0 错误。

## 道具预制体组件数值覆盖

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：道具栏目新增“组件数值覆盖”，用于同一预制体的不同配置；道具编辑窗口点击“编辑组件数值”打开独立窗口，组件不可增删搜索，默认组件排在最后并收起，每个组件可恢复预制体默认值。
- 接口：`LevelEditorComponentValueOverride` 保存组件路径、类型、字段路径和值；道具窗口采集覆盖，`LevelEditorTools` 与 `PlanningSceneBuilder` 共用同一套应用函数。
- 行为：保存后新放置/规划生成优先应用覆盖值；已有同名道具会先恢复预制体默认值再应用当前覆盖。
- 验证：Runtime 与 Editor 静态编译通过（手动合并响应文件时出现既有重复源警告），未做 PlayMode/窗口交互验收。

## 详情/装配选中方块场景引用

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：详情图/装配图选中已同步到场景的方块时，“聚焦玩家”所在工具行显示只读场景物体引用和“定位方块”按钮。
- 行为：引用框禁止替换，只做 `Selection` 与 `Ping`；实际替换或属性编辑仍通过正常 Inspector 完成。同步时若规划数据已有同形方块，也会回填其场景实例引用，不再因为 `BoxesMatch` 提前跳过。
- 存档门槛：顶部删除“同步到装配图”，改为“保存到当前存档”；只有当前存档已保存、保存后没有新的未保存改动、且选中方块已生成到场景时，“定位方块”才可用。详情/装配选择时会按房间、格子、类型和栏目标识即时反查场景物体，不依赖上一轮同步缓存。
- 验证：规划编辑器相关 Runtime/Editor 源码静态编译通过。

## 当前功能与协作分支整合

- 日期：2026-10-05；负责人：Codex；目标：整合本地已验收的颜色／关卡编辑进度、`Test` 场景、TA 水效果分支和音频分支至 `main` 并推送。
- 边界：保留当前窗口项目状态；未引用的 `ColorPickup_blue 1.playable` 副本和与本次功能无关的 `SampleScene` 删除不纳入提交。音频首次导入须补齐 Unity `.meta`，不代替音频成员做玩法接线。
- 验证／交接：TA 分支 `adce5df` 和音频分支至 `b22df5b` 的历史均合入当前分支；TA 同文件冲突保留已在 Test Play Mode 验证的水体共享模拟／渐显适配。音频共 14 首 Music、16 个 SFX，Unity 已生成全部 `.meta`，项目内 GUID 无重复；Console 导入后 0 警告／0 错误。Runtime、Editor 程序集重新编译均 0 警告／0 错误；尚未逐条试听、接线或验证最终包体。此条记录随本次合并推送 `main`，较下方旧任务条目的“不推送”状态更新。

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

## 栏目删除级联清理

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：删除方块栏目时，自动清理当前场景中对应栏目已放置的 `LevelEditorPlacedBlock`，并移除规划文档中引用已失效栏目的方块。
- 行为：按被删除栏目的旧名称清理场景实例；规划数据按 `paletteEntryId` 优先匹配，缺少 ID 时回退名称匹配，改名或改色不会触发误删。
- 验证：使用更新后的 Unity 6000.3.12 Editor 响应文件完成静态编译，0 错误。
- 交接提交：待提交。

## 水体功能组件

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：新增 `BlockWaterFeature`，将 TA 水体、Reveal、玩家游泳、入水/水花/离开音效和波纹调试封装为标准 `BlockFeature`。
- 接口：提供 `IBlockWaterSource` 与 `IBlockWaterSwitch`；`ColorBlock` 优先把水接触交给该接口，无组件时保留蓝色兼容逻辑；`ColorWorldManager` 不再为自管水体的功能实例重复生成视觉。
- 运行时：`BlockRuntime` 增加禁用功能过滤和 `RefreshFeatureSet()`，供后续颜色能力 profile 预装或切换组件。
- 约束审计：全项目现有 `BlockFeature` 派生类为 `BlockPowerFeature` 与 `BlockWaterFeature`；唯一挂载动力功能的 `红方块_*.prefab` 仍保持启用。校验器现只检查 enabled 功能，预装但禁用的 profile 组件不会误报冲突。
- 验证：使用比赛项目 `6000.3.12` 响应文件完成 Runtime 与 Editor 静态编译，0 错误。
- 交接提交：待提交。

## 颜色能力控制组件

- 日期：2026-10-05；负责人：Codex；分支：`codex/orpheus0829/添加功能组件`。
- 范围：新增 `BlockAbilityHost`、`BlockAbilityCatalog` 与编辑器自动扫描器；`BlockFeatureAttribute` 初始增加 `DefaultColorIds` 和 `RoleTags`，后续已收口为单一 `DefaultColorId` 精确匹配。
- 行为：编辑器扫描全部 `BlockFeature` 生成能力目录；方块放置/生成时默认补齐 `BlockAbilityHost`；Host 按当前颜色和角色预装全部模块，切换时校验约束后统一启停并刷新 `BlockRuntime`。
- 验证：使用比赛项目 `6000.3.12` 响应文件完成 Runtime 与 Editor 静态编译，0 错误。
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

## 开场主题音乐导入

- 日期：2026-10-04。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：音频分支最初导入开场主题曲，后续提交统一重命名并替换为 `bgm_wn_opening_01.wav`，同时增加 BGM 与 SFX；最终资源清单见 `Docs/Agent/Integration/AUDIO_HANDOVER.md`。
- 接口变更：无（仅音频资源导入）。
- 验证：音频分支已提交资源与交接文档；首次导入与 `.meta` 由整合者复验。
- 交接提交：`94db2c9` 起，最终资源提交 `b22df5b`。

## 策划蓝绿/红蓝/红绿交互落地

- 日期：2026-10-06；负责人：Codex；分支：`codex/interaction-manager`。
- 范围：依照物体交互图 skill 直接实现蓝方块水体增强、绿方块落差弹跳、红蓝喷流、红绿下一跳弹高、蓝绿锚点藤蔓，以及右键颜色轮盘站立屏蔽；新增锚点预制体、定义、目录和关卡编辑器道具栏目。
- 接口：新增 `PlayerHealth`、`PlayerFallDamage`、`PlayerHealthHud`、`GreenBouncePad`、`BlockSprayEmitter`、`VineGrowthEmitter`、`AnchorPoint`、`NextJumpBounceEmitter`；`InteractionNodeKind` 增加 `RequireOtherColor`；`PlayerController` 增加游泳倍率、摔落免疫和弹跳请求入口。
- 默认参数：水体速度倍率 `0.55`、进水恢复 `1`、最大生命 `5`、弹跳阈值 `2m`、弹跳曲线指数 `1.8`、弹跳速度 `10~20m/s`、喷流距离 `4m`/持续 `1.5s`、锚点搜索 `8m`、藤蔓速度 `4m/s`、段长 `1m`、上限 `32`。
- 图资产：红/蓝/绿方块定义已重写为清晰分支链，并加入 `受到其他物体颜色` 条件；预制体已绑定喷流、藤蔓、落差和下一跳组件。
- 验证：已完成静态源码/资产核对，Unity 编辑器与 PlayMode 尚未在本机本轮运行；推送前需在 Unity 6000.3.12f1 重载脚本并检查 Console、图窗口和实际接触矩阵。
- 未完成：未合并 `main`；需要在 Unity 内放置红蓝绿方块与锚点，确认藤蔓增长视觉、HUD、输入轮盘和水体状态，并在验收后更新本条记录。
