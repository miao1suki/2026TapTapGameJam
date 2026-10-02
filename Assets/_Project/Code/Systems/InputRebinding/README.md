# 输入重绑定系统

## 概述

输入重绑定系统用于编辑、保存和运行时加载项目按键映射。它将 Input System 的
Binding、Interaction、Action 和 InputActionAsset 管理封装在逻辑层与编辑器层，
玩法代码仍然只通过 `GameInput` 和 `InputActionId` 读取输入。

模块遵循单向依赖：

```text
Player
  -> InputAbstraction

InputRebinding Runtime
  -> InputAbstraction

InputRebinding Editor
  -> InputRebinding Runtime
  -> InputAbstraction
```

`InputAbstraction` 不引用玩家、Timeline、编辑器或重绑定窗口。删除或关闭重绑定
模块时，游戏运行时输入仍然可以独立工作。

## 目录结构

```text
Assets/_Project/Code/Systems/InputRebinding/
  Runtime/
    InputBindingService.cs
    InputBindingStorage.cs
    InputBindingTypes.cs
    InputBindingBootstrap.cs
  Editor/
    InputBindingEditorWindow.cs
    Project.InputRebinding.Editor.asmdef
  Runtime/
    Project.InputRebinding.Runtime.asmdef
```

共享的触发策略与运行时应答规则位于：

```text
Assets/_Project/Code/Systems/InputAbstraction/Runtime/
  InputActionInteractionPolicy.cs
  InputBindingOverrideStore.cs
  InputService.cs
  UnityInputSource.cs
```

## 输入模型

### Action 类型

```text
Move / Look / Navigate
  持续值输入，不显示点击、长按或触发策略

Jump / Interact / Cancel / Submit / Pause / Attack / CameraModeSwitch
  瞬时按钮动作，默认只按点击触发

Crouch / Sprint
  状态动作，默认允许点击与长按切换

PointerPrimary / PointerSecondary
  瞬时按钮动作，默认只按点击触发
```

### 触发策略

每个按钮动作有一个触发策略：

```text
仅点击
  所有绑定固定为按下触发

仅长按
  所有绑定固定为按住触发

点击/长按可切换
  绑定行允许在点击和长按之间切换
```

默认策略：

```text
Crouch  -> 点击/长按可切换
Sprint  -> 点击/长按可切换
其他按钮 -> 仅点击
```

绑定行中的点击与长按是当前实际触发方式。修改任意绑定行后，同一动作的所有
顶层绑定会同步，避免键盘和手柄使用不同触发时机。

### 动作语义

```text
Crouch / Sprint
  点击：切换状态
  长按：按住保持，松开结束

Jump / Interact / Cancel / Submit / Pause / Attack
  按下后触发一次

CameraModeSwitch
  触发一次视角切换

PointerPrimary / PointerSecondary
  触发一次 2D 平面旋转
```

蹲下和冲刺的点击切换状态会在以下情况被取消并恢复默认：

```text
跳跃
主动作 Timeline 开始
进入攀爬
Timeline 接管玩家控制
```

## 编辑器窗口

打开：

```text
Tools > 2026TapTap > Input > 按键映射
```

快捷键：

```text
Ctrl + Shift + K
```

窗口操作：

```text
拖动标题栏       移动工具窗口
右上角 -/+       收起或展开主体
X                关闭窗口
Ctrl + Z         撤销
Ctrl + Y         重做
Ctrl + Shift + Z 重做
```

窗口刷新时会保留：

```text
当前滚动位置
已经展开的动作
```

进入或退出 Play 模式时，窗口会执行以下流程：

```text
取消正在进行的改键
取消正在进行的键盘监听
释放旧 InputActionAsset
重新创建绑定服务
重建窗口内容
```

该过程不会重置用户保存的键位和触发策略。

## 绑定行操作

每个绑定行提供：

```text
改键
  进入交互改键，按下目标键、鼠标键或手柄键

重置
  恢复该 Binding 对应的默认配置

删除
  删除当前绑定
```

只有动作为“点击/长按可切换”时，绑定行才显示触发方式选择。

## 新增绑定

每个动作底部始终保留“新增绑定”区域，删除全部绑定后仍可重新添加。

可选内容：

```text
平台
  键盘、鼠标、手柄、触屏

初始键位
  根据平台显示可选控制

触发策略
  仅点击、仅长按、点击/长按可切换
```

动作会根据自身用途预选推荐平台和键位，例如：

```text
Jump             空格
Interact         E
Cancel / Pause   Escape
Submit           回车
Crouch           左 Ctrl
Sprint           左 Shift
CameraModeSwitch Tab
Attack           鼠标左键
PointerPrimary   鼠标左键
PointerSecondary 鼠标右键
Look             鼠标移动
Navigate         左摇杆
```

非键盘平台使用“初始键位”列表选择，然后点击“添加”。

## 键盘监听

键盘平台不使用键位下拉列表。点击“听取并添加”后：

```text
字段显示：监听中
当前识别候选：识别：Left Shift
成功识别：创建新绑定并显示最终按键
```

监听期间不会创建临时绑定。只有识别成功后才会调用
`InputBindingService.AddBinding`。

取消规则：

```text
收起当前动作栏
切换当前新增平台
关闭窗口
进入或退出 Play 模式
按 Esc
```

以上操作都会取消监听，不留下绑定。

键盘监听覆盖 Input System 能识别的全部键盘控制，包括：

```text
左右 Shift
左右 Ctrl
左右 Alt
左右 Meta/Win
字母键、数字键、功能键、方向键、小键盘键
```

`Esc` 固定用于取消监听，因此不能通过监听录入 `Esc`。非键盘平台仍使用
“初始键位”列表。

## 撤销与重做

历史快照同时保存：

```text
InputActionAsset JSON
所有动作的触发策略
```

因此以下操作都可以正确撤销和重做：

```text
新增、删除、清空绑定
改键、重置绑定
修改点击或长按
修改仅点击、仅长按、可切换策略
重置全部
```

历史上限为 50 条。

## 逻辑层 API

核心类：

```csharp
InputBindingService
```

常用方法：

```csharp
GetBindings(InputActionId actionId)
GetActionTrigger(InputActionId actionId)
StartRebind(actionId, bindingIndex, completed, canceled)
SetTrigger(actionId, bindingIndex, InputBindingTrigger trigger)
SetTriggerPolicy(actionId, InputActionTriggerPolicy policy)
AddBinding(actionId, device, trigger)
AddBinding(actionId, path, device, trigger)
RemoveBinding(actionId, bindingIndex)
ClearBindings(actionId)
ResetBinding(actionId, bindingIndex)
ResetAll()
Undo()
Redo()
Save()
Load()
Dispose()
```

事件：

```csharp
Changed
```

创建服务：

```csharp
InputBindingService service =
    InputBindingService.CreateFromInputService();
```

编辑器创建独立克隆：

```csharp
InputBindingService service =
    InputBindingService.CreateForEditor();
```

## 运行时加载

可选组件：

```text
InputBindingBootstrap
```

挂载后，`Awake` 创建服务并加载保存的绑定。即使不挂该组件，
`InputService` 也会在创建 Unity 输入源时：

```text
读取完整 InputActionAsset JSON
应用绑定覆盖
应用触发策略
归一化所有按钮动作
```

玩法层使用：

```csharp
GameInput.IsPressed(actionId)
GameInput.WasTriggeredThisFrame(actionId)
GameInput.GetActionTrigger(actionId)
```

持续状态使用 `IsPressed`。按钮动作的完成时机使用
`WasTriggeredThisFrame`，它内部对应 Input System 的
`WasPerformedThisFrame`。

## 存储

默认绑定存储：

```text
2026TapTap.InputBindings.Asset.v3
```

默认触发策略存储：

```text
2026TapTap.InputBindings.TriggerPolicy.v1.<ActionId>
```

默认映射来源：

```text
Assets/_Project/Code/Systems/InputAbstraction/Runtime/Resources/
  DefaultGameplayInput.inputactions
```

该资产使用固定 Action 和 Binding ID，保证新增、删除和改键后的完整 JSON
可以稳定恢复。

## 自定义存储

绑定数据通过以下接口保存：

```csharp
public interface IInputBindingStorage
{
    string Load();
    void Save(string json);
    void Clear();
}
```

默认实现：

```csharp
PlayerPrefsInputBindingStorage
```

注意：触发策略当前由 `InputActionInteractionPolicy` 使用独立 PlayerPrefs
存储。如果后续将绑定存储替换为云存档、配置文件或平台账号存储，还需要
同步迁移触发策略存储。

## 扩展方式

新增动作：

```text
1. 在 InputActionId 中加入稳定 ID
2. 在 InputActionCatalog 或默认 inputactions 中创建 Action
3. 设置必要的绑定和默认触发策略
4. 在玩法层通过 GameInput 读取
```

新增设备键位：

```text
1. 在 GetControlOptions 增加显示名称
2. 在 GetControlPath 增加 Input System path
3. 确认 ResolveDevice 能识别对应设备
4. 确认 GetGroup 返回正确 Binding Group
```

## 已知限制

- `Esc` 是监听取消键，不能通过键盘监听录入。
- 当前触发策略是动作级配置，同一动作的所有顶层绑定保持相同触发时机。
- 触发策略和绑定覆盖默认保存在本机 PlayerPrefs，不同机器不会自动同步。
- 编辑器窗口的原生灰色标题栏由 Unity `EditorWindow` 绘制，公开 API 无法移除。

## 测试清单

```text
键盘监听左 Shift、右 Shift、左右 Ctrl、方向键和小键盘
监听中收起动作栏，确认取消且未创建绑定
监听中关闭窗口，确认取消且未创建绑定
切换新增平台，确认旧监听被取消
删除绑定后重新添加
修改触发策略后撤销和重做
点击与长按切换后进入 Play，确认行为一致
进入和退出 Play，确认控制台没有 MissingReferenceException
```
