# R1 关卡

## 打开和编辑

Unity 固定 **6000.3.12f1**。ILorR 本机用 Unity Hub 添加并打开 `C:\Unity Programs\2026TapTapGameJam-R1`，不要打开原目录来找本次 R1。

Scene：`Assets/_Project/Development/LevelEditor/ILorR/R1/R1.unity`。

在 Project 搜索 R1，双击该 Scene，再点 Play。继续制作使用现有融合关卡编辑器；同目录 `R1.planning.json` 是可编辑存档，必须与 Scene 和双方 meta 一起保存、交付。不要重新导入源图覆盖后续手工编辑。

这是独立制作场景，没有加入正式构建列表，不替换 Gaming。出口门只打开通道，不跳转到尚未制作的 R2。

## 已确认布局

来源：《关卡设计.xlsx》Sheet1 的 **F6:BK46**，Excel 每格对应游戏一格。F6 对应规划 `(0,0)`、世界中心 `(0.5,-0.5,0)`；规划 Y 向下。

- 原黑格 446 格，只按确认移除 BK10 给出口门让位，交付 445 格实体地形。使用项目中性地形外观，未猜配美术。
- K40 出生；标准 Player.prefab 默认普通满跳 4 格。
- 三色场景物体全解锁；玩家仅红色染色，蓝、绿能力锁定。R1 开局组件独立初始化，不改共享玩家或其他场景。
- 左平台 P18:S18 开始，M10 曲柄控制，升到 P8:S8，行程 10 格。
- 右平台 AX36:BA36 开始，升到 AX22:BA22，行程 14 格。BH27、AT33 共用进度，换位置接续；松手使用原曲柄回落规则，载人加速回落。
- 出口竖门 BK10:BK11。H23、U26 两按钮一次按下保持，必须全部按下才开门，使用现有门展示等待。R/死亡重置保留门与按钮进度。
- K11:K22 中性梯子；I28:I32、Z17:Z25 开局完整藤蔓，没有额外生长段。
- 原 X30:AJ30 横藤蔓改为 **初始 13 格的固定横向绿方块**：根 X30，向右到 AJ30，固定不下落且不可搬运；支持底面横移、跳跃松手。保留绿色方块原有接触反应，不新增横藤蔓玩法。
- AD14、K31、N39 绿方块最大长度分别 8、9、9，对应 AD7、K23、N31 生长极限；极限标记不生成实体。
- W32:W40 向上定向水流；其余三色方块、弹性植物、岩浆按原图范围放置。
- 浅灰平台路线、“生长极限”“玩家最高处”仅为规划标注，不生成隐藏碰撞。

默认操作：方向移动、空格跳跃、F 曲柄/搬运、Q/E 放下、Tab 选色、左键使用能力、R 重生。按钮用脚踩，不按 F；实际按键可在设置中重绑定。

## 协作与实现

分支 `codex/ilorr/r1-level`，基于开始制作时最新 main `a96af4b6353453b1634875f18831d8a50b2722cb`。原项目未提交修改保留原位。不制作 R2/R3，不合并或推送 main。

复用规划模型、SceneBuilder、标准玩家、场景自有相机、SharedGameUI、RoomLifecycleManager、门按钮。补充共享曲柄进度、作者固定绿方块横向攀附、单实例自身几何与规划占格分离。普通未绑定曲柄、非固定方块默认行为不变。接口见 MECHANISM_SIGNAL_API、CONTACT_CUBES_API、PLANNING_COLLABORATION_API。

`R1.design.json` 保留原黑格和标注。`R1LevelBuilder` 只显式执行，不在导入/开窗自动重建；Build 拒绝覆盖已有 R1。UpdateConfirmed 仅供源图迭代，后续手工编辑使用正常关卡编辑器。

## 验证与限制

- `R1LevelBuilder.Verify`：3378 项检查通过，包含逐格地形、对象占格/位置、标准玩家/相机、持久机关绑定、保存重载、无变更增量保留对象、片段导入 ID 重映射、删除来源清理关系。
- `R1GameplayVerification.Run`：实际 Play 检查通过，包含出生安全、开局能力、双曲柄接续/单次累加/回落、独立左曲柄、双按钮开门、区域重置、13 格固定绿条底面抓附/横移/跳跃松手。门检查使用 TryPress API，不冒称踩踏通关。
- 已检查实际运行全图截图。本机报告和截图留在 Library 的 R1Verification.txt、R1GameplayVerification.txt、R1Overview.png，不提交 Library。
- 本机 Unity 启动有编辑器 Search 索引越界异常，来自 UnityEditor.Search；R1 编译及以上检查通过。没有因此擅自升级 Unity。
- **尚未完整从入口游玩到出口，也未打包验证；通关路线、难度和手感仍待策划试玩。**

Scene、同名 planning、双方 meta 和依赖成套交付。提交前运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`。不增加运行时联网、遥测或开发构建。
