# MVMap 物体转换接口

Editor 模块：`PlanningEditorPrototype.PlanningMVMapObjectImporter`。

`Read(json, palette, out report, prefabBySourceId = null)` 返回独立 `PlanningDocument`，不写场景/磁盘，不改变普通 `PlanningMVMapImporter.Read` 的布局语义。

- 输入先走 MVMap v1 校验，拒绝重复 ID、区域重叠、非法坐标以及尚不支持的外部门/钥匙/锁标记。
- `prefabBySourceId` 可按外部稳定 ID 指定已在道具栏目登记的 prefab 名；值为 null 表示白方块地形。指定映射优先于明确名称和试导入颜色规则。
- 当前名称规则包含重生、按钮、门、藤蔓、植物障碍、弹跳、岩浆、水；无明确名称时才使用临时颜色/形状规则。不是正式自动识别器，其他文件导入前应提供确认过的 ID 映射。
- `Partition` 无损拆分占格，不以包围盒替代几何。物体 ID 为外部 ID + 确定性矩形序号，重复转换保持稳定；门按钮用 `doorOwnerId`，禁止临时 instanceID。
- 外部物体区域全部映射到一个连通测试区域；没有真实房间边界，禁止把此区域身份视作正式离区重置分区。物体来源名称/ID/颜色保留在标签与报告，原始文件一并保存。
- 普通大物体用 isMerged 单实例；竖列藤蔓逐格输入现有 SceneBuilder 组链规则，不自行生成第二套母根。

持久结果在 Development/RoomLifecycleTests/MVMapObjects.planning.json 和 MVMapImportTest Scene 内嵌规划 JSON。Scene GUID 沿用旧测试图，布局文件则换成新源图。必须读取规划 JSON 或 Scene 内嵌数据，不能把布局导入冒充物体转换。

隔离验证入口：`Project.Rooms.Editor.MVMapObjectImportVerification.BuildAndVerify`，仅允许 FlowRetirementProbe 副本。生成 Scene、共享存档和逐区域映射报告。`RunPlayVerification` 在同一隔离项目验证开局锁色、调试解锁、预置藤蔓链、R 保留解锁和门引用。不要在用户打开的 Unity 项目运行第二个 Editor。

可选环境变量 `TAPTAP_WORKSPACE_DATA_PATH` 指定需要清理旧缓存的主项目 Assets 绝对路径；仅清理同 Scene GUID 的 Document 并更换 SavePath。原偏好先备份到主项目 Library；已绑定本地 Library 存档只有包含旧源图稳定 ID 才备份移除，不按文件名猜测或清空所有存档。不设置该变量时不操作其他项目的缓存。

窗口交互、全图可通行性、策划正式映射和最终打包仍需人工验收。
