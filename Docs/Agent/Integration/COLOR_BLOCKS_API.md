# 可变色方块接入 API

当前仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。本功能位于 `Assets/_Project/Code/Systems/ColorBlocks`，不依赖旧 2026Test 玩法。

## 状态职责

| 模块 | 负责 | 不负责 |
|---|---|---|
| `ColorCatalog` | 类型 ID、名称、Unity 层、目标材质、事件 ID、交互图数据 | 玩家进度 |
| `ColorWorldManager` | 解锁集合、方块当前类型、染色许可 | 材质渐变动画 |
| `ColorBlock` | 只读 `BaseColorTypeId`、可查询 `CurrentColorTypeId`、Renderer 材质切换 | 褪色动画、混色规则 |
| `HSVColorFadeManager` | 逐类型饱和度 0–1 和过渡时间 | 方块颜色属性 |
| `SelectiveHsvRendererFeature` | PC/Mobile URP 中按 Unity 层局部屏幕空间褪色 | 游戏进度、交互 |
| `ColorKeyPickup` | 玩家触发解锁及 Timeline 相机演出 | 决定新的混色规则 |

初始只有 `red`、`green`、`blue` 三个实际类型，界面余下三格为空位。类型 ID 是存档/脚本稳定键，改名只改 `displayName`，不要重命名已有 ID。

## 运行时调用

```csharp
var colors = Project.ColorBlocks.ColorWorldManager.Instance;
bool unlocked = colors.IsUnlocked("red");
bool firstUnlock = colors.Unlock("red");
colors.GrantRecolorAbility(); // 单独的调色能力，不由颜色钥匙自动授予
bool changed = colors.TryRecolor(block, "blue"); // 目标类型必须先解锁
colors.ResetProgress(); // 新游戏时显式调用；跨关卡不自动清空

string immutableBase = block.BaseColorTypeId;
string current = block.CurrentColorTypeId; // null 表示尚未解锁的无色状态

Project.ColorBlocks.HSVColorFadeManager.Instance.SetColorFaded("red", false, 1.2f);
float saturation = Project.ColorBlocks.HSVColorFadeManager.Instance.GetSaturation("red");
```

`Unlock` 是幂等操作，只有首次解锁返回 `true`。`TryRecolor` 还要求先单独调用 `GrantRecolorAbility()`；颜色钥匙本身只解锁对应颜色，不授予调色能力。方块的基础类型只有制作场景时配置；运行时不提供 setter。解锁基础类型时，当前类型立刻变为基础类型、材质立刻换为目标材质，屏幕层的 HSV 饱和度独立地从 0 过渡到 1。染色也只改当前类型，不会改基础类型。

## 事件

可使用现有 `EventMgr.Bind(owner, add, remove)` 订阅，随 owner 启停自动解绑：

- `EventMgr.OnColorUnlocked(ColorTypeDefinition)`：首次解锁。
- `EventMgr.OnColorTypeEvent(string eventId, string typeId)`：目录中配置的解锁事件 ID。
- `EventMgr.OnColorBlockChanged(ColorBlock block, string previousId, string currentId)`：方块状态变更。
- `EventMgr.OnRecolorAbilityGranted()`：调色能力首次授予。

Manager 自身也提供 `ColorUnlocked` 与 `BlockColorChanged` C# 事件。避免在回调中再次直接改相同方块造成重入；需要连锁逻辑可延后到下一帧执行。

## 相机和玩家

钥匙触发器要求玩家碰撞物体能找到 `Project.Player.PlayerController`。钥匙上的 `PlayableDirector` 绑定 `CameraTimelineTrack`；运行时拾取器会定位现有 `TimelineCamRig`，若只有 `CameraControlManager` 会在该物体上添加 Rig，再把玩家 Transform 作为演出目标。这条轨道仍经统一相机 Manager 接管，不直接操纵 Camera。Prefab 附带 2 秒演出 Timeline，策划可单独调整片段。

## 交互图边界

工作台的节点/连线保存在 `ColorCatalog.colors[*].nodes/edges`，目前是策划配置数据，**没有运行时执行器**。`ColorBlock` 实现 `IInteractionTarget`，颜色和调色能力都解锁后 `CanInteract` 才为真，但 `TryInteract` 暂返回 false。混色、条件判断、节点输出与碰撞触发规则在明确设计后再单独实现，不应把工作台草图当成已生效行为。

## 渲染约束

每个颜色类型独占一个 Unity 用户层（8–31）。现阶段 Renderer Feature 遮罩绘制 **不透明物体**，在透明物体与 UI 绘制之前执行；透明 Sprite/特效若要参与局部褪色需要另设计支持透明度的遮罩。带轮廓的默认方块 Shader 假设原始 Unity Cube 的局部坐标为 ±0.5；非立方体模型应替换相应材质。一个颜色层上的所有不透明 Renderer 会共享该颜色的褪色状态。多个颜色层会各执行一次遮罩及全屏 Blit，新增类型时应评估目标平台性能。
