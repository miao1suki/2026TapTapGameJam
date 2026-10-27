# 旧规划协作流程（已退役）

PlanningCanvas、本地/共享 JSON 存档槽、片段导入导出、JSON 接轨和“应用到场景”已退出正常关卡制作流程。本文件保留原路径，只为让旧链接明确跳转；不得再据历史 planning JSON 重建或覆盖当前 Scene。

当前制作入口是 `Tools/2026TapTap/关卡编辑器/场景编辑器`（快捷键 `M`）。完整操作见 [场景关卡编辑器](SCENE_LEVEL_EDITOR.md)。

当前协作与交付规则：

- Scene 是房间、通道、玩法对象、实例参数和 Unity 引用的唯一权威。
- GridArt Map 是美术格作者数据；稀疏区域归属资产只记录显式认领或新绘制的跟随内容。
- “装配/合并区域”是在同一 Scene 中把所选区域、所属逻辑组、房间重置归属、显式附属物和已认领美术整体平移，不拼接 JSON。
- 多人仍各自在自己的分支/Scene 工作；整合者基于 Scene 与资源依赖审查和合入，不采用对象锁、代理同步或共享存档。
- 交付 Scene/`.meta`、引用的 GridArt Map/区域归属资产及 `.meta`、新增 Palette/Prefab/材质/贴图/生成资源依赖；不提交 Library。
- 旧 `.planning.json` 与命名正式关卡 JSON 只作历史只读基线，不再同步、不进入提交门禁，也不能恢复已删除或已手调的对象。

提交前仍运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`；文件名为向后兼容保留，脚本现在检查 Scene 交付和 Unity `.meta` 完整性，不再要求 planning JSON。
