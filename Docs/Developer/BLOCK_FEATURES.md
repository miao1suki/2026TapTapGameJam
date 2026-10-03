# 方块功能组件

功能方块使用 `Project.BlockFeatures`，核心入口是 `BlockFeature` 和 `BlockRuntime`。
功能不绑定颜色，颜色只作为栏目、外观或状态数据使用。

## 编写约束

- 功能组件继承 `BlockFeature`，不要直接写 `Update`、`FixedUpdate`。
- 一个组件只负责一种主要职责，可以同时实现多个只读能力接口。
- 读取其他功能通过 `BlockContext.Query.TryGet<T>`，不要直接获取具体模块。
- 改变状态通过 `BlockContext.Commands.Send`，不要直接改其他组件字段。
- 瞬时事件通过 `BlockContext.Signals.Emit` 发送。
- 持久关系通过 `BlockContext.Links` 建立和查询。
- 动画、音效、特效、材质和颜色变化通过 `BlockContext.Presentation`。
- 使用 `[BlockFeature]` 声明阶段、顺序、依赖、冲突、端口和写入通道。
- 每个 `BlockFeature` 的声明必须把 `DisplayName` 写在第一项，并填写组件中文翻译；
  不修改 C# 脚本名或 Unity 组件名。组件编辑器折叠标题保留组件名，内容区第一行用粗体大号显示中文翻译。

`BlockRuntime` 会按阶段和顺序调度组件，并在每次 Tick 后按轮次派发信号和命令，
避免组件递归调用或依赖 Unity 脚本执行顺序。

组件编辑窗口会统一显示 `BlockFeature` 的阶段、顺序、提供/读取能力、写入通道、
最近信号/命令/联动/表达事件；没有序列化参数时会显示空状态提示。

功能组件通过 `CollectDebugValues` 和 `CollectDebugActions` 提供可读数据与可执行调试操作，
组件编辑窗口会自动生成对应按钮。`BlockPowerFeature` 已通过这套钩子提供可编辑调试参数、
“发送测试激发信号”和“重置计数”操作；动力网络规则仍未实现。

`BlockFeature` 的配置参数使用 `[SerializeField]` 暴露，可附加 `[BlockParameter]`
设置标签和提示。组件编辑窗口把参数放在“参数”Foldout 中直接编辑；运行时状态和调试操作
分别显示，不把配置参数伪装成只读数据。

动力组件先选择角色，再选择信号类型：

- `开关`：提供 `IBlockPowerSwitch`，由交互系统调用 `SetPower` / `Toggle`；可选择初始开启。
- `信号源`：不需要交互开关，放置后立即开始输出。
- `单次信号`：激活时发送一次。
- `持续信号`：开启时发送持续状态，关闭时发送停止状态。
- `脉冲信号`：开启时按“脉冲间隔（秒）”重复发送。

`传播距离（格）` 是可直接调试的参数，运行时按方块尺寸换算为世界半径，并对范围内的其他
`BlockRuntime` 转发激发信号。动力容量、消耗、供电方向、连接关系和优先级仍未实现。
`测试信号值` 是调试载荷，当前不代表正式动力数值。编辑器会根据角色和信号类型自动隐藏无关参数。
模式和功能阶段、通道、事件类型都已补中文 Inspector 显示。

## 动力组件

`BlockPowerFeature` 位于
`Assets/_Project/Code/Systems/BlockFeatures/Runtime/Power`。
当前提供 `IBlockPowerSignalTransmitter`、`IBlockPowerSwitch` 和两级参数化信号模式。

尚未实现：

- 动力容量、输入输出和消耗
- Port/Link 供电连接
- 供能优先级和通道仲裁
- 激活信号的正式类型和更复杂的传播规则
- 动力调试、连线和表现

在动力规则确定前，不要在其他功能里假设已经有了完整动力网络。
