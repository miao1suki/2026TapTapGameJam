---
name: word-note-runtime-api
description: WordNoteUI 运行时 API、位置消息、堆叠、组合动画和设备化提示文案的实现规范。
---

# WordNote UI 运行时规范

`WordNote_UI` 是 `Systems_UI` 场景中与 `GameUI` 同级的常驻 `ScreenSpaceOverlay` Canvas。

## 消息创建

基本入口：

```csharp
WordNoteHandle handle = WordNoteUI.Instance.Show(
    "消息内容",
    style);
```

常用快捷入口：

```csharp
WordNoteUI.Instance.ShowTimed(
    "消息内容",
    2f,
    WordNotePosition.Bottom);

WordNoteUI.Instance.ShowFlash(
    "消息内容",
    WordNotePosition.Center);

WordNoteUI.Instance.ShowPersistent(
    "长期消息");
```

新消息先进入总队列，再进入对应位置队列。

## 消息状态和 ID

- `WordNoteHandle.Id`：唯一 ID。
- `WordNoteHandle.State`：`Queued`、`Entering`、`Showing`、`Exiting`、`Finished`。
- `WordNoteHandle.ForceExit()`：按 ID 强制退场。
- `WordNoteHandle.ForceDisappear()`：按 ID 立即消失。

驻留消息默认不会被普通 `ExitAll` 或 `DisappearAll` 清除，必须通过 ID 强制退场；需要批量清理时显式传 `includePersistent = true`。

## 位置

九个位置：

```text
Center
MiddleLeft
MiddleRight
Bottom
BottomLeft
BottomRight
Top
TopLeft
TopRight
```

同一位置可同时显示多条消息，后续消息显示在上一条下方。

`stackSpacing` 是统一间距；前一条结束后，后续消息通过 DOTween 补位。

## 独立显示

`WordNoteStyle.participateInStackLayout`：

- `true`：参与位置队列堆叠和补位。
- `false`：固定在当前显示位置第一格，不占堆叠序号，不影响其他消息。

## 组合动画

`WordNoteMotionSettings.useCombinedChannels = true` 时，以下通道独立组合：

- `useFade`：显隐。
- `useHorizontalMove` + `horizontalDirection` + `horizontalDistance`：水平移动。
- `useVerticalMove` + `verticalDirection` + `verticalDistance`：竖直移动。
- `useScale` + `startScale` / `endScale`：缩放。
- `duration`：动画时长。
- `startSpeed` / `endSpeed`：初末速度。
- `easePower`：变速比例。
- `useTypewriter` / `typewriterCharactersPerSecond`：打字机逐字出现/消失及速度。

所有动画使用 DOTween 和 `SetUpdate(true)`，暂停 `Time.timeScale` 不阻塞界面消息。

## 颜色和描边

字体色和描边色使用 `WordNoteColor`：

```text
红 / 橙 / 黄 / 绿 / 青 / 蓝 / 紫 / 白 / 黑
```

描边开关是 `WordNoteStyle.useOutline`。

`WordNoteStyle.richText` 控制 TMP 富文本；局部着色使用：

```csharp
<color=#RRGGBB>文字</color>
```

## 设备化按键文案

不要自己拼：

```csharp
$"按 {key} ..."
```

统一使用：

```csharp
InputPromptUtility.FormatKey(InputActionId.Respawn);
InputPromptUtility.FormatAction(
    InputActionId.Respawn,
    "复活",
    "复活");
```

规则：

- 电脑或手柄：`按[R]以复活`。
- 手机或触屏：`按下复活以复活`。
- 不允许硬编码 `R`、`Q`、`E` 或具体手柄按钮名。

## 当前业务消息

- 搬运提示和非法提示位于 `Bottom`，白字黑描边。
- `PlayerInputDriver` 持有搬运提示的 ID，退出搬运状态时统一关闭。
- 复活提示位于 `Center`，长期驻留、无动画、大字号、白字黑描边，按下复活后关闭。
- 可搬运物候选提示位于物体正中心，黑字白描边，当前字号为 `2.7` 并使用细描边；电脑/手柄显示当前交互键后换行显示“搬运”，每行居中，手机只显示“搬运”。渲染层级必须高于场景和玩家。
- 选色提示位于 `Bottom`，文本为 `当前选择X色`，显示 `2` 秒，显隐与左移组合入场、显隐与右移组合退场，各 `0.5` 秒，白字黑描边；颜色名取 `ColorCatalog.displayName`，颜色词用对应 `swatch` 富文本着色。
- 染色反馈位于 `Bottom`，无效目标显示 `这个物体好像不能与这个颜色反应`，超出交互范围显示 `无法触碰到这个物体`，成功显示 `染色成功`；三条都与非法放置提示共用显示 `2` 秒、显隐与左移组合入场、显隐与右移组合退场、白字黑描边和同位置堆叠规则。
