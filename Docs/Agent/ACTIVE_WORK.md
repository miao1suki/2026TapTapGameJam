# 工作状态与交接（每次任务重读）

关卡交付提醒：修改关卡必须同时交付 Scene、同名 `.planning.json`、各自 `.meta` 及新增资源依赖；禁止只推场景或把存档留在 Library。提交前运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`，小地图拼合使用“协作与片段”。

## 2026-10-15 协作编辑器整合

- 用户要求先整合并推送此前已完成的协作地图功能；绿色钩子问题排查、收集品与四向尖刺本轮暂缓，不宣称已修复。
- 整合最新 main 与音频新增的重生点素材；保留本机 Pick 布局、字体备份、插件和 PackageManager 设置，不将其混入提交。合并后重新运行协作与相关回归验证。

## 2026-10-15 协作地图与片段交换

- 分支 codex/editor/collaborative-maps；验证并完善世界图整房间连同内容移动，项目共享存档/场景关联与提交检查、片段导入当前地图、选中房间或框选区域导出、水与道具同格及分层预览。保留本地Pick布局、字体、插件和设置，不自动提交推送。
- 存档兼容旧Library文件与场景嵌入数据；导入使用事务、ID重映射和冲突检测，不覆盖目标地图。编辑器使用现有UI Toolkit与自身撤回/自动同步入口，不改变运行时颜色行为。
- 已完成：世界图默认连内容/实际玩家一起移动，跨所有者完整装配补丁保护；世界图右侧“协作与片段”支持追加导入、定位预览、选房间/框选区域导出，保留内部门/按钮/基座/曲柄绑定并重映射ID；普通笔刷区域可裁剪，完整合并物体不静默切半。水与物品分层占格、显示与分别擦除，增量同步按ID+格匹配避免丢物体。
- 新存档默认Assets/_Project/Content/LevelMaps；场景保存/应用导出同名planning.json，旧Library存档复制迁移。共享存档外部更新时拒绝失效窗口覆盖并备份到Library/PlanningConflicts。新增.githooks提交检查并在本克隆启用；其他成员首次配置步骤写入协作约定。文档：LEVEL_EDITOR_COLLABORATION.md / PLANNING_COLLABORATION_API.md。
- Unity 6000.3.12f1隔离验证：协作数据/真实水体与物品同格生成/增量保留/分别擦除/玩家跟随/外部更新保护36项、窗口导入与自定义撤回/重做7项通过；旧网格与存档1284项通过，相关EditMode回归140/140通过。Windows PowerShell提交检查已验证漏存档、漏meta、漏暂存拦截，并在隔离Git仓库完成实际hook提交。最终窗口美观、真实鼠标框选、多机往返仍待策划人工验收；未打包，未提交/合并/推送。

## Timeline 对话轨道

- 日期：2026-10-15；负责人：Codex；分支：`对话气泡`。
- 范围：在 `Assets/GJ_Tools/TimelineTools3D/AboutTimeline` 新增对话轨道、对话片段和中文 UI Toolkit Inspector；轨道不绑定对象，直接向常驻 `WordNote_UI` 申请使用权。
- 行为：Timeline 片段开始后通过 `WordNoteUI.RequestTimeline` 创建消息，与普通 API 调用共用总队列、位置队列、Canvas、堆叠和动画；片段结束时释放本次 Timeline 句柄。
- 参数：片段直接使用 `WordNoteStyle`，包括字体、字号、颜色、描边、位置、显示模式、堆叠、入退场动画、组合通道、打字机和完整运动参数。
- 状态：已完成。世界空间气泡、气泡图和独立 Canvas 已移除；Runtime 与 Editor 程序集静态编译通过。

### 2026-10-15 三色钥匙测试片段清理

- 日期：2026-10-15；负责人：Codex；分支：`对话气泡`。
- 范围：删除三把颜色钥匙 `ColorPickup_*.playable` 中的临时对话测试轨，以及 `ColorProjectSetup` 自动补测试轨的逻辑；保留对话轨道模块本身，供后续按需添加。
- 验证：Runtime 与 Editor 程序集静态编译通过，三条 Timeline 的对话轨、WordNote 参数和对象 ID 已检查；Unity Play Mode 实际显示待验收。

## 绿色钩子

- 日期：2026-10-15；负责人：Codex；分支：`钩子`。
- 范围：新增 `Project.GreenHook` 的玩家钩子控制器和目标契约；绿色选色不再绘制目标预览线，改为玩家身上一格、50% 透明度的待机藤蔓头；点击有效目标后按四向吸附并以正常浇水藤蔓速度伸出。
- 行为：`ContactCube` 被拉向玩家；机关基座、藤蔓、弹性植物拉动玩家；路径忽略墙体和物品碰撞，但不能离开当前房间；开始伸出时锁定玩家重力、移动和交互；结束时恢复。
- 调整：出藤点上移，正式钩子伸长速度提高到 `5.22 格/秒`；ContactCube 被拉后会在落稳时寻找最近可用网格并吸附到格子中心。
- 命中修正：钩子伸出时不再默认拉取；藤蔓头必须在伸出过程中命中目标才进入拉动，未命中会按相同速度原速收回，收回完成后再解除玩家限制。
- 吸附修正：绿钩子拉动的 ContactCube 会持续重试网格吸附，每个候选格同时校验房间归属、关卡网格占用、其他可搬运物占用和物理碰撞；没有合法格时保持当前落点，不删除或覆盖其他方块。
- 双向兜底：ContactCube 先拉向玩家；回收路径上检测到任何物理物体时立即切换为拉动玩家向色块移动，只有玩家也受阻时才结束钩子。
- 终点修正：正式钩子与拉动线的终点改为目标有效碰撞体包围盒中心，不再使用距离玩家最近的碰撞体边缘点。
- 接触收尾：目标碰撞体一旦接触玩家立即结束拉动，并沿两者连线做最小分离，避免目标继续挤压玩家。
- 最终校准：ContactCube 拉动结束后只执行一次位置校准；优先范围失败会扫描当前房间全部格子，取最近合法格并对齐中心，禁止停留在两个格子中间。
- 拉玩家隔离：ContactCube 回收路径不干净时直接进入拉玩家模式，方块保持原位置，不执行分离或网格校准。
- 单运动体收口：拉方块与拉玩家共享一个互斥模式；切换到拉玩家时先停止并冻结 ContactCube，再对玩家施力，避免两个对象同时移动。
- 模式判定收口：ContactCube 先执行拉方块，按 Rigidbody 实际位置检测是否真实停滞；停滞才改拉玩家。移除路径几何和插值 Transform 判定，拉动速度从 4 翻倍为 8。
- 受阻判定修正：不再用“与玩家距离未缩短”判断物体受阻，改为检测方块或玩家单个物理步内是否真实位移；距离远但持续运动不会误切换成拉玩家。
- 接入：`PlayerColorWheel` 绿色分支转到 `GreenHookAbility`；`InteractionLinkEffect` 仅保留红蓝预览；两份正式玩家预制体已挂载绿钩子组件；`PlayerController`、`ContactCubeMotion` 增加钩子运动生命周期。
- 验证：Assembly-CSharp Runtime 与 Editor 静态编译通过；待 Unity 内实际手感、房间边界和两种拉动方向验收。
## 2026-10-15 门美术、搬运与高度调整整合

- 按用户授权提交并整合至 main；fetch 后 origin/main 仍为 f5d1618，无新增主线提交。远程未合入的旧TA实验分支混有退役场景/管线改动，本轮不作为新功能整包合入；当前已使用的TA资源保持不变。
- 范围：门三色按钮/独立图案与延迟开门、搬运实体阻挡与统一提示、Pick新增WordNote_UI、高度8/10/4/3。Pick只暂存本轮新增提示节点，用户关卡布局/连线测试改动保持本地；字体缓存、PackageManagerSettings、DOTweenPro和临时ICON不提交。
- 既有门/搬运Play验证及本轮高度14项通过；整合前重新运行140/140项EditMode通过（Library/MergeDoorCarryTests.xml），暂存内容diff检查通过。Unity版本不变，未重新打包。

## 2026-10-15 弹跳、落差与气泡柱高度

- 在当前分支调整普通植物回弹8格、搬运植物跳跃4格、摔死门槛10格、气泡柱出水上升3格；不改普通跳跃和触发/区域重置规则。保留此前所有本地改动，不提交推送。
- 气泡柱由额外速度改为明确的出水高度参数，按玩家重力与单元格尺寸换算；同步有效玩家/植物预制体与旧测试玩家落差覆盖。
- 已完成；Unity6000.3.12f1隔离编译及14项参数/实际刚体速度断言通过（Library/HeightTuning.log），检查两种玩家预制体、植物普通/高跳/搬运、气泡重复触发及不累加环境加速。git diff --check通过；未重新打包，未声明完整关卡手感验收。

## 2026-10-15 搬运实体阻挡与测试场景提示

- 当前分支 codex/art/door-button-feedback；保留未提交门美术及用户场景/字体/插件。新增搬运物实体扫掠，由玩家物理入口裁剪位移，不恢复搬运刚体推挤。排查并统一搬运中心提示字体、大小及合并物体缩放，Pick 接入既有 WordNote 提示，不重复添加暂停/输入系统。不提交推送。
- 已完成：搬运物完整 OBB 扫掠接入玩家所有移动入口与平台位移；三色 ContactCube 补统一字体，中心提示消除合并拉伸、随设备刷新。Pick 仅新增 WordNote_UI，排除本次新增节点后文件 SHA256 与任务前完全一致（ED2BEF7D…AA35）。Test/Gaming 不改布局，复用原组件。
- Unity6000.3.12f1 隔离140/140项EditMode、20项真实物理搬运断言及3场景接线通过（Library/CarryRegressionTests.xml、CarryCollisionPlay.log）；Pick实际GPU/Play14项提示验证通过（Library/PickCarryHints.log），消息样式由现有业务处理器触发，截图Library/PickCarryHints.png。没有重新打包或安卓真机验收；当前仅本地待用户测试。
- 原有 ContactCubeCarryVerification 的58项三色搬运/完整落点/放下/区域复位实际Play回归再次通过（Library/CarryExistingRegression.log）；无新增运行时Debug日志。

## 2026-10-15 门按钮三色美术与延迟开门

- 分支 codex/art/door-button-feedback；接入新增蓝绿按钮图，配置像素导入和16×16切片。门按钮依绑定顺序红/绿/蓝，门内白色图案按实际按钮数显示，独立响应对应按钮；全部按下后保留可配置的图案展示时间再开门。保留踩踏触发、保持/持续模式、重置与Timeline相机所有权。
- 已应用到当前项目：新原图4帧16×16切片、Point/FullRect/无Mip无压缩/上限64，保留原图与GUID；门图案数量匹配1/2/3按钮、各自独立点亮，红/绿/蓝按钮不参与颜色能力。Timeline先到门附近再展示0.8秒，等待时保持阻挡，暂停冻结；持续信号释放、重置、禁用取消等待。创建次序持久化，反向读取场景也保留顺序。
- Unity6000.3.12f1隔离140/140项EditMode、27项真实GPU/Play（含Timeline及暂停）、12项规划保存/乱序/删除/旧存档兼容、39项实际压力/R/离区重置断言通过。日志Library/DoorButtonFinalTests.xml、DoorButtonCamera.log、DoorPlanning.log、DoorPressureRegression.log；实际像素截图Library/FlowRetirementProbe/Library/DoorButtons-*.png。未重新打包/安卓真机验收。
- 不改用户Pick布局（SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35）、字体、插件和其他本机配置。源码/资源/预制体已在主项目，不提交、不推送，等待用户测试。

## 2026-10-15 玩法水体接入TA像素面片与定向气泡

- 已接入：WaterVisualFeature 的默认表现切换为 TA 水面/水中/水下分区，保留原游泳、水流、浮力、蒸汽、解锁与区域重置；定向水流和浮力柱自动添加对应流向气泡，排序为水后片→角色/气泡→透明扰动板。水面按相邻水族体积识别，合并块按格平铺，水和气泡继续共享蓝色透明渐显材质。
- 不重建或覆盖用户Pick场景、字体、插件与本机设置；既有音频修改一并验证后按本次授权提交整合。Unity版本不变。当前分支codex/audio/gameplay-cues，远程main基线dbbb803，fetch后无新提交。
- 验证：132/132 EditMode（Library/PixelWaterFinalTests.xml）；PC实际GPU/Play44项、Mobile渲染配置实际GPU/Play46项通过，覆盖水下玩家可见/折射、真实HSV白化、水面邻接、四向泡泡、渐显、区域重置和8次连续重置无重复捕获板。正式Gaming21项检查通过，9个水族体积全量自动接线、3组气泡、无旧模拟水。日志Library/PixelWaterGameplayGpuFinal.log、PixelWaterGameplayMobileGpu.log、PixelWaterGaming.log。移动管线为桌面GPU验证，未进行本轮Android真机/重新打包，不声明完整关卡人工验收。
- UI Toolkit配置Inspector、策划说明GAMEPLAY_PIXEL_WATER、接入API、系统索引及颜色Skill均已更新。主窗口不重载关卡，Pick文件SHA256保持ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35。
- 新水体接入后的41项音频Play回归通过（Library/PixelWaterAudioRegression.log），包含死亡与曲柄停止声。本次将此前仅本地的音频接线与水体表现一起整合至main并推送；旧记录中的“不提交推送”为当时状态。

## 2026-10-15 剩余玩法音效接线

- 补齐死亡统一入口与曲柄停止声：`EnterDeath` 死亡防重后播放PlayerDie，OnDied不再额外播放；曲柄循环转停止时播放MachineStop，重置/卸载静默清理。隔离实际Play检查41项通过，日志Library/DeathCrankAudioPlay.log；仍仅本地修改，不提交推送。
- 分支：`codex/audio/gameplay-cues`；已完成本地接入，不提交推送。接入搬运、平台、基座、气泡/浮力、植物燃烧、岩浆冷却/玩家触岩浆、UI确认；撞击仅用于下落至少6格的落地。解谜完成/发现秘密仍待触发点。
- 保留复活默认2秒无敌，不改输入、关卡存档或用户Pick/字体/插件设置；循环声遵守停止、禁用、区域重置清理。
- 新增12个Cue，目录60个Clip引用/52个Cue。UI确认同帧去重；气泡禁用退出回调禁止重新注册浮力/声音。落地判定兼容物理接触帧竖直速度已归零，不新增落差伤害。
- 隔离125项EditMode、32项真实Play检查通过；主项目Pick SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35。未重新打包/人工试听，水体既存禁用SetParent日志不在本轮修复范围。接入表AUDIO_HANDOVER与开发说明GAMEPLAY_AUDIO已更新。
## 2026-10-15 按钮压力统一与最新进度整合

- 用户授权合并并推送本轮及此前已完成的美术、隐私入口进度；保留本地 Pick、字体缓存、插件和本机设置，不纳入提交。
- 按钮统一为玩家脚部/三色接触方块底部压力触发，ButtonLockFeature 不再注册 IInteractionTarget。门按钮与红色按钮共用压力检测；一次保持与持续压住遵守原重置约定。
- 门与梯子专用白色线稿生成图移除黑底，包括门框内部与梯档之间封闭空隙；不对其他物体全局抠黑，原始素材不修改。曲柄达到上限即停循环音，角色精灵跟随插值后的 Transform，避免物理帧与相机插值错位。
- 重复红色接触块为旧能源条目迁移后的同一预制体；道具栏去重显示，保留存档条目 ID 和索引。
- 已整合 main 场景转场、音频实录补齐及响度更新、TA 像素水/定向气泡测试资源和连线起点修正；关卡水不自动替换为新实验方案。新增按钮按下/回弹与藤蔓攀爬 Cue，补齐有效音频资源 meta。
- 隔离最终106/106项EditMode通过，23项实际Play压力/状态/停音/音频/透明底/精灵对齐检查通过；门/梯生成图剩余不透明黑像素为0。MainMenu→Gaming→暂停返回三轮通过，音乐为atmosphere E minor、timeScale与玩家物理正常。日志Library/PressureIntegrationFinalTests.xml、PressureIntegrationFinalPlay.log、PressureIntegrationMenu.log；两份颜色skill验证通过。
- 用户主窗口不重载场景，Pick SHA256保持ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35。未重新打包/真机；水禁用期间旧SetParent日志及加载时重复EventSystem/AudioListener提示另记于UPDATE_2026-10-15_INTEGRATION.md，未声明整关无警告。
- 最后同步 TA 19c5524（气泡密度/宽度/速度及多列交错控制），完整快照再次106/106测试通过，结果Library/PressureMergedTests.xml；两份颜色skill通过quick_validate。最新main基线5c8d0e8，整合者验证后合入main并推送。

## 2026-10-15 弹跳/钥匙回归与 Android 原生隐私入口

- 已实现：弹性植物上升阶段保护（竖向/横向攀爬不覆盖回弹）；钥匙隐藏状态由美术脚本遵守，完整拾取仍等待演出。Gaming 原位保留邻藤蔓实测上升2.999998格，钥匙完成飞行后2294帧保持隐藏，日志 Library/PrivacyGameplayRegression.log。
- 原生隐私：PrivacyGate.androidlib 独立进程入口、离线完整政策、同意版本哈希、拒绝退出、设置查看/撤回；attachBaseContext 拦截未同意时的直接恢复入口。切换为 Activity（非GameActivity）是接入前置入口所需，最低API25与Unity版本不变。使用游戏自身common-privacy页，未创建外部网站、未改政策法律事实。
- 构建验证：隔离工程 MainMenu/Gaming 安卓开发包成功，0错误6警告；APK唯一Launcher为PrivacyActivity，UnityPlayerActivity exported=false，PrivacyApplication已合并；仅INTERNET权限，离线正文/项目字体均在APK内。测试包 Library/PrivacyGateChecks/Android/形之彩.apk，构建日志Library/PrivacyAndroidBuild2.log。初次构建缺默认35工具链后已在插件明确使用Unity自带36工具链，不安装SDK、不接受额外许可。
- 最终4/4项AndroidPrivacyTests通过（Unity入口/元数据、缺入口拒构建、Provider拒构建、原生进程与政策正文）；结果Library/PrivacyFocusedTestsFinal.xml。git diff --check通过。
- 尚未安卓真机验收/隐私检测；当前无连接设备，Android7.1最近任务恢复及撤回停止游戏仍需真机验证。政策内注册/实名/TapSDK等内容需负责人按实际行为核对。说明与API见ANDROID_PRIVACY。Pick哈希不变，无场景/Unity版本变更，无提交推送。
- 当前范围：修复弹性植物回弹被邻近攀爬覆盖、钥匙收集完毕被美术脚本再次显示；恢复 Unity 启动前的原生隐私授权。Android 保持最低 7.1，政策使用游戏自身 TapTap common-privacy 地址；不恢复 Bootstrap、不改 Pick、不提交推送。
- 诊断证据：Gaming 的 Prop [72,28] 已发 BounceTriggered，但 Prop [71,28] 的藤蔓攀爬分支清除向上速度；隔离禁用邻根后正常回弹。钥匙 CompleteVisual 关闭 Renderer 后被 MechanismArtworkPresentation.LateUpdate 重新开启。

## 2026-10-15 物体美术接入

- 续作范围：仅反转暂停轮盘触摸上下滑动；接入用途明确的门/按钮/曲柄/移动平台、三色接触方块与颜色钥匙素材。曲柄按实际进度正放/回弹倒放，门三色标记为按钮提示而非能力色。尺寸沿用原碰撞与单元格，合并重复/九宫格，保留未交付的水序列帧现状。待确认的梯子素材不擅自使用。不提交推送。
- 续作已应用：17份现有Prefab加机关精灵表现，门内绿/红/蓝按按钮绑定次序点亮；按颜色分离标记，避免邻色漏入。曲柄28帧读真实进度，松开后按原回弹等待/速度倒放；平台中段Tiled、端头保留且顶面对齐碰撞。接触块伸长/区域重置跟随尺寸，钥匙保持对应颜色；梯子按用户指出的tail第四行H形瓦片，未使用误认的九宫格边框；静态岩浆接入，水保持原TA方案。Game未解锁基础Mesh与正式精灵继续走集中HSV/透明交接。中文UI Toolkit Inspector和接入/使用文档已更新。
- 续作验证：隔离Unity6000.3.12f1，98/98项EditMode通过（含触摸上下反向及曲柄双向帧）；真实GPU/Play32项检查通过（锁定基础外观/真实精灵渐显中间透明、钥匙、门提示/开关重置、按钮触发帧循环、曲柄松开倒放、合并平台顶面对齐、接触块伸长/重置）。日志Library/MechanismArtworkTests.xml/log、MechanismArtworkGpu.log，截图在FlowRetirementProbe/Library/MechanismArtworkClosed/Open.png。应用前Prefab/Objects备份Library/MechanismArtworkBeforeBind。Pick SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35，未改碰撞/玩法存档/其他本地文件。未重新打包、未安卓真机验收、未提交推送。
- 范围：弹性植物底部贴格、TA 藤蔓192帧接入。用户确认水尚未交付，本轮仅做藤蔓、保留现有水效果。保留颜色解锁、集中 HSV/共享透明材质、生长燃烧和区域重置。分支 codex/art/ta-sequence-visuals，仅本地测试，不提交推送。
- 不覆盖 Pick、字体缓存、插件、临时 ICON 或本机设置；不修改玩法场景存档。TA 分支只取藤蔓序列帧资源，不合并其旧代码/管线/场景设置。
- 已应用源码、TA原序列帧和12页图集及Objects配置至主项目；植物底部贴原碰撞体底边，宽度/合并重复和原碰撞不变。藤蔓整根一个SpriteRenderer，默认24→160帧生长、末32帧循环，按当前总高度适配；烧毁后区域重置重新登记，禁用同帧挂接不残留旧表现。没有逐帧复制材质或运行粒子模拟。
- Unity6000.3.12f1隔离89项EditMode及真实GPU/Play检查通过（渐显中间透明、母根生长/子链、末段循环、逆向燃烧、烧毁重置、禁用重启、共享材质池、植物贴地/合并宽度/碰撞）。日志Library/VineSequenceBuild.log、VineSequenceGpu.log、VineSequenceTests.log/xml；文档OBJECT_ARTWORK及API已更新。Pick SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35。未重新打包，完整关卡/安卓真机与用户主窗口视觉待验收；未提交、未推送。
## 统一加载过渡：App 进入 / MainMenu → Gaming / Gaming → MainMenu

- 日期：2026-10-15；负责人：Codex；分支：`界面切换与绿色染色`。
- 范围：`GameSceneLoader` 统一管理 App 黑屏扩散、MainMenu 内容右移、加载文字、Gaming 玩家中心扩散、返回菜单背景与 UI 回场；新增 `GameTransitionOverlay`、`GameTransitionSettings` 和 `GameTransitionReveal` UI Shader。
- 参数：App 默认以屏幕正中心扩散；所有扩散前保持 0.5 秒全黑且边缘无柔化；加载文字使用 `Cubic_11 SDF`、120 号白字黑边，显示至少 2 秒；加载期间冻结背景滚动；玩家输入锁保持到 Gaming 黑屏扩散完全结束。
- 修正：黑屏层级置于背景副本下方，避免背景图被遮住；StartMenu 增加全局外部滚动冻结；加载文字同时从 `GameTransitionSettings` 和 `StartMenuController.SdfFont` 解析字体，避免运行时字体丢失。
- 追加修正：MainMenu 的 `__GameFlow/Game Transition Overlay` 现在直接序列化引用 `Cubic_11 SDF` 和 `GameTransitionSettings`；App 黑屏开始扩散时立即恢复背景滚动；返回 MainMenu 不再先铺全屏黑，背景直接覆盖 Gaming 画面滑入；加载文字先消失、背景再移出黑屏；SharedGameUI 的旧加载文字同步引用 Cubie 并放大到 120。
- 退出过渡：开始菜单“退出游戏”不再直接调用 `Application.Quit`，改由 `StartMenuFlowBridge` 请求 `GameSceneLoader`；黑屏从边缘向屏幕正中心反向缩圈，完全吞噬后保持 1 秒再退出。退出速度与保持时长在 `GameTransitionSettings` 中可调，重复点击有请求保护。
- 白屏修复：过渡遮罩不再只依赖运行时 `Shader.Find`；`GameTransitionSettings.asset` 直接序列化引用 `GameTransitionReveal` Shader，黑屏 `RawImage` 同时使用黑色和白色纹理兜底，避免构建或资源回退时遮罩显示成纯白。
- 返回主菜单追加：Gaming→MainMenu 现在先以当前玩家屏幕位置为中心收黑，黑屏完整覆盖后保持 0.5 秒，再启动菜单背景回场；收黑速度、柔化和保持时长均在 `GameTransitionSettings` 中可调。
- 主菜单鼠标兜底：`StartMenuController` 在 MainMenu 存活期间每帧检查并恢复 `Cursor.visible = true`、`Cursor.lockState = CursorLockMode.None`，避免从染色或玩法阶段返回后鼠标仍被隐藏。
- 清理：SharedGameUI 里旧的 `LoadingScreen/LoadingBadge/Label` 整棵树已物理删除，`GameUiRouter.loadingScreen` 置空，避免右下角蓝色旧加载字与统一过渡重叠。
- 验证：StartMenu Runtime、GameFlow Runtime、GameFlow Editor、编辑器测试、PlayMode 测试与 Assembly-CSharp 静态编译通过；实际 Unity 画面与手感待用户验收。
## 定向水流泡泡辅助特效

- 日期：2026-10-15；负责人：Codex；工作分支：`codex/codex/pixel-water-fit-bounds`。
- 范围：从 `Assets/ta_source/Paopao/0-5 (3).png` 原样提取六个逐级变大的空心泡泡像素图，制作视觉专用的定向泡泡流脚本、向上/向左/向右三个预制体，以及 `Assets/ta_source/Paopao/TestScene/WaterBubblesTest.unity` 独立测试场景。长度（1–10格）、流向排数（0.2–3排/格）、每排泡泡数（1–6）、交错程度（0–1）、流宽（0–4格）和流速（0.1–5格/秒）均可调；泡泡沿方向循环，源头小、尾端大，并保留随机性。
- 边界：所有新增运行资产（脚本、预制体、切片图和测试场景）收纳在 `Assets/ta_source/Paopao`；不修改正式场景、`Blue_WaterWay`、水流施力与碰撞逻辑或源图；测试场景只展示三种视觉预设。功能说明仍按仓库规范保存在 `Docs/Developer`。
- 状态：完成。运行资产已统一归档至 `Assets/ta_source/Paopao`；三个方向预制体的默认值为每排 2 颗、交错程度 0.65、宽度 0.8 格。运行池按参数动态扩展，最大 186 个泡泡。Unity 6000.3.12f1 在新增参数前已成功编译程序集；本次通过 Unity 生成的 `.csproj` 编译时，因缺少 `Temp/obj/Project.WaterBubbles.Runtime/project.assets.json` 且离线还原在工程引用图阶段失败，未得到本次 C# 编译结果。静态核对确认三个方向预制体字段、16 个序列化 SpriteRenderer 初始池、测试场景引用与六张 Sprite 导入设置。未在 Play 模式人工复核动画或打包。

## 像素水面双面片预制体

- 日期：2026-10-15；负责人：Codex；交付分支：`codex/codex/pixel-water-fit-bounds`。
- 范围：从 `Assets/ta_source/Newwater/0-5 (3).png` 切出表面、主体和内部三种蓝色像素水片，分别制作三个可复用预制体及独立测试场景。每个预制体只有后方像素水图面片和前方透明扰动面片；前片保持方正固定，只以片元噪波偏移屏幕采样 UV 折射后方画面，不做顶点位移。正交与 3D 视图均通过 Z 间距和排序层确定后片/角色/前片顺序。
- 边界：保留源图原样；不接入 InteractiveWater 顶面、碰撞波纹、反射相机或原有颜色水体玩法，不修改正式场景或 ProjectSettings/URP 配置。旧交互水保留透明物体前的底图捕获；像素水新增透明物体后的相机颜色复制和专用折射绘制，以确保角色进入取样画面。三个预设分别使用独立材质参数；`PixelWaterFilterController` 将前片的正交投影范围与后片 Sprite 自动对齐；已确认三张切片底部各有一行全透明像素，前片边界配置为去掉该行并上移半像素，避免滤镜板突出。控制器还提供运行时颜色、折射强度、噪波流速和时间变化幅度/速度调节。
- 状态：三个预制体、独立材质、片元折射 Shader 和自动移动角色的沙盒场景已创建；新增长期同步面片和运行时折射参数控制器，源图保留不变。静态核对确认每个预制体恰有两张视觉面片、Shader 顶点路径不位移、噪波只改变屏幕采样坐标，三种前片各引用一份独立材质。Unity 6000.3.12f1 已安装，但本轮批处理启动因 LicensingClient 通道拒绝连接以退出码 1 结束，未得到导入或编译结果；透明折射和角色穿越的 Game 视图效果尚未验收。最新交接修正了底部透明像素造成的滤镜板突出：三种前片裁去一行透明边并上移半像素，与水图可见范围对齐。按用户最新指示，本轮将当前水效果提交并推送至上述新分支；Unity 导入、编译和画面验证仍待后续完成，不视作已验收。

## 2026-10-15 当前进度整合

- 用户授权合并推送：整合网格美术编辑器、六分暂停轮盘、重生点/弹性植物美术、MVMap标注地图及共享规划存档、双平台触摸/安全区与气泡柱修复。以最新 origin/main 为基线验证后合入 main；保留 Unity6000.3.12f1、Android7.1 最低版本。隐私接入已按用户要求完全撤回，不随本次提交。
- 本地 Pick 场景、字体自动缓存、DOTweenPro 与 Examples、临时 ICON、PackageManager 本机序列化变化不纳入此次功能整合，不清理、不覆盖。Builds/Library 测试包与日志不提交。
- 整合前 Unity6000.3.12f1 隔离复验六个项目测试程序集83/83通过，待提交的功能源码/场景/资源与已验证快照一致；新增资源meta齐全，git diff --cached --check通过。此前双平台构建与气泡/多指/菜单往返验证记录沿用，未重新打包。安卓真机、完整通关和平台隐私审核不在通过范围。更新说明 Docs/Developer/UPDATE_2026-10-15.md。

## 气泡柱与双平台测试包

- 2026-10-14：修复合并长气泡柱下半段上浮；沿现有 GameInput 增加安卓方向、跳跃、冲刺、交互与左右放下触摸控制；主菜单和暂停界面适配安全区及不同屏幕比例。仅 MainMenu / Gaming 打出 Windows 与安卓测试包，游戏名称与 APK 均使用“形之彩”。保留现有本地场景/字体/插件与未提交功能；未授权提交、推送或合并。
- 已应用到主项目：SharedGameUI 的持久化 Mobile Gameplay Canvas、MainMenu 安全区/Expand 缩放，横屏自动旋转及正式安卓包名。气泡柱改用 Collider 下边界并以玩家半身浸没判定；UI 触摸边沿在下一帧统一可读，修复早执行玩家驱动漏读一次动作。未更改 PC 键位。真实两根气泡上下段均上浮、多指移动/冲刺/跳跃与暂停释放、三次 MainMenu 往返通过；83/83 编辑测试，413 安全区/字体/动作/无重叠断言通过。PC 实际 D3D11 启动无运行异常。
- Unity6000.3.12f1隔离快照构建 Windows 与 Android ARM64 IL2CPP Development 包成功，均只含 MainMenu/Gaming，位于 Builds/形之彩-20261014；APK 标签“形之彩”、包名com.miao1suki.shapeofcolor、minSdk25、文件形之彩.apk。构建存在现有 Shader/URP 剥离提示但0错误；尚未安卓真机、完整通关验收。文档 Docs/Developer/ANDROID_AND_PC_TEST_BUILDS.md；未提交未推送，主窗口不切换构建目标、Pick 文件原 SHA256 不变。

## 标注地图可编辑重建

- 2026-10-14：使用策划 map.mvmap (3).json 与标注图，仅重建图内范围至 Development/RoomLifecycleTests/MVMapImportTest；替换旧源图/物体/存档，保留 Scene GUID，备份至 Library/AnnotatedMap20261014。紫色只作三区房间划分；白墙、水域、定向水流、气泡柱、单向平台、三按钮绑定一门按标注制作。全部通过 PlanningDocument、参数覆盖和 PlanningSceneBuilder 应用，提供共享规划存档并检查实际窗口回读。Pick、正式关卡和未提交美术进度不覆盖；未授权提交/推送。
- 本地已生成79个可编辑实例/1570玩法格，3个紫色范围房间＋独立未划分区；两处下流水流（最右按用户确认由气泡改为下流）、两处上喷气泡、左/上/左转弯水流、一门三按钮（2保持/1持续）、6格单向平台和可编辑测试重生点。深蓝起点不作墙，额外未明确标记只存规划注释。新增水流上下方向，保持旧枚举0/1；共享存档首开绑定/显示修复，不改原增量算法或全局其他场景缓存。
- Unity6000.3.12f1隔离9592项几何/实际生成占格/稳定ID/重载/增量/参数/真实窗口存档往返断言、19项Play解锁/流向/三按钮开关门/房间与R重置、80项EditMode全部通过；GPU核对全图与实际UI Toolkit装配画布。检测并修复重生点外观网格接口缓存在域重载时的空引用。日志Library/AnnotatedMapBuild.log、AnnotatedMapPlay.log、AnnotatedMapTests.log、AnnotatedMapEditMode.xml及FlowRetirementProbe/Library/AnnotatedMap*。旧Scene/源图/存档/本机绑定已备份，Pick SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35；完整通关与打包未验收。未提交、未推送。
- 主Unity已安全打开MVMapImportTest与关卡编辑器（初始装配全图），本机绑定指向项目内共享JSON；应用后的Scene与存档SHA256和验证副本一致，一次性打开标记已消费。没有强行丢弃未保存Scene或退出Play。

## 重生点与弹性植物美术

- 2026-10-14：本地接入重生点未激活/已激活非当前/当前动画与弹性植物六帧素材；保持重生链、区域重置、绿色解锁、弹跳参数和作者碰撞不变。合并植物按格重复而非拉伸单株，正式表现走已有绿色 HSV/共享材质透明链。更新对应预制体并为旧实例提供兼容，不覆盖 Pick 场景或其他本地进度；本轮不提交不推送。
- 已应用主项目源码、两份预制体、Resources/ObjectArtwork配置、Art/Generated/ObjectArtwork切片；原图未修改。80项EditMode通过（新增7项），隔离真实Play/GPU验证三态重生点/重绑/动画、未解锁基础外观、绿色共享渐显材质与全屏白化阶段、4×1合并比例和原碰撞不变、实际弹跳/回静止/重置。当前Scene打开/资源导入时补外观，不自动保存，不重建关卡；Pick文件SHA256仍ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35。日志Library/ObjectArtworkBuild.log、ObjectArtworkTests.log、ObjectArtworkEditMode.xml、ObjectArtworkGpu.log；主窗口手感与打包待验收，未提交未推送。

## 美术网格试用版

- 2026-10-14：核对tail.png、SurfaceTiles与现有1:1关卡网格；新增独立GridArt模块（UI Toolkit、自动边角、多层视差、持久化分块网格），只增加美术入口，不改玩法存档、Pick或现有场景。沿用taptap-editor-ui规范，参考Unity RuleTile/Tiled/LDtk；缺失内拐角明确提示，不擅自推断美术含义。用户授权推送开发分支 `codex/ui/grid-art-editor`，暂不合并 main。

- 已应用本地源码与独立测试资产：关卡编辑器新增“网格美术”，继承格尺寸并显示Scene已应用物体的只读参考轮廓；支持框选/笔刷/擦除、多格装饰、可见/锁定/排序、中文瓦片库Inspector、按Scene记忆美术图和按资产记忆视角。自动地形以八邻格选四象限外角/边/内角，缺图提示；运行时直接引用分块Mesh/材质和原图，只更新背景层Transform。三层示例在Development/GridArt/TileArtTrial，无玩家、不加入构建。原tail.png与旧开发彩色库不修改。
- 隔离EditMode共73项通过（含12项GridArt），GPU/Play检查：保存后真实渲染、无碰撞、分层视差/像素吸附/往返无漂移、重复应用保留手工子对象、UI Toolkit纹理画布；日志Library/GridArtTests.log、GridArtEditMode.xml、GridArtGpu.log。原始示意图NPOT重采样导致边角取样偏移已修复；示例已核对截图。文档GRID_ART.md和Integration/GRID_ART_API.md含实际素材限制、操作、接口及扩展边界。Pick SHA256保持ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35；现有暂停本地进度、字体和插件不纳入本次提交。推送前重新验证73项通过；源码、独立测试场景及生成资产、接入文档一起交付。Unity保持6000.3.12f1，用户主窗口鼠标操作与正式场景美术仍待验收。

## UI美术绑定与水中玩家渲染

- 2026-10-14暂停尺寸微调完成：按用户反馈将径向间距48→64，像素缩放8→9（尺寸增加12.5%），文字半径同比调整；裁切宽度依据文字半径和最大缩放自动扩展，避免右侧文字被截。保持60°及既有功能/动画，共享UI已应用主项目。隔离GPU/Play逐一检查六项选中后的不透明像素不重叠，画面/文字/名单/循环/键鼠手柄触摸通过，61项EditMode通过；最终日志Library/PauseSizingMaskVerification.log、PauseSizingMaskEditMode.xml，git diff --check通过，Pick SHA256未变。不动Pick或玩家，未提交未推送。

- 2026-10-14暂停扇形间距修正完成：零径向间距未考虑阴影/选中放大，导致相邻扇形挤压；增加48 UI单位可调径向间距并随缩放一起偏移，保留60°方向和素材外圆心。共享预制体已重新生成并应用主项目。隔离GPU/Play逐一验证六项选中状态，对所有不透明像素进行相邻扇形Alpha命中检查，未发现重叠；循环/打断/名单/键鼠手柄触摸验证通过，61项EditMode通过。日志Library/PauseSpacingVerification.log、PauseSpacingEditMode.xml。只修改暂停布局/配置/共享预制体及验证，沿用场景流程和统一字体skill，不动其他UI、Pick或玩家；Pick SHA256未变，git diff --check通过，未提交未推送。

- 2026-10-14暂停扇形拼接完成：六块32×38素材按共同圆心（−7/32、23/38）零位移旋转0～300°，每步60°首尾循环，保留左侧轮盘位置及1.08/0.94圆心缩放凸出；图片随盘转、文字独立保持正向。用户确认新增制作名单，复用SettingsPanelController名单页并保持旧枚举值；旧五项实例初始化补齐第六项，不重复添加。六张小图可读Alpha用于透明留白命中剔除，原图不改。BuildPauseArtwork仅生成暂停切片/配置/共享UI，已将隔离生成资产应用主项目，未动Pick及颜色能力轮盘。61项EditMode通过，隔离GPU/Play验证覆盖共同圆心、六扇形方向、连续打断、正向文字、循环导航、键鼠/手柄/触摸、制作名单入口及实体/透明像素命中；日志Library/PauseSectorReleaseCheck.log、PauseSectorEditMode.xml，完整几何检查图在Library/FlowRetirementProbe/Library/PauseFullSectorGeometry.png（仅验证临时居中，产品仍在左侧）。Pick SHA256未变，git diff --check通过。遵循taptap-scene-flow与taptap-typography规范沿用现有流程/字体/输入；未提交未推送，主窗口实际视觉待用户验收。

- 2026-10-14暂停轮盘试版已撤回：用户验收后要求恢复上一版。已移除60°/居中轴心/首尾停止相关改动及试版测试、使用说明，暂停控制器、共享UI预制体和原测试恢复为05f02c3发布版本；原72°、左侧轴心、循环导航及选中凸出保留。Pick场景、本地插件和本机设置不动；未提交未推送，不应继续应用已撤回的试版。

- 2026-10-14发布：用户授权本版合并并推送main。包含主角精灵/死亡等待/重生相机、水下角色兼容、菜单与转盘美术、Atmosphere E Minor及正式往返暂停修复，代码、生成资源与说明一起发布；用户Pick场景、本地DOTween Pro及PackageManager本机设置不纳入。远程main核对仍772faac，无需引入其他待整合分支；保留全部本地未纳入内容。验证为60项EditMode、场景往返PlayMode1项及真实按钮往返3次通过，Unity保持6000.3.12f1。提交完成后以Git提交信息为发布凭据。

- 2026-10-14正式往返修复：隔离真实MainMenu按钮流程复现首次/再次进入仍播放开场曲、GameAudioService不存在；暂停返回菜单后timeScale=0/audioPaused=true残留。GameFlowController现于持久流程根安装并初始化GameAudioService；GameUiRouter返回/重载/初始化采用立即释放，状态相等时也执行强制清理，解决关闭Tween被卸载打断。保留普通Resume动画，不恢复旧常驻场景，不修改Pick/菜单布局/颜色玩法。三次真实按钮往返均切换Atmosphere E Minor且玩家物理正常、返回timeScale=1/audioPaused=false；新增3项EditMode回归及原有57项全通过。正式PlayMode回归1/1通过（编辑器测试Runner覆盖构建列表，显式预载Scene，再验证实际流程激活、两次进入/暂停/返回及关闭动画中再次返回；正常加载器路径由前述真实按钮Harness验证）。未提交未推送。日志Library/MenuRoundTripBaseline.log、MenuRoundTripFixed.log、MenuRoundTripEditMode.xml、MenuRoundTripPlayMode.xml。

- 2026-10-14暂停与音乐调整：进入玩法改为music.atmosphere.e_minor，沿用目录循环及Music总线；主菜单不变。暂停所有选项取消白化和半透明，保持原素材颜色/不透明，凸出、缩放、旋转与能力轮盘表现不变。删除仅为暂停白化新增的材质实例逻辑与生成引用，相关Shader/材质移至Library/RemovedPauseWhiten-20261014备份，不保留无用Assets。隔离PlayMode验证音乐Cue资源匹配及循环、暂停选中/未选中/打断后原色不透明、凸出缩放、能力轮盘及导航通过，日志Library/PauseOpaqueMusicVerification.log；git diff --check通过，Pick SHA256不变。仅修改相应代码/共享UI配置与文档，不修改Pick布局，不提交不推送；当前窗口听感待验收。

- 2026-10-14转盘效果审计：fetch后main仍772faac；以另一位程序59da0ee/630da79原版暂停表现为基线，恢复美术替换后削弱的径向凸出及像素白化。新增WheelArtwork.shader、共享材质引用及每项运行时实例，沿用1.08/0.94缩放、0.38白化、0.62透明度、OutCubic和快速打断；颜色能力原0.84缩小/0.58白色硬切未修改。保留此前确认的导航方向、移动端支持及美术替换，不回退占位几何。隔离PlayMode检查凸出/白化/透明度/Shader编译/快速切换/能力恢复与导航通过，57项EditMode通过；Pick未改，不提交不推送。详情见GAME_UI_ARTWORK.md，日志Library/WheelEffectsVerification.log、WheelEffectsProjectEditMode.xml。

- 2026-10-14视觉修正：按320×180参考统一菜单按钮与背景缩放；所有主菜单子页共享连续滚动背景；三色扇区改用像素连通区域切分，修复边界错色。清理误放于Generated根目录的旧生成切片，恢复GameUI目录唯一GUID引用；旧产物和被替换UI资产备份于Library/UiArtworkCorrection-20261014。ESC上下方向反转，同时接入Navigate/Submit、可点击按钮与触摸滑动，不改操作绑定。隔离GPU、扇区像素数及键鼠/手柄/触摸方向验证通过，项目EditMode57/57通过；Pick SHA256不变，git diff --check通过。当前窗口与实机触摸仍待验收，未推送。

- 2026-10-14；本地开发。保留MainMenu→Gaming正式流程、输入和统一像素字体；绑定开始菜单、三色能力轮盘、暂停/设置界面的现有美术，复用现有业务。检查精灵玩家与水下底图、透明队列/深度的兼容性，以GPU对比确认水覆盖而非简单置顶。仅修改对应源码、资源及MainMenu/共享UI资产，Pick布局及本地插件保持，不推送。
- 已应用：GameUiArtwork共享资源、预裁切PNG、开始/设置背景与按钮、暂停图标、三色轮盘及扇区重心缩放。遵循场景流程与字体skill，不改输入或启动链。截图检查完成；旧透明Shader水下不可辨像素0，新裁剪/深度Shader可辨5223像素，其中4039受水混合。UI和水的GPU日志在Library；说明见Docs/Developer/GAME_UI_ARTWORK.md。Pick SHA256仍为ED2BEF7DFF48F25C89D6A6097C17E20C02489CDFA526D032A3CE485114F4AA35，未推送。当前窗口及打包待验收。
- 根据视觉反馈修正：暂停美术块直接替换扇区并关闭旧底盘；主菜单按原图布局新增现有制作人员页入口，设置/退出并排，隐藏旧标题占位。按钮完整边界加2像素透明余量，背景生成独立Repeat纹理并从左向右循环滚动。GPU复验通过，检查无旧扇区残留及Repeat/UV运动；四项目程序集EditMode57/57通过。原UI资产备份于Library/UiArtworkOriginals-20261014。

## 主角像素表现绑定

- 重生相机平滑回位已应用：保留首次定位与显式Snap；PlayerController通过相机模块ICameraFollowRelocationSource通知跟随组件，死亡/主动重生后暂不使用大距离硬吸附、死区或瞬移观察提前量，平滑追到新位置，暂停冻结。相机输出仍仅由Manager负责；换目标/禁用解绑，显式Snap可打断追赶。遵循场景流程skill，不改正式流程或场景，未推送。
- 隔离真实PlayMode11项检查通过，含30单位跨距、实际Manager相机连续输出、暂停、重复R、显式Snap及旧目标解绑；项目四程序集EditMode57/57通过，0失败。日志Library/PlayerRespawnCameraVerification.log及PlayerRespawnCameraProjectEditMode.xml。此前未筛选的SDK/包测试不作为项目验收（1608项含196失败）；项目测试均通过。Pick SHA256未变，git diff --check通过，窗口手感/打包待验收。
- 死亡流程已修复：房间管理器同帧自动复活抢占动画，现通过PlayerController.CanAutomaticallyRespawn等待完整精灵死亡动画及配置的死亡Timeline时长，默认至少0.7秒；R主动重生仍即时。无协程或延迟回调，重复死亡不重启等待，暂停冻结，复活后旧条件失效。遵循颜色玩法skill的区域重置/解锁持久约定，不改场景或碰撞，未推送。
- 隔离真实PlayMode覆盖死亡首帧可见、完整播放后自动复活、暂停、重复死亡、R打断、第二次死亡和禁用表现组件后备；已有区域重生27项断言通过且无运行错误。日志Library/PlayerDeathVerification.log及PlayerDeathRoomRegression.log；Pick SHA256未变，git diff --check通过。窗口及打包仍待验收。
- 外观微调：精灵默认下移0.08格，可在中文Inspector中调整；两份预制体及运行时自动绑定统一生效。碰撞箱、物理与Pick场景不变。隔离复验102项资源/动画/Shader检查与15项PlayMode检查通过，新增偏移与碰撞独立断言；日志Library/PlayerSpriteOffsetVerification.log。未推送。
- 2026-10-14；Codex；本地已完成7套主角动作/35帧切片及只读玩家表现层。沿用PlayerController状态、速度与接地数据，死亡优先、演出/锁定暂停自动动画、切状态立即打断、复活恢复。精灵画布1×1格，保持现有碰撞、物理、输入、关卡布局不变；两份玩家预制体已绑定，裸场景玩家运行时自动接入。不覆盖用户Pick场景和插件，未提交或推送。
- Unity6000.3.12f1隔离验证通过102项资源/动画/URP精灵Shader编译检查、14项真实PlayMode检查及57项既有EditMode回归。两个玩家预制体的CapsuleCollider/BoxCollider序列化内容与任务前一致，Pick SHA256未变，git diff --check通过。Shader支持SpriteRenderer左右翻转与颜色，不改TA管线。日志Library/PlayerSpriteVerification7.log、PlayerSpriteEditMode.xml；实际窗口视觉与打包未人工验收。使用与接入见Docs/Developer/PLAYER_SPRITE_PRESENTATION.md。
## TA 连接预览起点高度适配

- 日期：2026-10-14；负责人：Codex；分支：`codex/ta/link-origin-height`。
- 范围：将 `InteractionLinkEffect` 无锚点时的本地发射偏移 Y 从 `0.9` 调为 `0.5`、再下调至 `0.3` 世界单位，避免角色缩放压低纵向起点；同步 `Player(暂定).prefab` 中的序列化值。Test 和 Gaming 场景继承 Prefab，不改场景文件。
- 边界：未改 `PlayerInteractionSensor` 的相机屏幕射线、点击位置、交互扫描中心/半径或染色玩法判定；只调整连接视觉的发射点。
- 验证：核对 `Player(暂定).prefab` 上 `sourceAnchor` 为空，Test 场景没有单独覆盖 `sourceOffset`；`git diff --check` 通过。未运行 Unity / Play Mode。

## 新美术导入参数整理

- 2026-10-14；Codex；已在主项目配置 `Assets/_Project/Resources/26TAPTAP` 的26张图片。按原图长边选择不小于原尺寸的最小二次幂上限：32～2048；游戏像素图启用 Point、禁用 Mipmap 与有损压缩，Clamp、NPOT None、Read/Write关闭。21张序列/素材板为 Sprite Multiple，玩家单帧与独立菜单背景共2张为 Sprite Single，2张索引与1张组合菜单参考为 Default。不切片，Multiple 的 Sprite 列表暂为空；PPU保持原值，待实际接入决定。不改原图/GUID/场景/玩法/TA 管线/视频。
- 静态校验26张图上限均覆盖原始分辨率，所有原图SHA256与GUID保持不变，参数二次检查无差异，git diff --check通过。运行中的Unity由自身资源刷新接收.meta变化；本次未启动独立编译或验证Game画面。
- 用户授权合并推送后重新 fetch：远程main仍为2877058，正式音频与TA进度均已纳入；未整合的旧交互图交接/旧水平台实验分支不重复引入。本次main提交新增24张PNG、2张索引JPEG、1段演示MP4及对应.meta/目录.meta和上述导入配置。用户Pick场景、本地DOTween Pro插件与PackageManager设置不纳入发布。

## MVMap 物体试导入与遗留锚点清理

- 2026-10-14；Codex；codex/mvmap-gameplay-import。用户确认保留植物障碍，删除无调用的 AnchorPoint；不删除 Respawn_Block 重生点或藤蔓。替换 Development/RoomLifecycleTests 的旧 MVMap 源图、测试场景及对应规划存档，按临时颜色/形状映射导入新图。正式 Gaming、RoomRespawnTest、用户 Pick、插件与本机设置不修改。验证后按用户授权合入 main 并推送。
- 新源图73个物体区域/2450格，名称均为 Room 数字，未指定颜色17处；推测映射需策划确认。外部物体区域不等于运行时房间，测试暂用单一区域，不凭颜色猜房间重置边界。旧资产备份 Library/MVMapReplacement20261013。
- 已生成并同步134个实例，Scene GUID保留；共享存档MVMapObjects.planning.json与Scene内嵌数据一致。23格藤蔓为一母根22节段，黄门绑定两个临时推测按钮；无源图钥匙，不增测试键位，可用现有运行态调试解锁。主项目旧导入场景作用域缓存已备份清理；本地其他测试1存档不动。
- Unity6000.3.12f1隔离验证：2624项转换/精确几何/稳定ID/场景重载/增量保留/引用断言、10项真实PlayMode解锁与R回归、57项项目EditMode全部通过，0编译错误，既有CrankPlatformAuthoring废弃API警告仍存在。源图2450格无重叠、无额外填充；Pick SHA256与任务前一致。日志Library/MVMapReplacement20261013；窗口实际交互、全图通行、最终映射及打包未人工验收。说明MVMAP_OBJECT_IMPORT.md、MVMAP_OBJECT_MAPPING.md及MVMAP_OBJECT_IMPORT_API.md。

## 连线修复发布与最新 TA 整合

- 2026-10-13；Codex；用户确认连接已正常，要求删除诊断并合并推送。移除 InteractionLinkEffect 临时日志/目标引用/诊断菜单，保留水体尺寸与表现碰撞过滤修复；整合最新 TA 分支 bef0737 的染色和蒸汽资源、使用文档，验证后推送 main。用户未提交 Pick 布局、DOTween Pro 插件与本机 PackageManager 设置不纳入提交。
- 完成修复提交352420d与TA合并ccba6db，唯一文档索引冲突同时保留双方有效记录。合并后Unity6000.3.12f1隔离编译、9项水体PlayMode与57项项目EditMode回归全部通过；连线模块已无LinkDiagnostic/诊断菜单。此次发布含先前本地main的机关/曲柄、正式场景/字体、音频整合提交；未合并旧交互图退役实验及旧水平台管线实验分支。窗口新增TA视觉、完整流程与打包未人工验收。验证日志Library/Integration20261013/MergedWaterFix.log及MergedEditMode.xml。

## 水体合并与空气连线修复

- 2026-10-13；Codex；按 Pick 实际日志修复 Water Visual 继承22单位宽 Trigger、随四格水块缩放成88单位的问题。范围为 InteractiveWater 的方块模式碰撞尺寸和 InteractionLinkEffect 的特效子碰撞过滤；全场景共享修复，不重写 Pick/Test/Gaming 布局，不改红平台信号规则或万能方块接色。仅本地开发，验证后交用户测试，不推送。
- 已应用主项目。隔离真实 Play Mode 9项断言通过：四格宽合并水、2D/3D尺寸、远处无连线、人为恢复旧巨型 Trigger 后仍拒绝、靠近水体保留合法连线且使用玩法根 Collider。不以 Renderer.enabled 排除共享母根粒子的藤蔓节段。日志 Library/Integration20261013/WaterAirLinkVerification.log；窗口画面/打包仍待验收。
- 项目四个 EditMode 程序集57项回归全部通过，0失败；git diff --check通过；Pick与修改前备份SHA256一致。修复只保留本地未提交，未推送。

## Pick 空目标连线诊断

- 2026-10-13；Codex；用户反馈第一房间正常，通道右侧和第二房间连到空气，要求日志查真实目标。仅为 InteractionLinkEffect 增加编辑器 Play Mode 三秒节流日志，目标路径、Collider/Trigger、外观、线尾与退场状态完整记录；组件右键可关闭/请求记录，构建不包含诊断。不改变目标判定、不排除万能方块、不保存用户 Pick 场景。待异常位置复现确认对象。
- 验证：隔离 Unity 6000.3.12f1 编译/场景检查通过；诊断夹具确认目标路径、Collider、Renderer、线尾与退场字段，并复现范围裁切使端点脱离碰撞体的边界情况（0.03065 世界单位）。这不等于确认 Pick 截图根因；完整复现需读取实际位置日志。结果在 Library/Integration20261013/LinkDiagnosticVerification.log，用户 Pick 与备份 SHA256 一致。

## 最新音频、正式场景与本地机关整合

- 2026-10-13；Codex；用户要求拉取并合并。整合远程 main 59da0ee（音频、统一字体、MainMenu→Gaming）及音频分支 480de74；一并保存本地已验证的绿块碰撞/搬运、红块供能、独立曲柄多平台与跳跃改动。保留本地 Pick、像素字体、插件和本机设置；字体冲突单独询问。此次未要求推送。
- 已完成本地整合提交 2f4891d（main）与 1d8d870（音频）。字体按用户确认备份至 Library/Integration20261013/Cubic_11-local.asset 后使用远程字体；Pick 完整备份并校验未改。隔离 Unity 通过57项项目 EditMode、28项曲柄规划、43项玩法断言及 MainMenu/Gaming/Test/Pick 的 UI/玩家/缺失脚本检查。UI 与玩家说明见 Docs/Developer/UI_PLAYER_AND_LINK_AUDIT.md；窗口画面、手感及打包未人工验收。未推送。

## 独立曲柄与多平台绑定

- 2026-10-13；Codex；沿用 codex/green-solid-climb，仅本地。曲柄改为独立控制器，创建时生成独立升降平台，支持一对多；平台合并/解除合并保留持久绑定，拒绝不同控制器混合合并。保留通电制动、载人下降、颜色解锁和区域/R 重置。保留用户场景布局、字体、插件，不提交/推送。
- 已实现并应用主项目。Red_Crank 保留 GUID、移除配套子模型；Red_CrankLift 为独立消费者，规划参数提供添加平台、绑定/解除曲柄、绑定/解除制动基座，Scene 提供同级创建与选择绑定。绑定以 crankOwnerId 持久保存；合并拒绝不同控制器、解除继承当前绑定、生成后按稳定 ID 重连并记录 Prefab override。删除配套平台不重复生成。旧规划加载/修改时仅在空格安全补建一次，裸旧 Scene 通过 Inspector 添加，不强制覆盖用户布局。资产备份 Library/CrankSplitBackup。
- Unity 6000.3.12f1 隔离验证：28 项规划/Scene 创建/合并/解除/增量替换/保存重载断言、43 项真实玩法（含两平台共享进度、载人下降、基座制动与重置）、8 项旧持久基座绑定通过。项目四个 EditMode 程序集的56项也通过。窗口鼠标/完整关卡及打包未人工验收。
- 验证环境：网络包服务超时，隔离副本切换到已缓存的同版本依赖；主项目 Packages/Unity/管线不变。首次规划夹具未给房间占格、Unity JSON 将 null 转为空字符串影响同种比较、即时 Collider 查询未同步物理的问题分别已修正；失败日志保留。嵌入包使全量测试意外包含第三方包自身测试（其中196项失败），与项目56项区分记录，不当作项目验收。仅项目程序集复验结果见 Library/CrankProjectEditMode.xml。说明更新到 MECHANISMS_AND_JUMP 与 MECHANISM_SIGNAL_API；未提交/推送。

## 红块供能、平台与基础跳跃定稿

- 2026-10-13；Codex；沿用 codex/green-solid-climb，只改本地。范围：三色接触方块钥匙门控、旧能源兼容替换、基座驱动平台/曲柄与绑定预览、单向平台/无碰撞隐藏块、玩家一格尺寸与四格上升（用户追加：第1格中心到第5格中心）、弹性植物三/八格回弹和搬运四格跳跃。保留既有区域/R 重置与用户 Pick 布局、字体和插件；不推送、不合并。
- 已应用当前工作项目，未提交/推送。旧 Red_Battery 保留 GUID 并替换内容；新道具/玩家网格与调色预制体通过隔离 Unity 生成后按明确清单复制回主项目，备份 Library/MechanismUpgradeBackup。Test 只调整测试玩家外形/碰撞规格与跳高（9增8删），出生位置和关卡布局不变；Pick 原有布局与未提交内容不覆盖。
- Unity 6000.3.12f1：56 项 EditMode、40 项真实玩法断言、8 项持久绑定断言、6 项 Test/Pick 作者资源检查通过；原有 50 项接触反应、58 项搬运、39 项按钮/区域重置回归通过。含旧能源升级、任意红块供能、断电停台、载人跟随/曲柄快降和制动、实际4格普通跳跃、单向落地、植物3/8格与实际携带4格跳跃/再充能，绑定存档/重建/局部擦除重连。早期物理测试暴露的单向跨步漏判与平台 Update 跟随丢帧已修复，移动平台统一固定步先于玩家更新；冻结测试玩家的曲柄夹具与植物再充能后的断言时机已校正。失败和最终日志均保留于 Library。完整窗口手感/最终美术/打包未人工验收。
- 说明：Developer/MECHANISMS_AND_JUMP.md、Integration/MECHANISM_SIGNAL_API.md；ContactCubes API 与颜色 skill 已覆盖旧的“不受钥匙”和“搬运一律禁跳”描述。局部擦除批量基座保留被绑定格的关系，擦除被绑定格解绑；合并/复制来源仍需重映射 ID 或重绑。

## Pick 场景 TA 效果接入

- 2026-10-13；用户指定将 TA 最新效果应用到 Pick；已 fetch，最新 TA 仍为 main 已包含的 cafb424。范围仅 Pick 测试玩家的 InteractionLinkEffect 配置及说明，不覆盖场景布局、玩家位置、轮盘、道具或此前搬运工作；沿用已开启的 PC Stencil 像素化及 HSV 管线。未提交/推送。
- 完成：Pick 原 RuntimePlayer 实例新增连线组件 override，使用当前颜色目录和 TA 材质，sensor/colorWheel 由同物体自动解析。备份 Library/PickTAEffectBackup/Pick-before-TA.unity；与备份相比仅新增该组件，不重保存整场景。
- Unity 6000.3.12f1 隔离 Pick 导入 6 项检查通过（164 个对象、无 Missing Script、唯一玩家连线、材质/目录、管线配置）；D3D11 真 Play 18 项检查通过，红绿蓝连线几何/材质、无选色隐藏、实际 Pick Camera 的 HSV 渲染执行、连线 Shader 无编译错误。批处理摄像机仅在副本接 RenderTexture 才有 Game 帧，首次无目标纹理的 HSV 执行断言未通过，日志保留；副本验证生成物/选色/摄像机纹理不写回主场景。完整窗口手感和打包未人工验收。

## 三色接触方块接入搬运

- 2026-10-13；沿用 codex/green-solid-climb，本地未提交/推送。三个 ContactCube 预制体挂现有 CarryableFeature，F/Q/E 等输入绑定不变；通过 ICarryMotionParticipant 挂起刚体与接触反应，放下/取消后归还控制。绿块长度保留，放下前完整 OBB 查空，来源房间约束与 R 作者状态恢复不变。黏住仍未实现。
- 隔离 Unity 6000.3.12f1：56 项 EditMode 通过；58 项搬运运行断言通过（实际 PlayerController 拿起/放下入口、三种预制体注册、完整长块落点、挂起/归还、重复搬运、区域重置），50 项三色生长/水柱/攀爬回归通过。日志在 Library/ContactCubeCarry*；未修改用户 Pick、字体、插件，未进行用户窗口手感或打包验收。
- 按钮/区域重置最终 39 项通过。首次回归发现测试玩家默认随机重生范围越出 A 区，离区复位冲掉压力测试位置；仅将隔离测试玩家出生位置与随机半径限制在 A 区内再验证，未修改正式玩家重生参数。失败与最终日志均保留于 Library。

## 绿块实体碰撞与攀爬修复

- 2026-10-13；codex/green-solid-climb；绿块生长后的支撑检测与玩家碰撞隔离存在穿透风险。修复绿块母体/伸长实体始终阻挡玩家、无水柱时禁止水平推动、底部检测不随高度产生大间隙，补充侧面攀爬。不改用户场景，未提交/推送。
- Unity 6000.3.12f1 全项目编译、56 项 EditMode、50 项方块运行断言通过，含实际长至六格、支撑、侧爬、持续朝向不穿透/不推移、横向攀附与水柱回归；按钮/重置 39 项最终运行断言通过。首次按钮回归在 R 后再次压下的短等待处失败，诊断复跑通过，随后增加有界等待物理查询并再验证通过，保留失败日志于 Library；完整用户场景手感/打包待验收。

## MainMenu → Gaming 正式场景流程

- 日期：2026-10-13；负责人：Codex；分支：`音乐`。
- 场景：正式序列固定为 `Assets/_Project/Scenes/Start/MainMenu.unity` → `Assets/_Project/Scenes/Playing/Gaming.unity`；新增 `.agents/skills/taptap-scene-flow/SKILL.md` 并在 `AGENTS.md` 建立强制阅读入口。
- 接线：MainMenu 新增 `__GameFlow` 与 `GameFlowController`，`StartMenuPrototype` 增加 `StartMenuFlowBridge`；`GameSceneCatalog` 只登记 MainMenu/Gaming；Gaming 的 Main Camera 增加 `CameraControlManager`、`CameraModeController`、`CameraFollowController` 和 `LevelSceneContext`，自动把 `Player(暂定)` 接为随行目标；`SharedGameUI.prefab` 已放入 Gaming。
- 兜底：`GameSceneCatalog` 移到 `Content/GameFlow/Resources`，`GameFlowController` 在场景引用为空时通过 `Resources.Load` 自动补回目录；进入 MainMenu 时若缺少流程根或开始菜单桥，也会由运行时自动补齐，避免旧内存场景导致空引用和无法跳转。
- 验证：GameFlow Runtime、GameFlow Editor、编辑器测试、PlayMode 测试和 Assembly-CSharp 静态编译通过；场景 ID 无重复；未提交。

## 开始菜单统一 Cubic 字体与全局 Typography Skill

- 日期：2026-10-13；负责人：Codex；分支：`音乐`。
- 范围：开始菜单场景与 `MainMenuUI.prefab` 的 `StartMenuController.uiSdfFont` 统一指向 `Cubic_11 SDF`；`TextSdfMirror` 移到 `Project.Shared.Runtime`，开始菜单新增 `StartMenuTypography`，所有场景内和运行时生成的 UGUI 文本自动挂 SDF 镜像。
- 标题：主菜单显示名和 Unity `productName` 统一改为“形之彩”。
- 规则：新增 `.agents/skills/taptap-typography/SKILL.md`，并在根 `AGENTS.md` 要求后续修改任何游戏内文字、UGUI/TMP、HUD、字幕、提示、成就、设置或动态文本时先读取该 skill。
- 验证：共享、GameFlow、StartMenu、GameFlow Editor 与 Assembly-CSharp 静态编译通过，`git diff --check` 通过；Unity 画面字体效果待复验。

## 音乐分支：音频目录、音量分流与玩法接线

- 日期：2026-10-13；负责人：Codex；分支：`音乐`。
- 范围：扩建 `AudioManager`、`GameAudioService`，新增 `AudioCatalog` 与 `AudioCuePlayer`，不改场景、AudioMixer、Timeline。
- 新增：45 个现有音频登记为 37 个 Cue；音频按 `Music` / `SoundEffect` 分为两条实际音量总线路；开始界面和 ESC 设置的三个滑杆直接控制运行时总音量、音乐和音效，不再受预览布尔拦截；循环音效按 owner 起停。
- 已接：开始菜单 BGM；玩家跳跃、脚步、游泳、攀爬、入水、出水、死亡、重生；开关门；曲柄循环；蒸汽喷发/循环；藤蔓生长；颜色钥匙获得；选色、上色成功和非法提示。
- 未接：区域环境音淡入淡出、各关卡 BGM 选曲、正式 UI 点击音；当前保留程序生成占位音。
- 验证：`Project.Audio.Runtime`、`Project.GameFlow.Runtime`、`Project.StartMenu.Runtime`、`Project.GameFlow.Editor` 与 `Assembly-CSharp` 响应文件静态编译均 0 错误；`AudioCatalog` 45 个 Clip GUID 与仓库音频一致，`git diff --check` 通过；Unity 内试听与完整 PlayMode 待验收。

## 最新交互连线、接触方块与管线整合（2026-10-13）

- 用户授权合入 cafb424、最新 main 和接触方块任务并推送 main；采用 TA 的 Stencil 像素化及像素大小，重新启用 HSV，关闭当前 PC 管线内重复的旧像素化阶段。执行顺序为像素化后 HSV。
- 本地 Pick、字体、DOTween Pro 和 PackageManagerSettings 保留且不纳入提交；字体先备份再恢复本地版本。其他远程分支已包含在 main 的历史不重复合并。
- 整合完成：Unity 6000.3.12f1 全项目编译、56 项 EditMode、39 项按钮/重置、41 项方块运动、25 项房间运行断言通过；D3D11/RTX 4080 图形验证确认 HSV 实际执行、Stencil 开启、像素大小 42.8、内置像素化关闭及连线/Stencil Shader 无编译错误。完整关卡视觉和打包未人工验收。合并仅交接文档冲突，保留双方记录；当前任务按授权进入 main 并推送。

## 接触方块重置与门按钮模式

- 2026-10-13；Codex；继续 codex/contact-color-cubes。范围：验证三色方块按布置位置/状态经 R 重置；门按钮接受玩家及三色接触方块，新增保持/持续模式与区域重置策略。持续模式释放后关门，保持模式及其进度不随 R/离区清除。按追加要求约束三色/可搬运方块留在布置区域，拒绝跨区放下、越界回位；玩家跨区取消携带并复原来源房间。不修改用户 Scene；本次按用户授权参与 main 整合。
- 验证：Unity 6000.3.12f1 全量编译、56 项 EditMode 回归、39 项新增真实运行断言及既有 25 项房间运行回归通过。覆盖三色重量、两模式、玩家脚部、R 回到重力前作者位置和状态、持续信号关门、离区复原、越界归还及固定归属。隔离项目既有 Unity Search 启动索引异常仍在；完整窗口交互/美术/打包待用户验收。用户 Pick、字体、插件和本地设置不改、不提交。

## 独立三色接触方块

- 2026-10-13；Codex；codex/contact-color-cubes，基于 main 82b8b5a。新增三种独立道具：接触状态机、绿色长度、红蓝水柱与已有颜色目标转发；按追加需求完成独立刚体运动、重力、落稳锁定、空中玩家碰撞隔离、横喷初段重力保护和横向长出部分上下攀附禁跳。搬运、放下和黏住待另一程序接入，不修改用户场景。完成只推分支，不合并。
- 绿色蓝接触每秒长一格，红接触暂按每秒缩一格；红蓝同时接触锁长度。接触结束保留长度，区域重置恢复初始状态。多个同色邻居采用稳定优先级。
- 已生成三种动态刚体预制体并加入道具栏目，中文 UI Toolkit Inspector。方块彼此不接色，接触旧目标转发固定颜色；蓝块每个红邻居独立水柱。玩家复用水流运动入口，方块通过 PushRequested 交给 ContactCubeMotion；玩家新增 HorizontalClimbing=6，位姿只由 PlayerController 写入，原输入与状态编号不变。
- 验证：Unity 6000.3.12f1 隔离项目全量编译、56 项 EditMode 回归、41 项真实 PlayMode 断言通过（四态、接触增减/方向/锁长/上限/重置、真实水柱推动动态方块、水柱退出、重力掉落/落稳/玩家碰撞恢复、保护不无限续期、上下攀附/横移/禁跳/退出、搬运挂起接口及房间恢复）。Unity Editor Search 启动索引有既有异常，不属于运行时代码；完整画面与打包未验收。用户 Pick、字体、DOTween Pro、本地设置未提交。说明：Integration/CONTACT_CUBES_API.md、Developer/CONTACT_CUBES.md。

## 最新进度整合（2026-10-12）

- 用户授权整合最新进度至 main；任务源为 codex/world-grid-one-to-one，TA 新提交为 e18507d（交互目标识别与像素链条）。包含统一世界图、MVMap 试导入、区域重生/重置、当前重生点外观和旧标准流程场景退役。
- 保留用户未提交的 Pick、字体、DOTween Pro 和 PackageManagerSettings，不纳入本次提交；不引入已过期 TA 水体管线实验分支。
- 合入 TA 后验证：Unity 6000.3.12f1 全项目编译、56 项 EditMode、25 项真实运行态重生断言、1 项独立菜单 PlayMode 全部通过。实际链条视觉、完整 Game 和打包仍需人工验收。合并冲突仅为 ACTIVE_WORK 末尾交接记录，已保留双方内容。

## 当前重生点外观与退役场景清理

- 日期：2026-10-12；负责人：Codex；分支：`codex/world-grid-one-to-one`；已提交 c90d083，本次按用户授权整合至 main。
- 范围：重生点默认半径 0.5→1.5 格，自有测试 Scene 1.25→1.5 格；当前绑定与历史激活分离，只有当前点使用青色材质，换点恢复旧点外观，并提供 IsCurrent / CurrentChanged 美术接口。不改用户 Pick。
- 用户确认旧标准流程整体退役：移除五个 Systems 场景、旧三关和 Ending（含 .meta），清空 catalog/构建引用；导航改为列出实际场景，旧生成菜单撤下且生成入口拒绝重新生成。MainMenu 保留原 UI、改为独立 Camera/EventSystem，移除旧流程根。Content/UI 两个 prefab 原样保留，跨场景开始/返回等待新流程接线。
- 恢复：删除前 Scene 副本位于 Library/RetiredStandardScenesBackup；Git 历史同样可恢复。空 Levels/Systems 目录仅为未来关卡组织保留，不包含旧场景。
- 验证：Unity 6000.3.12f1 全项目编译、56 项 EditMode 回归、25 项真实 PlayMode 重生断言和 1 项主菜单独立 PlayMode 测试通过（唯一 Camera/AudioListener/EventSystem，无旧系统自动加载）。完整 Game 画面/手感/构建未人工验收。不自动提交或推送。

## MVMap 试导入、区域重生与重置

- 日期：2026-10-12；负责人：Codex；分支：`codex/world-grid-one-to-one`；实现完成，按用户要求仅提交/推送分支，不合并 main。
- 世界图真实占格作为房间/通道边界；最近进入范围的重生点优先，存活 R 与死亡可重生。区域离开/重生仅恢复所属物体，保留门、按钮、已拾取钥匙和全局颜色解锁；已解锁物体回到刚解锁的基础行为、布置位置。
- 将 MVMap 主稿的 65 区域 / 17,281 格导入独立测试场景，外部一格对应房间一格；保留外部 ID、颜色、备注与不连通片，不猜长条区域为通道，不凭空生成关卡内容。
- 保留并不提交用户 Pick 场景、像素字体、DOTween Pro 与 PackageManagerSettings；不升级 Unity/Packages/管线。
- 验证：Unity 6000.3.12f1 全项目隔离编译和 47 项 EditMode 回归通过；34,958 项导入/持久化/真实占格及规划增量应用断言、22 项真实 PlayMode 断言、1,292 项统一网格迁移断言通过。窗口手势、完整 Game 外观和打包尚未人工验收；验证环境有 Unity Editor Search 索引异常，未计作玩法运行错误。
- 说明：Developer/ROOM_RESPAWN_AND_RESET.md、Integration/ROOM_LIFECYCLE_API.md。旧场景需在编辑器再应用一次并保存以写入区域；没有世界图数据不猜边界。新 Scene 内嵌地图，首开可回读；钥匙与锚点绑定的跨运行存档未新增。

## 世界图统一单元格与框选搭建

- 日期：2026-10-12；负责人：Codex；分支：`codex/world-grid-one-to-one`。
- 修改前已整合并推送 main 至 cc1176c，包含旧任务、最新交互链条、音频及导入元数据；过期 TA 大改管线分支未直接覆盖现有管线。
- 当前范围：世界/房间/装配以房间图单元格为统一 1:1 坐标，旧存档按原步长展开世界占格、不缩放内部内容；世界图拖拽矩形创建/擦除。与本次 MVMap/重生版本一起提交并推送分支，暂不合并 main。
- 验证：Unity 6000.3.12f1 隔离全项目编译、47 项 EditMode 测试通过；统一网格验证 1292 项断言通过，包含旧默认图及本地“测试1”存档、内部装配矩形、负坐标、非方形步长、重复载入、首次保存备份、矩形事务与既有装配编辑。实际 UI 手势、现有完整 Scene 与打包未人工验收。
- MVMap：后续任务已实现布局试导入，见本文件上方“MVMap 试导入、区域重生与重置”；不改写策划源文件，不转换灰盒及锁规则。

## 藤蔓持久化链接与独立 UI 启动

- 日期：2026-10-12；负责人：Codex；当前分支：`codex/neutral-ladders-door-hints`。
- 最新 main（c94fb60）已合入当前分支，保留最新暂停/主菜单接线。字体按用户授权备份至 Library/UserAssetBackup/VineBootstrap-20261012 后采用远程版本；Pick、插件与本地设置不动。
- 范围：规划参数资产引用持久化、旧 JSON 不信任数字 ID、稳定增量签名、母藤蔓/节段同步与 prefab override 持久化；退役 Bootstrap 场景与强制启动入口，保留设置/存档/暂停/开始菜单。2026-10-12 已整合推送至 main，提交 7c97bf0，后续整合点 cc1176c。
- UI：完整开始菜单与共享暂停界面提取至 Content/UI/MainMenuUI.prefab、SharedGameUI.prefab，内部组件/物体引用不指向原场景；保留原场景界面，MainMenu 独立 __GameFlow 根为完整流程入口。InputBindingBootstrap 是输入重绑定初始化，不随场景 Bootstrap 删除。
- 排查：只读检查用户 Pick 副本，11 个母藤蔓全部缺失 growthSegmentPrefab，0 子节段；1 个水功能物的 waterPrefab 有效。公共 JSON 引用风险覆盖水 prefab、材质/Mesh、粒子、字体与 Timeline 资产，不影响枚举/数值或 Unity 原生 GUID/fileID 引用。未替用户保存 Pick；从所属规划图重新应用可修复旧生成藤蔓。
- 验证：Unity 6000.3.12f1 全项目隔离编译通过；599 项资源往返/水方向枚举/规划文档/藤蔓组链、无改动增量保留、场景保存重载及 UI 内部引用断言通过，其中 56 个非空资源字段覆盖当前颜色预制体；7 项 GameFlow Editor 测试和 1 项主菜单→暂停→123关→结束 PlayMode 测试通过。批处理 nographics 不代表窗口视觉、用户关卡手感或打包验收；完整 Pick 尚待用户测试。说明：Developer/PLANNING_REFERENCES_AND_VINES.md、GAME_FLOW.md、Integration/FOUNDATION_API.md。

## 无色梯子、门提示与横门

- 日期：2026-10-11；负责人：Codex；分支：`codex/neutral-ladders-door-hints`。已实现，按用户要求提交并推送此分支，不合并、不推 main。
- 范围：独立无色正面/侧面梯子，复用玩家攀爬 API；门专属提示块、1–3 按钮状态美术配置、按钮增删动态更新；新增横门道具及占格预览，旧竖门 ID/预制体保持兼容。
- 不修改已保存场景、字体、插件和本地设置，不接入 TapSDK 或导入音频资源包。
- 验证：项目 Runtime/Editor 静态编译及 diff 检查通过；Unity 6000.3.12f1 隔离项目 35 项 Editor 检查（四 prefab 导入、唯一提示、九状态组合、增删绑定、上限/孤儿清理）和 17 项运行时检查（真实 DOTween 两轴开门、按下/重复/重置、删除按钮与门、梯子多 Collider 与禁用解绑）通过。播放器/镜头协作者在隔离项目为桩，梯子接触通过脚本入口注入，不代表完整 Player 物理手感与 Timeline 已验收。完整关卡编辑器交互、相邻梯子衔接、像素化显示和打包待测试。
- 文档：Integration/NEUTRAL_MECHANISMS_API.md、Developer/NEUTRAL_MECHANISMS.md。美术配置：Resources/DoorHintStyle.asset，共九个 Sprite 状态；素材未到位使用孔位占位。新梯子不加入颜色层，不修改旧竖门 prefab。
## 用户要求推送当前全部工作区改动

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：按用户此前“所有改动都推上去”的授权，将工作区内 Test 场景、PC Renderer 设置、字体资源、LinkEffect 源码/Shader/材质/文档、StencilPixelEffect 材质、GivingColor 目录元数据及相关交接文档一并纳入；`.vscode` 属于本机编辑器配置，按仓库规则排除。
- 交付边界：只推送当前任务分支，不直接推 `main`，由整合者后续合并。
- 验证：Unity 6000.3.12f1 Runtime C# 静态编译通过；暂存差异检查通过。Shader 导入与实际 Game 画面尚未复核。

## TA 交互连线场景接入说明

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：新增面向程序的场景挂载说明 `Assets/ta_source/LinkEffect/连接效果使用说明.md`，说明玩家根物体挂载方式、Inspector 必要引用、材质与检测层配置、测试场景参考；不改场景或运行时代码。
- 验证：仓库根目录与 `origin` 地址已核对；文档与系统索引链接已检查；`git diff --check` 通过。
- 交接：仅本地改动，不提交或推送。

## 表现优化：搬运落点占用修复

- 日期：2026-10-12；负责人：Codex；分支：`表现优化`。
- 范围：修复搬运放下时可落入已有方块/道具网格的问题；目标格判定新增 `LevelEditorPlacedBlock.ContainsCell` 网格占用检查，并跳过玩家和当前搬运物自身的格子。
- 行为：物理 Collider 继续作为补充检测，但同格已有网格物体时直接判非法，不再出现方块重合。
- 根因：规划场景中的方块、道具共享同一个 `__PlanningMapGenerated` 根，旧逻辑用 `transform.root` 忽略当前搬运物时会连同整张地图一起忽略。
- 修正：改为只忽略玩家自身层级和当前搬运物自身层级，不再按共同根跳过地图方块。
- 同步表现：`PlayerColorWheel` 的交互范围圈改为仅在持有选中颜色时显示，并以可调周期和透明度持续慢速呼吸；无选色时完全隐藏。
- 交互收口：范围圈出现/退场改为呼吸淡入淡出并最终 alpha 为 0；颜色成功应用一次后自动清除当前选色。
- 鼠标状态：游戏内默认隐藏鼠标；持有待用颜色时显示，颜色成功应用或选色清空后隐藏；ESC/菜单暂停时保留鼠标，暂停期间不被颜色系统覆盖。
- 染色反馈：持有待用颜色时点击无效目标显示 `这个物体好像不能与这个颜色反应`，目标超出交互范围显示 `无法触碰到这个物体`，成功应用后显示 `染色成功`；均复用非法放置提示的 2 秒下方提示与左入右出动画。
- 范围判定顺序：左键先按鼠标世界命中点判断是否超出交互圈，再判断物体能否接收颜色，避免圈外普通物体误报为不能反应。
- 圈外判定：空气和实体统一按交互圈距离判断；只要点击点在圈外都显示无法触碰。空气通过玩家所在世界平面换算点击点，实体使用射线命中点。
- 消息扩展：`WordNoteUI` 新增加打字机入退场通道、可调打字速度和 `richText` 样式开关；玩家选择颜色后显示 `当前选择X色`，位置居中下方、存活 `2` 秒、显隐结合左入右出、白字黑描边，颜色名读取颜色目录显示名并用对应色卡着色。
- 钥匙 Timeline：红、绿、蓝三条拾取演出改为强制正交 2D、固定前方朝向，机位 `(0, 1, -10)`，关闭看向角色并锁住相机纵深，只保留平面平移和推拉缩放，避免露出 3D 场景侧壁。
- 操控解锁：颜色钥匙前段 Timeline 到达揭示进度后立即启动相机回位并释放玩家控制锁；颜色渐显、拾取视觉和回收继续并行收尾，不再延迟输入解锁。
- 验证：Runtime / Editor 静态编译 0 错误，`git diff --check` 通过；尚未 PlayMode 复测。

## 搬运逻辑重做（静态实现完成，待 PlayMode）

- 日期：2026-10-11；负责人：Codex；分支：`搬运`。
- 范围：推翻旧的“右键长按 + 鼠标吸附拖拽 + 原位回退”能源搬运；新增共享 `CarryableFeature`，能源方块由自身 `IColorObject.IsActive` 门控，万能方块常态可搬。
- 玩家链路：输入驱动只读 `F` 交互、`Q/E` 放下并把命令交给 `PlayerController`；玩家按默认 1×1 占格和上下左右紧邻格维护拾取队列，按 `F` 固定首项，冻结 0.1 秒后目标关闭 Collider、置顶并 DOTween 跟随玩家头顶。
- 携带约束：携带期间只允许移动和冲刺；`Q/E` 只检查玩家占格方向的紧邻格，非法落点保持携带；物体放下动画结束后恢复碰撞和控制。
- 墙体检测：左右放下先沿玩家中心向目标方向做 `1.1` 格射线；忽略玩家与当前搬运物，命中任何其它 Collider 直接判非法，防止单层薄墙被目标格检测绕过。
- 重置：可搬运物记录拿起前的位置；携带中按“复活/重置搬运”或玩家死亡时，目标 DOTween 回到原位，完成后恢复 Collider、取消携带并释放控制。`Respawn` 的显示名改为“复活/重置搬运”。
- 提示：携带后通过 `WordNoteUI` 在下方分区持续显示“按 左/右放下键 将方块放置在左/右侧，按 复活/重置键 重置方块”，参与堆叠，显隐各 `0.5` 秒；非法落点也进入下方分区，显示“位置非法，请留出足够空间”并存活 `2` 秒，显隐与左移组合入场、显隐与右移组合退场，各 `0.5` 秒。两条都使用白字黑描边。
- 组合动画：`WordNoteUI` 的入退场支持显隐、水平移动、竖直移动、缩放四条独立组合通道；方向、距离、时长、初末速度和变速比例都可编辑。
- 输入资产：`Interact` 默认从 `E` 改为 `F`；新增 `DropLeft=Q`、`DropRight=E`，均固定点击；旧 `Carry` 保留枚举兼容但从按键映射显示中隐藏。
- 接口：搬运目标只依赖 `IPlayerCarryHost`，不直接依赖 `PlayerController`；`CarryableFeature` 与 `CarryTargetMotion` 已挂到 `Red_Battery`、`Universal_Block` 预制体。
- 验证：使用 Unity 6000.3.12f1 响应文件完成 Runtime、InputAbstraction、InputRebinding、Editor 与测试程序集静态编译，均 0 错误；`git diff --check` 通过。Unity PlayMode、实际鼠标/键盘输入、DOTween 视觉和预制体导入仍需本机复测；当前未提交。

## WordNote 全局文字提示

- 日期：2026-10-11；负责人：Codex；分支：`搬运`。
- 范围：在 `Systems_UI` 的 `__System_UI` 下新增与 `GameUI` 同级的 `WordNote_UI` Canvas，挂载 `WordNoteUI`。
- 接口：维护一个总队列和九个位置队列；支持驻留、定时、转瞬即逝、按 ID 强制退场、按位置退场、全屏退场和立即消失。
- 动画：所有条目使用 DOTween 的入场/退场；支持原位显隐、左右/上下移动、淡入淡出移动和缩放，并暴露初末位置、透明度、缩放、速度、时长及变速参数。
- 堆叠：同一位置可同时显示多条消息，公共 `stackSpacing` 控制上下间距；前一条结束后，后一条通过 DOTween 补位。消息可单独关闭“参与堆叠排序”，此时固定占第一格且不影响其他消息。
- 颜色：字体色和描边色提供红、橙、黄、绿、青、蓝、紫、白、黑；描边开关、宽度和柔化参数暴露在 Inspector。
- 默认字体：玩家屏幕文字提示默认使用 `Cubic_11 SDF`。
- 验证：新增运行时代码与 Editor Inspector 静态编译通过；未进行 PlayMode 视觉验收，当前未提交。

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

## TA 交互预览链条初版

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：仅新增 `Assets/ta_source/LinkEffect` 中的链条预览资源，并在 `Assets/Scenes/Test.unity` 的 TestPlayer 和测试目标上接线。
- 接口：独立组件读取玩家现有选色与交互范围；新增可选 `ILinkEffectPreviewTarget` 供测试或后续目标声明预览状态与锚点，不修改 PlayerInteractionSensor、PlayerController、颜色物体或输入接口。
- 表现：一份 URP Unlit Shader/材质按色卡显示红绿蓝链节；可行时完整流动，不可行时断续变暗。TestPlayer 未选色时启用红色测试色，旁边红色测试目标用于即刻预览。
- 验证：Unity 6000.3.12f1 编辑器已导入新增脚本、Shader 和材质，Runtime/Editor 程序集重载完成；Test 场景新增 YAML 对象 ID 无重复，新增脚本与材质 GUID 引用核对通过。实际重载 Test 场景并进入 Play Mode，确认角色与红色测试目标之间显示红色链条；运行时把测试色改为 blue 后，链条切换为较暗的蓝色断续显示。退出 Play Mode 后测试色恢复为场景预设的 red。
- 边界：保留工作区已有的 Renderer、字体、PixelEffect、GivingColor 等改动，不纳入本任务提交。
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

## TA 交互预览识别与像素噪波改造

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：只修改 `Assets/ta_source/LinkEffect` 的目标识别与视觉资源，并从 `Assets/Scenes/Test.unity` 移除上次新增的 `LinkEffect_Red_TestTarget`；保留既有 TestPlayer 挂载，不改原有玩法脚本和场景元素。
- 实现：只读扫描 `IInteractionTarget`、`IColorApplicationTarget` 与 `PlayerCarryTargetRegistry`，排除只有基类空实现的染色功能；按颜色处理类型、目标现态、母藤蔓剩余生长量及子节段类型判断可行性。多个碰撞体按物体合并。Shader 改为带透明度、像素格噪波和断边的线条，不再绘制链节。
- 验证：Unity 6000.3.12f1 已重新导入脚本与 Shader，Test 场景可进入并退出 Play Mode；Console 为 0 错误、0 警告，场景层级确认旧测试方块已消失。未在本轮对每一种真实目标逐一做画面操作验证。
- 交接：仅提交 LinkEffect 目录、Test 场景中本组件参数与旧测试方块移除、此记录及系统索引；工作区已有 Renderer、字体、PixelEffect、GivingColor 改动保留原样。

## TA 交互预览染色判定与树枝线修正

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`；本轮只在本地修改，不提交或推送。
- 范围：仅修改 `Assets/ta_source/LinkEffect` 的目标扫描与 Shader；不改玩家、颜色物、原有场景元素或渲染管线。
- 目标：让选色后的预览与 `PlayerColorWheel` 的实际接色结果一致，包含藤蔓红/蓝反应；Shader 绘制自然树枝轮廓并对整体轮廓像素化。
- 实现：按屏幕命中的首个接色组件和各 `OnColorApplied` 的实际返回条件筛选目标，选色时不再把 F 专用物体画成染色目标；母藤蔓、子节段的红色反应及母藤蔓可生长时的蓝色反应均被覆盖。被锁定控制或搬运时隐藏连线。Shader 用连续的树枝主干与枝杈生成轮廓，并按屏幕像素格统一采样轮廓和纹理，不从中间挖透明洞；未改 Renderer/Pipeline 配置。
- 验证：Unity 6000.3.12f1 随附 Roslyn 使用项目响应文件完成 Runtime C# 编译，0 错误；本轮文件 `git diff --check` 通过。Unity batchmode 在导入前退出，Shader 的编辑器导入和实际画面仍待打开 Unity 复核。

## TA 交互预览紫色材质修复

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`；仅本地修改，不提交或推送。
- 原因：Unity `Editor.log` 明确报告 `InteractionLink.shader` 第 54 行 `point` 为意外标记，后续第 56 行 `floor` 也因解析失败报错；Shader 编译失败使连线呈紫色。
- 修复：仅将噪波函数的参数 `point` 改名为 `samplePosition` 并同步其引用。树枝形状、像素化逻辑和渲染管线均未改动。
- 验证：对照日志与源码确认触发错误的保留字已移除；待 Unity 重新导入后查看 Console 和实际连线画面。
- 边界：仅保留本地改动，未提交或推送；原有 Renderer、PixelEffect、GivingColor 与本地 `.vscode` 变动未纳入本轮。

## TA 三色引导线表现

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：仅编辑 `Assets/ta_source/LinkEffect` 的 Shader、独立预览组件及其 Inspector/说明；红色火焰和火星、蓝色主干与环绕水纹、绿色嫩枝与短芽。每个新目标的连线从玩家端向目标端逐渐延伸。
- 实现：预览组件按颜色 ID 传入 Shader 风格；按目标实例缓存出现时间并以可调 `revealDuration` 平滑推进遮罩。扫描顺序变化不重播，离开检测范围或切换颜色时重置。Shader 共用屏幕像素采样，红线绘制黄芯橙身红边和流动火星，蓝线绘制主干及两条相位交替的浅蓝水纹，绿线绘制细枝、短分枝和随进度长出的嫩叶；无色目标保留中性色枝干。
- 验证：Unity 6000.3.12f1 的项目响应文件完成 Runtime C# 编译，0 错误；Windows SDK DXC 对从 Shader 提取的顶点与片元 HLSL 编译均为 0 错误。Unity batchmode 在项目导入前以退出码 1 结束，尚未获得 Unity Shader 导入或 Game 画面的复核结果。
- 边界：不改原有玩法脚本、Test 场景元素或渲染管线；本轮保持本地改动，不提交或推送。

## TA 材质链条粗细参数

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：在 `InteractionLink` 材质 Inspector 增加“链条粗细”，并使运行中的 LineRenderer 使用这个材质值；不修改 Test 场景元素或渲染管线。
- 实现：Shader 属性 `_LineThickness` 默认 `0.2`，材质资产显式保存该值。预览组件每帧读取共享材质属性并赋给 LineRenderer.widthMultiplier，材质 Inspector 改值会直接影响 Play Mode 的实际线宽；移除组件端重复的宽度字段，避免两个控制入口不一致。
- 验证：项目 Runtime C# 响应文件编译 0 错误；Windows SDK DXC 顶点与片元 HLSL 编译均 0 错误；本任务文件 `git diff --check` 通过。Unity 游戏画面尚未复核。
- 交接：仅本地改动，不提交或推送。

## TA 三色链条自然化与范围约束

- 日期：2026-10-12；负责人：Codex；分支：`codex/ta/link-effect-preview`。
- 范围：修改 `Assets/ta_source/LinkEffect` 的 Shader、目标显示状态、Inspector 与文档，并只调整 Test 场景里现有 LinkEffect 组件参数；不改原有场景元素、玩法脚本或渲染管线。
- 实现：只在选中 red/green/blue 时显示，检测按 `PlayerInteractionSensor` 扫描半径逐帧复核，绘制控制点限制在同一检测圆内。目标离圈/失效后短暂像素溶解；玩家清除选色时立即隐藏。材质面板只保留有效的粗细、像素大小、噪波幅度和效果速度，脚本接管的属性隐藏。红蓝线整条由透明淡入，绿色从玩家端按长度生长；绿线加粗并沿线生成四条轻微随机弯曲、末端回钩的短枝，叶片与小花随枝条成长。火星密度降低，水纹随机断续，线头两端柔和收尖；路径抖动使用材质噪波强度与效果速度。Shader 从 LineRenderer 曲线读取逐段累计的实际长度，图案用固定世界尺寸与间距生成，线变长时增加图案数量而非拉伸原图案。
- 追加调整：蓝线外围环绕和扩散波纹恢复为可选表现，由材质“水纹外围效果强度”独立控制，值为 0 时只显示中央主干；组件 Inspector 明确显示“线条生成时长（秒）”，共同控制红蓝淡入和绿线生长，并用 `FormerlySerializedAs` 保留原场景中的旧时长值。
- 验证：Runtime C# 编译 0 错误；限定任务文件的 `git diff --check` 通过。当前 Shader 调整后尚未完成 Unity 导入编译或 Game 画面复核。
- 交接：仅本地改动，不提交或推送。

## 音频资源第四批（方块、岩浆与平台音效）

- 日期：2026-10-13。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：新增 5 个音效：`sfx_notice_confirm`、`sfx_gp_block_pickup_01`、`sfx_gp_block_drop_01`、`sfx_gp_lava_cool`、`sfx_gp_platform_move_loop`。
- 接口变更：无。音频提交为 `5eddb21`、`97b6c9e`；本次提交只补记文档。
- 文档：`Docs/Agent/Integration/AUDIO_HANDOVER.md` 增补清单行与拾取/放下、岩浆冷却、平台循环、确认提示接线项。
- 验证：远端分支与本地一致；未在 Unity 中导入试听，`.meta` 待首次导入生成。
- 交接提交：本次文档提交。

## 音频第一版完成（第五批：环境声与机关音效补齐）

- 日期：2026-10-14。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：新增 2 个环境声（`amb_env_room_loop`、`amb_env_water_flow_loop`）与 11 个音效（按钮、机器启停、植物燃烧、浮力进出、受击、气泡循环，以及解谜完成与隐藏要素提示）。
- 里程碑：第一版音频制作完成，仓库现有 63 个音频文件（环境声 6、BGM 14、SFX 43）。
- 接口变更：无。音频提交为 `3d2f6d6`，本次提交只补记文档。
- 文档：`Docs/Agent/Integration/AUDIO_HANDOVER.md` 重写清单与接线表，覆盖全部 63 个文件。
- 验证：文档与仓库音频逐文件比对，无遗漏；未在 Unity 中导入试听，`.meta` 待首次导入生成。
- 交接提交：本次文档提交。

## 音频修订（脚步声与攀爬，新增藤蔓音效）

- 日期：2026-10-15。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：同路径修订 `sfx_footstep_land_walk_01~03`（陆地脚步）与 `sfx_gp_plr_climb_01~02`（玩家攀爬）；新增 `sfx_gp_plr_climb_vine`。
- 接口变更：无。修订沿用原路径与原文件名，引用与 GUID 不受影响；音频提交为 `34007fc`。
- 文档：`Docs/Agent/Integration/AUDIO_HANDOVER.md` 增补藤蔓攀爬的清单行与接线项。
- 验证：远端分支与本地一致；本次未在 Unity 中试听。
- 交接提交：本次文档提交。

## 音频响度统一（-28 LUFS）

- 日期：2026-10-15。
- 负责人：Codex。
- 分支：`codex/codex/taptap-music`。
- 范围：`Music/` 下 20 个文件（环境声 6、BGM 14）统一响度。用 ffmpeg loudnorm 两遍测量 + 线性归一化，目标 -28 LUFS、True Peak ≤ -3 dBTP。
- 结果：实测落在 -28.55 ~ -27.56 LUFS，True Peak 最大 -5.73 dBTP。
- 接口变更：无。文件名与路径不变，引用与 GUID 不受影响。
- 文档：`Docs/Agent/Integration/AUDIO_HANDOVER.md` 新增「响度标准」一节，含逐文件实测值。
- 验证：替换后逐文件 SHA256 与归一化产物一致；未在 Unity 中试听。
- 交接提交：本条目随本次提交。
