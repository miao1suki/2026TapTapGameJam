# 非颜色门 API

新增横门、无色梯子与门提示的接口见 [NEUTRAL_MECHANISMS_API.md](NEUTRAL_MECHANISMS_API.md)。旧竖门 ID 保留，新添加按钮上限为三个。

源码：`Code/Systems/Mechanisms`。DoorController / DoorButton 独立继承 MonoBehaviour，不挂 ColorObject、BlockAbilityHost 或 BlockRuntime，不实现 IColorApplicationTarget。不复用 Red_Button 的开关式信号逻辑。

- `DoorController.State`：Locked / Unlocked，只读。
- `IsWaitingToOpen` / `UnlockDelay`：等待状态和图案展示时间，默认0.8秒。可用Timeline镜头先到门附近，再保留展示时间。等待时State为Locked且碰撞保留；暂停冻结，持续信号释放、ResetDoor或禁用取消等待，不残留延迟回调。
- `DoorController.Buttons`：只读绑定列表。
- `SetButtons(values)`：生成器完成增量整合后绑定实际 Scene 对象。
- `NotifyButtonPressed(actor)`：重新评估全部绑定按钮；全部按下后进入图案展示等待，结束才开门，含持续按钮且信号不齐时取消等待或关门。不清除其他保持按钮进度，门提示同步更新。接口名称兼容既有调用，同时用于释放通知。
- `ResetDoor()`：停止自身 Tween / Timeline、释放控制锁、恢复关闭姿态、实体碰撞和按钮初始状态。
- `StateChanged`：订阅者按生命周期取消订阅。
- `DoorButton.State / IsPressed / Owner`：只读运行状态和所属门。
- `DoorButton.BindingIndex`：绑定列表位置；0/1/2对应红/绿/蓝美术，不是颜色能力或颜色层。
- `DoorButton.Mode / Persistent`：Latched（一次按下保持，默认/旧场景兼容）或 Held（持续压住）。mode 有 BlockParameter，规划编辑器参数模式可写入实例配置并随存档/增量同步保存。
- `DoorButton.TryPress(actor)`：供程序主动申请按下，接受玩家或 ContactCube，不是输入交互接口。正常玩法每个物理步用 NonAlloc OBB 查询并检查玩家脚部或方块底面；只查询非 Trigger 实体。持续模式须有有效物理重量，否则下一步释放；多个重量/Collider 不会让其中一个离开时误释放。
- `ResetForRoom()`：保持模式无操作，持续模式恢复未按下，并通知门重评估。持续模式禁用时也释放。显式 `ResetDoor/ResetButton` 是清空调试/进度接口，可重置保持模式，不应在 R 或离房间时直接调用。

PlanningBox 的 `singleInstance` 使 1×2 门只生成一个实例；`doorOwnerId` 保存按钮所属门的规划 ID。DoorEntryId=`mechanism-door-standard`；内部按钮 EntryId=`mechanism-door-owned-button` 不进入 palette。按钮专用 prefab 由 PlanningDoorUtility 解析，不依赖随机生成的入口 ID。

门放置预览按固定 1×2 绘制；详情/装配拖拽都以按下格为上格，一次只创建一扇。Scene 放置器使用世界坐标下格作为根位置，提示向上扩展两格。DoorButton 模型局部中心 y=-0.44、尺寸 (0.6,0.12,0.6)，底面位于所属格底 y=-0.5。根 Trigger 中心 y=-0.35、尺寸 (0.6,0.3,0.6)，Awake 同步修正旧场景实例；脚部容差按 GridCellSizeUtility 换算，仅玩家和三色接触方块可触发，玩家辅助 Trigger 不触发。

删除门清理其所属按钮；删除按钮保留门且解除绑定。NotifyDocumentChanged清理孤儿按钮，新增/保存使用既有规划事务与撤销。PlanningBox.doorButtonOrder保存创建次序，旧存档缺字段按规划文档顺序兼容；BindSceneDoors按此顺序解析实际PlanningBoxId，不依赖Hierarchy或instanceID。删除后保留相对次序，颜色按现有列表重新分配；不能引用staging中即将销毁的对象。按钮移动不跟随门作为子物体。

镜头运行时构造 TimelineAsset / CameraTimelineTrack / CameraTimelineClip，保留当前投影和角度，移向门中心后停留，再交还统一相机。自身仅操作门轴，不直接写 Camera。自身持有并清理 director、Timeline、clip、track、临时锚点、Tween 和玩家控制锁；最多等待已有 Timeline 15 秒，超时跳过镜头，不阻塞开门。

区域离开和存活/死亡 R 统一经过 RoomLifecycleManager，只恢复相关区域并保留颜色解锁。门本身持久；持续按钮恢复后取消门信号，保持按钮不重置。没有区域管理器/规划房间时不会猜测房间范围，应通过关卡编辑器应用规划。全局显式进度清空事件可按“全局进度重置时关门”配置重置门与全部按钮，不用于普通区域重置。
