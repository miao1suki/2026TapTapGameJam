# Boss 追击寻路接入

## 源码

- `Assets/Boss_Logic/Navigation/BossPathNode.cs`
- `Assets/Boss_Logic/Navigation/BossPathfinder.cs`
- `Assets/Boss_Logic/Navigation/BossPathExecutor.cs`
- `Assets/Boss_Logic/Navigation/BossNavAutoBaker.cs`

## 责任边界

`BossPathfinder` 生成带类型路径序列；`BossPathExecutor` 是马夫，只向 `BossController` 下发基础通行动作，不直接写 Rigidbody。

- 不传送 Boss。
- 不处理阶段血量或玩家伤害。
- 不自动绕开危险道具；默认 `PassThrough`。

## 节点图

`BossPathNode` 保存有向边列表。每条边包含：

- `target`：目标节点。
- `kind`：跑动、爬升、跳跃、下落、乘平台、危险通过、自定义。
- `cost`：相对距离成本。

最后节点回连起点即可形成回环追击。

## BossPathfinder

- `SetTarget(Transform)`：手动指定目标。
- `targetTag`：目标为空时按 Unity Tag 查找。
- `RequestRepath()`：立即重算。
- `ClearPath()`：清空路径。
- `CurrentPath`：当前节点序列。
- `CurrentSteps`：带通行类型的路径段。
- `StartNode` / `GoalNode` / `PathVersion`：只读调试信息。
- `TryGetNextNode(out BossPathNode)` / `TryGetNextStep(out BossPathStep)`。
- `PathChanged`：路径变化事件。
- `StepsChanged`：路径段变化事件。

## BossPathExecutor

- `execute` 控制是否接管移动。
- `moveSpeed`、`climbSpeed`、`jumpRetryDelay` 控制马夫的基础执行节拍。
- 跳跃时长、水平速度和最大跳高/最大垂直速度只由 `BossController` 配置；重力始终使用 `Physics.gravity`，马夫不可传入或修改。
- `autoJumpHeight` / `autoDropHeight`：跑动节点带高度差时自动切换跳跃或下落。
- `autoJumpOnObstacle`：前方被方块阻挡时自动改发跳跃；`obstacleProbePadding` 控制探测余量。
- 自动处理 `Run`、`Jump`、`Climb`、`Drop`、`Ride`。
- `Ride` 当前按目标相对高度在跑动和爬升间选择；移动平台专用同步后续可继续扩展。
- 执行期间通过 `BossController.SetExternalMotion` 提供马夫指令；没有路径时调用 `ClearExternalMotion`，恢复原追击。

## 默认规则

- `hazardPolicy = PassThrough`：危险边照常参与寻路。
- `autoRepath = true`：按 `repathInterval` 重新计算到玩家当前节点。
- `nodeSearchRadius`：Boss 和玩家寻找最近节点时的搜索范围。

## 自动烘焙

- 数据来源：`LevelEditorPlacedBlock.ActiveInstances`。
- 默认按 Boss `3×3` 体积检查净空。
- 自动生成跑动、跳跃、下落边。
- 运行时图放在独立根节点，不修改场景或规划存档。
- `bakeOnlyWhenNoManualNodes` 为真时，已有手摆节点会跳过自动烘焙。
