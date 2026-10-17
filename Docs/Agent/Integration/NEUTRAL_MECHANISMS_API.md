# 无色梯子与门提示 API

运行时源码：`Assets/_Project/Code/Systems/Mechanisms/Runtime`。所有新增机关直接继承 MonoBehaviour，不使用 ColorObject、BlockAbilityHost、BlockRuntime、IColorApplicationTarget；不受颜色锁/HSV 颜色层控制。输入不新增按键，玩家物理只由 PlayerController 写入。

## NeutralLadder

- `SideFacing`：预制体朝向标记；正面与侧面为两个独立预制体。
- Trigger Enter/Stay 按 Collider 保存玩家，调用现有 `EnterClimb`、`SetClimbTopHeight` 和攀爬速度 API。
- 同一玩家多个 Collider 只在最后一个离开后解绑。禁用/销毁/玩家碰撞体失效时清理来源与全部倍率缓存，无逐帧全场景扫描。
- 没有新状态机或输入实现；爬梯手感沿用现有攀爬状态。Physics 触发要求玩家 Rigidbody 与双方碰撞层矩阵正常。
- `BlockParameter` 提供关卡编辑器的攀爬倍率、自然/加速下滑速度配置。

## 门与提示

- `DoorController.IsHorizontal`：横门固定类型标记，竖门旧序列化默认 false。
- `MaximumButtons = 3`：编辑器新增绑定上限，历史超过三个的门不自动截断。
- `PressedCount`：实际已按下数量。
- `RefreshHint()`：幂等创建或更新唯一提示子物体。Awake、SetButtons、按下、删除和重置入口调用，无逐帧材质写入。
- `SetButtons(values)` 接受自己的 Buttons 列表，先快照再重建，避免解绑过程中修改枚举列表。外部迁移按钮时会从原门解除关联。
- `ForgetButton` 同步提示并清理 Owner；运行中移除未按下的按钮后，若剩余非空按钮全已按下会解锁。零按钮不解锁。
- DoorHint 的所有者是门，生命周期和位置跟随门。实际按钮独立于门层级，规划删除门时清理关联按钮；运行中 Destroy 门也清理其按钮。
- `DoorHintStyle.Resolve(total, pressed)`：1/2/3 总数与 0..total 进度映射。缺少美术时回退孔位模型。共享默认资源位于 Resources/DoorHintStyle.asset，构建保持引用。
- 横门 `openEuler = (90,0,0)`，竖门默认 `(0,90,0)`；均沿用旧 Timeline 镜头和 Collider 关闭流程，不直接操作 Camera。

## 规划标识

`mechanism-door-standard` 保留；新增 `mechanism-door-horizontal`、`mechanism-ladder-front`、`mechanism-ladder-side`。按钮仍使用内部 `mechanism-door-owned-button`，提示不是可独立放置的 palette 项，也不写入 PlanningBox。生成器、逆向导入、鼠标预览和参数模式分别处理固定占格；横门2×1、竖门1×2、两种梯子默认均1×1。

2026-10-17梯子规格：新放置正面/侧面梯子与共享Prefab均为居中1×1，Trigger保留原深度和横向宽度。生成器始终按PlanningBox实际宽高适配，不依赖isMerged推断尺寸，因此旧1×2存档重新应用不会缩小；逆向导入读取SizeCells，不再硬编码两格。未重建的旧场景实例由NeutralLadder依据既有SizeCells及单格根缩放恢复旧两格Trigger和下格中心轴，Artwork刷新与Awake均调用，避免修改共享Prefab后旧梯子变短。外观按Trigger平铺；已按矩形缩放的新长梯不重复扩大。不新增规划字段，不使旧场景签名因默认字段变化而全量失效。

验证应涵盖详情/装配放置、自动同步与撤销、按钮增删、绑定变更、门删除、重置、多 Collider 玩家、梯子禁用及相邻梯子交接。静态编译不能替代实际玩家攀爬手感、Timeline 和打包验收。
