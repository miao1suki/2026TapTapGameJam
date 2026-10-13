---
name: taptap-typography
description: 2026TapTap 游戏内所有文字资源的统一字体规则；新增或修改开始菜单、暂停界面、HUD、字幕、提示、成就、设置和任何 UGUI/TMP 文本时必须使用。
---

# 项目统一字体

游戏内所有玩家可见文字统一使用：

`Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset`

对应源字体为同目录的 `Cubic_11.ttf`。不要使用 `LegacyRuntime.ttf`、Arial、默认 TMP 字体或临时字体作为最终可见字体。

## 适用范围

- 开始菜单、设置、存档、制作人员、暂停轮盘和所有子页面。
- HUD、字幕、WordNote、任务提示、交互提示、机关提示和弹窗。
- 成就、收藏品、设置项、按键映射说明和调试/测试界面。
- 运行时动态创建的 UGUI `Text` 和 `TextMeshProUGUI`。

## 接入规则

- TMP 或世界空间文字直接使用 `Cubic_11 SDF`。
- 已有 UGUI `Text` 使用共享的 `TextSdfMirror` 保留原布局，并由镜像层显示 SDF 字体；不要把大量已保存 UI 强行迁成 TMP。
- 开始菜单统一通过 `StartMenuTypography` 配置和刷新，字体字段为 `uiSdfFont`。
- 暂停界面统一通过 `EscMenuController` 的 `uiSdfFont` 配置和刷新。
- 新 UI 必须暴露字体引用或复用上述共享配置，不允许把字体路径写死为运行时字符串。
- 如果新增动态生成文字，确保创建文本时立即接入 SDF 镜像；不能只处理场景里已存在的文本。

## 验证

- 确认最终可见文字不残留默认字体、Arial、`LegacyRuntime.ttf` 或空字体引用。
- 确认动态生成的按键行、存档行、成就条目和提示文字也显示为 `Cubic_11`。
- 确认描边、字号、换行和自动缩放仍沿用原 UI 布局，不因字体镜像而改变控件尺寸。
