# Timeline 对话轨道接入

## 模块

- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Track/DialogueTrack.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Clip/DialogueTimelineClip.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Behavior/DialogueBehaviour.cs`
- `Assets/_Project/Code/Systems/WordNote/Runtime/WordNoteUI.cs`

## 绑定契约

`DialogueTrack` 不绑定任何对象。`DialogueBehaviour` 在片段开始时调用：

```csharp
WordNoteUI.Instance.RequestTimeline(content, style)
```

该方法与普通 `Show` API 共用同一个 `WordNote_UI` 实例、总队列、九个位置队列、堆叠和动画系统，只把来源标记为 `Timeline`。

片段暂停、结束、Graph 停止或 Playable 销毁时，行为调用 `WordNoteHandle.Release()`，只释放本次 Timeline 申请的消息。

## 参数

`DialogueTimelineClip.style` 直接使用 `Project.WordNote.WordNoteStyle`，不维护第二套气泡参数。字体为空时继承 `WordNoteUI.defaultFont`，仍遵守项目 `Cubic_11 SDF` 字体规范；`style.bold` 控制 TMP 加粗。

## 验证

- Runtime 与 Editor 程序集静态编译通过。
- 对话轨道不再创建世界空间对象、Sprite 或独立 Canvas。
- `WordNote_UI` 同时保留 API 调用和 Timeline 申请两种来源。
