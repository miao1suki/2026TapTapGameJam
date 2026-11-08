# 2D 平台跳跃关卡编辑器：成熟工作流与现成工具选型

审查日期：2026-11-08。项目基线：`codex/2d-vine-ending-fixes`，`69a8a4b`，Unity `6000.3.12f1`。

本轮查阅开发者官方文档、导入器维护者文档，并只读核对本项目源码。没有安装插件、启动 Unity、修改代码／场景／美术或执行迁移。以下“适合本项目”“成本较低”属于架构判断，尚未经过同房间制作与性能实测。

## 1. 结论与对上一份报告的补充

**应先做现成工具选型，再决定下一轮编辑器开发。** 当前问题同时涉及制作体验、地形数据和维护成本。继续添加自研面板功能，不能单独解决这些问题。

上一份 [参考图分析](SILENCE_GARDEN_TERRAIN_WORKFLOW_REVIEW.md) 说明了连续轮廓、自动边角和装饰分层，也提出了保留当前基础的改进办法。它没有完成替代工具选型；其中“优先补现有系统”的建议，应当加入本报告的替代方案比较后再决定。已经写过四分块和分块渲染，并不能证明继续维护它们最划算。

本次建议：

1. **把 LDtk 作为制作体验的主要标杆。** 它的地形数据、自动美术、实体、世界布局最贴近“画地形，再在同一界面摆物件”的目标。
2. **把 Unity 原生 Tilemap + 2D Tilemap Extras 作为优先验证的工程方案。** 它能沿用当前 Scene、真实 Prefab 和 Unity 引用，适合检验能否用较少适配替换通用自研能力。
3. **Tiled + SuperTiled2Unity 为第三个实际候选。** 地形、自动铺图、对象和世界组织都值得对照；重点验证配置复杂度与导入后的项目接线。
4. **Ferr2D 用来理解轮廓式地形方案。** 当前素材与玩法偏固定格，不优先以曲线路径全面替代。
5. 用一个独立小房间进行相同任务比较，再定工具与迁移范围；这轮不改变正式 Gaming 的作者权威。

## 2. 成熟工作流通常是什么样

平台跳跃游戏有瓦片地形、路径／多边形地形、手工模块拼装及混合方案，没有所有成熟项目统一使用的编辑器。对于我们这种固定像素格、房间探索和机关较多的项目，最值得参考的是以下创作闭环。

### 2.1 同一视口完成创作，内容有明确类型

设计师持续留在地图或 Scene 视口：选择地形主题，画／填／挖出空间，规则更新边角；再从素材库选择草、石头、门、钥匙、敌人或触发区域放置，点中实例直接改参数，然后运行测试。

创作界面统一，数据仍按用途组织。例如 Tiled 在一个地图中提供 Tile、Object、Image 与 Group 图层；对象支持自由位置、尺寸、旋转与独立属性。LDtk 同时组织层、实体与多个关卡。它们直接支持“同一地图中制作多类内容”的工作方式。[Tiled 图层文档](https://doc.mapeditor.org/en/stable/manual/layers/)、[LDtk 产品与界面说明](https://ldtk.io/)

| 作者选择的内容 | 合理的作者表达 | 通常应得到的结果 |
|---|---|---|
| 普通实心地形 | 地形格及类型，或闭合轮廓 | 对应外观与静态碰撞；编辑形状后保持一致 |
| 单向平台／斜坡 | 明确的特殊地形类型 | 正确通行方向／表面，不与普通实心墙混淆 |
| 草、石、壁画、背景 | 装饰 Tile／Sprite／必要时 Prefab | 默认无玩法碰撞，可调整层序和局部变体 |
| 门、按钮、钥匙、重生点 | 带属性和身份的实体／Prefab | 可编辑参数、引用、房间归属与运行行为 |
| 房间、通道、相机区 | 区域或专用实体 | 导航、镜头与加载／重置语义 |

这是建议的项目分类，不是要求所有工具采用相同底层类型。一个纯装饰石块与一个可搬运石块可以外观相近，但放置行为应明确区分。

### 2.2 地形编辑直接带来正确外观

LDtk 的 Auto-layer 可以读取 IntGrid，并根据规则输出瓦片；同一个来源还可驱动前景与背景自动层。Tiled Terrain Brush 根据边／角连接选择瓦片，必要时更新邻居；Automapping 可进一步按输入规则生成输出。[LDtk Auto layers](https://ldtk.io/docs/general/auto-layers/)、[Tiled Terrains](https://doc.mapeditor.org/en/stable/manual/terrain/)、[Tiled Automapping](https://doc.mapeditor.org/en/stable/manual/automapping/)

项目应追求这样的动作：选择“石墙”后画出形状，即可得到相应可站立表面和美术。草、苔、墙芯或边缘由配置好的规则产生；作者仍能覆盖局部或在独立层手修。需要先制作规则与合适素材，自动铺图不会自动完成美术设计。

“填满后挖房间”与“从空白添加平台”共用一套地形表达即可。洞穴适合前者，室外浮岛适合后者；不必把整张世界预先做成实体。

### 2.3 世界视图与房间视图连贯

LDtk 提供自由、线性和 GridVania 世界布局；Tiled World 可在一个视图展示多个地图并切换编辑。世界布局不要求所有内容存为一个巨大文件。[LDtk World](https://ldtk.io/docs/general/world/)、[Tiled Worlds](https://doc.mapeditor.org/en/stable/manual/worlds/)

对本项目，世界装配图应能定位房间，房间内同屏显示地形、装饰与机关。当前区域是精确不规则格集合，不能直接假设它们等同于外部工具中的矩形 Level／Map。

## 3. 四组现成资源的实际价值

### 3.1 Unity Tilemap + Tile Palette + 2D Tilemap Extras

**已确认能力：** Rule Tile 按邻接规则选择输出，支持固定、随机和动画等配置；GameObject Brush 能在 Scene 放置和操作 GameObject，官方将它作为自定义对象画笔的参考。[Rule Tile](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@4.0/manual/RuleTile.html)、[GameObject Brush](https://docs.unity3d.com/Packages/com.unity.2d.tilemap.extras@4.0/manual/GameObjectBrush.html)

TilemapCollider2D 可按瓦片生成碰撞，并通过 CompositeCollider2D 合并形状。这提供了现成的普通地形碰撞基础。[Unity 6.3 瓦片碰撞文档](https://docs.unity.com/en-us/engine/6000.3/manual/unity2d/tilemaps/tilemap-collider-2d/2d)

**对本项目的价值：** 地形绘制、瓦片资源和碰撞可以评估交给 Unity；门、钥匙等继续使用当前 Prefab 和 Inspector。项目只保留必要的放置适配与语义工具，避免再维护整套通用瓦片编辑器。

**仍需实现或验证：** 按项目主题组织统一素材库、放置时建立房间与作者身份、现有颜色／遮蔽材质接入、真实地形查询与搬运、旧地图转换。默认 GameObject Brush 不会自动理解我们的机关关系和存档身份。大面积普通墙也不应改成逐格 GameObject Brush。

**当前状态：** `Packages/manifest.json` 有引擎 Tilemap 模块，但没有声明 `com.unity.2d.tilemap.extras` 或 `com.unity.2d.tilemap` 编辑器包。不能把“引擎模块存在”当成工具链已完整接入。引用 Extras 4.0 文档用于核对功能，不是本轮锁定安装版本；包版本需在固定 Unity 版本中验证，不自动升级编辑器。

### 3.2 LDtk + LDtkToUnity

**已确认能力：** LDtk 面向平台与俯视角关卡，具有可定制实体与字段、自动地形美术和世界布局。它是《Dead Cells》导演制作的工具；这不代表《Dead Cells》当年使用了 LDtk。[LDtk 官方说明](https://ldtk.io/)

维护者提供的 LDtkToUnity 支持 ScriptedImporter、自动重新导入、Tile／Auto 层、IntGrid 碰撞和实体 Prefab。IntGrid Tile 可配置碰撞类型、物理层、材质或可选 GameObject。[导入器作者说明](https://cammin.itch.io/ldtk-to-unity)、[IntGrid Tile](https://cammin.github.io/LDtkToUnity/documentation/Topics/topic_IntGridTile.html)

**对本项目的价值：** 策划和美术可在专门的轻量地图工具中完成地形、手工装饰和实体布局；通用画图、规则编辑和世界导航不用继续由我们维护。

**主要成本：** 需要明确外部文件与 Unity 生成结果的所有权。若选外部工具作为新关卡的作者来源，应建立确定性导入、实体到真实 Prefab 的映射、两阶段引用绑定，以及房间语义适配。不能一边修改导入输出，一边期待重导入永远保留这些修改。

LDtk 有实例 IID，但它与项目现有收集品 ID 不会自动相等；迁移需要映射并保留旧身份。新物体必须有独立身份，不能由可变名称或位置生成存档键。[LDtk 唯一标识说明](https://ldtk.io/docs/game-dev/json-overview/unique-identifiers/)

LDtk 可将关卡分别存为 `.ldtkl`，便于评估按房间协作；全局定义与共享规则仍需要协作管理。[独立关卡文件](https://ldtk.io/docs/game-dev/json-overview/optional-separate-levels/)

**待实测：** Unity `6000.3.12f1`、URP Renderer2D、现有着色器及移动端实际表现。本轮没有安装导入器，不能宣称这些组合已经兼容。导入器维护者也记录了大量实体连线与高成本逐格 Prefab 造成 Scene 卡顿的情形，采用现成工具仍须控制数据规模与显示开销。[导入器排障说明](https://cammin.github.io/LDtkToUnity/documentation/Topics/topic_Troubleshooting.html)

### 3.3 Tiled + SuperTiled2Unity

**已确认能力：** Terrain Sets 自动连接边角，Automapping 按输入／输出规则处理重复铺图；Object 层承载自由摆放对象与自定义属性，World 管理多个地图。详见前述 Tiled 官方文档。

SuperTiled2Unity 支持通过属性控制物理层、触发器、显示层序，以及是否导入某层碰撞或外观；可通过 Prefab Replacement 或 Custom Importer 将地图对象接成项目 Prefab。[导入属性](https://supertiled2unity.readthedocs.io/en/latest/manual/custom-properties-importing.html)、[导入扩展](https://supertiled2unity.readthedocs.io/en/latest/manual/extending-the-importer.html)

**对本项目的价值：** 工具能力完整，适合需要灵活地形连接、多图层装饰与多地图制作的团队；值得用来比较地形规则与素材组织体验。

**主要成本：** Terrain／Automapping 规则和属性配置需要学习；我们的真实机关引用、重置与颜色逻辑仍需导入适配。跨房间引用、对象 ID 的项目级唯一性和重导入稳定性不能省略。当前 Unity 版本的实际兼容与性能仍未验证。

### 3.4 Ferr2D

**已确认能力：** 以路径控制点编辑地形，按边方向应用纹理并生成碰撞；可用于内外轮廓，提供边覆盖和碰撞配置。[工具作者说明](https://ferrlib.com/tool/ferr2d)、[快速入门](https://ferrlib.com/tool/ferr2d/quickstart)

**对本项目的价值：** 可参考“编辑轮廓就得到边缘与碰撞”的制作方式，尤其适合未来非格状地形。

**适用判断：** 目前素材、机关位置与玩家尺度都围绕固定像素格，路径地形会引入新的像素对齐、窄墙与碰撞验收工作。优先级低于前三者；也不把它视为房间、机关和物件编辑器的完整替代。作者提供 2D／3D 碰撞选择，本项目候选配置仅限原生 2D；Unity 6.3／Renderer2D 兼容未在本轮验证。

## 4. 当前编辑器究竟缺在哪里

本节来自本仓库当前源码，并非根据截图猜测。

| 当前事实 | 对制作体验的影响 | 选型时如何处理 |
|---|---|---|
| `SceneLevelEditorOverlay` 已共用 Scene，但对象与美术是独立模式与选择器 | 换同类内容仍要理解两套入口；“同一视口”尚未形成连贯的内容工作流 | 比较统一素材库、自动切换正确画笔／目标层的体验 |
| `SceneArtAuthoring.Paint` 修改 Map 图层与美术归属；普通美术笔画没有同步生成玩法碰撞 | 墙外观与实体要分别维护，容易出现漏贴或空墙 | 新普通地形编辑应统一派生外观与碰撞 |
| `GridArtBuilder` 已有四分块邻接和 32×32 渲染块 | 有可保留的旧资源基础，也有持续维护的通用算法 | 评估转换素材／规则成本；不要仅因已有实现便拒绝替代 |
| `SceneObjectAuthoring` 已保护作者组和反向 Unity 引用，提供门／按钮／曲柄／基座绑定 | 这是项目专用价值 | 无论换何种地形工具都需保留或适配 |
| `RoomLifecycleManager` 注册真实玩法组件及房间重置成员 | 外部放入一个图标不会自动完成运行接线 | 导入／放置适配必须建立项目语义 |
| `PlanarPoseQuery.Copy` 只实现 Box／Circle／Capsule／Polygon | 假想姿态查询收到 Tilemap／Composite 会走不支持分支 | Tilemap 试验必须覆盖此调用路径；当前姿态原生 Distance 与假想姿态路径要分别验证 |
| GridArt 有颜色 ID、法线与内容裁切处理 | 替换成普通 TilemapRenderer 后不保证得到相同表现 | 验证颜色获取、遮蔽、法线、像素换算与层序 |

`PlanarPhysics.ComputePenetration` 只有当前真实姿态走原生 `a.Distance(b)`，假想姿态会复制双方形状到隔离查询场景。因此，“Unity 的 Tilemap 能碰撞”与“我们所有查询已支持 Tilemap”应分开验收。这里确认的是适配缺口，不是已经在正式图复现了 Tilemap 错误；正式图本轮未迁移。

本地证据：

- [当前工具用法](SCENE_LEVEL_EDITOR.md)
- [SceneLevelEditorOverlay.cs](../../Assets/_Project/Development/LevelEditor/Orpheus0829/Editor/SceneLevelEditorOverlay.cs)
- [SceneArtAuthoring.cs](../../Assets/_Project/Development/LevelEditor/Orpheus0829/Editor/SceneArtAuthoring.cs)
- [SceneObjectAuthoring.cs](../../Assets/_Project/Development/LevelEditor/Orpheus0829/Editor/SceneObjectAuthoring.cs)
- [GridArtBuilder.cs](../../Assets/_Project/Code/Systems/GridArt/Editor/GridArtBuilder.cs)
- [RoomLifecycleManager.cs](../../Assets/_Project/Code/Systems/Rooms/Runtime/RoomLifecycleManager.cs)
- [PlanarPoseQuery.cs](../../Assets/_Project/Code/Systems/PlanarPhysics/Runtime/PlanarPoseQuery.cs)
- [PlanarPhysics.cs](../../Assets/_Project/Code/Systems/PlanarPhysics/Runtime/PlanarPhysics.cs)

## 5. 哪些通用能力应优先采用现成实现

| 能力 | 本次建议 |
|---|---|
| 画笔、矩形填充、选区、吸管、复制、基础瓦片绘制 | 优先使用成熟工具，避免继续另做一套 |
| 通用邻接接边与规则编辑 | 先验证 Rule Tile／LDtk／Tiled 是否满足素材需求；不足部分再定制 |
| 普通静态地形碰撞与合并 | 优先验证 TilemapCollider2D／CompositeCollider2D 或工具输出，不逐格维护完整玩法组件 |
| 普通装饰批量绘制 | 尽量用稠密瓦片／分块渲染；有动画或行为再用对象 |
| 房间归属、机关接线、颜色物体、收集与存档身份 | 继续由项目维护，做成清晰适配层 |
| 特殊光照／遮蔽／水体和演出 | 运行系统继续独立；通过编辑器属性或专用对象接入 |

水、藤蔓、可搬运块、运动平台、颜色功能块与带进度身份的物品具有不同运行行为，不能为了减少对象一概压成普通静态地形。相反，普通墙芯也无需承担每个玩法物体的生命周期。

小物件可以共用素材入口，并通过条目类型决定生成 Tile、Sprite 装饰或 Prefab；这是界面统一方案。目标层、是否碰撞及吸附尺度应在放置预览中明确，避免同一个“石头”让作者猜它是否可搬运。

## 6. 三条路线的取舍

以下为基于现有工程的定性评估，不是安装后的实测排名。

| 路线 | 优势 | 主要代价 | 当前定位 |
|---|---|---|---|
| Unity 原生地形 + 项目专用语义工具 | 直接保留 Scene／Prefab／Inspector，运行效果随时预览 | 素材规则转换、Tilemap 查询适配、统一内容入口仍需开发 | 优先工程候选 |
| LDtk 主创作 + Unity 确定性导入 | 现成地形／实体／世界工作流，策划与美术可使用专门工具 | 新作者权威、旧数据迁移、双向修改边界、引用与运行预览差异 | 重点体验候选，可成为长期方案 |
| 保留现有 GridArt + 专门地形语义 | 旧素材／表现和引用变化较少 | 仍承担规则、界面、碰撞联动、格式与性能维护 | 作为对照与兜底，不能默认胜出 |

Tiled 路线与 LDtk 一样属于外部作者工具＋导入，但地形规则和对象配置体验不同，应在需要更灵活规则时进入同一轮比较。

不建议把 LDtk、Tiled、Unity Tilemap 和现有 GridArt 全部设为同一区域的可写来源。可以在试验期并列比较；正式采用时，每类作者数据都应有明确来源，生成输出不接受另一路静默回写。

当前项目规定 Scene 是玩法权威、GridArt 是已有美术权威。研究外部工具不改变此约定。若后续选择外部来源，需要在专项迁移中一起更新所有权、保存、导入与协作规则；不能用旧 planning JSON 重建当前 Gaming。

## 7. 建议做的同房间比较

下一轮原型使用独立测试场景／工程，不直接转换正式图。本轮只定义比较项目，没有声称已经完成原型。

**比较对象：** Unity 原生方案与 LDtk；Tiled 视前两者表现加入。现有编辑器作为基线。各方案使用同一套合法项目素材、相同地形形状与机关任务，不以漂亮的商店示例代替。

**任务清单：**

1. 制作一个带内洞、薄墙、楼梯、悬垂柱和一格横条的房间，包含相邻房间边界。
2. 完成添加、矩形填充与挖空；核对可见边、真实碰撞与孔洞。
3. 在同一视口放草、石、门、按钮、钥匙、重生点和一组移动平台机关，并修改参数。
4. 修改墙形，基础美术与碰撞同步更新；人工修饰和保护区保留。
5. 一次撤销／重做恢复完整笔画，复制有身份物品获得新身份；保存重开结果不变。
6. 模拟两名成员修改不同房间，再合并；记录共享规则冲突、修改文件大小及复查成本。
7. 连续重新导入或重建三次，确认引用、收集身份和人工覆盖稳定，不要求每次手工补接。
8. 在项目运行系统中测试搬运贴墙放置、爬藤、房间往返重置、钥匙颜色表现与遮蔽。

**记录指标：**

- 策划／美术完成相同任务的时间、入口切换次数、需要程序帮助的次数。
- 常见薄墙／转角／材质交界质量，以及一主题所需素材与规则配置量。
- 连续绘制、跨块擦除、Undo、保存、导入的中位数／P95／最大停顿，机器与规模一起记录。
- Scene／Hierarchy 对象数、内存、生成与作者文件体积；避免用空场景流畅度替代真实规模。
- 按房间协作时的冲突范围、身份与引用稳定性，以及项目适配代码量。

先让策划与美术各自完成一次相同任务，再讨论最终选型。原型应先限定为一个小房间和最少适配；若为了完成原型已开始重写整套通用工具，应暂停扩大范围并重新评估方案。

## 8. 资源范围与未确认事项

本报告优先比较已能核实官方功能和 Unity 接入路径的资源。没有给第三方商店插件编造价格、维护状态、Unity 6.3 兼容或性能成绩；Ferr2D 仅纳入轮廓地形类别比较。

外部工具的通用能力有文档依据，本项目的最优选型仍待小房间实测。没有“某成熟平台游戏必然这样制作”的统计结论，也没有由参考截图反推其具体引擎或编辑器。工具、导入器和本项目运行系统应作为三层分别评估，避免把“编辑时很好用”直接等同于“接入后已经可交付”。
