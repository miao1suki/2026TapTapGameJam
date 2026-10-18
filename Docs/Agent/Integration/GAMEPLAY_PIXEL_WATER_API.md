# 玩法水体表现接入

`WaterVisualFeature` 优先加载 `Resources/ColorBlocks/WaterPresentation`，生成 `GameplayWaterPresentation`。旧字段 `waterPrefab/waterLocalOffset/waterScale` 仅在新配置缺失时作为旧 InteractiveWater 后备路径；当前标准资源齐全时不再创建旧波纹水、顶板或反射相机。无需迁移既有 Scene 或规划 JSON。

## 所有权与资源

- 浮力柱 `topExitRiseCells` 默认3格，替代旧的 `topExitBoostSpeed` 额外速度字段。出水使用 `PlayerController.ApplyGridRiseImmediate(cells)`：按玩家单元格尺寸/重力计算上升速度，保留水平速度，不叠加环境额外加速或先前上浮速度；同次出水只触发一次。其他使用 `ApplyVerticalBounceImmediate(speed)` 的机关保持原有额外加速行为。

- WaterSourceFeature、DirectionalCurrentFeature、BuoyancyColumnFeature 继续拥有水域/推力/玩家接触和红色蒸汽反应，不在表现脚本重新实现玩法。
- `GameplayWaterPresentation` 只在初始化、体积/相邻水体变化、方向/密度/速度变化时重建；注册表限当前 Scene。按 Collider 世界边界和 `GridCellSizeUtility` 定义大小，缓存每格尺寸；大块最多3种后片Mesh和1个前片Mesh，不为每格创建GameObject。
- 相邻判断使用全部已激活水族体积：最上沿Surface，下一行Body，更深Interior。面片在XY平面，当前横版相机从-Z观察；不承诺旧四视角视差玩法。
- 源图16×17，UV去掉底部一行透明像素，实际16×16覆盖一格。原始TA图片和独立预制体不改。
- 气泡复用 TA `DirectionalWaterBubbleStream`，新增Down=3而不修改Up=0/Left=1/Right=2。按10格长度、最多3格宽度分段，长度与世界尺寸匹配；普通水域无气泡，定向水流四向，浮力柱向上延伸至连通水域顶面。
- `WaterVisualFeature.ShowFlowBubbles/FlowBubbleDensity/FlowBubbleSpeed` 为只读配置访问，字段使用现有BlockParameter中文Inspector及规划参数保存。

## 形状、水下物品与墙遮挡

水与普通物品/墙分层占格，不改变普通 Layer、HSV 或颜色解锁契约。颜色屏幕命中优先同体积的非水物体，包含没有颜色反应的墙，不能继续穿透墙去染水。非水物品不遮掉整格水，仍可显示水中效果。

水层仅与岩浆互斥，其他所有物体均可同格；普通水、水流、气泡柱可彼此叠放。编辑器绘制、拖动、替换、片段拼接及应用场景统一验证水/岩浆的实际形状，含跨房间和装配补丁，不因包围矩形的空洞误报。不同水类型分别保留玩法，同类型同格生成时去重。运行时、Scene美术预览和关卡编辑器共用“重叠水只画一层”的表现原则，避免透明叠黑；方向气泡仍归各自水流/浮力柱。

显式染色命中可通过 `IColorApplicationHitTarget.ApplyColorAt(colorId, actor, worldHit)` 传入实际落点；`WaterVisualFeature` 从该水格寻找就近的可见水面，避开合并空洞及墙封顶，不再固定合并体中心。蒸汽只推选中列的一格宽度，普通 `ApplyColor(colorId, actor)` 保留兼容，按actor附近选择水面。同格多种水被点击时仅处理一个反应，避免叠加。

红蓝鼠标容差由 `PlayerColorWheel.aimToleranceCells` 配置，默认0.65格。指针与候选落点都必须在玩家中心原 `ScanRadius` 内，不继承绿色额外射程；先取准确前景命中，空处才做NonAlloc容差候选并再次验证遮挡/房间。提示圈在相机更新后绘制，仅做几何计算；超范围弧段置灰、静止不闪。当前各色最近一条交互连线策略不变。

不规则合并使用根上的 `LevelEditorMergedShape`：`WorldBounds` 仅用于遍历边界，`ContainsWorldPoint` 和压缩 `Volumes` 才定义水域；前后片网格都按实际格生成，不填孔洞。多 Collider 仍只有一套水状态，退出一片但仍在另一片内不能清掉游泳状态。浮力柱按实际片段检查，岩浆冷却继承形状和大小。

`WaterCellVisibility` 根据 `LevelEditorPlacedBlock.ActiveRevision` 缓存当前 Scene 实体墙的实际格，只在增删/配置变化后重建；不是每帧扫描场景。水的初始网格、正式前后面片以及 InteractionLinkEffect 都忽略被墙覆盖的格；部分被遮挡的大水体将连线落到范围内的可见水格，全遮挡时不出线。该遮挡不删改规划、碰撞或水状态，不禁止水与墙同格存储。

## 材质与管线

全部正式水和气泡材质通过 `ColorAppearanceManager.GetSharedMaterial(source,"blue",true)` 获取；同一来源材质共享，无逐物体逐帧改材质、无逐帧实例化。实际透明度由集中管理器写 `_ColorRevealOpacity`。气泡自身游动透明度与该值相乘，不覆盖解锁渐显。

后片使用既有ColorVineReveal透明网格Shader、TA图片；气泡使用既有Pixel Sprite Reveal；前片继续使用TA PixelWaterDistortion，新增 `_ColorRevealOpacity` 与原 `_Opacity` 相乘。TA独立预制体默认Reveal=1兼容。

所有面片/气泡保留blue普通Layer与Real标记。后片排序-100、气泡0、玩家10；前片在水体前沿，专属PixelWaterRefraction通道于AfterRenderingTransparents取样/绘制，因此包括角色与气泡。WaterUnderlayRendererFeature按唯一LightMode筛选，不再要求普通Water Layer；SelectiveHsvRendererFeature的mask增加此LightMode，HSV继续在像素化之后运行。PC/Mobile Renderer均已具备所需Feature。不把游戏物体Layer改为PixelEffect，不引入另一套全局管线设置。

生成根归WaterVisualFeature所有，禁用时先SetActive(false)再延迟Destroy，不在禁用回调SetParent；同帧重新挂接不复用旧表现。注册表与捕获请求成对注销。区域重置走现有Host重新挂接，保留蓝色解锁状态。

## 验证入口

持久测试 `GameFlow/Tests/Editor/PixelWaterPresentationTests.cs` 验证资源三分组、透明参数、四向枚举稳定性及双端Renderer依赖。实际GPU/Play检查在本机忽略的Library/FlowRetirementProbe隔离项目中进行，不覆盖用户场景；结果与未验收项记录于ACTIVE_WORK。
