---
name: taptap-pause-wheel-ui
description: 2026TapTap ESC暂停轮盘的完整创意、交互、动画、输入映射和设置复用规范。
---

# ESC 暂停轮盘 UI 完整规范

## 核心目标

将现有 `PauseScreen` 做成一个左侧半屏左轮转盘式暂停菜单。转盘负责选择入口，右侧面板负责显示对应内容；设置、存档和按键业务全部复用已有逻辑。

## 场景边界

- 场景：`Assets/_Project/Scenes/Systems/Systems_UI.unity`
- 主节点：`__System_UI/GameUI/PauseScreen`
- 默认只修改 `PauseScreen` 子树。
- 设置继续复用 `SettingsPanelController`。
- 存档继续复用 `SavePanelController`。
- 设置与存档的具体业务逻辑不得在本模块重写。

## 主界面结构

建议层级：

```text
PauseScreen
├─ DismissLayer
├─ WheelViewport
│  ├─ WheelDecoration
│  └─ OptionArc
├─ ContentViewport
│  ├─ PaintTransition
│  └─ ContentPanel
└─ PageControllers
```

### WheelViewport

- 停靠屏幕左侧，紧贴左边缘。
- 仅显示转盘的右半边，左半边在屏幕外并被裁切。
- 根节点使用 `RectMask2D` 或等效裁切方案，不能让隐藏部分穿透。

### WheelDecoration

- 只负责左轮外壳、扇区、装饰环等视觉。
- 入场动画只做半圈，并使用逐渐减速的缓动，不能转满一圈。
- 旋转结束后必须对齐第一个选项。
- 装饰层可以短暂经过右侧之外或隐藏区域，但不承担选项逻辑。

### OptionArc

- 真正的逻辑选项只排列在可见的右半圆。
- 选项节点、命中区域、确认目标和颜色绑定都属于这一层。
- 选项永远不能进入左侧不可见区域后仍被当作可交互目标。
- 切换选项时移动或重排 `OptionArc` 内的可见选项，不依赖 `WheelDecoration` 的实际角度做逻辑判断。

## 轮盘选项

每个选项至少包含：

```csharp
string displayName;
Color pageColor;
Material sectorMaterial;
GameObject panelRoot;
Button closeButton;
Func action;
```

颜色必须在 Inspector 配置，禁止在代码中写具体 RGB。
轮盘视觉资源必须分层暴露：

- `wheelMaterial`：整轮装饰层材质。
- `wheelSectorMaterial`：所有扇区的默认材质。
- `EscWheelOption.sectorMaterial`：单个扇区覆盖材质，留空时使用默认扇区材质。
- `paintStrokeSprite`、`paintStrokeMaterial`：子页面颜料过渡资源。
- `EscWheelOption.pageColor`：每一页选定的主题颜色，同时驱动扇区高亮和颜料过渡。
- 暂停界面保留现有 UGUI `Text` 作为布局和动态数据源，渲染层由 `TextSdfMirror` 驱动 `TextMeshProUGUI`；字体使用 `uiSdfFont = Cubic_11 SDF`。
- 轮盘标签字号由 `wheelLabelFontSize` 配置，SDF 笔画粗细使用 `textStrokeThickness`，描边使用 `textOutlineColor` / `textOutlineDistance`。

推荐选项：

```text
继续游戏
设置
收藏品
存档
退出到主菜单
```

## 入场动画

打开暂停界面时：

1. 显示 PauseScreen。
2. `WheelDecoration` 完整旋转一圈。
3. 旋转逐渐减速，平稳停止。
4. 对齐第一个选项。
5. `OptionArc` 与主界面内容完成显隐和聚焦。

所有动画使用 DOTween。

## 选择输入

支持：

- W/S：读取移动输入的纵向值。
- 鼠标滚轮：可读取 `Mouse.current.scroll`；若以后接入输入动作，再改为读取映射。
- 鼠标滚轮两次换项之间有可调的最短间隔，避免一次快速滚动跨过多个选项。

切换时：

- 轮盘按选项角度旋转并吸附。
- 受控切换只走相邻选项的最短角度，不能滚动一次绕完整圈。
- 首尾选项循环时沿当前滚动方向只走一格，必须无缝，不允许回到起点方向时绕整圈。
- 旧选项的选中效果在转动过程中逐渐消失，新选项同步逐渐增强，并在转动结束时完成切换。
- 当前选项固定停在可见轮盘中央的对齐角度。
- 当前选项始终比其他选项更大并提亮泛白。
- 新选项使用自己的配置颜色。
- 不改变选项的右半屏逻辑位置。
- 不通过点击扇区改变选中状态，只通过滚轮或纵向移动输入切换。
- `snapDuration` 控制吸附速度，`scrollStepInterval` 控制鼠标滚轮灵敏度，均在 Inspector 中配置。

## 确认输入

确认读取：

```csharp
InputActionId.Jump
```

同时支持鼠标左键、鼠标右键确认。

禁止写死空格。

确认后的顺序：

1. 当前选项的轮盘元素轻微降低视觉权重。
2. 转盘向左偏移一小段距离。
3. 转盘缩小。
4. 对应子面板从屏幕右侧水平进入。
5. 子面板按选项绑定颜色播放颜料铺洒过渡。

这里的“虚化”是轮盘元素的值变化，例如透明度、缩放、颜色强度或轻微偏移，不是屏幕后处理模糊。

## 颜料铺洒

- 过渡层位于内容文字和控件下方。
- 起点为屏幕左上角。
- 目标为当前子页面大标题右侧附近。
- 不得铺展到屏幕正中心。
- 运动速度逐步衰减。
- 只使用当前选项绑定的颜色。
- 颜色不可硬编码。

没有正式美术时，使用 UGUI Image + DOTween 做占位；后续可替换 Sprite 或 Shader。

## 子页面

每个子页面必须有：

- 大标题
- 内容区域
- 右上角红色关闭 X
- 统一的打开和退出动画
- 可复用的现有业务控制器
- 打开时占用右侧区域，左侧保留可配置宽度的轮盘空间；子页面期间底部遮罩透明，轮盘保持渲染。
- 轮盘选项打开的顶层页面背景固定为纯黑；设置页里的声音、按键映射、制作人员三个内层子页背景完全透明，只显示其控件。
- 设置页 UI 必须根据 `SettingsPage` 的实际宽高响应式缩放；左侧栏目和内容区跟随屏幕增大/缩小，底部返回区域锚定真实底边，不能只按固定像素摆放。
- 内容区左边界必须根据左侧栏目缩放后的实际右边界加固定间距动态计算，不能与栏目按钮重叠。
- 存档页 UI 必须根据 `SavePage` 的实际宽高响应式缩放；标题、状态、存档列表、新建和返回按钮随页面尺寸变化，不能在固定 `830×360` 区域内缩成小块。

设置页：

- 复用 `SettingsPanelController`。
- 不新增音量、画质或按键业务。

存档页：

- 复用 `SavePanelController`。
- 后续完整游戏进度恢复使用现有或新定义的存档接口。

收藏品页：

- 从 `AchievementManager.GetAllAchievements()` 直接读取项目成就目录，不维护第二份成就数据。
- 使用两列 `GridLayoutGroup` 长条列表；每条依次显示成就名、成就简介、成就要求，右侧为只读完成图片。
- 新增成就必须优先从左到右填满当前行，再进入下一行。
- 单个成就也保持左上角排列，不能因为数量少而居中。
- 成就标题和成就要求使用同一字号（默认 `36`）；简介保持正常小字并使用灰色。
- 无正式素材时默认使用白色成就块、纯黑标题和纯黑要求；简介保持灰色。
- 成就块材质由 `rowMaterial` 配置，达成图片由 `completedSprite` 配置；达成时只显示整块灰色遮罩与右侧达成图片，不改变文字、内容或底色。
- 成就要求只显示成就树当前轮到的未完成条件，并随 `AchievementProgressChanged` 实时更新，不重建列表或重置滚动位置。
- 未解锁不显示遮罩和达成图片；解锁只增加灰色遮罩与右侧图片，不改长条透明度或底色。
- 成就解锁事件发生时，收藏页立即刷新。
- 每次进入收藏页都重新读取当前成就状态。

## 退出规则

唯一退出入口是当前子页面右上角 X 的 `Button.onClick`。

点击 X：

```text
子页面退场动画与轮盘恢复动画同时开始
→ 子页面退出
→ 返回暂停主轮盘
```

再次按下暂停输入：

```text
找到当前子页面的 X
→ 直接触发同一个 Button.onClick
```

禁止：

- 给 ESC 写第二套退出函数。
- 绕过 X 直接关闭子页面。
- 设置页返回直接回到游戏场景。

主轮盘界面：

- 暂停输入打开/关闭暂停界面。
- 子页面打开时，暂停输入只触发 X 回调。

## 输入映射

- 暂停输入：`InputActionId.Pause`
- 确认输入：`InputActionId.Jump`
- 鼠标确认：鼠标左键或鼠标右键
- 上下移动：`InputActionId.Move`
- 鼠标滚轮：优先读取鼠标设备滚轮值

禁止：

- 写死 `KeyCode.Space`。
- 写死 `KeyCode.Escape` 作为唯一暂停键。
- 直接读取键位字符串替代输入映射。

## 现有系统复用

- `GameUiRouter` 负责暂停状态和 InputActionId.Pause。
- `EscMenuController` 负责轮盘状态、选项切换、子页面和 DOTween。
- `SettingsPanelController`、`SavePanelController` 只作为子页面业务组件接入。
- 不修改设置业务，不新增设置控件。

## 完成检查

- 轮盘可见区域只显示右半边。
- 逻辑选项不进入左侧隐藏区域。
- 入场完整转动并停在第一个选项。
- W/S 和鼠标滚轮可切换，Jump 映射、鼠标左键或鼠标右键可确认。
- 鼠标滚轮不会因为连续滚动帧一次跨过多个选项。
- 选项颜色来自 Inspector。
- 整轮材质、扇区默认材质、单扇区覆盖材质、颜料资源和每页颜色均可从 Inspector 配置。
- 轮盘标签默认使用 `Cubic_11 SDF`、字号 `32`、加粗笔画和黑色描边；所有 PauseScreen 文本使用同一 SDF 字体、笔画粗细与描边设置。
- 子页面从右侧进入。
- 颜料从左上角铺向标题右侧附近。
- 颜料不遮挡标题和控件。
- 子页面有红色 X。
- 子页面打开后停在右侧区域，左侧轮盘保持可见，子页面期间的底部遮罩透明。
- 轮盘选项顶层页为纯黑背景，设置内部三个子页为全透明背景。
- 设置页元素在宽高变化时重新计算比例，不固定在 1920×1080 的静态尺寸。
- 存档页元素在宽高变化时重新计算比例，列表和底部按钮跟随页面尺寸。
- 子页面退场时轮盘必须同步恢复进场，不能等子页面完全消失后再出现。
- ESC 触发同一个 X 的 `Button.onClick`。
- 设置业务未被重写。
- 所有新增动画使用 DOTween。
- 轮盘白线占位装饰默认关闭。
