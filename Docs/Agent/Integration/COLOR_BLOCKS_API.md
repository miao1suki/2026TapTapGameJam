# 可变色方块接入 API

当前仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。本功能位于 `Assets/_Project/Code/Systems/ColorBlocks`，不依赖旧 2026Test 玩法。

## 状态职责

| 模块 | 负责 | 不负责 |
|---|---|---|
| `ColorCatalog` | 类型 ID、名称、Unity 层、编辑识别材质、事件 ID、交互图数据 | 玩家进度 |
| `ColorWorldManager` | 解锁集合、方块当前类型、染色许可 | 材质渐变动画 |
| `ColorBlock` | 只读 `BaseColorTypeId`、可查询 `CurrentColorTypeId`、未解锁显示白色基础材质／解锁后隐藏基础网格、水体触发区域 | 混色规则、正式视觉效果 |
| `HSVColorFadeManager` | 逐类型饱和度 0–1 和过渡时间 | 方块颜色属性 |
| `SelectiveHsvRendererFeature` | PC/Mobile URP 中按 Unity 层局部屏幕空间褪色 | 游戏进度、交互 |
| `ColorKeyPickup` | 玩家触发解锁及 Timeline 相机演出 | 决定新的混色规则 |
| `ColorInteractionRunner` | 执行颜色目录中明确类型的基础交互图节点 | Timeline 节点、未定的混色规则 |

初始只有 `red`、`green`、`blue` 三个实际类型，界面余下三格为空位。类型 ID 是存档/脚本稳定键，改名只改 `displayName`，不要重命名已有 ID。

关卡编辑器的“红方块 / 绿方块 / 蓝方块”栏目分别绑定 `Assets/_Project/Content/ColorBlocks/Prefabs/ColorBlock_red.prefab`、`ColorBlock_green.prefab`、`ColorBlock_blue.prefab`，采用直接预制体模式。`LevelEditorBlockEntry.ManagedColorTypeId` 与预制体的 `ColorBlock.BaseColorTypeId` 必须一致，颜色目录还须保留相应类型；Scene 绘制和 `PlanningSceneBuilder` 都拒绝失效的栏目，后者在清理旧生成内容之前验证。编辑器不对这些预制体套栏目材质色、贴花或组件模板。其他栏目和已放置的普通方块不被自动改造。

预制体中的 `targetRenderer` 是基础网格：编辑态使用 `ColorCatalog.colors[*].targetMaterial` 识别材质，Prefab 根层号与颜色目录一致；运行时未解锁则使用 `ColorCatalog.NeutralMaterial`（白底黑框），解锁后禁用该 Renderer，不通过属性块染成 RGB 编辑色。这样 Scene／栏目缩略图可以辨认类型，玩家 Game 与构建版在未解锁时看到白色基础方块，解锁后只看到已接入的正式视觉。Scene 缩略图实例不会注册运行时 Manager。蓝色解锁后的视觉仅由 `ColorWorldManager` 创建的 TA 水效果承担；绿色解锁后由 `BlockVineFeature` 提供攀爬能力，正式藤蔓视觉尚未接入；红色正式视觉未接入，解锁后暂不可见但碰撞和交互保留。后续接入正式视觉时须另建运行时效果，不得复用编辑识别材质。`RoyTestScene` 的旧 RGB 基础方块已一次性替换为对应预制体，其他类别保持原样；`Test` 场景的旧红色能源块也已替换。关卡编辑器道具栏目内置 `ColorKey_red/green/blue`，钥匙从开始即使用预制体的对应颜色材质；工作台新建钥匙直接实例化对应预制体，不生成重复 Timeline。

## 运行时调用

```csharp
var colors = Project.ColorBlocks.ColorWorldManager.Instance;
bool unlocked = colors.IsUnlocked("red");
bool firstUnlock = colors.Unlock("red");
colors.GrantRecolorAbility(); // 单独的调色能力，不由颜色钥匙自动授予
bool changed = colors.TryRecolor(block, "blue"); // 目标类型必须先解锁
colors.ResetProgress(); // 新游戏时显式调用；跨关卡不自动清空
colors.SetUnlockedForCurrentSession("blue", true); // 仅当前运行实例，不写存档
colors.SetAllUnlockedForCurrentSession(false); // 仅当前运行实例，全部锁回

string immutableBase = block.BaseColorTypeId;
string current = block.CurrentColorTypeId; // null 表示尚未解锁的无色状态

Project.ColorBlocks.HSVColorFadeManager.Instance.SetColorFaded("red", false, 1.2f);
float saturation = Project.ColorBlocks.HSVColorFadeManager.Instance.GetSaturation("red");
```

`Unlock` 是幂等操作，只有首次解锁返回 `true`。`TryRecolor` 还要求先单独调用 `GrantRecolorAbility()`；颜色钥匙本身只解锁对应颜色，不授予调色能力。`SetUnlockedForCurrentSession` / `SetAllUnlockedForCurrentSession` 只修改当前运行实例，供 Play Mode 测试自由解锁或锁回；不写场景、颜色目录或存档。方块的基础类型只有制作场景时配置；运行时不提供 setter。解锁基础类型时，当前类型立刻变为基础类型，HSV 进度只驱动独立运行时视觉（目前为蓝水），不显露编辑代理；染色也只改当前类型，不会改基础类型。

## 事件

可使用现有 `EventMgr.Bind(owner, add, remove)` 订阅，随 owner 启停自动解绑：

- `EventMgr.OnColorUnlocked(ColorTypeDefinition)`：首次解锁。
- `EventMgr.OnColorTypeEvent(string eventId, string typeId)`：目录中配置的解锁事件 ID。
- `EventMgr.OnColorBlockChanged(ColorBlock block, string previousId, string currentId)`：方块状态变更。
- `EventMgr.OnRecolorAbilityGranted()`：调色能力首次授予。

Manager 自身也提供 `ColorUnlocked` 与 `BlockColorChanged` C# 事件。避免在回调中再次直接改相同方块造成重入；需要连锁逻辑可延后到下一帧执行。

## 相机和玩家

钥匙触发器要求玩家碰撞物体能找到 `Project.Player.PlayerController`。钥匙上的 `PlayableDirector` 绑定 `CameraTimelineTrack`；运行时拾取器会定位现有 `TimelineCamRig`，若只有 `CameraControlManager` 会在该物体上添加 Rig，再把玩家 Transform 作为演出目标。这条轨道仍经统一相机 Manager 接管，不直接操纵 Camera。内置 Timeline 用约 2 秒拉远；拾取器在末段暂停 Director，调用 `Unlock` 并等待 `revealDuration` 完成，再 `Stop`，由 Rig 平滑交还正常相机。正交 2D 的视野放大由 `ICameraLensOverrideSource` 经 Manager 的模式投影后覆盖镜头尺寸，透视镜头则靠运镜距离拉远。无有效相机轨时跳过镜头等待并正常解锁。

## 交互图可执行子集

工作台的节点/连线保存在 `ColorCatalog.colors[*].nodes/edges`。目录仅保留类型化节点；已废弃的自由文本节点不再显示或保存。每一种基础色有自己的图，图在该颜色的 `ColorBlock` 发生接触时运行；`ColorUnlocked` 在 `ColorWorldManager.Unlock` 首次成功时运行。

触发节点：玩家碰到本方块、玩家离开本方块、两个 `ColorBlock` 接触、该颜色首次解锁。接触支持 Collider Trigger 与普通 Collision，且只在接触开始/结束时触发，不逐帧触发。两个完全静态 Collider 相贴不会产生 Unity 接触回调；测试两块接触时至少一方要具备合适的 Rigidbody/Trigger 物理配置。条件节点：需要玩家、需要本方块、对方必须是指定色（另一方没有当前色时以其基础色匹配）。操作节点：本方块改色、解锁指定颜色、指定颜色褪色/恢复。操作失败会停止该分支；本方块改色复用 `TryRecolor`，因此要求目标色已解锁且已获得调色能力。图每次最多访问 64 个节点，环路不会无限执行。

这些节点只使用颜色域 API，不直接操作 `BlockRuntime` 的 State/Channel。`BlockFeature` 的 Requires/Conflicts/Writes 约束仍照常适用于挂在该物体上的 BlockFeature；若未来出现写 `BlockChannel.Color` 的 Feature，应先定义桥接与唯一写入方，不能并行写颜色。玩家控制器提供 `Swimming` 状态与 `EnterWater(Component)` / `ExitWater(Component)`；已解锁的蓝色方块通过 Trigger 自动维护游泳状态，无需交互图节点。九种交互矩阵中的其他蓝水行为、混色变物体等效果还未实现。`IInteractionTarget.TryInteract` 仍返回 false，不要把它当成染色入口。颜色钥匙的相机拉远→解锁／恢复→相机交还由 `ColorKeyPickup`/Manager 负责，不需要再用图重复执行。

## 渲染约束

每个颜色类型独占一个 Unity 用户层（8–31）。现阶段 Renderer Feature 遮罩绘制 **不透明物体**，在透明物体与 UI 绘制之前执行；透明 Sprite/特效若要参与局部褪色需要另设计支持透明度的遮罩。带轮廓的默认方块 Shader 假设原始 Unity Cube 的局部坐标为 ±0.5；非立方体模型应替换相应材质。一个颜色层上的所有不透明 Renderer 会共享该颜色的褪色状态。多个颜色层会各执行一次遮罩及全屏 Blit，新增类型时应评估目标平台性能。

`ColorBlock` 运行时将其子层级的 Renderer 对象设到当前类型的层；基础网格仅在未解锁时启用，并且材质固定为白色中性材质。无交互的场景物体无需挂 `ColorBlock`，可在 `Tools/2026TapTap/颜色/褪色调试` 选择物体并批量把 Renderer 对象标记到某个颜色层；此操作支持 Undo、保存场景时持久化。Unity Layer 还参与碰撞矩阵与相机剔除，关卡接线后需在 Physics/Camera 配置中核对。

蓝色方块解锁后，`ColorWorldManager` 按 BoxCollider 边界合并相邻矩形水区，实例化 `ColorCatalog.BlueWaterPrefab` 对应的 TA 水效果。大型单块按自身边界生成一整个水区。水视觉实例的 `InteractiveWater.SetReveal(progress)` 用与蓝色 HSV 恢复相同的进度，正面通过 `_MainColor` Alpha 渐显，顶面从小展开；`FrontMesh.shadergraph` 为透明表面。方块水体的模拟纹理在运行时共享、尺寸降至 256×256，并关闭逐水区反射相机；TA 原始独立水预制体默认进度 1，不受这一配置影响。视觉实例由 Manager 管理，不写入场景。蓝色块的碰撞体在激活后切为 Trigger，玩家进入/离开时切换游泳状态。静态颜色方块不应把蓝色水碰撞体当成可站立地面。
