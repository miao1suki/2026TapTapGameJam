# 物体美术接入

## 2026-10-18 平台新图与三色单向平台

曲柄平台更新为`43452C733652487AC26FD041686C880B.png`的6×48×16帧，自动平台更新为`BB587FC3F1E5565D857F67F813F19880.png`的9×48×16帧，仍0.08秒/帧。完整帧、旧实体占格/路线/绑定均保留，不通过扩平台或抬碰撞消缝。新增`MechanismArtworkEntry.supportTopPixels`（新版14）为完整帧底到可见支撑顶边的原像素距离，`PlatformArtworkLayout.VisibleThicknessCells(kind,sprite,library)`统一读取；Scene代理、规划、运行和`LevelEditorMergedShape.ApplySprite`一致，不再硬编码旧5像素条。

三色单向平台仅是同一`OneWayPlatform`的`appearanceColor`参数，Neutral=0兼容旧白色，Red/Green/Blue=1/2/3。新栏目稳定ID为`mechanism-oneway-red/green/blue`，通过既有ComponentValueOverrides保存枚举，默认1×1；旧白色ID及3×1默认占格不改。三色16×16帧来自新增小图底排，天然1格，改参数只换图与视觉Layer；不能添加ColorObject、颜色能力反应或未解锁实体门控。中性共享Sprite材质+ColorCatalog视觉Layer采用装饰HSV语义，隐藏/启用、碰撞和房间归属仍由原组件负责。

窄生成入口`WorldObjectArtworkSetup.BuildPlatformFrames()`和`BuildColoredOneWayPlatforms()`只改相关图集/源图导入设置、Objects.asset与三色栏目，不保存Scene/planning；保留其他库条目、TA藤蔓、告示牌及持久SpriteID。隔离入口`PlatformArtworkRefreshAudit.BuildAndVerify`生成并检查尺寸、支撑面、旧短平台、颜色覆盖/去黑、重复生成身份，并输出真实GPU横竖平台/三色单向平台对照图`Library/PlatformArtworkContactSheet.png`；`RunPlayVerification`检查锁色、解锁与房间重置后三色平台都保持可见可站立。完整美术GPU回归仍由`ArtGridRefinementAudit.Run`执行。产物必须连同png.meta、Objects.asset、LevelEditorPalette.asset与三个源图.meta一起交付，不同步Library。

流动岩浆/蓝色冷却蒸汽与三类植物燃烧已接入TA正式表现，旧场景通过共享配置后备生效，不重建布局。燃烧使用原Renderer的MPB及Sprite/HSV共用轮廓；藤蔓保留192帧及自顶向下玩法次序。详见 [TA_EFFECTS_API](TA_EFFECTS_API.md)。下文“静态岩浆”指无TA配置时的原后备。

## 统一像素尺度与关卡预览

当前场景美术以 `GridArtworkMetrics.PixelsPerCell = 16` 为唯一基准；详细换算及资源尺寸见 `Docs/Developer/ART_GRID_CONVENTIONS.md` 和 `ART_GRID_REFINEMENT_API.md`。物理占格不再作为任意缩放贴图的依据：32×32重生点显示2×2格，24×24玩家显示1.5×1.5格，已有碰撞保持不变；确证4倍导出的tail瓦片按有效16像素显示1格。旧存档不因外观更新自动扩占格或移位。

`ObjectArtworkPlanningPreview.TryGet` 是道具栏目及关卡画布的统一只读2D预览入口，返回纹理、UV、自然格数、旋转、镜像和平铺信息。已接入正式素材的物体不应再请求3D预制体截图。水采用正式水面/中/底瓦片，藤蔓采用真实序列帧的缓存采样；原像素、透明区、孔洞及实例素材覆盖必须保留。

Scene编辑态统一由`ArtworkScenePreview`缓存并绘制2D代理：ObjectChangeEvents、开关场景、资源变化与Undo驱动局部重建，hierarchyChanged合并发现未走Undo的新实例，平移/重绘不扫描场景，不触发玩法Awake/挂接。代理Mesh/材质是HideAndDontSave，不创建场景节点；旧Renderer只临时forceRenderingOff，Play/重载/关闭恢复。规划构建所有参数设置后统一调用`QueueObject`，下一编辑器tick批量刷新；`RefreshObject`仅用于需要立即预览的显式操作。不再调用运行时Refresh产生持久视觉子节点；显式运行外观Ensure/Refresh保留给资源生成及隔离审计。见[Scene预览说明](../../Developer/SCENE_ARTWORK_PREVIEW.md)。

平台不足3格用`PlatformArtworkLayout.SliceUV/CroppedFrame`居中裁原像素；旧逐格batch不视为合并体。新放仍3格，旧位置、占格和绑定不迁移。Canvas支撑面与真实Collider一致，竖向UI−90°对应Scene+90°；单向平台不能借用移动平台首帧。

## 机关与道具精灵

`MechanismArtworkPresentation.Ensure(owner)` 补齐支持的物体，`RefreshScene(scene)` 在场景加载时补旧实例；正式Prefab已持久绑定，动态实例继承组件。不要通过重建Scene/规划存档接入美术。组件只读机关状态，不写引用、物理或重置逻辑。

`ObjectArtworkLibrary.Find(kind)` 返回帧组；`ProgressFrame(progress,count)`用于曲柄双向取帧。曲柄读实际Progress，不另建计时Tween。门读取State与OpenDuration，支持中断倒放；doorClosed为空门框，doorIndicators/doorWhiteIndicators为红/绿/蓝顺序的彩色/白色独立图案，按实际按钮数显示。DoorButtonKind(BindingIndex)选择三色按钮帧组，新枚举追加、不重编号旧资产。精灵沿开门进度收窄模拟转轴，不修改作者碰撞或机关状态。

子节点BoxCollider换算到根本地空间作为显示范围，不修改碰撞。绿色生长/重置后更新铺图范围，平台顶边对齐支撑面，Tiled左右边界保留端头和像素密度。缓存格尺寸接口，不逐帧扫描场景/读图/实例化材质。颜色精灵复用集中共享材质和原HSV遮罩，只在正式阶段隐藏占位Mesh，不能提前隐藏基础阶段；钥匙始终显示对应颜色核心。

`WorldObjectArtworkSetup.BuildMechanisms()` 在Editor生成切片和门独立标记，幂等绑定Prefab，不保存用户Scene。Point/FullRect/无Mip，必须将.meta、Objects.asset和Prefab一起交付。梯子采用tail第四行H形瓦片，不用九宫格边框；没有水序列帧，不移除既有TA水。背景不在此入口自动布置。

`ObjectArtwork/Runtime` 为独立资源/帧索引/比例计算模块；`BlockFeatures/Runtime/WorldObjectSpritePresentation` 只负责显示，不写玩家、颜色解锁或重生绑定，不改变作者碰撞体和稳定规划ID。

## 状态与接口

- 重生点读取 `RespawnAnchorFeature.IsActivated` / `IsCurrent`，分别对应 Locked / Used / Current；切换时重置帧时钟，当前动画循环。
- `BouncePlantFeature.BounceTriggered` 仅在 `TryGetBounceSpeed` 成功后发出；不能从 CollisionStay 重复调用视觉动画。
- `BouncePlantFeature.AppearanceReset` 在 Detach / ResetForRoom 时发出；表现停止临时播放并回静止帧。
- `WorldObjectSpritePresentation.Ensure(owner)` 补齐旧实例，不能替换物体身份或全量重建关卡。重生点 Awake 与植物 OnAttach 提供运行时后备。
- `PlayBounce()` 仅测试/外观用，不产生弹跳速度；真实逻辑仍从 `IPlayerBounceSurface` 进入 PlayerController。
- `Refresh(delta)` 刷新外观；暂停时使用0，不用unscaledDeltaTime推进装置/植物动画。

## 材质与尺寸

植物使用 `ColorAppearanceManager.GetSharedMaterial(source,"green",true)`，共享池统一更新 `_ColorRevealOpacity`，不为每株复制/逐帧修改材质。渲染器位于绿色 Layer，MPB 给原HSV遮罩提供颜色组/正式阶段/精灵Alpha纹理；HSV仍走现有全屏阶段，不另算一份本地HSV。重生点走无色共享材质。

Sprite Tiled使用FullRect切片、Point采样、无压缩/Mipmap；用原BoxCollider确定平铺覆盖范围，用 `GridCellSizeUtility` 和16有效像素/格确定单幅素材的自然世界尺寸。对子节点反向补偿非等比缩放，不能直接把一幅植物图拉成合并长条。图片底部固定到碰撞体底部，静止帧的上方留白不再通过抬高整图补偿。`plantIdleTop` 保留为旧配置兼容，不再参与位置计算。精灵尺寸更新只写表现子物体，不改变碰撞体/物体根姿态。

原占位Mesh保留：编辑时隐藏；运行时由ColorObject负责黑白基础阶段和退场，不能由表现组件提前关闭基础阶段。植物正式阶段不使用颜色目录的编辑边框材质。

## 资产与编辑兼容

`WorldObjectArtworkSetup.Build()` 在编辑器生成资源并绑定两份正式道具Prefab；幂等更新切片、保留源图片/GUID、作者位姿和玩法字段。当前Scene只补表现，不自动保存或重建。导入Objects.asset及打开Scene时补齐既有实例；缺资源时保留原有功能后备，不创建空白美术。

资源位于 Resources/ObjectArtwork 和 Art/Generated/ObjectArtwork，必须与源码、.meta、Prefab同时交付。原始两张装置图和弹簧图未改动。场景文件不因本次绑定强制提交。

## 藤蔓序列帧

`ClimbableVineFeature.UpdateVineSequence` 优先读取作者配置或 Resources 中的 ObjectArtworkLibrary；`VineFrame(progress,idleTime,animateIdle)` 支持生长/倒放和长成末段循环。每个母根创建一个SpriteRenderer，子节段只负责原有碰撞/攀爬，不创建各自的动画。无Animator、无运行时读图、无粒子Simulate；源图经 `WorldObjectArtworkSetup.BuildVineSequence()` 在Editor生成12页图集，192帧完整保留，Point/无Mip、HQ压缩，公共横向裁切、逐帧底部和可见高度裁切。

`ColorAppearanceManager.RegisterVine` 对SpriteRenderer保留精灵Shader，复用共享池和统一可见性；粒子备用仍走旧Shader适配。换帧只改sprite及HSV遮罩Alpha纹理，不逐帧复制/改材质。Root.ResetForRoom须重新RegisterVine，防止燃烧Unregister后的对象永远隐藏。Detach立即隐藏并摘下旧视觉子物体，再延迟Destroy，避免同帧重新挂接产生重影。序列帧不能改变规划稳定ID、母根链接、区域或碰撞体。

藤蔓布局共用`VineArtworkLayout`，抵消父根缩放并按真实cellWorldSize摆放；编辑读取`ArtworkRoot/AuthoredArtworkCells/AuthoredArtworkRootBounds`不调用会修改根系的RestoreInitialGrowthLinks。Canvas同作用域连续竖列先取整根帧后逐格`VineSlice`，不是每格单独初始帧。宽度与自定义序列配置覆盖保留，断格/跨房间不合成同根，修改/擦除清理链采样缓存。

2026-10-15隔离Unity6000.3.12f1：89项EditMode通过；真实GPU/Play检查初始隐藏、渐显中间透明、完整生长、末段循环、单根单实例、燃烧倒放、烧毁重置、禁用重启、材质池不增长及单株/合并植物底边和宽度。日志在Library/VineSequence*，未验证安卓真机与完整关卡通关。
