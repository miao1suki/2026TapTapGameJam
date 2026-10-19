# Boss 门槛与正式 Start 的局部作者接线

入口 `Project.BossLogic.Editor.BossStartAuthoringPatch.Run` 仅允许在静音、非 Play 的 `Library/FlowRetirementProbe` 执行；主工程拒绝运行。不要在关卡编辑器持有未保存文档时运行。工具不调用全图重建，不移动、删改现有几何。

- Gaming 在实际标准玩家的出生位置生成独立、无碰撞、无 Renderer 的 Start 目标。规划记录锚在所属格，Prefab 的 Destination 子节点保存精确偏移，因此不把玩家的小数坐标吸到格中心，也不把传送组件挂到玩家上禁用玩家碰撞。
- 原 Boss 入场 `box_aa7a24dc` 保持稳定 ID、位置、占格及全部 Timeline、模式、碰撞尺寸，只换成独立的 Boss 专用触发器模板，并保存新增 Gate 的 `minimumRatio=.3`、`returnDestinationId=Start`、`returnPoint=null`、`showHint=true`。
- 仅迁移该入场记录原本无效的 `targetCollider=reference:local` 为 `reference:null`。真实 `ContactTimelineTrigger.ManagedCollider` 会解析为自身 BoxCollider；工具验证此行为。不把场景对象引用写成跨存档引用。
- 现有 PlanningBox 没有“额外组件列表”；组件布尔/数值覆盖也不会自动创建缺失组件。因此使用新模板 Prefab 与独立道具栏目持久化 Gate，不修改共用普通 Timeline 触发器 Prefab，不给其他触发器增加门槛。

最终关卡与 Boss 片段都导入同一 Gaming。Start 目标只存在于正式 Gaming；独立 Boss 测试场景没有同场景 Start 时仅拒绝入场/提示，不自动跨场景加载。无需额外的 Boss 假起点。

报告 `Library/BossStartAuthoringPatch.txt`：验证原物体数量、全部原 Transform/父级/激活状态/PlanningBoxId不变；除明确 Gate 迁移及新 Start 外原规划记录不变；Gaming 同名规划、正式命名存档一致。另调用真实单物体 SceneBuilder 检查 Gate/Start 重建，并对两条记录执行 `ValidateDependencies` 与真实存档导入，确认模板、Timeline 资源、门槛和 Start ID 不丢失。该检查不等于整个 Boss 地图已导入或剧情通关。

通过后由整合者核对增量并成套交付：Gaming Scene、Gaming.planning.json、正式关卡.json、Boss Scene、Boss.planning.json、共享 LevelEditorPalette、`Assets/Boss_Logic/Authoring` 两个新 Prefab，以及各自 meta。不能只复制 Scene 或只提交模板。
