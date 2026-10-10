---
name: taptap-pause-wheel-ui
description: 2026TapTap ESC暂停轮盘UI规范，覆盖左半屏转盘、逻辑选项不进入隐藏侧、选项颜色、颜料铺洒过渡、红色X统一退出和既有设置/存档复用。适用于修改 PauseScreen、暂停菜单、转盘选择、暂停输入或子页面过渡时。
---

# 暂停轮盘 UI 入口

本 Skill 是 `PauseScreen` 和 ESC 暂停菜单开发的强制入口。

每次修改暂停菜单前，必须完整读取：

`Assets/_Project/Code/Systems/GameFlow/SKILL.md`

该文件保存完整创意、交互规则和实现边界。本文件只负责让 Codex 发现并进入完整规范。

## 适用任务

- 修改 `Assets/_Project/Scenes/Systems/Systems_UI.unity` 的 `PauseScreen`
- 修改 `EscMenuController`、暂停输入、暂停页面状态或子页面切换
- 新增轮盘选项、选项颜色、鼠标滚轮/W/S 选择
- 新增打开、确认、关闭、颜料过渡或红色 X 动画
- 将设置、存档、收藏品等面板接入暂停菜单

## 强制边界

- 设置、存档、按键映射等业务复用队友现有脚本，不重新实现。
- 不用硬编码 RGB 色值；颜色由 Inspector 配置。
- 不用硬编码空格；确认键读取 `InputActionId.Jump` 映射，并支持鼠标左键、鼠标右键确认。
- 暂停输入读取 `InputActionId.Pause` 映射。
- 子页面退出必须触发同一个红色 X 的 `Button.onClick`，ESC 不另写退出分支。
- 转盘的装饰层可以转完整一圈，但逻辑选项不得进入左侧不可见区域。
- 颜料过渡只作为占位实现时也必须避开标题文字和主要控件。
