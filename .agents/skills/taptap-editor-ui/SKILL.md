---
name: taptap-editor-ui
description: 为 2026TapTap 制作或修改策划编辑器工具，应用统一 UI Toolkit、可拖动折叠关闭与清晰工作流规范。
---

# 编辑器工具

先读根 AGENTS.md 和 Docs/Agent/EDITOR_UI_STANDARD.md，重新读 PROJECT_CONTEXT、ACTIVE_WORK、SYSTEM_INDEX，确认当前项目与远程。
在已有模块中实现或扩展工具，不自动恢复被排除的旧玩法。
Scene工具用UI Toolkit可拖动HUD或Unity Overlay，支持折叠、关闭、菜单重开和窗口范围夹紧；EditorWindow使用原生拖动关闭和Foldout管理正文。
以当前用户任务组织界面：短标题、一个主操作、输入选择、即时预览、明确保存状态；高级参数折叠。
图片使用ObjectField输入，预览和操作不互相遮挡。Undo和批量删除范围必须正确。
先验收最小可用流程，再写Docs/Developer的使用说明与Docs/Agent/Integration的接口说明，更新索引。
旧Inspector不作为新工具范例；不得用IMGUIContainer当作UI Toolkit改造的完成标准。

## 规则优先级

本 Skill 前文原有的编辑器工具、UI Toolkit、可拖动折叠关闭、工作流和验收要求优先。以下“自制组件 Inspector”只作补充；如与原文或 `Docs/Agent/EDITOR_UI_STANDARD.md` 冲突，以原文和该标准为准。

## 自制组件 Inspector（强制）

日后新建或修改项目中会出现在 Inspector 里的自制组件，必须同时提供自定义编辑器，不能以 Unity 默认 Inspector 作为完成状态。第三方 TA、Timeline、Package 组件不主动改写，除非任务明确要求。

除 Unity 自动显示的脚本名和脚本类名外，组件编辑器中的可见文字全部使用中文：

- 组件标题、Foldout/分组名、字段名、工具提示、枚举显示名、按钮、空状态、警告、只读状态和调试操作全部翻译。
- 脚本名、组件名和类名保持原样，不为翻译改名。
- 公共参数按用途分区；高级或低频参数默认折叠。
- 运行时只读信息与可编辑参数分开；只读信息不能伪装成普通输入框。
- 互斥或依赖参数使用显隐控制，相关选项默认在同一个分区。
- 重置、删除、生成等操作按风险区分颜色和确认范围，不改变原有运行时功能。

在不改变上述优先规则的前提下，优先复用 `Project.Editor.ProjectInspectorUtility` 和现有 UI Toolkit Inspector 模式，至少保证中文标题、Foldout、条件显隐、只读状态和调试区完整。新增或修改后必须验证编辑器程序集编译，并检查展开、折叠、空选择、多选择和参数显隐。
