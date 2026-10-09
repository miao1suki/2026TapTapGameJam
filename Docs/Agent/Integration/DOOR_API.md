# 非颜色门 API

源码：`Code/Systems/Mechanisms`。DoorController / DoorButton 独立继承 MonoBehaviour，不挂 ColorObject、BlockAbilityHost 或 BlockRuntime，不实现 IColorApplicationTarget。不复用 Red_Button 的开关式信号逻辑。

- `DoorController.State`：Locked / Unlocked，只读。
- `DoorController.Buttons`：只读绑定列表。
- `SetButtons(values)`：生成器完成增量整合后绑定实际 Scene 对象。
- `NotifyButtonPressed(actor)`：所有非空绑定按钮均已按下且至少有一个按钮时，解锁一次。由 DoorButton 调用。
- `ResetDoor()`：停止自身 Tween / Timeline、释放控制锁、恢复关闭姿态、实体碰撞和按钮初始状态。
- `StateChanged`：订阅者按生命周期取消订阅。
- `DoorButton.State / IsPressed / Owner`：只读运行状态和所属门。
- `DoorButton.TryInteract(actor)`：沿用 IInteractionTarget / E，距离读取 GridCellSizeUtility；一次按下后不回弹。

PlanningBox 的 `singleInstance` 使 1×2 门只生成一个实例；`doorOwnerId` 保存按钮所属门的规划 ID。DoorEntryId=`mechanism-door-standard`；内部按钮 EntryId=`mechanism-door-owned-button` 不进入 palette。按钮专用 prefab 由 PlanningDoorUtility 解析，不依赖随机生成的入口 ID。

删除门清理其所属按钮；删除按钮保留门且解除绑定。NotifyDocumentChanged 清理孤儿按钮，新增/保存使用既有规划事务与撤销。增量整合完成后 BindSceneDoors 从 PlanningBoxId 重新解析引用，不能引用 staging 中即将销毁的对象。按钮移动不跟随门作为子物体。

镜头运行时构造 TimelineAsset / CameraTimelineTrack / CameraTimelineClip，保留当前投影和角度，移向门中心后停留，再交还统一相机。自身仅操作门轴，不直接写 Camera。自身持有并清理 director、Timeline、clip、track、临时锚点、Tween 和玩家控制锁；最多等待已有 Timeline 15 秒，超时跳过镜头，不阻塞开门。

现有 `ColorRuntimeService.ResetForRoom()` 已清除解锁、关闭颜色功能、ResetAll HSV、发 OnRoomColorReset；门可选订阅该事件。全仓库未发现房间退出/R 重生调用该入口，R 只对死亡玩家调用 RequestRespawn，并未清除颜色解锁。此次仅检查，不改变其他程序的重生/房间规则。
