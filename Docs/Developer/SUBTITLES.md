# 字幕与提示文字

## 使用

1. 在道具预制体上添加 `SubtitleTrigger`，确保物体有 `Collider`；组件会自动把 Collider 设为 Trigger。
2. 默认 `进入游戏时隐藏物体外观` 已开启，会关闭该物体及子物体的所有 Renderer，但保留 Collider。
3. 选择“中间大字”或“下方小字”，填写字幕文字；默认使用字幕管理器中的对应默认样式。
4. 需要单独覆盖字体、字号、颜色、位置、停留时间和进出动画时，关闭“使用管理器默认样式”后再编辑“字幕样式”。位置支持屏幕中央、上下方、四角、左右侧中央和自定义坐标。
5. “允许重复激发”关闭时，玩家只在第一次进入触发区域时看到字幕；开启后每次重新进入触发区域都会再次显示。
6. Play Mode 中可点击“预览字幕”检查当前触发器的实际效果。

## 管理器

`SubtitleManager` 会在首次显示字幕时自动创建：

- 统一管理“中间大字”和“下方小字”两个显示层。
- 配置默认字体文件、字号、颜色、屏幕位置、偏移、停留时间。
- 分别配置进入动画、退出动画、时长、曲线、移动距离、缩放、旋转、过冲、漂浮、故障和打字速度。新增动画包含淡入横滑、弹性弹出、回弹入场、旋转入场、打字机、故障抖动、漂浮和脉冲。
- 使用 `ScreenSpaceOverlay` 画布；不依赖、申请或写入任何相机组件。

运行时 API：

```csharp
SubtitleManager.Instance.ShowLarge("中间大字");
SubtitleManager.Instance.ShowBottom("下方小字");
SubtitleManager.Instance.Show(new SubtitleCue
{
    layer = SubtitleLayer.Center,
    text = "自定义字幕"
});
SubtitleManager.Instance.HideAll();
```

`SubtitleTrigger` 只负责触发并通过 `SubtitleServiceRegistry` 调用 `ISubtitleService.Show(cue)`，不持有字幕画布、字体或动画逻辑。

## 解耦接口

- 字幕端通过 `ISubtitleService` 暴露显示、隐藏接口；`SubtitleServiceRegistry` 负责拿到当前服务，触发组件不直接依赖具体管理器类型。
- 相机端通过共享的 `ICameraViewportSource` 暴露只读视口信息。
- `CameraControlManager` 实现 `ICameraViewportSource`；`SubtitleManager` 只通过接口查询视口，不引用具体相机组件。
- 当前字幕使用 `ScreenSpaceOverlay`，没有相机也能正常显示；相机接口只作为未来世界空间字幕、视口适配或自定义布局的可选扩展点。
