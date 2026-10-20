# GridArt 接入与维护

房间装配总览：GridArtLevelRoom.areasWorld/edgesWorld由PlanningArtAdapter使用PlanningOwnerPreview几何投影为世界XY，保留异形、空洞和分离块；Contains做精确选房，旧适配器无轮廓时回退boundsWorld。GridArtWindow.RoomOverview仅管理只读视图、保留式房间标签及返回绘图导航，不修改Scene/Map/Planning；平移缩放不重读场景或重建逐格几何。

补充美术内外角：`GridArtAuthorBorderLibrary`按明确选区导入256×190原生图，原白线自动槽改13个真实切片，追加20个手动边角/纹饰、3色草地（colorId=red/blue/green）与第二版2个中心纹样。共享库203项；旧笔刷索引和Sprite对象保留，不能把新图套tail4倍倍率。导入可幂等同步，参见[白线来源与规则](../../Developer/GRID_ART_WHITE_BORDER.md)。

独立关卡美术，不替代PlanningDocument、SurfaceTiles或玩法碰撞。操作见[开发者说明](../../Developer/GRID_ART.md)。遵守taptap-editor-ui、EDITOR_UI_STANDARD。

## 数据所有权

- GridArtMap每关独立，世界XY原点0，格中心`(cell+.5)*cellSize`，负坐标Floor，与房间／装配图一致。ownerSceneGuid首次应用设置；跨Scene只读，需独立副本。
- GridArtLayer稳定Guid，存显隐、锁定、depth、parallax、tint、sortingOrder、lighting3D；新增lockParallaxX/Y（默认false）与maxParallaxOffset（每轴格数，0不限），预览/运行统一GridArtScene.LayerOffset。Scene绑定OnEnable/SetBindings时按id解析Map设置，不需重烘网格；旧缺字段保持原行为。见[视差说明](../../Developer/GRID_ART_PARALLAX.md)。followLevel/sourceGroup/sourceBrush定义自动底图，cells为生成占格缓存。空sourceGroup表示整关，其他为稳定房间/通道ID。自动层不接受指针绘制，关闭联动后可手绘；装饰放独立层。sortingOrder=int.MinValue兼容旧值layerIndex-100；显式值不随插层变化。
- 默认库GridArtLibraryService.SharedPath：`Assets/_Project/Content/Art/SceneArtLibrary.asset`。地图仍存整数笔刷编号，禁止重排／删除既有槽位。sourceKey采用资产GUID/localId、切片库GUID＋稳定tile.Id或来源库GUID＋槽位，不用InstanceID。切片生成器会重建Sprite子资产，同步必须按tile.Id修复原槽位的Sprite引用，不得仅靠Sprite localId去重；已配置显示尺寸和规则不自动覆盖。
- brush.size为排布／擦除占格，visualSize>0为实际显示格数，否则兼容旧Size；contentRect为Sprite有效内容裁区；normalMap为同尺寸图集。新导入按GridArtworkMetrics（16有效源像素/格）计算visualSize，sourcePixelScale仅用于确证的整数导出。tail1408×704已逐RGBA块确证4倍，旧库64px单块仍为1格；不是4格。详见[换算标准](../../Developer/SCENE_ART_PIXEL_CONVERSION.md)。
- GridArtScene输出不增加Collider、BlockRuntime、颜色接口、重置脚本或相机控制权。玩法合并不删美术。输出根需世界原点、无旋转、缩放1且独立于共享Prefab。“美术层_”子树只放生成内容。

## 编辑器API

- `GridArtWindow.OpenForGrid(size,reference)`：关卡入口，新图继承格尺寸；自动由适配器刷新轮廓，不再依赖Hierarchy选择。新图同Scene存为`<scene>_Art.asset`。关卡ScenePainter在美术绘制时退出事件处理，避免一笔画两种物体。
- `GridArtLevelSource.Read(Scene)`：由Assembly-CSharp-Editor的PlanningArtAdapter注册委托，返回世界XY的GridArtLevelSnapshot/Group，避免GridArt.Editor反向引用关卡程序集。优先DocumentForScene，次选同名planning.json，再选场景内嵌规划，最后只读PlacedBlock；规划Y向下坐标只在适配器转换。`groups.bounds`仅含房间/通道/装配补丁中的非道具墙地块（黑、白、自定义块及旧Platform标记），按实际合并覆盖保留孔洞，不混入道具、水和区域。`referenceItems`另含正式2D道具/水/定位轮廓，`referenceOnlyNames`说明缺正式素材的轮廓；它们不能当作自动墙地来源，也不能写入游戏渲染器。
- `GridArtLevelSource.NotifyChanged()`：关卡加载、笔画结束、规划撤回发出失效通知；工作台延后0.35秒合并读取。Hierarchy事件只失效，不在回调中全场扫描。Synchronize只替换联动层，内容相同不改版本/列表，不覆盖手工层。没有工作台订阅时不采集快照。
- `Shared()`：创建／加载共享库；首次从当前tail图裁20纹样与白线边框，不改源图、不引用旧开发图集。
- `Synchronize(library)`：追加SurfaceTilePalette切片、GridArtSupplementLibrary的混合图集静态装饰、GridArtProjectLibrary的墙地／边框／正式道具静态外观，以及GridArtShallowGrassLibrary的三色浅草连接。排除Plugins／Development／测试及整张动画板。新槽位按有效源像素计算，旧槽位不静默改变显示尺寸。只更正已知生成默认名称／分类，不覆盖自命名；正式外观按稳定kind/state键更新引用，不将重新生成的Sprite视为新物体。窗口聚焦同步共享库，也可手动，不每帧扫描项目。浅草独立资产持有生成纹理及Sprite，目标库仅追加引用，不覆盖本地库编辑。详见[盘点与裁区](../../Developer/SCENE_ART_LIBRARY.md)、[浅草连接](../../Developer/SHALLOW_GRASS_CONNECTIONS.md)。
- `Import(library,source,pixelsPerCell)`：单Sprite或已切片图片全部Sprite；旧参数为源码兼容保留，新导入统一16有效源像素/格，不改变源图设置。切图器新方案无缩放，容量不足明确失败。旧切图方案仍保留兼容，显式转换后重新生成。
- `Connect(map,destination)`：先校验失效编号，复制原库定义、重映射所有图层并录Undo；来源库GUID＋原槽位去重，保留自定义尺寸／规则／法线，不修改原库。不要直接换已有绘图的palette引用。
- `GridArtGeometry.Cells(bounds,size,limit)`：轴对齐包围盒格中心覆盖、去重、十万格上限；合并前后相同轮廓等价，不是非矩形精确投影。
- `BuildQuads(layer,palette)`：同笔刷八邻格／四象限选图，缺内角回中心；同层支持多纹理，拒绝打包Sprite、错误法线尺寸。
- `Apply(map,scene,recordUndo=true,saveMap=true)`：先验证，再生成自己子树。自动预览传recordUndo=false，保留未变层/块对象，比较网格和材质，只持久化变化资产；不增加额外Undo步数。显式API默认保留旧生成层Undo。saveMap=false保留未保存的Map/Palette。输出路径仍为`Art/Generated/GridArt/<sceneGuid>/<mapGuid>`，32×32格按颜色纹理＋法线分块；无全局SaveAssets，Scene只标脏，旧未引用文件不自动删。
- `GridArtScenePainter.IsPainting`：工具互斥。启动时关闭SurfaceTiles绘制，旧工具再启动则退出新绘制。捕获鼠标控制权，Esc／关闭／切图取消当前未完成笔画，松开完成一笔并排队保存/预览。每框4096格、每图十万格。

GridArtWindow.Automation负责0.65秒延后自动保存/预览，两个开关默认true、按项目/Scene记忆。保存只限Map/Palette，关闭/重编译/切Scene补存完成笔画；进入Play前刷新源。Scene变化后不得向新Scene应用旧Map。SaveAll显式保存Scene，自动流程不保存整Scene。关闭实时预览时保留手动刷新入口。美术资产磁盘签名冲突停止覆盖并备份Library/GridArtRecovery。

窗口不每帧重建；滑条只使画布失效，不销毁正在拖动的控件；笔画结束只更新画布/状态，不重建侧栏。未变图层缓存、视口剔除、复用绘制列表。GridArtLayer.Set/Erase使用非序列化格索引，直接改cells列表或通过Undo恢复后须InvalidateLookup；多格图的擦除仍覆盖整个占格。保留素材滚动位置、Foldout与每图视图。素材表单直接在工作台编辑，库Inspector保留为维护入口。

`GridArtBrushGrid`按ScrollView视口实际宽度确定列数，只在GeometryChanged或筛选重建时布局；默认250点侧栏两列，横向滚条关闭。卡宽扣除边距与高DPI取整余量，选中描边不改列数；禁止回退为逐帧重建或按屏幕像素代替UI点计算。

画布`RepaintView()`只变更预览父节点的translate/scale、256图块批次可见性与顶层参考线，不失效瓦片几何；`Invalidate()`用于数据/素材/视差变化。`GridArtPreviewBatch`按原排序中连续同纹理片段分批，不按纹理全局重排，保持透明叠图顺序。预览坐标以32显示单位/格缓存，显示变换为zoom/32；不参与地图存储或运行时Mesh。不要在generateVisualContent中修改UI树。

工作台合成分为背景（`depth > 0`）、只读关卡参考、表面/前景（`depth <= 0`）、工具覆盖四段；远景与近景不能因同纹理合批跨过关卡参考。每段内按绘制顺序、再由远到近排序。`background-artwork-cache`和`artwork-cache`同步同一pan/zoom并复用批次，不能把所有美术重新塞到参考物上方；这不会修改地图的深度、占格或存档。关卡参考的显隐/透明度设置仍独立生效。

两套关卡画布共用`GridViewportDrawing`：显示线间隔至少16点、总数预算320，2的幂次整格LOD与世界原点对齐，直接提交线条四边形，避免密集Painter2D交叉路径细分。它只接收显示坐标，不负责鼠标命中、吸附或坐标迁移。房间所有权使用`PlanningOwnerPreview`缓存精确矩形覆盖和孔洞/外轮廓，文档修改必须走`InvalidatePreviewData`；不能以缓存几何替换实际存档cells。组装预览按每个房间一次求原点，不在每个box上扫描整房间。

## 渲染与兼容

SetCamera、RebaseParallax、ParallaxOffset API保持。LateUpdate10000只读跟随后的相机；绝对偏移delta*(1-factor)，不改Z、不累计漂移，可像素吸附。换相机重新建参考。

默认URP Unlit透明，ZWrite Off、Cull Off。lighting3D走URP Lit，带Normals/Tangents，同UV颜色／法线，关闭受光清理旧keywords。不是URP 2D Light路径。

颜色装饰：brush.colorId为空代表无色；layer.colorId为空继承各素材、none强制无色、其他ID覆盖。PlanningArtAdapter注入GridArtColorSource.Read，从现有ColorCatalog取id/displayName/unityLayer；禁止硬编码层号/层名。BuildQuads携带colorId；Apply按颜色ID额外拆批，同图集三色不能合到同Renderer。没有ColorBlock、颜色交互、逐装饰Update。GridArtScene序列化colorBindings，在生成及运行OnEnable时一次性设置MPB：atlas alpha、Group=-1、Real=1、UseVertexAlpha=0、Opacity=材质_BaseColor.a，避免装饰卷入初始/真实材质交接，也不依赖不可序列化的Editor MPB。运行时HSV由现有SelectiveHsvRendererFeature处理。

仅固定XY。联动底图随黑白及自定义墙地块更新，但手工美术不自动随房间移动；未实现背景循环、缩放补偿、转向重投影、动画、多地形过渡；不可恢复退役总拼框架。

SurfaceTiles六面保留：AuthoringService/ScenePainter/MeshBuilder拒绝持久化Prefab源和PrefabStage，记录实例覆盖，不ApplyPrefabInstance。Bake先保存Scene、以Scene GUID隔离；仍要求单源图集，统一多源走GridArt。

## 交付与验证

Scene及同名planning.json、Map、Library、原图、生成资源和meta一起交付，不只交场景，不提交Library日志。本轮不修改正式玩法场景。

GridArt/SurfaceTiles EditorTests覆盖边角、负坐标、裁区、多图集、迁移、合并轮廓、材质隔离、Scene所有权、法线、持久化、Undo/redo、Toolkit。资产夹具为唯一临时目录；主编辑器有未保存场景时跳过资产测试，使用隔离项目验证。GPU与窗口尺寸也在隔离项目验证，实际策划制作体验仍需验收。
