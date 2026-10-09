---
name: taptap-color-gameplay
description: 2026TapTap 颜色大洗牌开发规范，覆盖固定颜色物体、钥匙解锁、房间重置、万能方块代理、固定形态、直接组件交互和交互图退役。适用于修改 ColorBlocks、色物体能力、万能方块、颜色反应或交互图迁移时。
---

# 颜色大洗牌开发入口

本 Skill 是颜色系统开发的强制入口。

注意：万能方块现在作为前期辅助代理保留，自身显示纯色，并把同一颜色广播给左右上下四个紧邻目标，不拥有玩法能力。
功能组件的最新机关、水域、植物和参数要求以完整规范中的“2026-10-07 功能组件补充定稿”为准。

## 开始前必须读取

每次涉及颜色系统开发前，必须完整读取：

`Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`

新增或修改颜色钥匙、可拾取物或掉落物动画时，还必须读取：

`.agents/skills/taptap-drop-items/SKILL.md`

该文件保存完整目标规范。本文件只负责让 Codex 在项目中发现并进入该规范，不替代完整内容。

## 适用范围

以下任务必须先应用本 Skill：

- 修改 `Assets/_Project/Code/Systems/ColorBlocks`
- 新增或修改红色、蓝色、绿色场景物体功能
- 修改颜色钥匙、颜色解锁或房间重置
- 新增万能方块、颜色代理或 `IColorApplicationTarget`
- 修改 `ColorObject`、`BlockAbilityHost` 或 `BlockFeature`
- 拆除 `InteractionManager`、GraphView、Definitions 或 Catalog

栏目规则：

- 方块栏目只做地图墙壁和几何碰撞。
- 全部颜色玩法预制体从道具栏目放置。
- 颜色玩法预制体位于 `Content/ColorBlocks/Prefabs`，使用 `Red_*`、`Blue_*`、`Green_*` 命名。

## 不可违反的结论

- 颜色是钥匙解锁分组，不是物体的动态当前颜色。
- 旧 `ColorBlock` 适配器已经删除，不要再恢复动态染色方块。
- 物体的具体形态在放置时固定，不会跨颜色或同颜色切换形态。
- 一个功能只由一个组件负责。
- 功能组件直接处理触发、状态、表现、生成物和清理。
- 所有按格计算的距离、高度、检测范围统一读取 `IGridCellSizeProvider` / `BlockFeature.GridCellWorldSize`；组件不再暴露“每格大小 / 每格高度”。
- 母藤蔓通过“生长物预制体”生成 `Green_LadderSon`；生长物使用 `LadderSonFeature`，继承攀爬和出现动画，但不能再次被蓝色浇灌生长，只能被红色烧毁。
- 机关控制方向是消费者绑定信号源：移动平台统一绑定基座、按钮、曲柄并订阅 `IMechanismSignalSource`；基座只检测能源并输出信号，不保存或寻找机关。
- 岩浆是全局唯一死亡方式；水、水泡、玩家控制和功能组件不得保留摔落伤害、摔落免疫或相关调试参数。
- 浮力水柱不设置最高顶起格数；只要气泡在玩家下方，且玩家碰撞体仍与任意蓝色物体重叠，就持续上浮，直到玩家整个身体离开全部蓝色体积。
- 左键颜色交互不再选择交互圈内最近目标；它先做屏幕射线命中前景物体，再检查目标是否在交互圈内，圈外和 UI 点击都不响应。
- 左键射线直接查找 `IColorApplicationTarget`；所有 `BlockFeature` 都实现该接色入口，由具体组件重写 `OnColorApplied` 执行红绿、蓝绿、红蓝等直接反应。
- `Tab/F` 的“调色”动作只开关染色轮盘，并允许选择点击或长按触发。点击为开关式，长按为按住式；玩家驱动的 2D/3D 切换已删除，`E` 继续负责按钮、曲柄等交互。
- `PlayerInputDriver` 只把输入转换为 `PlayerController` 上的轮盘/移动/跳跃命令；`PlayerColorWheel` 不直接读取输入。
- 藤蔓是实体阻挡物，按格距离进入攀爬；W 上爬、无输入自然下滑、S 加速下滑、空格固定，AD 可离开范围，且暴露对应速度参数。
- 弹跳植物必须是实体阻挡物并只在正确落点触发一次弹跳；按钮按格半径检测交互；移动平台碰到实体方块后反向。
- 能源方块迁移改为交互圈内长按“搬运”键拿起、鼠标网格吸附拖拽、松手放下；搬运键默认右键并锁定长按，拖动时全输入锁住，目标格无效回原位，原位也无效则从原位发散寻找合法格。
- 能源拖拽不能离开玩家交互范围；松手必须强制结束拖拽并恢复移动、跳跃、轮盘、染色和交互输入。
- 万能方块自身显示纯色，并向左右上下四个紧邻的 `IColorApplicationTarget` 广播颜色。
- 万能方块不是颜色源、不是能力本体、不能搬运。
- 物体之间的固定反应使用类型安全接口，不经过交互图。
- 所有可拾取掉落物必须继承 `Project.Items.DropItemBase`；`OnEnable` 只走出现阶段，拾取通过 `TryBeginCollection` 进入收集阶段，收集表现结束后才由基类回收。
- 掉落物的出现、收集、停止 Tween 必须写在 `DropItemBase` 的动画钩子里，子类只处理具体拾取效果；不得在子类触发入口直接 `Destroy`、`SetActive(false)` 或提前关闭 Renderer。
- 当前尚未导入 DOTween，禁止引用 `DG.Tweening`；导入后仍只在 `DropItemBase` 中实现动画，不在每个掉落物子类重复写 Tween 生命周期。
- 高频路径禁止反射、字符串方法调用、全场景扫描和无意义协程。
- 新增或修改自制组件时，必须提供中文 UI Toolkit Inspector、Foldout、条件显隐和可调试状态。

## 工作流程

1. 完整读取 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。
2. 按根 `AGENTS.md` 读取项目持续文档。
3. 盘点改动涉及的预制体、场景、颜色资产和现有图引用。
4. 先补直接组件路径，再删除旧图路径。
5. 运行时验证锁色、解锁、房间重置、重复进入和清理。
6. 同步更新功能文档与 `Docs/Agent/ACTIVE_WORK.md`。

## 完成检查

- Runtime 和 Editor 编译 0 错误。
- `git diff --check` 通过。
- 没有新增 `InteractionManager`、`InteractionObjectDefinition`、GraphView 或反射依赖。
- 没有运行时颜色切换或同色形态切换。
- 房间退出后颜色物体、万能方块和生成物全部重置。
- 新增或修改拾取物时，确认其继承 `DropItemBase`，且没有在子类绕开基类直接销毁或禁用。
