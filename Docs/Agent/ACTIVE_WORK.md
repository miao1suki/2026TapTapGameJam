# 工作状态与交接（每次任务重读）

## ESC 暂停界面雏形

- 日期：2026-10-10；负责人：Codex；分支：`ESC页面雏形`。
- 范围：直接在 `Assets/_Project/Scenes/Systems/Systems_UI.unity` 的 `PauseScreen` 下写入真实 UGUI 层级；没有保留 Editor 生成菜单或生成器脚本。
- 主界面：灰色遮罩、缩放入场的 `EscWindow`、五段轮盘选项“继续游戏 / 设置 / 收藏品 / 存档 / 退出到主菜单”；当前选项固定停在可见轮盘中部，使用缩放与提亮区分，不通过点击扇区改变选中状态。
- 子页面：设置页复用 `SettingsPanelController` 数据源并从右侧滑入；存档页复用 `SavePanelController` 并开启保存入口；收藏品页先做花架子；子页面右上角 X 返回主界面。
- 背景规则：轮盘选项打开的 `SettingsPage / SavePage / CollectionPage` 固定纯黑；设置页内的 `Sound Page / Key Binding Page / Credits Page` 背景 alpha 为 0，只显示其中的控件。
- 设置页响应式布局：新增 `EscSettingsResponsiveLayout`，按 `SettingsPage` 实际宽高计算统一缩放；内容区左边界按左侧栏目缩放后的右边界动态计算并加 `contentGap`，避免与栏目按钮重叠；底部返回区域锚定真实底边。
- 存档页响应式布局：新增 `EscSaveResponsiveLayout`，按 `SavePage` 实际宽高缩放标题、状态、存档列表和底部按钮；列表随页面尺寸放大/缩小，新建和返回按钮锚定真实底边。
- 修正：设置和存档子页隐藏重复的底部返回键；切换页面和返回主界面时清理按钮悬停/按下缩放与提亮；主界面的“退出游戏”改为返回开始游戏界面。
- ESC：`GameUiRouter` 仅在 Systems_UI 的 UI 系统根存在时响应；暂停动作在子页面会直接触发当前页面 X 的 `Button.onClick` 返回主界面，在主界面才关闭整个 ESC 界面；没有 Systems_UI 场景时不响应。输入继续使用 `InputActionId.Pause` 的映射。
- 存档：存档页读取会调用 `SaveGameService` 并按 `levelLabel` 尝试切回对应关卡；保存会把当前关卡标签写入选中槽位，完整游戏进度恢复仍待后续游戏进度 API。
- 设置：继续沿用 `RuntimeSettingsPolicy.ApplyChangesToRuntime`，当前默认预览模式不改变实际游戏设置。
- 轮盘实现：`EscMenuController` 已切换为左轮轮盘状态机；桌面现有矩形按钮会在初始化时转换为 `EscWheelSectorGraphic` 扇形，整名位随 `WheelPivot` 旋转，文字保持水平；入场只做半圈并逐渐减速，受控切换只走相邻选项最短角度，首尾循环沿当前方向无缝走一格，旧选项的选中效果在转动中逐渐消失、新选项同步增强，滚轮带可调换项间隔，选中项固定回到 `selectedAngle`，确认读取 `InputActionId.Jump`，并支持鼠标左键、鼠标右键。子页面占用右侧区域并为左侧轮盘保留宽度，打开子页面时底部遮罩透明且主轮盘保持激活；子页面退场与轮盘恢复同时开始；原白线占位装饰已关闭。
- 编译修正：初始化转换为扇形时会移除旧的 `Image`，避免同一 GameObject 同时存在两个 `Graphic` 导致暂停菜单报错；选项文字在运行时从 Inspector 配置同步。
- 视觉配置：暂停轮盘 Inspector 已暴露整轮材质、扇区默认材质、单扇区覆盖材质、颜料笔画 Sprite/Material；`EscWheelOption.pageColor` 表示每一页选定的主题颜色，并同时用于扇区高亮与颜料过渡。
- 字体配置：PauseScreen 下所有 UGUI `Text` 保留为布局和动态数据源，渲染层由 `TextSdfMirror` 镜像到 `TextMeshProUGUI` 并统一使用 `Cubic_11 SDF`；轮盘标签默认字号 `32`，通过 SDF `Face Dilate` 加粗笔画并添加黑色描边，字号、笔画粗细、描边颜色和距离均暴露在 Inspector。
- 收藏品页：新增 `AchievementCollectionPresenter`，直接读取 `AchievementManager.GetAllAchievements()` 构建两列成就长条，新增条目优先填满当前行且单项也保持左上排列；标题与要求使用同一默认字号 `36`，简介保持 `16` 并使用灰色；无素材时默认白色块、纯黑标题和纯黑要求；每条右侧为只读完成图片；`rowMaterial` 暴露块材质，`completedSprite` 暴露达成图片；达成后只覆盖整块灰色遮罩并显示右侧图片，不改变文字、内容或底色；成就要求按成就树跳过已满足叶子、显示当前轮到的未完成条件，并监听 `AchievementProgressChanged` 原位刷新，不重建列表或重置滚动位置；每次进入页面都会重新读取状态。
- 成就目录：运行时 `AchievementCatalog.asset` 已挂入当前固定目录下的成就；新增 `AchievementCatalogAutoSync`，编辑器导入/删除成就资产或进入 Play Mode 前自动扫描 `Assets/_Project/Content/Data/Global/Achievements` 并同步运行时目录；新增成就不再依赖手动点“应用到游戏逻辑中”。

## 当前进度合并至主线

- 日期：2026-10-10；负责人：Codex；用户已授权将 `codex/world-planning-tools` 合并并推送 main。源功能提交：`ed6625a`，以快进方式整合世界图工具、门按钮、颜色表现解耦、起跳手感及水体恢复修复。
- 验证范围沿用下方交付记录；完整 Pick 场景、最终像素化/HSV 合成和打包仍待验收。用户本地 Pick 场景、字体、DOTween Pro 与 Package Manager 设置不进入此次合并，保留原状。

## 水体透明恢复修复与当前进度交付

- 日期：2026-10-10；负责人：Codex；分支：`codex/world-planning-tools`。本次按要求提交并推送当前任务进度，不合并、不推 main；更新下方此前“仅本地、不推送”的交付状态。
- 原因：水体由 MeshRenderer 绘制，旧 Sprite Lit Shader Graph 在运行时依赖 SpriteRenderer 参数。隔离运行态 GPU 对照中，正式透明度已经为 1，旧子目标仍不可见；URP Unlit 透明子目标恢复可见。水面与横截面均改为网格适配子目标，原水效节点和源材质颜色保留。
- 同步修复：水体深度改为真正的材质属性，使按实例设置的深度进入 Shader，避免顶面使用零深度；生成水体注销时先停用并移出宿主再延迟销毁，避免同帧重新绑定发现待销毁实例。保留分类表现集中调度与纯 HSV API。
- 提交范围：包含本分支此前世界图/通道/区域规划、玩家贴纸拖拽、门按钮踩踏、分类表现解耦及起跳手感的任务改动与说明。排除用户新保存的 Pick 场景、像素字体、本机 DOTween Pro 和 Package Manager 设置，原文件保留本地。
- 验证：Runtime/Editor 静态编译、39 项管理器源码配引擎桩断言、13 项构造器 IL 检查、两水 Shader Graph 引用/透明目标/深度属性检查通过；Unity 6000.3.12f1 独立临时项目 GPU 确认正式水体初始透明、过渡中渐显、解锁后横截面及顶面可见、重置透明、再次解锁可见。相同运行态替回旧 Sprite 子目标后仍不可见（Alpha 已为 1）。完整 Pick 场景、最终像素化/HSV 合成、移动手感和打包仍需复测，不将隔离检查视为整关验收。诊断文件仅在忽略的 Library 内，不进入提交。

## HSV 与分类表现解耦

- 分支：`codex/world-planning-tools`；仅本地，不提交或推送。
- 范围：HSV 仅提供饱和度/白化 API；独立管理器分基础方块、水、藤蔓统一编排，按源材质/颜色/阶段共享运行时材质。处理零 Alpha 交接、首次可见初始化、遮罩重复 Alpha、重置和生命周期。保留场景与玩家参数。已实现；源 TA 资源不变，藤蔓在运行时使用透明 Shader，Resources 材质保留构建引用。
- 验证：Runtime / Editor 全量静态编译、39 项实际管理器源码配引擎桩的演出/HSV/共享材质/注册/重置断言、13 项构造器检查、两水 Shader Graph 引用检查和 diff 检查通过。尚未进行 Game/GPU/Frame Debugger/打包实测，不能据此认定闪烁及水面最终视觉已验收；透明重叠和拖尾 UV 是复测重点。

## 颜色透明交接与起跳手感

- 初始化修复：用户运行报错 `MaterialPropertyBlock.CreateImpl`；已移除 ColorObject、HSVColorFadeManager、ClimbableVineFeature（含 LadderSonFeature）和 InteractiveWater 的原生字段初始化，改为主线程使用入口惰性创建并复用。不改场景或玩法参数。Runtime / Editor 静态编译、38 项引擎桩逻辑断言、10 项编译产物构造器检查和 diff 检查通过；构造器检查确认四类均不再创建原生 PropertyBlock。Unity 实际加载、克隆、Play Mode 待复测，静态检查不能替代生命周期验收。

- 日期：2026-10-09；负责人：Codex；分支：`codex/world-planning-tools`；仅本地开发，不推送。
- 范围：基础外观变白并隐去、零透明度交接正式外观再显现；水/藤蔓 Alpha 支持与 HSV 遮罩配合；玩家藤蔓顶部蹬跳、下落保护及气泡/弹跳/蒸汽瞬时起跳。遵守颜色功能与表现解耦、玩家统一 Rigidbody 写入。
- 边界：不改已保存 Scene、玩家输入和其他成员资源位置；更新材质接口/中文参数/接入规范。已实现，仅本地待测试，不提交或推送。
- 验证：Runtime / Editor 全量静态编译通过；实际渐变管理器源码配引擎桩与玩家速度计算方法的 37 项断言通过（透明交接、重复/反向请求、重置、颜色隔离、顶部方向与速度、持续喷出不叠加）。水 Shader Graph JSON 引用结构已检查。尚未执行 Unity GPU / Physics / 打包验收，尤其需检查透明水面的深度与排序，以及顶部越出手感。

## 门按钮踩踏触发

- 日期：2026-10-09；负责人：Codex；分支：`codex/world-planning-tools`；仅本地修改，不推送。
- 范围：门按钮由 E 交互改为脚部接触 Trigger，移除输入目标接口，更新预制体、中文 Inspector 与接入说明；保留门的全按钮解锁、演出和重置逻辑，不改已保存场景。
- 状态：已移除 E 交互接口，Enter/Stay 检查玩家脚部后按下一次并通知门；低位 Trigger 在 Awake 兼容旧实例，预制体与中文参数面板已同步更新。
- 验证：Runtime / Editor 全量静态编译与补丁检查通过；实际 DoorButton 源码配引擎及门通知桩的 12 项触发过滤、重复通知、重置及有效性断言通过。未执行 Unity Physics / Play Mode / 打包验收；未改 Scene、未提交或推送。

## 玩家贴纸拖拽隔离

- 日期：2026-10-09；负责人：Codex；分支：`codex/world-planning-tools`；仅本地修复，不提交或推送。
- 范围：玩家贴纸的鼠标捕获、拖动阈值、坐标换算与窗口失焦清理；保留当前玩家位置、规划存档和已保存场景，不修改运行时玩家控制。
- 检查：旧拖拽没有捕获丢失回调或按键保持检查；详情图局部玩家坐标与全局鼠标坐标混算，边缘贴纸还会把玩家跳到视口。
- 状态：玩家工具独占贴纸拖拽，其他工具不拾取贴纸；三像素阈值、从原位置按屏幕位移移动、CellSize 换算、捕获丢失/窗口失焦/场景视图切换/Play Mode 清理已实现。编辑平面约束不再在 Play Mode 写玩家。
- 验证：Unity 6000.3.12f1 Editor 全量静态编译与补丁检查通过；实际手势源码配轻量 Vector2/Mathf 桩的 13 项断言通过。实际窗口鼠标、失焦、撤销与场景切换待复测；未修改 Scene、未提交或推送。

## 世界图布局与独立通道编辑

- 日期：2026-10-09；负责人：Codex；分支：`codex/world-planning-tools`；本地开发，不提交或推送。
- 范围：左侧列表/右侧世界工具、独立通道占格与自动命名、整体多选移动、区域规划、删除所有权清理、装配图定位和绘制修复。
- 边界：不改已保存 Scene、其他成员素材或运行时玩法；区域规划仅编辑预览。保留既有像素字体、Package Manager 设置和本机 DOTween Pro 未提交内容。
- 状态：左列表/右工具、独立青色通道与连接名、整体多选移动、区域底色/注释/移动/删除、最后格删除与来源清理、装配定位/归属/绘制/擦除均已实现。世界格固定映射到详情格；旧存档需先另存并检查预览，再应用。
- 验证：Unity 6000.3.12f1 主 Editor 全量源码静态编译 0 错误；实际 Utility/Layout 源码配轻量数据/数学桩的 23 项断言通过；补丁检查通过。新增内存回归菜单“验证世界图与装配编辑”。该菜单、实际 Unity 窗口交互、自定义撤回和自动同步复测尚未执行，不视为界面已验收。
- 说明：`Docs/Developer/LEVEL_EDITOR.md` 与 `Docs/Agent/Integration/WORLD_PLANNING_API.md`。未提交、未推送、未合并。

## 关卡编辑、门与颜色表现主线整合

- 日期：2026-10-09；负责人：Codex；来源分支：`codex/editor-mechanisms-pixelization`；目标：`main`，按本次用户要求合并并推送。本条更新下方“仅本地/仅分支、不合并”的旧交付状态，不删除历史记录。
- 范围：`c0edca9` 累计功能、地面按钮/简体字体/两格提示修正、当前已保存 Pick 场景与栏目序列化；排除本机 DOTween Pro 和 Package Manager 设置，不额外合并其他协作分支。
- 说明：`Docs/Developer/UPDATE_2026-10-09_EDITOR_AND_MECHANISMS.md`，系统改动、操作入口、数据边界与未验证项目集中于此。
- 验证：本次顺序复核 Runtime/Editor 静态编译及 Git 补丁；实际 Game/GPU、门镜头、撤销复测与打包验收仍待进行。远程 main 检查时没有新增提交，合并不需要冲突取舍。

## 门放置提示与地面按钮

- 日期：2026-10-09；负责人：Codex；分支：`codex/editor-mechanisms-pixelization`；仅本地修改，不提交或推送。
- 范围：按钮占位模型平放至所属格底部；门鼠标预览、拖拽预览与实际放置统一 1×2；关卡工具指定简体中文系统字体，避免默认 CJK 字形回退。保留已保存场景和其他本地修改。
- 验证：Editor 静态编译及 diff 检查通过；按钮底面 y=-0.5、水平薄板尺寸、Trigger 与“门”字 U+95E8 检查通过。详情/装配/Scene 提示均覆盖两格，Scene 放置尺寸同步为 1×2。Unity 内模型、简体字体实际字形和鼠标交互待实际验收。

## 当前开发进度分支交付

- 日期：2026-10-09；负责人：Codex；分支：`codex/editor-mechanisms-pixelization`。按用户要求提交并推送当前进度，不合并、不推送 main；本条更新下方此前各项“仅本地、不推送”的交付状态。
- 范围：关卡编辑器合并/参数/自动同步/场景工作区与撤销改进、玩家攀爬与新版轮盘、颜色外观/HSV、TA 水与藤蔓依赖及根系生长燃烧、非颜色门/按钮、学习项目选择性像素化与默认关闭的描边 API；包含本机已保存的 Pick、Test 场景和像素字体依赖。
- 排除：本机 DOTween Pro 商业插件及示例、Package Manager 本地编辑器设置；不删除这些本地文件。原有免费 DOTween 核心保持不变。
- 验证：既有 Runtime/Editor 静态编译、门规划 9 项数据断言、HSV 时钟桩 8 项断言及 Git 补丁检查通过；实际 Game/GPU、Shader 导入、门镜头、编辑器撤销和打包验收尚未完成。该分支是待进一步测试的开发进度，不作为正式版本。

## 非颜色门与绑定按钮

- 日期：2026-10-09；负责人：Codex；范围：独立门/按钮状态机、Timeline 门镜头、规划放置与绑定维护；检查房间/R 重置接线与 URP学习 像素化。只改本地，不修改已保存场景、不推送。
- 状态：门/专属按钮 prefab、1×2 单实例生成、自动/追加按钮、移动/擦除/孤儿清理、规划/Scene 虚线、中文参数与状态面板、全按下开门与独立 Timeline 演出已实现。
- 像素化：按追加要求接入 URP学习 Shader/HLSL 三阶段及 MIT 许可，Rendering Layer 128 / Layer12 选择性遮罩；描边 API 默认 false，HSV 排在像素化后；PC/Mobile 显式引用，新处理替代旧 02StencilPixelEffect。
- 重置检查：现有 ResetForRoom / OnRoomColorReset 能重置颜色；未找到房间退出或 R 重生调用，R 仅死亡重生。未自行修改输入/房间规则。
- 验证：门规划实际 helper 源码加轻量数据桩的 9 项独立断言通过；Runtime / Editor 静态编译及 diff 检查通过；两份 prefab 内部引用、门碰撞尺寸、按钮 Trigger、PC/Mobile Shader 绑定检查通过。未执行 Unity 内实际放置/保存/镜头/输入验收、Shader GPU 编译及像素化/HSV 图像与打包验收。

## 藤蔓加粗、粒子种子初始化与 HSV 遮罩修复

- 日期：2026-10-09；负责人：Codex；范围：藤蔓实例表现、URP 全屏颜色通道；保留场景及其他本地修改，不推送。
- 检查：PC 默认 Renderer 确认挂载启用 HSV Feature；Pick 摄像机 RendererIndex=-1；未发现代码主动切换 Renderer。旧 overrideShader 遮罩依赖源材质 MainTex/BaseMap，且未包含 GBuffer / 旧粒子 Always 标签，是通道漏覆盖的风险点。
- 改动：独立 overrideMaterial + 专用拖尾 alpha PropertyBlock；完整覆盖常用 shader 标签；每颜色执行时显式绑定 RenderGraph 纹理/参数。新增 Inspector 管线、通道执行和三色渐变数值状态。藤蔓默认整束宽 1.2 格、枝条宽 ×3；StopEmittingAndClear 后才设置 seed，不改 TA 源素材。
- 验证：Runtime / Editor 静态编译与 diff 检查；最终 Game / GPU 遮罩与打包视觉尚未验收，不将“编译通过”作为秒切问题已视觉解决的证据。

## 水体像素化、藤蔓表现与根系燃烧

- 日期：2026-10-09；负责人：Codex；范围：颜色恢复节奏、TA 水/藤蔓表现、同根系顶到底燃烧。不改已保存关卡、不推送。
- 方案：保留颜色 GameObject Layer；以 Rendering Layer 位单独标记水和藤蔓像素化。按 TA 粒子 30.13/32.43 时间采样过渡，母根统一管理视觉和节段燃烧，保留预置节段的房间重置能力。
- 状态：已实现默认 5 秒/显式时长 ×3 与钥匙等待适配、独立 Rendering Layer 像素遮罩、母根粒子缓存/高度适配、触水长满、同根系逐节燃烧与预置节段重置。TA 两个 Grow 实例保存的参数一致；使用共用粒子源，不修改源资源。
- 验证：Runtime / Editor 静态编译 0 错误、diff 检查通过；实际 HSV 源码时钟桩的 8 项时长/交接/完成/重置断言通过。透明水体/拖尾渲染、首次快照 CPU/内存与实际根系燃烧尚未 Game/GPU/Profiler 验收。未提交、未推送。

## 快捷视图与颜色外观交接

- 日期：2026-10-09；负责人：Codex；范围：关卡编辑器 W/R、颜色恢复阶段与水体运行时表现。
- 目标：黑白基础外观先变纯白，交接纯白正式外观后恢复颜色；运行时禁止使用编辑识别材质。不改已保存场景、不提交或推送。
- 状态：已接入编辑器焦点内 W/R；分离编辑识别与正式材质，恢复按基础外观→纯白交接→正式外观恢复执行；透明水体加入蓝色层并纳入通道。保留现有本地关卡、玩家及 TA 改动。
- 验证：Runtime / Editor 静态编译通过，实际 manager 源码配时钟桩的 8 项阶段/反向/立即/重置测试通过；未执行 Unity Game/GPU 视觉及打包验证。未提交、未推送。

## 场景规划与自动同步记忆

- 日期：2026-10-09；负责人：Codex；范围：关卡编辑器本地偏好与场景切换。
- 目标：各 Scene 独立恢复规划图、存档路径、视图与自动同步开关；不改 Scene 文件，不推送。
- 状态：已实现项目路径 + Scene GUID 的本地工作区；旧全局规划只迁入首个 Scene，其他新工作区默认空地图。同步开关按场景持久化，来源删除记录仅会话内保留以区分重启时未保存的 Scene 内容；Play Mode 退出重新绑定，不误删除规划。Editor 编译与 diff 检查通过，实际切换窗口/场景待验收；未推送。

## 合并物体网格预览边缘

- 日期：2026-10-09；负责人：Codex；范围：关卡编辑器正面预览与画布缩略图。
- 目标：按合并尺寸生成无额外留边的正交正面图，不再把单块缩略图拉伸成长条；不修改 Scene、模型、碰撞或规划数据。仅本地修复，不推送。
- 追加：实例参数窗口读取 BlockParameter / InspectorName / Tooltip 中文元数据，恢复功能中文名称、参数分组和字段下方用途说明。
- 验证：Editor 静态编译及 diff 检查通过；实际 Unity 窗口预览与参数显示待验收。未提交、未推送。

## 关卡编辑器合并、参数与自动同步

- 日期：2026-10-09；负责人：Codex；范围：规划文档、网格选择模式、场景生成、参数覆盖与快捷键。
- 目标：同种矩形选区合并为单一实例/逻辑并可解除；栏目默认参数与单个规划物体参数分别保存；自动同步包含场景手动删除回流；窗口打开时 Ctrl+Z/Y 走规划历史。
- 场景：不写已保存场景文件。保留其他未提交内容；完成后本地测试，不推送。
- 状态：合并/参数模式、单实例矩形生成及解除、默认/独立参数、自动同步与删除回流、独立撤销快捷键已实现。Runtime / Editor 静态编译与 diff 检查通过；新增内存规划回归验证菜单，Unity 内实际交互与原生崩溃复测尚未执行。未提交、未推送。

## 关卡编辑器反馈与撤销崩溃

- 日期：2026-10-09；负责人：Codex；范围：规划编辑器场景应用 Undo、画布预览刷新、道具栏目布局与按钮焦点。
- 证据：Unity Editor.log 在 `Ctrl+Z` 前反复报告从 `generateVisualContent` 修改 UI Toolkit 树；原生崩溃栈位于 `UndoManager::PostApply → Transform::CheckStructure`。前一版场景应用把临时根节点及其子物体同时加入、移动、销毁于同一 Undo 组。
- 处理：临时生成流程不记录 Undo，成功进入正式根节点时才记录创建；移除绘制回调里的视觉树更新，预览改为定时异步刷新；扩大道具区并消除按钮残留焦点。
- 验证：SurfaceTiles Editor 与主 Editor 静态编译通过；Unity 内实际撤销及视觉效果仍需复测。未提交、未推送。

## 关卡编辑器日常应用与窗口易用性

- 日期：2026-10-09；负责人：Codex；范围：规划编辑器窗口、画布视图状态和场景生成器。
- 目标：M 开关窗口、恢复上次画布位置、默认展开且加宽道具面板、突出增量应用；全量重建保留为需确认的独立操作。
- 场景：不修改任何已保存 Scene；保留当前工作树中其他成员及此前未提交的改动。
- 状态：已实现并通过 Editor 静态编译及 `git diff --check`；Unity 窗口交互、增量应用后的场景结果和 Undo 仍待人工验收。暂不提交或推送。

## TA 新资源本地接入

- 日期：2026-10-09；负责人：Codex；来源：`origin/codex/ta/water-platform-demo` 的 `d090070`、`5136bee`；状态：仅拉入当前工作区，未提交、未推送。
- 接入：藤蔓生长粒子与贴图、PixelEffect Shader/材质、PC Renderer Feature 和 `PixelEfect` 层。尚未把粒子预制体绑定到藤蔓玩法组件。
- 暂不接入：TA 同步提交中的 `.vscode`、`Assets/_Recovery`、瓦片库重写、Level_01 自动光照组件及缺少对应目录的 `paopao.meta`；这些不属于已验证的效果依赖。
- 验证：已核对目标路径原本干净并保持本地 Test/玩家待测改动；Unity 中的渲染视觉仍待 Play Mode 确认。

## Test 轮盘与攀爬手感本地测试

- 日期：2026-10-09；负责人：Codex；状态：本地待 Play Mode 测试，不提交、不推送。
- 范围：给 `Assets/Scenes/Test.unity` 中独立的 `TestPlayer` 挂新版轮盘外观；调整 `PlayerController` 的藤蔓内侧上爬、外侧离开和内侧按键加跳跃的蹬墙行为；只涉及玩家控制和既有藤蔓接入，不修改其他成员场景。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；Test 场景组件与轮盘资源引用已核对；实际攀爬轨迹和轮盘显示等待本机 Play Mode 验收。

## 拾取物与轮盘整合

- 日期：2026-10-09；负责人：Codex；分支：`codex/pickup-flight-preview`；整合 `origin/main` 的 `DropItemBase`、钥匙感知/控制锁及回弹改动，并保留 Pick 飞行与轮盘实现。
- 掉落物：`DropItemBase` 统一执行出现、磁吸、停止动画，通过 `IDropItemCollectionAnimation` 在收集阶段调用飞行动画；颜色钥匙触碰玩家时立即解锁和锁控制，但外观等飞行结束后再隐藏，镜头与飞行都结束才回收。
- 轮盘：正式 `RuntimePlayer.prefab` 直接挂载 Pick PSD 轮盘外观，所有使用该玩家预制体的场景继承新版轮盘；Pick 场景移除旧独立轮盘实例。
- TA：`origin/codex/ta/water-platform-demo` 已是 `origin/main` 的祖先，目前没有额外新提交可合并。
- 待验证：Unity Play Mode 中的钥匙触发/飞行、相机演出并行及正式场景轮盘表现。

## 拾取飞行动画

- 日期：2026-10-09；负责人：Codex；分支：`codex/pickup-flight-preview`；范围：新增独立的 DOTween 拾取表现组件与 Pick 场景测试拾取物；不接入背包、奖励、正式拾取判定或输入键位。该分支只推送供协作，等待另一边的拾取物完成后再准备合并 main。
- 场景：仅追加 `Assets/Scenes/Pick.unity` 的测试物，不改其他成员场景。
- 对接：预留公开播放入口和完成回调，正式拾取逻辑未来自行决定何时触发及如何结算。
- 表现：四段 DOTween 三次贝塞尔飞行；先弧线反弹到远离玩家的随机落点，转向头顶时延续一段向外惯性，再短暂慢速靠近、快速收至中心。每个测试球每次触发都生成独立随机双控制点，头顶与玩家中心为精确目标。三个测试球在玩家当前位置附近，触碰仅播放动画。
- 节奏：修正连续阶段在边界同时减速至零所造成的视觉停顿；该组件每帧只更新位置，不做场景扫描或纹理读回。Pick 轮盘原型在场景初始化时另有一次性的 PSD 纹理读回与拆分，可能导致启动瞬间停顿，不能据此推断拾取动画持续掉帧。实际掉帧仍须 Unity Profiler 核实。
- 文档：`Docs/Agent/Integration/PICKUP_FLIGHT_API.md` 与 `Docs/Developer/PICKUP_FLIGHT.md`。
- 验证：三次贝塞尔回旋修正后，使用项目 Unity 6000.3.12f1 的 Roslyn 响应文件完成 Runtime 与 Editor 静态编译，均为 0 错误；三处场景实例引用和 `git diff --check` 已核对。Unity 日志显示测试预制体正常导入；Play Mode 中的实际轨迹、触发范围与帧率仍需本机确认。分支只交付 DOTween 免费版核心及其原始 readme，不包含本机 DOTween Pro。

## Pick 三色轮盘原型

- 日期：2026-10-09；负责人：Codex；范围：在用户新建的 `Assets/Scenes/Pick.unity` 增加原型组件、正式 `RuntimePlayer` 测试实例和测试地面，不改原 `Test` 场景。
- 数据与输入：原型绑定 `PlayerColorWheel` 作为外部视图；选色走现有 `PlayerInputDriver` 与 `ColorRuntimeService`，不设 `1/2/3` 解锁或 `R` 重置等临时键位。原有 `R` 保留为玩家重生。
- 表现：直接拆分本地 `轮盘.psd` 原有黑色扇区，不另叠绘扇区；以各块原图可见像素重心等比缩放，指向时硬切白色半透明并使用 DOTween 缓动；未解锁显示问号。
- 外部参考：`TogetherYear/UV` 链接的网页和 Git 访问均返回仓库不存在，未迁移其源码。本分支只交付 DOTween 免费版；本机 DOTween Pro 不进入 Git。
- 验证：视觉修正后使用 Unity 6000.3.12f1 自带 Roslyn 再次编译通过（0 错误），`git diff --check` 通过；Pick 场景中的玩家预制体、地面和轮盘资源引用已静态核对。完整 Unity Play Mode 仍需复核。普通 `dotnet build` 因本机生成工程目标为 .NET Framework 4.7.1、DOTween DLL 面向 4.7.2 而失败，不能据此判断 Unity 编辑器编译结果。
## 弹性植物落差回弹比例

- 日期：2026-10-09；负责人：Codex；分支：`修衰落高度和拾取范围`。
- 公式：回弹高度按 `落差 × 当前回弹比例` 计算，落差越高，比例从“低落差回弹比例”插值到“高落差回弹比例”。
- 默认：低落差回弹比例 `0.9`，高落差回弹比例 `0.5`，参考高度 `8` 格，最低弹跳高度 `0.3` 格；高落差默认保留至少一半，但不会超过原落差。
- 衰减：连续弹跳仍乘既有“连续弹跳衰减比例”；最大弹速改为可选安全上限，默认 `0` 表示不限制。
- 资源：`Green_JumpPlant.prefab` 已更新新参数；`PlayerController` 增加高度反算弹速接口 `CalculateVerticalRiseSpeed`。
- 验证：Unity 6000.3.12f1 Runtime 与 Editor 静态编译 0 错误，`git diff --check` 通过。

## 掉落物启停生命周期基类

- 日期：2026-10-09；负责人：Codex；分支：`修衰落高度和拾取范围`。
- 基类：新增 `DropItemBase`，在 `OnEnable` 重置状态并调用出现动画钩子；拾取通过 `TryBeginCollection` 进入收集阶段，收集动画完成后才调用 `RecycleItem`。
- 磁吸目标：拾取开始时把玩家传给 `DropItemBase.CollectionActor`，基类动画以后可通过 `CollectionTarget` 实现逐渐缩小并追随玩家，Tween 到达玩家后才完成回收。
- 拾取阶段：玩家进入感知范围只启动磁吸，不结算颜色；钥匙保留碰撞，真正碰到玩家后调用 `CompleteAttraction`，再关闭碰撞、解锁颜色、隐藏钥匙并进入镜头演出。
- 控制锁：新增 `IPlayerControlLockTarget`，颜色钥匙在拾取动画开始时获取控制锁句柄，动画结束或对象禁用时释放；输入驱动把锁检查前移，锁定期间不再处理交互、搬运、染色、移动、跳跃和普通动作。
- 玩家感知：`PlayerController` 新增“拾取感知半径（格）”、世界半径换算、XY 平面距离判断和选中物体的 Gizmo 可视化；掉落物以后统一查询玩家范围，不单独配置拾取半径。
- 颜色钥匙：`ColorKeyPickup` 改为继承 `DropItemBase`，解锁和关闭碰撞仍在拾取开始时立即执行，Renderer 不再提前隐藏，拾取动画完成后才隐藏并销毁。
- 规则落库：新增独立 `taptap-drop-items` Skill，并同步 `taptap-color-gameplay` 与 `ColorBlocks/SKILL.md`；以后新增拾取物必须继承 `DropItemBase`，Tween 钩子只写在基类，子类不得绕开基类直接销毁或禁用。
- 对象池约束：`OnDisable` 只负责停止动画和清理；池化掉落物以后应重写 `RecycleItem`，不能在 `OnDisable` 中等待消失动画。
- 依赖：项目尚未导入 DOTween，当前基类只提供出现、收集、停止动画的稳定钩子；DOTween 导入后在基类钩子内接入，不改变钥匙玩法时序。
- 验证：Unity 6000.3.12f1 `Project.Shared.Runtime.rsp` 与 `Assembly-CSharp.rsp` 静态编译 0 错误。

## 玩家死亡状态与重生

- 日期：2026-10-08；负责人：Codex；分支：`codex/orpheus0829/扩充道具`。
- 状态机：`PlayerController` 新增 `PlayerStateId.Death` 和独立死亡锁；死亡期间移动、跳跃、交互、调色、搬运、拖拽和普通动作全部封锁。
- 死亡动作：`PlayerController` 新增可配置 `ActSO deathAction`，由死亡事件触发现有 `PlayerActionRunner` 播放 Timeline，并强制 `DirectorWrapMode.Hold` 保持最后一帧。
- 重生输入：新增 `InputActionId.Respawn`，默认键盘 `R`、手柄右摇杆按下，固定仅点击；`PlayerInputDriver` 死亡时只消费重生输入。
- 重生位置：优先使用 `respawnPoint` 或注册的 `IPlayerRespawnPoint`；没有复活点时在死亡位置周围 `4 格`内随机寻找可落地位置。每次死亡都会更新死亡点，连续死亡以最新死亡点为准。
- 安全检查：随机点先向下吸附到实体地面，再检测 `LavaHazardFeature`；找不到安全随机点则回退默认复活点。
- 致死高度：`PlayerController` 新增“致死高度（格）”，默认 `8` 格；玩家落地时若本次下落高度大于等于该值，直接进入 `PlayerStateId.Death`，不使用血条。落入水中、落到可用弹性植物或下落期间进入过攀爬状态会免除摔落死亡。
- 复活保护：`PlayerController` 新增“复活无敌时间（秒）”，默认 `2` 秒；复活后伤害和摔落致死统一忽略，`PlayerHealth.TakeDamage` 也遵循无敌状态。岩浆改为 `OnTriggerStay` 持续致死，无敌结束后仍留在岩浆中会立即死亡。
- 重生锚：`Respawn_Block` 使用米黄色高对比材质，挂载 `RespawnAnchorFeature` 并通过 `IPlayerRespawnPoint` 注册复活点；道具栏目新增“重生锚”入口。
- 重生锚状态：组件不可碰撞，按 Unity Tag 检测玩家，支持激活范围与复活随机半径；未激活/已激活材质、激活动画可配置，渲染排序固定为玩家下一层。
- 颜色物兜底：`ColorObject` 在未解锁状态统一强制碰撞体为实体且关闭功能；解锁后恢复预制体原本的触发/实体状态。所有蓝色水族功能同时二次校验蓝色解锁，避免未解锁仍产生玩家水体状态。
- 功能组件兜底：`BlockFeature.CanRunFeature` 统一约束“已挂载且同物体颜色对象已激活”，玩家接触、交互、携带和颜色应用入口都走该约束；`IPlayerBounceSurface` 增加可用状态接口，禁止禁用植物通过玩家落地射线绕过。
- 重生锚校正：不再开局自动激活初始锚点；默认激活半径设为 `0.5` 格，玩家踩到第 N 个锚点时通过 `ActivateThrough` 自动激活主链 `0...N` 的全部锚点；渲染排序每帧同步到玩家排序以下。
- 气泡柱校正：离开气泡触发器后持续跟踪玩家，只要仍在气泡柱范围内且与任意蓝色水体有重叠就继续上浮；玩家完全出水时增加默认 `0.75` 的“出水额外上推速度”，越出水面后回落，重新碰到水体一点就立即恢复托起。
- 重生链：严格的单链结构，每个 `RespawnAnchorFeature` 持有 `nextAnchorId`、链表序号和完整链表快照；不包含自定义名字、引领点或树结构。未接入主链的节点不编号，接入后自动显示 `出生点N`；玩家死亡时只从已激活节点中选择链表序号最大的节点。
- 链表图：Inspector 按钮打开固定尺寸、不可最大化的 `RespawnAnchorChainWindow`，内部改用 Unity 原生 `GraphView + Node + Port + Edge`；打开时自动 `FrameAll` 完整显示所有节点，节点只显示 `N号重生点` 且不可拖动，位置按世界坐标相对关系固定，并在场景内移动重生锚时实时刷新；顶部“节点间距”滑杆只缩放相对距离；连接依靠端口拖拽和原生曲线边，右键连线截断，支持按场景顺序初始化和画布缩放平移。存在主链时只能从最高点继续，且始终只保留一条链；每次连线操作自动标脏并保存场景，Undo 可完整回退。
- 保存接口：`RespawnAnchorChainRuntime` 提供 `CaptureSaveData`、`TryRestoreSaveData`、`ResetRuntimeState`；`RespawnAnchorChainSaveData` 保存场景名、稳定 `anchorId`、`nextAnchorId`、链表序号、初始标记和激活状态。当前不接文件读写或存档槽。
- 验证：输入程序集、Runtime 和 Editor 静态编译通过；“重生锚”预制体、材质和栏目引用核对通过；`git diff --check` 通过。

## 当前规划地图固化为示例地图

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 来源：项目当前存档 `Library/PlanningEditorSaves/111.json`。
- 资源：复制为 `Assets/_Project/Development/LevelEditor/Orpheus0829/PlanningEditorPrototype/Editor/DefaultPlanningMap.json`，随项目一起保存。
- 行为：`PlanningDocument.CreateDefault()` 优先读取该 JSON 作为内置“示例地图”，解析失败或资源缺失时才回退旧代码示例。
- 验证：Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 能源方块合法邻格回弹修正

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 问题：`WouldTrapPlayer` 使用接近整格的 Bounds；玩家站在相邻格时与目标格仅贴边，`Bounds.Intersects` 仍返回 `true`，导致合法邻格被拒绝并回原位。
- 修改：卡住玩家检测改为检查玩家碰撞体中心是否落入目标格核心区域；玩家站在相邻格、身体边缘伸进目标格时不再误判，只有玩家中心确实在目标格内才禁止放置。
- 吸附：能源块开始拖拽时先把当前真实位置吸附到所属格中心，避免预制体根节点不在格中心时，拖动一格仍被换算回原格、表现为需要多拖一格。
- 验证：Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 弹性植物玩家侧落差与跨植物衰减

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 架构：`PlayerController` 管理当前下落高度、最近落地落差、落地向下速度、连续弹跳冷却和衰减倍率；弹性植物只根据玩家给出的本次落差计算并返回弹跳速度。
- 接口：新增 `IPlayerBounceSurface`。玩家在真实落地时从脚下碰撞体获取弹跳表面并请求速度，植物不再依赖 `OnCollisionEnter/Stay` 猜测摔落。
- 跨植物：连续弹跳衰减不再保存在单个植物上，玩家在不同弹性植物之间连续弹跳时不会因为换植物而重置衰减。
- 输入：弹性植物顶面按住 S 时不会触发新弹跳，已有的向上弹跳速度会被 `PlayerController` 清零；已删除“按住跳跃提高 1.5 倍高度”的参数和计算。
- 触发收口：摔落弹跳只在首次接触植物顶面时尝试，不再从 `OnCollisionStay` 的持续接触中补触发，避免走路进入后被判定为落地反弹。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 道具组件数值编辑覆盖隐藏子物体

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：`LevelEditorPropComponentEditorWindow` 的组件枚举。
- 行为：预览从只枚举根物体组件改为递归枚举所有子物体组件并包含 inactive/disabled 组件；组件列表继续排除 Transform，数值覆盖按原有组件路径和索引保存。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 红蓝蒸汽与弹性落地修正

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 蒸汽：所有 `WaterVisualFeature` 蓝系组件统一新增红交互蒸汽参数：瞬间/持续、持续时长、推力、作用高度、热浪特效资源、瞬间特效停留。
- 行为：红色作用于任意蓝系物体后不销毁该物体，在物体顶部生成热浪资源并按配置向上推玩家；持续模式期间重复施加推力，结束后清理热浪。
- 弹性植物：把有效下落判定改为 `PlayerController` 自己记录最近离地时长和落地时间；即使测试玩家没有 `PlayerFallDamage`，只要满足最近确实处于下落状态就触发摔落弹跳。
- 弹性植物第二轮：改用 `LandedThisStep` 判定真实落地，摔落弹跳改为立即写入 Rigidbody 竖直速度；同平面走入不会触发，落地反弹不再等下一物理步。
- 弹性植物第三轮：`LandedThisStep` 只表示重新接地，仍会把走进高差误判为落地；现在额外记录空中最高点、落地真实下落距离和落地向下速度，只有 `LastAirTime`、下落距离或当前下落速度满足条件才触发。
- 冷却熔岩：`CoolLava_Block` 的 BoxCollider 从触发器改为实体碰撞，可以直接站上去。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 藤蔓生长物独立组件

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 生长链：母藤蔓新增“生长物预制体”参数，`Green_Ladder.prefab` 绑定 `Green_LadderSon.prefab`。逐块/瞬间生长都只能生成该预制体，不再复制母藤蔓自身。
- 组件：新增 `LadderSonFeature`，继承攀爬和出现动画能力；红色可以烧毁，蓝色浇灌不会再次生长，防止无限套娃。
- Inspector：`BlockFeatureEditor` 现在沿完整继承链收集 `BlockParameter`，LadderSon 会完整显示与藤蔓相同的攀爬参数；生长参数仍按内部禁浇灌状态隐藏。
- 弹性植物：补回有效落地记录，使用最近落地时间与最近下落距离作为碰撞回调期间的兜底，避免下落距离在碰撞前被清零导致完全失去反弹。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 移动平台载人修正

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：`MovingPlatformFeature` 的玩家承载链路。
- 行为：平台只在玩家真正站在顶面时登记承载；平台每帧把自身位移作为命令交给 `PlayerController`，由玩家的 Rigidbody 在自己的 FixedUpdate 中应用，不再直接改玩家 Transform。
- 收紧：承载登记要求玩家处于接地状态、脚底接近平台顶面且中心在平台水平范围内；侧面/角接触法线阈值提高到 0.8，半空擦到平台不会登记为承载。
- 边界：不把玩家挂成平台子物体，避免 Rigidbody 与移动父物体产生双重位移和物理抖动；离开顶面、侧面接触或碰撞结束时立即取消承载。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 点击染色与气泡柱持续上浮修复

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 点击染色：左键射线改为直接查找 `IColorApplicationTarget`；`BlockFeature` 统一实现接色入口，水域、水流、气泡、岩浆、藤蔓、植物障碍和弹性植物补上直接反应路径。
- 反应：红色清除绿色物；蓝色让藤蔓生长；红色可通过预制体参数替换蓝色物；蓝色作用于岩浆。
- 气泡柱：删除“上方必须有蓝系物体”和“最高顶起格数”条件；默认推力翻倍到 7。持续跟踪玩家，玩家碰撞体与任意蓝色物的垂直重叠超过自身高度一半时保持浮力和气泡水状态；露出超过一半后立即移除气泡水状态、清空浮力并清除剩余向上速度，避免继续冲高；重新下沉会立即恢复上浮，横移离开气泡列或落到气泡下方才清除状态。
- 气泡柱进入/离开：原先触发体和“气泡列保留”使用两套横向范围，轻微离开可能先被移除跟踪，再回来时不会重新触发。现在统一用气泡碰撞体与玩家碰撞体的实际横向重叠范围判断，回到重叠范围后立即恢复跟踪和上浮。
- 弹性植物：扩大顶部接触容差，增加碰撞停留兜底，降低默认最短触发间隔；新增“连续弹跳衰减比例”、“衰减重置时间”和“最低弹跳高度（格）”。只有存在有效下落距离时才触发摔落弹跳，同平面走入不再自动弹；连续衰减后的预测高度低于最低弹跳高度时不再触发。
- 验证：Unity 6000.3.12f1 Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。

## 藤蔓顶面与弹性植物高跳修正

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 藤蔓：进入攀爬前新增侧向接近判定；玩家站在藤蔓顶面时不再进入攀爬，离开侧向范围后正常退出。
- 弹性植物：顶部接触优先按碰撞体上下边界判断；默认基础弹速提高到 14，摔落转化提高到 1.8，最大弹速提高到 28，保证跳跃和摔落都能形成明显高跳。
- 边界：不修改玩家基础跳跃参数，不修改其他绿色功能组件。

## 冷却熔岩深蓝高对比材质

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：`CoolLava_Block` 预制体的表现材质。
- 行为：新增独立 `CoolLava_Block` 材质，沿用红蓝绿同一套描边 Shader，基础色为深蓝，轮廓保持黑色。
- 边界：不修改 `Color_blue` 等共享颜色材质，也不修改预制体碰撞、组件、位置或替换逻辑。

## 融合编辑器栏目紧凑排版

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：仅调整右侧“方块 / 道具放置器”的栏目布局。
- 行为：方块栏目滚动区改为和道具栏目一致的一行内容高度，超出后在栏目内部滚动；道具栏目紧跟方块栏目，不再被方块栏目的固定空白推开。
- 边界：不修改栏目数据、贴画、组件模板、放置、拖拽或场景生成逻辑。
- 验证：Unity 6000.3.12f1 `Assembly-CSharp-Editor.rsp` 静态编译 0 错误；`git diff --check` 通过。

## 固定功能组件第一版

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：新增 `Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features`，实现红色机关/危险物、蓝色水域、绿色植物三类固定功能组件，并挂到 12 个 `Red_* / Blue_* / Green_*` 玩法预制体。
- 红色：`LavaHazardFeature`、`MechanismBaseFeature`、`ButtonLockFeature`、`CrankPlatformFeature`、`MovingPlatformFeature`；另外准备 `LiftFeature`、`SpikeFeature`、`LadderFeature`，等待对应预制体。
- 机关补充：`Red_Battery` 改为 `EnergyBlockFeature`，只有能源方块允许迁移；`Red_Foundation` 的 `MechanismBaseFeature` 只检测贴贴能源并作为 `IMechanismSignalSource` 输出；按钮改为无实体阻挡的交互目标；曲柄改为长按交互推进。
- 蓝色：`WaterSourceFeature`、`DirectionalCurrentFeature`、`BuoyancyColumnFeature`。
- 绿色：`ClimbableVineFeature`、`BouncePlantFeature`、`PlantObstacleFeature`。
- 接口边界：玩家接触统一走 `IPlayerContactReceiver`；机关供电/接收预留 `IMechanismSignalSource`、`IMechanismSignalReceiver`；红绿、蓝绿、红蓝互相反应暂不实现，只保留类型安全接口和 TODO 口径。
- 输入特例：`Interact` 固定为“点击 + 长按”，按键映射不能把它改成仅点击或仅长按；玩家输入驱动同时分发 `IInteractionTarget` 和 `IInteractionHoldTarget`。所有 `BlockFeature` 新增 `IFeatureVisualTarget` 专属材质和渲染器接口。
- 蓝色表现：三种蓝色功能组件统一继承 `WaterVisualFeature`，蓝钥匙解锁时从 `ColorCatalog.blueWaterPrefab` 实例化队友水体，并暴露水体预制体、本地偏移和缩放。
- 第二轮校正：除岩浆外取消坠落伤害；水泡改为格数最高顶起和标签识别；水流改为左右枚举；弹跳植物改为摔落/跳跃/两者模式；按钮改为材质拉杆；曲柄改为每秒进度和无人回弹动画；基座改为四方向网格检测；能源方块加入顺时针/逆时针一格迁移；移动平台改为格数、强制载人、Tag 选择和任一/全部开关逻辑；水域和水泡不再暴露摔落免疫，玩家控制器与坠落记录中的免疫栈一并删除。
- 第三轮校正：`LevelEditorPlacedBlock` 记录并实现 `IGridCellSizeProvider`；能源、曲柄、基座、移动平台、水泡删除组件内每格尺寸字段，统一读取关卡格子世界尺寸。藤蔓不再配置独立段预制体、段高、动画时长或缩放曲线，直接克隆自身向上生成，逐块生长时每块播放自身出现动画，瞬间长完时只播放最上方新块。机关控制方向收口为平台等消费者绑定基座/按钮/曲柄，基座不再持有绑定机关列表。
- 第四轮校正：浮力水柱上方蓝系检测从世界单位距离/半径改为“向上检测高度（格）”和“横向检测宽度（格）”；检测盒从气泡上边缘开始，按关卡格子世界尺寸换算。
- 第五轮校正：`PlayerColorWheel` 左键从“交互圈内最近目标”改为“屏幕射线命中最靠前可交互物体，再检查是否在交互圈内”；圈外和 UI 点击不响应。万能方块继续作为前期辅助代理，由点击目标转发颜色。
- 万能方块校正：删除单目标字段，自身改为纯色材质，向左右上下四个紧邻的 `IColorApplicationTarget` 广播颜色；新增“持续染色 / 过一会褪色”模式和褪色延迟秒数，Inspector 显示当前颜色、倒计时和广播目标数。
- 第六轮校正：`Tab/F` 只开关染色轮盘，`E` 负责交互，左键只执行已选颜色染色；删除玩家驱动的 2D/3D 切换。水族水体按格子中心对齐；藤蔓改为实体阻挡并按格进入攀爬，支持自然下滑、S 加速下滑、空格固定与 AD 离开；弹跳植物改为实体碰撞单次弹跳；按钮增加按格交互半径；移动平台碰到方块后反向。
- 马夫/马修正：`PlayerInputDriver` 读取输入后只调用 `PlayerController` 的轮盘、移动、跳跃和交互命令；`PlayerColorWheel` 不再直接读取 `GameInput`。
- 字幕展示道具：为 `DisplayBlock.prefab` 接入 `DisplayBlockFeature`，运行时隐藏碰撞体和方块外观，创建世界空间 TMP 文字；支持内容、字体、相对方块高度的字号比例、颜色、九宫格相对位置、左中右对齐，以及相对 `Player` Renderer 的上方/下方排序。
- 能源方块拖拽校正：删除一格瞬移方案，改为交互圈内长按“搬运”键拿起、鼠标网格吸附拖拽、松手放下；搬运键默认右键并锁定长按。拖动时全输入锁住、碰撞关闭、渲染最上层，目标无效回原位，原位无效则从原位发散寻找合法格，并禁止放到会卡住玩家的格子。
- 输入与拖拽修正：`Carry` 缺失 action 时自动补右键/手柄北键；编辑器刷新也会修复旧资产，键盘“＋添加”可直接进入听取。拖拽位置限制在玩家交互范围圈内，松手强制结束拖拽并恢复全部控制；“调色”动作允许选择点击或长按，点击式为开关，长按式为按住。
- 道具演示标签：道具栏目新增 16 个复用 `DisplayBlock` 的字幕展示条目，排除已有字幕块和锚点；条目名使用 `字幕：xx道具`，`DisplayBlockFeature.text` 覆盖为对应道具名，用于摆放在功能道具旁做演示。
- 预制体：需要接触的玩法物改为 Trigger；移动平台和植物障碍保持实体碰撞。每个玩法物只挂一个固定功能组件。`PlayerHealthHud` 已从玩家预制体和源码中删除，岩浆保持一碰即死。
- 验证：Runtime/Editor 静态编译 0 错误；已实现 15 个固定功能组件，12 个现有预制体均已挂载对应组件；玩家水体推动与浮力已并入 `PlayerController` 游泳速度计算；`git diff --check` 通过。PlayMode 手感、碰撞矩阵和实际水体表现待 Unity 内复验。
- 交接提交：待提交。

## 示例地图道具大厅重建

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：重建 `PlanningDocument.CreateDefault()` 内置示例地图；不修改正式关卡场景。
- 布局：R1 扩为 3×1 世界格道具大厅，18 个现有道具分两层陈列；玩家起点记录在大厅地面并随示例地图读取时同步到已引用的场景玩家。
- 其他房间：R2-R5 改为地面高度一致的红色机关、蓝色水流、绿色攀爬和终点预留房；通道按各房间地板高度连接，避免悬空或断线。
- 验证：Runtime/Editor 静态编译 0 错误；示例地图包含 18 个道具引用，ID 全部存在且无重复；`git diff --check` 通过。
- 交接提交：待提交。
- 后续调整：R1 扩为 7×2 世界格，改为单层测试长厅；玩家和三把钥匙集中在最左侧，其余道具按通用展示、红色、蓝色、绿色连续分区，一屏地面内逐个检查功能。

## 万能方块预制体基础环境

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：整理用户新增的 `Assets/_Project/Content/ColorBlocks/Prefabs/Universal_Block.prefab`。
- 行为：预制体改为只保留 Mesh、Collider、Renderer 和 `UniversalColorBlock`；状态渲染器指向自身 Renderer，使用中性材质，层恢复为 Default。移除误挂的 `ColorObject`、`BlockRuntime` 和 `BlockAbilityHost`，避免万能方块被当成红色解锁物体。
- 栏目：关卡编辑器道具栏目已有“万能方块”入口，本轮不重复新增。
- 验证：预制体结构、道具栏目引用和中性材质已核对；Runtime/Editor 静态编译 0 错误；`git diff --check` 通过。
- 交接提交：待提交。

## 颜色大洗牌完整性审计与护栏清理

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：全项目审计旧交互图代码、资产 GUID、预制体挂载、关卡编辑器栏目职责、颜色对象职责、玩家选色入口、文档和退役 Skill。
- 清理：删除已无引用的 `ColorBlock` 过渡适配器、`ColorBlock_*` 预制体及其编辑器入口；保留 `ColorCatalog` 的水体预制体引用，只移除旧的自动生成水体逻辑；移除方块栏目残留的三色管理字段、校验和默认栏目；停止给普通墙块自动补 `BlockAbilityHost`；删除轮盘遗留的待执行目标路径；删除退役交互图 Skill 中的旧正文。
- 边界：不改场景文件，用户将在预制体和生成逻辑清理后重建场景；不新增水源、水流、岩浆、机关等具体玩法。
- 验证：Runtime/Editor 静态编译 0 错误；`git diff --check` 通过；旧类型、旧 GUID、栏目字段和预制体挂载的非场景引用清零。`Assets/Scenes/Test.unity` 与 `Assets/RoyTest/RoyTestScene.unity` 仍保留待重建的旧三色预制体实例，本轮按用户要求不修改场景。
- 交接提交：待提交。

## 颜色大洗牌结构迁移

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：删除旧物体交互图运行时、GraphView、编辑器同步、Definitions/Catalog 和动态能力目录；把现有 `ColorBlock` 改成固定颜色状态过渡组件；新增 `ColorObject`、`IColorObject`、`IColorApplicationTarget`、`IColorApplicationProxy`、`UniversalColorBlock`；把 `BlockAbilityHost` 改成只启停预制体已有 `BlockFeature` 的薄宿主；钥匙和颜色应用改为直接路径。
- 删除：`InteractionManager`、`InteractionGraph`、`InteractionObject`、`InteractionObjectDefinition`、`InteractionObjectCatalog`、交互图和定义编辑器、图资产、`BlockAbilityCatalog` 与动态目录；移除关卡编辑器中的交互入口和序列化字段。
- 保留：`ColorRuntimeService`、`HSVColorFadeManager`、`InteractiveWater`、`ColorKeyPickup`、玩家生命/水体/攀爬/轮盘相关能力，以及锚点、藤蔓、弹跳等具体玩法组件。
- 边界：本阶段只完成结构迁移和固定颜色启停，不新增水源、水流、浮力、岩浆、机关等具体玩法；旧 `BlockWaterFeature`、`BlockVineFeature`、`BlockPowerFeature` 已删除，颜色 prefab 当前只保留渲染、碰撞、`ColorBlock`、`BlockRuntime` 和 `BlockAbilityHost` 占位骨架。
- 约束扩充：`BlockFeatureAttribute` 新增 `Category` 和 `Interactions`，基础接口新增 `IPlayerContactReceiver`、`IObjectContactReceiver`、`IColorReactionReceiver`，`BlockFeature` 实现 `IRoomColorResettable`，为后续固定形态组件提供统一约束。
- 验证：Runtime/Editor Roslyn 静态编译 0 错误；图类型、图资产和旧动态能力目录引用已清零；待 Unity 重新导入后复验预制体、场景和 Inspector。
- 栏目边界：方块栏目只保存地图墙壁、地形和几何碰撞；红蓝绿玩法预制体全部通过道具栏目放置。已配置玩法预制体位于 `Assets/_Project/Content/ColorBlocks/Prefabs`，使用 `Red_*`、`Blue_*`、`Green_*` 命名。
- 预制体骨架：12 个颜色玩法 prefab 已从过渡 `ColorBlock` 切换为 `ColorObject`，当前只保留渲染、碰撞、`BlockRuntime` 和 `BlockAbilityHost`；旧具体功能组件已从代码和 prefab 中移除。
- 交接提交：待提交。

## 颜色大洗牌 Skill 强制入口

- 日期：2026-10-07；负责人：Codex；分支：`codex/orpheus0829/颜色大洗牌`。
- 范围：把颜色大洗牌之后的固定颜色物体、钥匙解锁、房间重置、万能方块代理、固定形态、直接组件交互和交互图退役规范写入颜色脚本目录，并建立项目级 Skill 入口。
- 接口：新增 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md` 作为完整规范；新增 `.agents/skills/taptap-color-gameplay/SKILL.md` 作为发现入口；更新根 `AGENTS.md` 和交互 Skill，要求颜色系统开发前完整读取完整规范。
- 边界：本记录只建立规范和发现入口，不修改运行时颜色代码、场景或预制体。
- 验证：文档和 Git 补丁检查；Runtime/Editor 未修改。
- 交接提交：待提交。

## 编辑器 Play 模式输入焦点修正

- 日期：2026-10-06；负责人：Codex；分支：`codex/orpheus0829/start-menu-settings`。
- 范围：新增编辑器专用的 Input System 焦点策略，在进入 Play 前后都将输入改为始终发送到 Game 视图，并忽略 Game 视图失焦导致的后台设备禁用；同时让运行时读取输入前自动恢复 Gameplay 动作表，并强制把只读 Move 动作修复为默认 WASD 加手柄绑定。
- 边界：仅在 `UNITY_EDITOR` 下启用，不进入玩家构建；不修改玩家控制、动作绑定、场景或 InputActionAsset。
- 验证：使用项目 Editor 响应文件通过 Roslyn 编译，0 错误。

## 开始界面、设置与存档空壳

- 日期：2026-10-06；负责人：Codex；分支：`codex/orpheus0829/start-menu-settings`。
- 范围：新建 `Assets/_Project/Development/StartMenu/Orpheus0829/Scenes/StartMenuPrototype.unity`，使用 Canvas/UGUI 搭建开始界面原型；主菜单提供开始游戏、设置、退出，开始游戏二级菜单提供继续上次游戏、选择存档和返回；设置提供声音、按键映射、制作人员介绍。
- 脚本结构：按项目系统目录约定新增 `Assets/_Project/Code/Systems/StartMenu/Runtime|Editor`、`Assets/_Project/Code/Systems/Audio/Runtime` 和 `Assets/_Project/Code/Systems/Saving/Runtime`；音频管理器先只提供全局静音，按键映射只消费现有 `InputBindingService`/`InputActionInteractionPolicy`，不改核心绑定逻辑、不新增操作；存档模块先提供空壳接口和界面。
- 场景：已创建独立原型场景 `Assets/_Project/Development/StartMenu/Orpheus0829/Scenes/StartMenuPrototype.unity`，不加入 Build Settings；编辑器脚本 `Tools/2026TapTap/UI/生成开始菜单原型` 直接在场景内创建 Camera、EventSystem、Canvas、面板、按钮、设置页、存档页和占位背景，不使用运行时临时 Canvas，也不生成 Prefab 资产。生成器只允许手动点击，不自动执行。
- 适配：CanvasScaler 不固定输出分辨率，改用 `Screen.width/Screen.height` 在实际设备上实时计算 `ConstantPixelSize.scaleFactor`；窗口或设备分辨率变化时自动重算，缩放范围钳制在 1x 到 2.5x，避免高分辨率屏幕上按钮过小。
- 界面：运行时全部使用原生 UGUI Canvas/Image/Text/Button/Toggle/ScrollRect，不使用 UI Toolkit；按键映射改为顶部设备切换、每行一个操作、左侧操作名、中间单个改键框、右侧点击/长按列，只列出当前设备真实存在的绑定；点击改键框会显示“正在接听，请按下目标按键”，接听期间临时关闭 InputSystemUIInputModule；移动固定只读为默认键位，底层 `InputActionBindingPolicy` 统一禁止修改，编辑器窗口和运行时设置页都查询该策略并禁用对应控件；`Look / 视角` 通过统一显示策略从两套按键映射界面隐藏，底层动作仍保留；键盘鼠标显示 `WASD（A/D 左右，W/S 上下）`；设置页改为左侧栏目列表加右侧内容面板；存档卡片补上删除按钮。
- 安全开关：新增 `Project.Settings.Runtime.RuntimeSettingsPolicy.ApplyChangesToRuntime`，当前默认 `false`，统一控制运行时设置页的按键映射、音乐音量和存档操作。关闭时三者都只写入内存预览：不改正式 InputActionAsset、不写音量 PlayerPrefs、不创建或删除磁盘存档。原编辑器按键映射窗口保持原有持久化逻辑，不受这个开关影响。
- 输入链：`InputActionBindingPolicy` 统一提供按键映射界面的动作集合与显示名；原编辑器、玩家动作抽屉和运行时设置页都读取同一份数据。`CameraModeSwitch` 的显示名统一改为“调色”，内部动作 ID 暂时保留以兼容现有相机绑定。
- 平台适配：按键映射页按实际设备和平台显示设备栏目；编辑器也会隐藏未连接的手柄，桌面端只显示键盘鼠标并仅在检测到手柄时显示控制器，触屏隐藏；移动端显示触屏，并仅在检测到手柄时显示控制器。手柄连接状态变化时自动刷新栏目。
- 声音设置：移除运行时界面的布尔全局静音，新增总音量、背景音乐、音效三条滑块；AudioManager 统一保存三项音量，实际音乐为 `总音量 × 背景音乐`，实际音效为 `总音量 × 音效`，目前仅建立控制关系和持久化，未绑定正式音频资源。
- 音频接入：`GameAudioService` 保持 `Systems_Audio` 常驻播放职责，初始化时获取 `AudioManager` 并订阅音量变化；音乐源应用音乐音量、音效源应用音效音量、`AudioListener` 应用总音量。主菜单和后续 ESC 设置只操作同一份 `AudioManager` 音量数据。
- 场景链路：开始菜单正式生成路径改为 `Assets/_Project/Scenes/Flow/MainMenu.unity`，场景自带 `GameFlowSceneRoot` 和 `StartMenuFlowBridge`；开始按钮在真实 GameFlow 下请求进入 `Level01`，单独打开原型时仍进入本地二级菜单。`GameUiRouter` 检测到场景内 `StartMenuController` 时会隐藏常驻的旧主菜单画面，避免叠两层 UI。
- 异步加载：新增 `GameSceneLoader` 和 `IGameLoadingScreen`，流程场景切换统一走异步加载；新增 `GameLoadingScreenAdapter`，后续把加载 Canvas、进度条和百分比文本引用拖入即可接管显示。常驻系统场景启动阶段不显示加载画面，正式流程场景切换会显示。
- 存档显示：存档名称统一为“章节进度 + 保存时间”，时间使用本地时间 `yyyy-MM-dd HH:mm:ss` 24 小时制；存档卡片主名称显示该组合文本，操作区保留读取和删除。
- 验证：正式 `Assets/_Project/Scenes/Flow/MainMenu.unity` 已生成并包含 Camera 外的实际流程 UI；场景包含 `StartMenuPrototype`、`StartMenuFlowBridge`、`GameFlowSceneRoot`、Canvas、主菜单、开始二级菜单、设置和存档页面。一次性 `StartMenuPrototypeBuilder.cs` 及 `Project.StartMenu.Editor` 已删除。PlayMode 交互仍需在 Unity 中复验。

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
- 接口：新增 `PlayerHealth`、`PlayerFallDamage`、`PlayerHealthHud`、`GreenBouncePad`、`BlockSprayEmitter`、`VineGrowthEmitter`、`AnchorPoint`、`NextJumpBounceEmitter`；`InteractionNodeKind` 增加 `RequireOtherColor` 与 `RequirePlayerColor`；`PlayerController` 增加游泳倍率、摔落免疫、弹跳请求入口和自动补齐 `PlayerColorWheel`。
- 默认参数：水体速度倍率 `0.55`、进水恢复 `1`、最大生命 `5`、弹跳阈值 `2m`、弹跳曲线指数 `1.8`、弹跳速度 `10~20m/s`、喷流距离 `4m`/持续 `1.5s`、锚点搜索 `8m`、藤蔓速度 `4m/s`、段长 `1m`、上限 `32`。
- 图资产：红/蓝/绿方块定义已重写为清晰分支链，并加入 `受到其他物体颜色` 条件；预制体已绑定喷流、藤蔓、落差和下一跳组件。
- 验证：已完成静态源码/资产核对；补充 `PlayerColorWheel` 的玩家自动挂载、UGUI EventSystem 兜底、绿色方块站立时的轮盘目标屏蔽，以及包含子 Collider 的最近点距离检查。使用 Unity 6000.3.12f1 Roslyn 响应文件完成 Runtime/Editor 静态编译，均 0 错误；`git diff --check` 通过。Unity 编辑器与 PlayMode 仍需在本机清空 Console 后实测轮盘、距离圆和接触矩阵。
- 编辑器复验补充：本机 Unity 进程正在运行，但当前 Codex 桌面自动化会话的原生窗口清单为空，无法代点菜单；已直接读取 `%LOCALAPPDATA%/Unity/Editor/Editor.log` 获取 Console 记录。日志中发现 `PlayerColorWheel` 的 `ColorBlock` 命名冲突，已改为显式使用 `Project.ColorBlocks.ColorBlock`。锚点预制体的旧报错来自脚本重载/资产管线刷新期间的重复定义；交互同步现在会按预制体资产路径复用已有定义，并在目录引用暂时丢失时恢复已有资产，避免再次生成“锚点 1”。Unity 重启后的最新日志未再出现编译或预制体保存错误。
- 空 GUID 资产修复：自动同步曾把锚点定义写成重复资产，并在目录中留下 `{fileID: 0}`；已恢复唯一的 `锚点.asset`（GUID `c8d1e2f30456789abcdeffedcba9876`），同步修正目录、关卡栏目和锚点预制体引用，并将旧式 ScriptableObject 类标识改为 Unity 6000 可解析格式。Unity 同时补齐了锚点预制体 `MeshRenderer` 的 GameObject 引用；目标资产中不再有全零 GUID。同步代码现在会优先选择不带数字后缀的正式定义，并移除目录中同一预制体的重复条目。使用 Unity 6000.3.12f1 的 Roslyn 响应文件完成 Runtime/Editor 静态编译，均 0 错误；目标 YAML GUID 检查通过。Console 中此前的错误属于自动同步期间的历史记录，待清空面板后再做最终复验。本地修复暂不推送，等待开发者确认。
- 清理补充：确认 `锚点 1.asset` 只是同一预制体的无引用副本，已删除该副本及 `.meta`，目录、关卡栏目和预制体统一指向正式 `锚点.asset`；水体组件中原“颜色管理器水体”文案改为 `ColorRuntimeService` 共享颜色水体，保留实际仍在使用的共享视觉模式。
- 运行时兼容修复：颜色轮盘与生命值 HUD 的 UGUI 文本统一使用 Unity 6000 支持的 `LegacyRuntime.ttf`，移除会在右键打开轮盘时抛出的 `Arial.ttf` 内置字体异常；Runtime/Editor 静态编译均 0 错误。
- 本轮交互修复：颜色钥匙先解锁颜色、恢复材质并启动水体渐显，再播放相机 Timeline，避免演出阻塞玩法状态；右键轮盘改为按住显示、按指针方向高亮环形选项、松开确认并隐藏；左键使用当前选色能力触发物体图，默认能力为空，切换和交互结果均输出 Console 日志。
- 蓝色钥匙链路诊断补充：将解锁与 `SetColorFaded(false)` 前移到 `OnInteractionPlayerEntered` 的同步入口，并在物体图触发、方法调用、Unlock 成功/失败和钥匙入口处增加诊断日志；确认 `Publisher socket is null` 来自 `com.merry-yellow.code-assist` 的本地 MQTT 编辑器插件，不属于运行时交互图。Computer Use 当前未返回可控 Unity 窗口，Runtime/Editor 静态编译仍为 0 错误。
- 颜色目录反序列化修复：`ColorTypeDefinition` 补齐 Unity 所需的 `[Serializable]`，修复 `ColorCatalog.asset` 虽含 red/green/blue 但运行时 `Find("blue")` 为空的问题；钥匙解锁链路待 Unity 重载脚本后实测。
- 未完成：未合并 `main`、未推送；需要在 Unity 内放置红蓝绿方块与锚点，确认藤蔓增长视觉、HUD、输入轮盘和水体状态，并在验收后更新本条记录。当前 Computer Use 未返回可控 Unity 原生窗口，只能以 Editor.log 和静态编译作为本轮证据。
