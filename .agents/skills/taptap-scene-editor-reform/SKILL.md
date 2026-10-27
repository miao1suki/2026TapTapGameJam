---
name: taptap-scene-editor-reform
description: 为 2026TapTap 设计、实现或审查“编辑器改版方案”：把关卡制作迁移到锁定正交 2D 的 SceneView，直接编辑房间、通道、真实对象、参数、引用与 GridArt；退役 PlanningCanvas、JSON 存档和 JSON 拼接协作。涉及关卡编辑器改版、场景直接绘制、区域整体移动或旧规划流程退役时使用。
metadata:
  short-description: 编辑器改版方案
---

# 编辑器改版方案

目标不是给旧规划画布增加一个 Scene 预览，而是让作者在正交 2D SceneView 中直接修改最终关卡。

最终所有权：

- 玩法布局、房间/通道、真实对象、实例参数和对象引用以 Scene 为唯一权威。
- `LevelEditorPalette.asset` 只提供可放置模板，不保存已放置关卡状态。
- GridArt 继续使用 Scene 引用的稠密 `GridArtMap` 资产承载美术格，避免数万 GameObject；作者仍只在 SceneView 绘制。
- PlanningCanvas、本地槽位、共享 JSON、片段导入导出和“应用到场景”退出正常工作流。区域装配只表示同一 Scene 内把一个或多个区域及其所属内容整体平移。

## 开始前

每次重新读取根 `AGENTS.md` 及其规定的持续文档，并应用：

- `.agents/skills/taptap-editor-ui/SKILL.md`
- `.agents/skills/taptap-git-worker/SKILL.md`
- 涉及颜色物体、钥匙或房间重置时，完整读取 `.agents/skills/taptap-color-gameplay/SKILL.md` 与其指定的系统级 Skill。

先读 [当前项目证据](references/current-project-evidence.md)，不得用旧聊天结论替代源码核对。设计或改接口时读 [目标架构](references/target-architecture.md)；实施时必须按 [实施阶段](references/implementation-stages.md)；验收时完整执行 [验证矩阵](references/verification-matrix.md)。

## 不可违反的边界

1. 打开、关闭、聚焦、切换模式、进入或退出编辑状态必须是零写入：不改 Scene、Prefab、Map、Palette、生成资产，不增加 Undo，不触发迁移或重建。
2. 只有用户显式的绘制、擦除、移动、改参、绑定、归属、拆分或保存动作可以写入；每个动作形成一个完整 Unity Undo 组。
3. 不从 JSON 重建现有正式 Scene，不在工具启动时补组件、改格子尺寸、改颜色、缩放物体、重绑房间或认领美术。
4. 所有新对象直接实例化真实 Prefab；实例参数直接保存在 Scene 的 Prefab override/组件字段中，不再复制到规划覆盖 JSON。
5. 机关关系使用真实 Unity 引用和现有类型安全 API，不用字符串 ID 重新搭一套关系图。稳定作者 ID 只负责身份、逻辑组和拾取进度。
6. 房间与通道都直接编辑 `RoomLifecycleManager` 的 `RoomRegion`；通道由 `corridor=true` 区分，连接关系由四邻接实时推导，不保存 from/to 接轨数据。
7. 擦区域不隐式删物体，擦物体不隐式改区域；重叠格按对象通道区分，禁止使用“找到这个格的任意一个对象”作为删除依据。
8. 区域整体移动必须同时处理区域格、所属对象、`LevelEditorPlacedBlock.Cell`、房间重置归属和显式归属的美术；保留作者稳定 ID、拾取身份和对象引用。
9. 旧 GridArt 默认保持原位且无归属。只允许作者通过可预览、可撤销的“认领到当前区域”操作建立归属；背景、天气和全局层默认不跟房间移动。
10. 不允许在正式 `Gaming.unity` 上试错。先在隔离工程或一次性测试 Scene 通过当前阶段门槛，再显式启用正式场景。

## 工作方法

1. 先确认当前请求是研究、实现、审查还是退役。只要求研究时不得写源码、场景或资产；创建/维护本 Skill 和文档不等于授权实现。
2. 为涉及的阶段建立“现有源码事实 → 目标行为 → 写入对象 → Undo 范围 → 失败回滚 → 验证”的对应表。没有源码证据的假设先验证，不直接落到正式场景。
3. 先消除自动写入，再加新交互；先建立只读索引，再允许生成或删除；先支持旧 Scene 原样读取，再考虑显式迁移。
4. 新 UI 使用一个 UI Toolkit SceneView Overlay/HUD，中文短标题，可拖动、折叠、关闭、菜单重开，并把区域、对象、绑定/参数、美术作为互斥模式。
5. 所有修改通过集中服务提交；Scene GUI 只负责命中、预览与手势，不直接散落调用 `DestroyImmediate`、`SetDirty` 或 `MarkSceneDirty`。
6. 每个阶段完成后运行对应验证并记录真实结果。未通过零写入、Undo 或正式数据保护门槛时，不进入下一阶段。

## 完成标准

- 策划不打开 PlanningCanvas，不选择 JSON，不点击“应用到场景”，即可在 SceneView 完成房间、通道、对象、参数、连接和美术制作。
- 原 Palette 方块、道具、Prefab 和模板默认参数必须在卡片式选择器中可搜索、可定位、可直接绘制；Palette 只在生成瞬间提供 Prefab 与默认参数，实例落入 Scene 后完全独立，不保存来源栏目、不认领、不被模板回写。
- 关闭并重开 Unity 后，Scene 与其引用的美术资产能恢复全部作者状态；运行时不读取规划 JSON。
- 区域整体移动一次撤销即可完整还原，稳定拾取身份、机关引用、房间重置和美术归属不变。
- 正式场景在未执行显式作者动作时哈希不变；旧场景无需批量重建或批量添加组件即可进入新工具。
- 旧 JSON 工作流退役时，同一次变更更新根约定、提交守卫、开发者说明、接口文档和系统索引，不能让旧规则继续要求三份规划同步。
