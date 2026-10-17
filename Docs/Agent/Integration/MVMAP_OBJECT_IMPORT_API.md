# 标注地图与 MVMap 接入

## MRoom3 新增绿色进阶关（2026-10-17）

`Assets/Scenes/MRoom3.unity` / 同名planning / 原始mvmap成套交付；只选`area_1ge22nuuqv`与用户确认误归旧区域的平台`room_34jiraqppa`，不重导旧MRoom2或正式Gaming。`MRoom3MapImport.Build`仅隔离执行，使用既有规划生成和持久化API，不能在主工程自动重跑。

4个平台/3无重力基座，均充能终点、断电作者回位；两个平台3共享基座3。远处竖平台按确认从X61移至X60、保留原墙。6门/13保持按钮按编号稳定绑定，68尖刺朝所在墙的通道侧。唯一标准玩家、默认锁色、单个测试重置区域，无新增钥匙。原图坐标/行程与编辑方式见 [MROOM3_IMPORT](../../Developer/MROOM3_IMPORT.md)；真实窗口和运行验证入口为`MRoom3SceneAudit.Run`，-noaudio隔离运行。

## m_room_2 命名物体图（2026-10-16）

2026-10-17第二版源`m-room-2.mvmap (2).json`替换同名测试图，24分类、六平台/三基座/两曲柄/三门七按钮/六重生点。按用户确认两个曲柄仍按进度联动，四个基座消费者为充能终点/断电回位，其中单格托红块与左竖平台共用基座、上移1格。两排尖刺均向上，门按数字后缀配对保持按钮；侧梯按邻墙决定左右。最长及新增竖路线补网页占格缺口，左下平台补4格水。对象ID按本版源重新生成，Scene/配对规划/原始源三件成套替换并保留meta；不得重跑第一版旧绑定修复夹具。第一版的“三重生点/四平台”等历史描述由本段覆盖。来源、坐标与编辑说明以最新Developer/MROOM2_IMPORT.md为准。

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
