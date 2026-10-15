# Timeline 对话轨道接入

## 模块

- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Track/DialogueTrack.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Clip/DialogueTimelineClip.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Behavior/DialogueBehaviour.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Runtime/DialogueBubbleView.cs`
- `Assets/GJ_Tools/TimelineTools3D/AboutTimeline/Runtime/DialogueBubbleDefaults.cs`
- `Assets/_Project/Resources/UI/DialogueBubbleDefaults.asset`

## 绑定契约

`DialogueTrack` 绑定 `Transform`。`DialogueBehaviour` 在片段播放时从 Playable 的绑定对象创建 `DialogueBubbleView`，不要求目标物体挂载额外 Host，也不修改玩家、颜色系统或绿色钩子状态。

运行时气泡对象只在片段存活期间创建，片段停止、Graph 停止或对象销毁时同步清理。

## 默认资源

片段字段为空时按以下顺序解析：

1. `DialogueTimelineClip` 上的 Sprite、Material、TMP_FontAsset。
2. `DialogueBubbleDefaults` 中的同名默认引用。

默认配置资源位于 `Assets/_Project/Resources/UI/DialogueBubbleDefaults.asset`，字体固定指向项目 `Cubic_11 SDF`。若默认配置缺少 Sprite 或字体，组件会输出警告并跳过显示，不会退回 Unity 默认字体。

## 参数边界

- `riseDuration` 只控制气泡位移、缩放与淡入的并行动画。
- `typewriterStartDelay` 从 `riseDuration` 结束后才开始计时，保证气泡先浮现、再等待、最后逐字显示。
- `DialogueBubbleView` 只负责表现层；不发送成就、交互、房间或颜色信号。
- 需要接入玩法时，应由外部 Timeline/ActSO 或事件层驱动，不在气泡运行时组件内建立跨功能依赖。

## 验证

- Runtime 与 Editor 程序集静态编译通过。
- 资源 GUID、材质引用、默认字体引用与 `.meta` 已核对。
- 未在 Unity Play Mode 中实际验收动画节奏和画面层级。
