# 标注地图与 MVMap 接入

## m_room_2 命名物体图（2026-10-16）

新增独立 `Assets/Scenes/MRoom2.unity`、配套 `MRoom2.planning.json` 与原始 `MRoom2.mvmap.json`，不替换旧试图或正式 Gaming。`MRoom2MapImport.Build` 为隔离可重现作者夹具；按明确对象名映射现有调色道具/无色道具，不能调用旧颜色猜测入口。源 room 是物体分类而非真正房间，故本图单个重置区域，原坐标保留。用户确认使用侧面梯子、默认锁色与指定重生点附近出生，不额外加钥匙。

四个平台由两基座/两曲柄以稳定 ID 绑定。最长路线补回植物障碍占用的格，路线注释与植物重叠而不新增实体；向左的平台使用 `MovingPlatformFeature.reverseRoute`，不移动作者起点或写负距离。参数覆盖 Boolean 使用 `"1"/"0"`，不是 `"True"/"False"`。详见 [地图与配置](../../Developer/MROOM2_IMPORT.md)。

## 早期标注图

当前试图是策划 `map.mvmap (3).json` 中的标注部分，不使用上一版“按颜色猜所有物体”的映射。

- 一次性作者夹具：`Development/RoomLifecycleTests/Editor/AnnotatedMapSetup.cs`。`Build()` 仅允许在 `Library/FlowRetirementProbe` 隔离副本执行，按源稳定 ID 和截图范围构造 `PlanningDocument`，经已有 `PlanningSceneBuilder.TryApplyDocument` 生成 Scene，经 `PlanningSaveStore.Save` 保存 JSON。不要在用户打开的项目启动第二个 Unity Editor，也不要把它做成持续重建服务。
- 每个真实房间使用稳定 ID，box 为世界坐标减房间最小占格坐标；世界/详情/装配统一 1:1。未划分区单独保留，不宣称新增策划房间。
- 墙/水不规则占格用既有 `Partition` 精确分解；门按钮用 `doorOwnerId`，流向/按钮模式用 `LevelEditorComponentValueOverride`，不持久化 instanceID，不绕开原增量绑定或区域重置。
- `CurrentFlowDirection` 保留 Left=0、Right=1，追加 Up=2、Down=3。定向水流仍经 `PlayerController.SetWaterVelocity`，不是直接操作玩家刚体；全屏 HSV 与原水族表现保持现有链路。
- `PlanningEditorWindow.LoadDocument` 在无本机存档路径时匹配当前 Scene 同目录 `.planning.json` 与内嵌规划数据。`PlanningSaveStore.LoadAll` 同时显示当前场景绑定的共享 JSON，仍保留原 Library 存档，不全局扫描 Assets、不覆盖未保存工作区。
- 本机换图时先备份旧 Scene/JSON/原绑定，只更新这张 Scene 的缓存。主项目可用 Library 下的一次性 `open-ready.flag` 安全打开已应用场景与编辑器；有未保存场景或处于 Play 时不切场景，不丢弃用户工作。
- 上一版 `PlanningMVMapObjectImporter.Read` 保留通用临时推测能力，但紫色按墙的旧规则不能用于当前图；旧 `MVMapObjectImportVerification` 的73区/藤蔓试图断言只适用于历史源文件，当前改用 `AnnotatedMapSetup.Build`。

使用与范围见 `Docs/Developer/MVMAP_OBJECT_IMPORT.md`，对应映射见 `MVMAP_OBJECT_MAPPING.md`。隔离验证日志放 Library，不提交测试截图、生成日志或本机缓存。
