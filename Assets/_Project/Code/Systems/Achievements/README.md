# 成就系统

## 目录

```text
Assets/_Project/Code/Systems/Achievements/
  Runtime/
    AchievementManager.cs
    AchievementSO.cs
    AchievementCatalogSO.cs
    AchievementDetector.cs
    AchievementSignalBridge.cs
    AchievementTypes.cs
    AchievementSaveService.cs
    IAchievementUnlockReceiver.cs
    IAchievementSignalSource.cs
  Editor/
    AchievementToolWindow.cs
    AchievementSOEditor.cs
    AchievementManagerEditor.cs
    AchievementDetectorEditor.cs
    AchievementSignalBridgeEditor.cs
    AchievementEditorHelp.cs
    AchievementEditorStyles.cs
    Graph/
      AchievementGraphView.cs
      AchievementGraphNode.cs
      AchievementGraphBaker.cs
      AchievementGraphWindow.cs
```

## 运行时

`AchievementManager` 是全局唯一成就入口：

```csharp
AchievementManager.Instance.TriggerById(
    1001,
    "0001",
    count: 1,
    progress: 1f);

AchievementManager.Instance.TriggerByName(
    "击杀五十个怪物",
    "0001",
    count: 1,
    progress: 1f);

AchievementManager.Instance.Trigger(
    1001,
    "0001");

AchievementManager.Instance.Trigger(
    "击杀五十个怪物",
    "0001");
```

界面术语与代码名的对应关系：

```text
触发一次    代码：AchievementConditionMode.Trigger
累计数量    代码：AchievementConditionMode.Count
完成百分比  代码：AchievementConditionMode.Progress
失败条件    代码：lockConditionIds / IsLockCondition
完成条件    代码：rootNodeId / RootNodeId
```

“触发一次”忽略 `count` 和 `progress`。“累计数量”使用 `count`。
“完成百分比”使用 `progress`，默认 `1` 表示增加 `1%`。

“失败条件”表示这个条件一旦达成，当前成就将永久无法完成。它不是用来解锁
成就的条件，也不会在之后恢复。

Canvas 后续实现：

```csharp
public interface IAchievementUnlockReceiver
{
    void OnAchievementUnlocked(string displayName);
}
```

然后在 `OnEnable` 注册，在 `OnDisable` 注销。没有接收者时，解锁仍然会保存，
只是不显示提示。

## 检测组件

`AchievementDetector` 挂载在物体上，列表中的每一项可以配置：

```text
源组件
信号名
成就编号或显示名
条件编号
增加计数
增加进度（%）
```

支持自定义 `IAchievementSignalSource`，以及以下官方 UI 组件：

```text
Button.onClick
Slider.onValueChanged
Toggle.onValueChanged
Dropdown.onValueChanged
InputField.onValueChanged
ScrollRect.onValueChanged
```

检测组件只负责转发信号，不判断成就是否完成。

## 玩法信号接入

玩法组件不直接引用成就系统。统一数据流如下：

```text
玩家 / 相机 / 平台 / 按钮 / 游戏流程 / 战斗
    -> GameplaySignalHub.Emit
    -> AchievementSignalBridge
    -> AchievementDetector
    -> AchievementManager
```

`GameplaySignalHub` 和 `AchievementSignalIds` 位于
`Project.InputAbstraction.Runtime`，玩法程序集只需要依赖输入抽象程序集。
`AchievementSignalBridge`、`AchievementDetector` 和 `AchievementManager`
位于 `Project.Achievements.Runtime`，因此不存在玩法系统反向依赖成就系统的问题。

通常的配置方式：

1. 在发出信号的物体上添加 `AchievementSignalBridge`。
2. 把“信号来源”指向同物体上的玩法组件；该组件会通过
   `IAchievementSignalProvider` 提供可选择的信号列表。
3. 在任意物体上添加 `AchievementDetector`，把“源组件”指向该 Bridge。
4. 选择信号、成就、条件和增加量。
5. Bridge 的信号来源留空时，会作为全局桥监听所有物体发出的同名信号。

当前支持下拉选择的信号：

```text
PlayerJumped
PlayerLanded
PlayerActionStarted
PlayerActionCompleted
LadderClimbStarted
LadderClimbEnded

CameraModeChanged
CameraTransitionStarted
CameraTransitionCompleted
CameraFaceFront
CameraFaceRight
CameraFaceBack
CameraFaceLeft

GameStarted
GamePaused
GameResumed
LevelGoalReached

InteractButtonActivated

PlatformButtonSignalReceived
PlatformButtonSignalCanceled
PlatformMovementStarted
PlatformMovementStopped
PlatformReachedButtonTarget
PassengerBoarded
PassengerLeft
ProjectionTeleport

DamageTaken
EntityDied
HitTarget
```

`AchievementSignalBridge` 也提供 `Emit`、`EmitWithName`、`EmitWithCount`、
`EmitWithProgress` 和 `EmitWithValues`，可供 UnityEvent 或以后新增的玩法脚本调用。

## 编辑器工具

菜单：

```text
Tools > 2026TapTap > Achievements > 成就工具
```

工具窗口提供：

```text
选择数据盒目录
批量创建数据盒
扫描和校验编号
编辑数据盒
自动整理条件编号
应用到游戏逻辑中
设置 JSON 存档目录和文件名
```

数据盒 Inspector 只读，编辑入口在工具窗口中。
选中成就后，在“成就信息”区填写“成就名字”和“成就简介”。
“发布信息”区预留给隐藏状态和平台成就编号；平台编号留空即可。

默认目录下提供示例数据盒：

```text
Assets/_Project/Content/Data/Global/Achievements/
  Achievement_9001_AllFeatures.asset
```

示例包含一条需要两个条件全部达成的路径、另一条任意一个条件达成的路径，
以及一个失败条件。默认示例不包含取反和 XOR。

工具窗口中的“打开成就具体条件逻辑图”会打开关系画布。画布支持添加条件节点、
条件组和“完成条件”，并通过端口拖拽连线。界面上使用：

窗口顶部的“当前数据盒”可以直接切换或拖入其他 `AchievementSO`。如果当前
逻辑图有未保存修改，切换前会询问是否保存。

条件方块会直接显示“编号 + 实际条件文字”，例如“0002 · 收集 0/3 枚”。
具体文字放在“条件结果”和连线端口同一行，节点高度随文字换行自动调整，不需要
手动展开。条件组和最终结果方块会用一行“接入：编号”显示来源。

```text
全部达成           所有条件都完成
任意一个达成       完成其中一个就可以
只能有一个达成     这些条件里只能完成一个
必须未发生         连接进来的条件必须没有发生
```

点击“保存到成就”才会把节点和连接写入 AchievementSO。节点位置只用于编辑器
显示。

“失败条件”现在是独立方块：先点“＋ 失败条件”，再把要禁止的条件连进去。
一个失败条件方块只接收一个条件；需要多个失败条件时生成多个方块。

工具窗口和成就具体条件逻辑图的工具栏都提供“撤回 / 重做”。右侧顶部也能直接修改
成就名字和成就简介。逻辑图右侧属性面板可以修改选中条件节点的前后文字、
达成方式和目标值，也可以修改条件组的组合方式。
成就简介输入框会按面板宽度自动换行。

保存前会检查“完成条件”、条件组连接数量和循环连接。保存后，运行时只读取
`conditions`、`logicNodes`、`inputNodeIds`、`rootNodeId` 和
`lockConditionIds`，不会加载 GraphView。

## 运行时加载

默认游戏逻辑目录：

```text
Assets/_Project/Code/Systems/Achievements/Runtime/Resources/
  AchievementCatalog.asset
```

工具窗口的“应用到游戏逻辑中”会把所有成就数据盒引用写入该目录。运行时管理器
只读取这个目录，不会直接扫描所有 `AchievementSO`。

编辑器同时提供固定目录自动同步：`AchievementCatalogAutoSync` 会监听
`Assets/_Project/Content/Data/Global/Achievements` 的增删改，并在进入 Play
Mode 前重新扫描该目录、更新 `AchievementCatalog.asset`。运行时仍然只读取
Resources 目录，因此打包后的行为稳定，不依赖 `AssetDatabase`。

## 存档

默认路径：

```text
Application.persistentDataPath/Achievements/achievements.json
```

存档目录输入框留空时使用上面的默认路径；界面下方会显示实际目录。
开发阶段如果游戏逻辑目录版本变化，现有成就存档会被视为不兼容并重建。
