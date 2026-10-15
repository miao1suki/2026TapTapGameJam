# 规划地图协作接口

仅 Editor，源码在 `PlanningEditorPrototype/Editor`，沿用现有 `PlanningDocument` JSON、1:1 单元格和稳定 ID，不引入新运行时房间系统。使用说明见 [LEVEL_EDITOR_COLLABORATION.md](../../Developer/LEVEL_EDITOR_COLLABORATION.md)。

## 数据与事务

- `PlanningFragmentUtility.Extract(source, owners, area, out detached)` 返回深拷贝；owners 为 null 表示不限所有者，area 为 null 表示不限矩形。裁剪后重锚局部坐标和 mergeParts，切过完整物体拒绝，不改源数据。
- `Import(target, source, anchor, includePlayer, out detached)` 返回合并后的新文档，失败不写目标。anchor 是导入源房间占格包围盒左上格。内部房间/物体/合并原始片/门钥匙锁/区域 ID 全量重映射，门按钮、基座、曲柄绑定按新 ID 重连。跨片段绑定解除，孤立按钮移除；不能把绑定随意指向目标地图同名或旧 ID。
- `ValidatePlacement` 为轻量占格预检，UI 不在每次鼠标绘制时重复序列化完整地图。
- UI 替换 document 后调用 `OnCanvasDocumentChanged`，使用现有快照撤回/重做与同步队列。不得添加 Unity Undo 物体快照路径。
- 保留 prefab/栏目 ID、持久化参数和资产 GlobalObjectId；临时 instanceID 不作为引用。JSON 不打包缺失资源，协作者必须提交同一栏目和 prefab 依赖。

## 场景与共享文件

`PlanningSaveStore.SaveFolder` 为项目 `Assets/_Project/Content/LevelMaps`；`LegacySaveFolder` 只作兼容读取。写入先到 `.writing` 临时文件，成功后替换，不在重复保存时重写相同内容；Unity 元数据不重建。旧粗网格文件沿用首次覆盖前备份。

`PlanningSharedSaveUtility.SceneSavePath` 返回 Scene 同名 `.planning.json`；SceneBuilder 应用后与 `PlanningSceneSaveProcessor` 的 `sceneSaved` 回调自动导出（覆盖首次保存与另存场景）。编辑器优先采用发生变化的共享副本，避免本机 EditorPrefs 遮住队友更新。保存 Scene 不应拿旧生成几何覆盖已有较新的规划存档。更名存档展示名不改变 Scene 配对文件名。

`Ops/LevelMaps/Test-PlanningMapCommit.ps1` 检查暂存的规划场景是否有有效配对 JSON、meta 和漏暂存修改。新克隆启用 `.githooks`；已有 hook 组合调用，不覆盖。它检查交付完整性，不尝试自动修复布局或擅自暂存文件。

窗口读入/成功保存后调用 `ObserveSceneCopy` 记录共享文件哈希；`VerifySceneCopy` 在覆盖或生成前检查外部更新，冲突时将本地数据备份到 Library/PlanningConflicts 并拒绝覆盖。禁止绕过此检查从失效的窗口写回。未保存场景的应用文档按 scene handle 暂存，首次保存后导出，关闭场景清理暂存。

## 移动与水层

`TryMove` 保留房间物体/出生点局部位置，并移动所选所有者的全局装配补丁。跨选区的完整补丁拒绝移动；`Reanchor` 递归同步 mergeParts。数据移动失败必须保持原图。

`PlanningWaterLayerUtility.IsWater` 识别旧 Water 类型及带 `WaterVisualFeature` 的道具（含水流、气泡柱）。SceneBuilder 以水层/普通层分别占格；增量匹配必须使用 `(PlanningBoxId, Cell)`，不能只以 Cell 匹配，同格水和物品属于不同实例。不得为此改变颜色 Gameplay 的真实材质/HSV/解锁路径。

预览独立排序水→物品，选取/擦除顺序相反。“仅水层”筛选也应用于参数点击和装配擦除。

## 验证

隔离项目执行 `PlanningEditorPrototype.PlanningCollaborationVerification.Run`。入口限制项目路径含 `FlowRetirementProbe`，测试会生成独立 Scene，不在主工作区执行。覆盖深拷贝、重复导入、ID/绑定、边界与冲突拒绝、合并原始片、整房间移动、磁盘原子保存、真实 prefab 的水/物品同格生成与增量保留、分别擦除、Scene 配对保存。交互窗口人工拖拽和多人真实 Git 往返仍需验收。
