# 可变色方块与颜色属性

颜色系统现在只负责颜色目录、解锁状态、当前颜色属性、局部 HSV 褪色和蓝色水体视觉。物体之间的接触、停留、颜色条件、褪色/恢复和方法调用全部在物体交互管理器的图中配置。

## 状态职责

| 模块 | 负责 | 不负责 |
|---|---|---|
| `ColorCatalog` | 类型 ID、名称、Unity 层、编辑识别材质、事件 ID | 交互图 |
| `ColorRuntimeService` | 解锁集合、方块当前类型、水体视觉聚合和颜色事件 | 玩家选色和物体交互关系 |
| `ColorBlock` | 基础颜色属性、接触事件收集、交互图方法入口、未解锁视觉 | 交互顺序和条件 |
| `HSVColorFadeManager` | 逐类型饱和度和过渡时间 | 物体定义和交互连线 |
| `SelectiveHsvRendererFeature` | 按 Unity 层的局部屏幕空间褪色 | 游戏进度和交互关系 |
| `ColorKeyPickup` | 玩家触发解锁和 Timeline 相机演出 | 物体交互图配置 |

`ColorCatalog` 仍保留 `red`、`green`、`blue` 三种属性。三色方块预制体位于 `Assets/_Project/Content/ColorBlocks/Prefabs`，其交互定义位于 `Assets/_Project/Content/Interactions/Definitions`，打开物体交互管理器会自动同步。

## 运行时调用

```csharp
var colors = Project.ColorBlocks.ColorRuntimeService.Instance;
bool unlocked = colors.IsUnlocked("red");
bool firstUnlock = colors.Unlock("red");
colors.ResetProgress();

Project.ColorBlocks.HSVColorFadeManager.Instance.SetColorFaded("red", false, 1.2f);
```

`ColorRuntimeService.Unlock` 会对同色方块触发物体图的 `Manual` 根节点；三色默认图把它连接到恢复颜色节点。没有交互定义的旧场景才使用直接恢复作为兼容兜底。

## 物体交互入口

`PlayerColorWheel` 只维护玩家选中的已解锁颜色，并在右键轮盘中提供选择。颜色选择不会直接修改物体；选择完成后，目标物体的 `Manual` 图由 `InteractionManager` 执行。`ColorBlock` 在接触开始、结束和停留时向 `InteractionManager` 发送事件。默认三色图提供：

- 玩家进入/离开 → `RequirePlayer` → `OnInteractionPlayerEntered/Left(GameObject)`。
- 物体触碰/停留 → `RequireOtherObject` → 对应方法入口。
- 手动执行 → 由 `RequirePlayerColor` 读取玩家选色，再执行物体自己的效果；颜色钥匙解锁时的无玩家手动触发仍只走恢复节点。

蓝色水体和绿色攀爬仍由各自 `BlockFeature` 实现，但开关时机由上述图节点调用 `ColorBlock` 方法。策划可以在这些节点后继续连接受到颜色、受到物体、延迟、Timeline、褪色、恢复和脚本方法节点。

## 相机和渲染

颜色钥匙仍通过 `TimelineCamRig` 和 `CameraControlManager` 演出；不直接操纵 Camera。颜色层上的不透明 Renderer 由 `SelectiveHsvRendererFeature` 处理。蓝水实例由 `ColorRuntimeService` 聚合创建并按 HSV 恢复进度渐显。

颜色不再有独立工作台、颜色交互图或颜色管理器入口。请从 `Tools/2026TapTap/物体交互管理器` 编辑具体物体的连线。
