# 暂停存档页删除当前存档

`PauseScreen/SavePage` 在“待开发内容，请关注后续消息 / 已自动存档”下方居中显示红色“删除当前存档”按钮。按钮复用 `GameUiArtwork.settingButton` 切片资源，颜色、文字色、描边色、字号、尺寸和文案间距均可在 `SavePanelController` Inspector 配置。

## 行为

- 按钮只在暂停轮盘的存档页启用，主菜单复用同一控制器时不显示。
- 无确认弹窗、无额外动画；点击后立即请求删除。
- `SavePanelController.DeleteCurrentSaveRequested` 只负责 UI 到存档层的单向请求。
- `RunSessionCoordinator` 统一执行：删除 `run.json`、`.tmp`、`.bak`，重置本轮收集品、颜色钥匙/能力、Boss、重生点和临时运行状态，把玩家恢复本轮初始位置，再通过现有场景流程返回开始界面。
- 成就、音量/设置、按键映射、手柄绑定和手机触控布局属于全局数据，不得随本次删除清除。

## 验证

`RunSaveServiceTests.DeleteCurrentSaveButtonUsesExistingArtworkAndRaisesRequest` 检查真实按钮结构、现有切片资源、Cubic 字体、红色按钮和 UI 请求。完整清档后的场景返回和玩家位置恢复仍需 Play Mode 验证。
