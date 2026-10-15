# 绿色钩子

绿色钩子是玩家选中绿色后的主动位移能力。红、蓝选色流程不变；绿色不再显示
范围内所有可交互物体的预览连线。

## 操作与表现

- 选中绿色后，根据鼠标相对玩家的方向显示一段一格长的绿色待机藤蔓头。
- 待机藤蔓头固定 `50%` 透明度，方向吸附到上、下、左、右。
- 点击交互圈外、UI 或无效目标时沿用现有非法提示。
- 点击交互圈内有效目标后，待机藤蔓头消失，正式藤蔓从玩家身上伸出。
- 藤蔓出藤点位于玩家身体偏上位置，默认 `sourceOffset.y = 0.75`，可按 `sourceOffset` 调整。
- 藤蔓伸出速度按格计算，默认 `5.22 格/秒`，可在 `GreenHookAbility` 中调整。
- 钩子伸出开始时锁住玩家重力、移动、跳跃、交互、染色和搬运。
- 藤蔓头必须在伸出过程中命中目标；命中后才开始拉动。
- 正式钩子和拉动阶段的视觉终点取目标所有有效碰撞体的包围盒中心，不会勾在碰撞体边缘或底部。
- 没命中目标时，藤蔓以相同的伸长速度原路收回，收回完成后再解除玩家限制。

## 方向与房间

- 点击位置相对玩家的方向会吸附到最近的上下左右轴。
- 钩子距离不超过 `PlayerInteractionSensor.ScanRadius`。
- 藤蔓头与目标碰撞体最近距离进入“命中判定半径”后视为抓到目标；半径按格配置。
- 钩子路径忽略墙体和物品碰撞，可以穿过它们。
- 钩子路径不能离开玩家当前房间；离开房间、进入其他房间或经过无归属格时拒绝。
- 房间判定使用 `RoomLifecycleManager.FindRegion` 沿轴向逐格检查。

## 抓取规则

- `ContactCubeMotion`：色块向玩家移动，直到接触玩家或无法继续移动。
- ContactCube 到玩家之间的回收路径只要存在任何物理碰撞体，就直接切换为拉动玩家，改由玩家向色块移动。
- `MechanismBaseFeature`：玩家向基座移动，直到接触基座或无法继续移动。
- `ClimbableVineFeature`：玩家向藤蔓移动，直到接触藤蔓或无法继续移动。
- `BouncePlantFeature`：玩家向弹性植物移动，直到接触植物或无法继续移动。

抓取期间仍然使用物理碰撞，因此物体或玩家被墙、地面或其他实体挡住时不会穿过。
色块回收路径有任何物体都会改拉玩家；玩家也无法继续接近时，连续受阻达到“受阻确认时间”后钩子结束。
目标接触玩家时会立即结束拉动，并在两者之间保留最小接触间隙，避免碰撞体互相挤压。
被钩子拉动的 ContactCube 在落地稳定后，会在可重试时间内反复寻找最近的可用网格，
候选格必须同时通过房间归属、关卡网格占用、其他可搬运物占用和物理碰撞检查，
不会为了找空位删除或覆盖其他方块；搜索半径由 `绿钩子吸附搜索半径（格）` 控制。

## 生命周期

- 钩子开始、结束、失败、死亡、房间重置或组件禁用时，都会清理拉动状态并恢复玩家控制。
- 被拉动的色块会暂时挂起接触反应和自身运动，结束后恢复重力与原有约束。
- 绿色技能成功启动一次后清除当前绿色选色。

## 主要源码

- `Assets/_Project/Code/Systems/GreenHook/Runtime/GreenHookAbility.cs`
- `Assets/_Project/Code/Systems/GreenHook/Runtime/GreenHookContracts.cs`
- `Assets/Player/PlayerController.cs`
- `Assets/_Project/Code/Systems/ContactCubes/Runtime/ContactCubeMotion.cs`
- `Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/MechanismBaseFeature.cs`
- `Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/ClimbableVineFeature.cs`
- `Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/BouncePlantFeature.cs`
