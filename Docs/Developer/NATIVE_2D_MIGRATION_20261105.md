# 原生 2D 迁移实施与验收

日期：2026-11-05。Unity：6000.3.12f1。分支：`codex/2d-long-term-migration`。迁移前正式基线：`9a68c64`。

## 结果与交付边界

本分支已实施原生 2D 转换：玩法由 Box2D 驱动，场景与 Prefab 使用 Rigidbody2D/Collider2D，PC 和手机使用 URP Renderer2D，相机只使用正交 XY 构图。没有保留以 3D 物理执行玩法的后备通路。main 未合并，正式发布版本及真实用户存档、成就没有被修改。

这是对现有项目的整体实现迁移，不是新建空的 2D 模板再复制地图。房间、机关、作者身份、美术资产和 Unity 引用继续沿用现有资源；禁止从历史 planning JSON 重建场景。迁移前审查见 [2D_MIGRATION_REVIEW_20261104.md](2D_MIGRATION_REVIEW_20261104.md)，其中暂留 3D 物理的阶段建议已被用户选择的原生 2D 目标取代。

## 1. 物理与坐标

- Rigidbody → Rigidbody2D；Box/Sphere/Capsule Collider 分别转换为 Box/Circle/CapsuleCollider2D。尖刺的 MeshCollider 转为具有相同 XY 三角形的 PolygonCollider2D。
- 原碰撞组件 fileID、引用及作者对象身份保留，重力、Kinematic 和可冻结的平面轴转换为对应 2D 属性。没有通过全部重新实例化破坏机关引用。
- 查询、Trigger/Collision 回调、接触点、搬运避碰、平台运输、水体/岩浆/梯子、颜色接触生长、房间重置、Boss 和拾取链路均使用原生 2D 类型。
- XY 是唯一玩法平面。删除了用 Z 接近程度决定是否碰撞、攀爬或触发的旧判断；表现层 Z、SortingLayer、sortingOrder 仍可用于绘制顺序。
- `PlanarPhysics` 位于 `Assets/_Project/Code/Systems/PlanarPhysics/Runtime`，提供批量查询和现有游戏坐标的衔接；内部只调用 Physics2D，没有 PhysX 查询或 3D 模拟。
- 假想姿态查询使用独立 Physics2D 场景及复用形状代理，避免为了试探搬运位置而临时移动真实作者物体；退出 Play、程序集重载时清理预览场景。
- 3D 物理自动模拟关闭，实际 Unity 属性检查为 `simulationMode=Script`；2D 物理仍按 FixedUpdate 工作。

原生查询有两个需要长期遵守的差异：显式 Trigger 查询必须与 Unity 6 原生过滤行为一致；从玩家内部发出的 Raycast 可能先命中玩家自己。本次分别通过同步查询的作用域过滤，以及落地/重生射线取最近非自身命中修复，不能恢复为只读第一个命中。

## 2. 玩家和移动接触的适配

标准玩家物理宽度为 **0.96 格、高度 1 格**，逻辑占格仍为 1 格。Box2D 有接触皮肤，完全 1 格宽的胶囊在 1 格竖井会被侧墙夹住，因此给物理宽度留下少量余量；没有缩小玩家美术。

玩家精灵脚底以 CapsuleCollider2D 实际 bounds 为基准，避免接触皮肤造成的微小悬空。接触块落稳判断允许 Box2D 的小范围负接触距离，同时仍拒绝真实穿墙。刚体二维位置回写保留 Transform 表现 Z，防止把 Vector2 隐式转 Vector3 时清零深度。

搬运、接触生长、平台搭载、重生地面选择和收集品保存分别运行专项验收；没有以编译通过代替接触验证。平台运输专项包括真实 Play Mode 和自然 FixedUpdate，不通过直接调用回调制造通过结果。

## 3. 相机与 Timeline

- 删除 Perspective、立体 Orbit、FOV、绕 X/Y 轴倾斜等实际镜头功能，只保留 Side2D + Orthographic。
- 相机支持 XY 平移、正交半高缩放、绕屏幕 Z 轴旋转；CameraControlManager 仍是实际镜头唯一写入者，玩法与演出仍通过租约交接。
- `CameraState` / `CameraViewportInfo` 移除 FOV；`ICameraLensOverrideSource.TryGetLensOverride(out float orthographicSize)`、`TimelineCamRig.SetShotLens(float orthographicSize)` 仅传正交半高。
- 工具目录从 `Assets/GJ_Tools/TimelineTools3D` 移为 `TimelineTools2D`，原 `.meta` GUID 保留。相机 Inspector、场景镜头框、角色轨运动与编辑控件改为 XY；UI Toolkit Inspector 使用中文标签。
- `OrbitCameraControl` 等个别类名为保持脚本引用而保留，但实现已是平面平移/缩放，不再执行立体环绕。序列化 `yaw` 的兼容字段表示画面 Z 旋转，不再表示绕世界 Y 轴转动。

原演出资源的坐标和引用保留，不能只把新工具脚本单独拷贝进旧 3D 工程。未来新 Clip 不应加入 FOV 或立体相机模式。

## 4. 渲染、光照与现有效果

PC_Renderer、Mobile_Renderer 在原 GUID 上改为 Renderer2DData。自有平面 Shader 的通道适配 Universal2D；水体底图采集、折射、HSV、RGB 故障和可见区域效果显式申请颜色输入，避免 Renderer2D 直接输出时读到空的中间颜色纹理。

删除旧 SSAO 与依赖 3D Renderer 的冗余 Stencil Feature；当前颜色/可见区域系统保留自身平面遮罩。原 Directional Light 改为 Global Light2D，萤火虫池中的动态光改为 Point Light2D；移除旧 UniversalAdditionalLightData，避免它重新补建 3D Light。

已有美术大多采用 Unlit 自定义材质，本次保留原风格。转换为 Renderer2D 不会自动把每份 Unlit 材质变为受光 Sprite-Lit 材质；后续若要整体重做受光美术，需要单独设计材质，不应直接替换 TA 全部材质。

### 星空

原星空依赖 3D Skybox，直接换渲染器会使背景消失。本次以原材质/GUID/参数实现 `StarPanorama2D.shader`，由 `StarPanoramaTimeDriver` 为正交相机适配临时平面背景。星云、银河、闪烁和原配色保留；正式场景 GPU 图确认有效，且渲染预览不会把作者 Scene 标脏。

### 水体及几何

旧水面平面反射相机和 `SimplePlanarReflection` 已退役，保留现有水体底图/折射效果。旧 InteractiveWater 的采样平面由 XZ 改为 XY，重复碰撞体移除。

普通墙和基础形状使用平面 Mesh，尖刺使用平面三角形；合并白块不再生成立体侧面和背面。SurfaceTiles 只提供 Front，取消六面编辑。283 组梯子占位层级中已停用的 2264 个 MeshRenderer 与对应 MeshFilter 删除，侧梯占位父节点的 Y 轴立体旋转清零；真实梯子 Sprite 和 Collider2D 不受影响。空 Transform 的作者 fileID 暂留，避免无意义的引用/层级身份重写。

保存资源中的旧内建 Sphere/Capsule Mesh 也分别替换为 `Resources/Planar/UnitDisk` / `UnitCapsule`；运行时工厂复用同一套平面资源。5 份已无引用的旧玩家/尖刺立体 Mesh 归档后移除，水面预览网格由 XZ 转为 XY。资源审计额外检查项目 Mesh 的厚度，避免只改碰撞体而保留立体占位几何。

## 5. 资源与依赖清理

静态扫描当前追踪资源得到：

| 项目 | 数量/结果 |
|---|---:|
| 原生 Collider2D 组件 | 15,512 |
| Rigidbody2D | 149 |
| 保存的 Camera | 29，全部正交 |
| Mesh 资产厚度检查 | 608 份，均为平面 |
| 已检查的原 Scene Transform XY | 17,750，未改变 |
| 旧 3D 刚体/碰撞体/关节/Light/Terrain/NavMeshAgent 组件 | 0 |
| 正式 GridArt 作者资产与历史 JSON | 原样保留 |

这些是整个追踪资源集的统计，包含独立工作场景，不等同于 Gaming 单场景运行时对象数。

移除未使用的 AI Navigation、AI、Cloth、TerrainPhysics、Vehicles、Wind、Umbra、VR、XR 共 9 项直接包依赖；没有升级包版本。移除没有项目外部 GUID 引用的 TMP Examples & Extras，保留项目使用的 TMP 字体及 Essential Resources。

Unity 的 `com.unity.modules.physics` 和 `terrain` 仍由 URP 17.3 依赖，不能为删除名称而破坏包解析。保留这些引擎模块不代表玩法使用 3D；自动模拟已关闭，正式 Scene 和代码没有 3D 物理执行路径。Transform/Vector3、平面 Mesh、粒子、着色噪声的三分量计算也不属于需要删除的立体玩法。

## 6. 地图、美术与持久化的保护

转换前资源快照和一次性工具保存在本机忽略目录 `Library/PlanarMigration`；不提交这些备份。转换不自动执行于打开场景、启动游戏或构建阶段。

验证脚本 `Ops/Migration/audit_native_2d.py --compare-ref 9a68c64` 检查资源类型、所有保存相机、Scene Transform XY、GridArt 作者文件和历史 JSON。正式 Scene 不从 planning 重建；作者稳定 ID、房间配置和机关脚本引用不重置。原 MonoBehaviour 数据仅为新背景增加材质接线，并去掉已退役的附加 3D Light 组件；物理组件按原 fileID 转换。

本地未跟踪的 DOTweenPro 与旧卡顿报告没有混入交付。TapTap 配置、包名、正式 ICON、版本号、联网边界和正式进度文件不因迁移被改写。隔离验证使用独立 company/product 身份，不删除真实存档或成就。

## 7. 实际验收

所有 Unity 验收在独立工程执行。正式作者资源只读打开，未保存测试位置或重新生成正式美术。

| 验收 | 结果 |
|---|---|
| 全量 EditMode | **310/310 通过**，GPU 模式最终复跑 |
| 新原生 2D 查询/深度/假想姿态契约 | 5 项通过 |
| 玩家基础移动与手感 | 61 项通过 |
| 移动平台运输 | 30,136 项断言通过 |
| 接触块重力与旋转 | 167 项通过 |
| 收集品接触 | 51 项通过 |
| 收集品持久化 | 57 项通过 |
| 收集品与 Boss 进度 | 58 项通过 |
| Boss 房间重置 | 57 项通过 |
| 岩浆遮挡与接触 | 92 项通过 |
| 连通空间/门/过渡 GPU 验收 | 58 项通过，覆盖 PC/Mobile Renderer2D |
| 7 份真实玩法 Scene | 无 Missing Script、无 3D 物理/Light、无透视相机 |
| 星空预览 | 渲染后作者 Scene 保持 clean |
| 绿绳相机/指针/挂接释放 GPU 专项 | 1274 项通过，含连续运镜与画面 Z 旋转 |
| 玩家与岩浆绘制前后顺序 GPU 专项 | 10 项通过 |
| Gaming GPU 帧 | 1600×900，非空画面，缺 Shader 洋红像素 0 |
| Windows 非 Development 验证构建 | 成功，0 错误；最终增量构建 3 条警告，此前完整构建 92 条 |
| Windows 验证包启动 | 实际启动并观察 14 秒，无运行时异常日志；非完整通关 |

7 份 Scene 为 Gaming、Boss、R1、R2、R3、MRoom2、MRoom3；Gaming 检查到 3213 个 Collider2D、109 个 Rigidbody2D、1 个 Light2D。统计包含不活动对象。

最终 EditMode 曾以 `-nographics` 执行，3 个 UI Toolkit 视图测试因没有图形设备失败；使用实际 GPU 后 310 项全部通过。这是测试环境限制，没有通过吞掉 Error 日志或放宽断言让它通过。

本机验证产物位于忽略目录：`Library/PlanarFinalGpuEditMode.xml`、`Library/PlanarFinalFormal.log`、各 `Library/Planar*.log`；GPU 图与 Windows 验证包在隔离工程的 `Library/PlanarMigration`、`Builds/Native2DSmoke`。不能把这些构建日志或缓存当作作者资产提交。

## 8. 尚未声称完成的验证

本次没有制作新的正式 PC/Android 发布包，也没有完整通关或 Android 真机验收。Windows 构建仍有既有 Shader/兼容 API 警告，最终增量构建为 3 条，此前含完整 Shader 编译时为 92 条，计数受编译缓存影响；0 错误不等于零警告。PC/Mobile Renderer2D GPU 专项均在本机 D3D11 环境测试，不能替代 Android GLES3 实机性能证明。

Box2D 与原 PhysX 的接触皮肤、摩擦、堆叠和连续碰撞手感不会完全相同。本次已处理已测出的差异，但合入主线前仍应针对长距离平台、窄通道、复杂堆叠、存档恢复、Boss 全流程及不同手机运行一轮人工回归。不要把“专项全部通过”表述为“所有关卡已完整通关”。

## 9. 后续制作规则

1. 在本分支打开 Unity 6000.3.12f1；让 Unity 导入迁移后的组件和 Renderer2D，不能使用旧内存 Scene 覆盖磁盘资源。
2. 新碰撞体使用 Box/Circle/Capsule/PolygonCollider2D，新动力体使用 Rigidbody2D；回调使用 `OnTriggerEnter2D` / `OnCollisionEnter2D` 等。
3. 查询可用 Physics2D，或复用 PlanarPhysics 的无分配查询与形状代理。不要把 Z 深度当物理分区，不创建 Primitive Cube/Sphere。
4. 相机保持正交，只平移 XY、缩放半高及旋转 Z。新增 Timeline 参数使用二维构图，遵守相机唯一写入者约定。
5. 美术继续现有 GridArt、SpriteRenderer 与平面效果；无需为了“2D”强行把已有作者资产重刷为 Tilemap。
6. 场景、Prefab、代码和必要 `.meta` 成套交付。各专项使用独立分支，经验证后由整合窗口合入；本分支没有自动合并 main。
