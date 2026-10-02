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
