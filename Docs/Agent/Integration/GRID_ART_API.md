# GridArt 接入与维护

独立关卡美术，不替代PlanningDocument、SurfaceTiles或玩法碰撞。操作见[开发者说明](../../Developer/GRID_ART.md)。遵守taptap-editor-ui、EDITOR_UI_STANDARD。

## 数据所有权

- GridArtMap每关独立，世界XY原点0，格中心`(cell+.5)*cellSize`，负坐标Floor，与房间／装配图一致。ownerSceneGuid首次应用设置；跨Scene只读，需独立副本。
- GridArtLayer稳定Guid，存显隐、锁定、depth、parallax、tint、sortingOrder、lighting3D；followLevel/sourceGroup/sourceBrush定义自动底图，cells为生成占格缓存。空sourceGroup表示整关，其他为稳定房间/通道ID。自动层不接受指针绘制，关闭联动后可手绘；装饰放独立层。sortingOrder=int.MinValue兼容旧值layerIndex-100；显式值不随插层变化。
- 默认库GridArtLibraryService.SharedPath：`Assets/_Project/Content/Art/SceneArtLibrary.asset`。地图仍存整数笔刷编号，禁止重排／删除既有槽位。sourceKey采用资产GUID/localId、切片库GUID＋稳定tile.Id或来源库GUID＋槽位，不用InstanceID。切片生成器会重建Sprite子资产，同步必须按tile.Id修复原槽位的Sprite引用，不得仅靠Sprite localId去重；已配置显示尺寸和规则不自动覆盖。
- brush.size为排布／擦除占格，visualSize>0为实际显示格数，否则兼容旧Size；contentRect为Sprite有效内容裁区；normalMap为同尺寸图集。旧图不需批量重建。
- GridArtScene输出不增加Collider、BlockRuntime、颜色接口、重置脚本或相机控制权。玩法合并不删美术。输出根需世界原点、无旋转、缩放1且独立于共享Prefab。“美术层_”子树只放生成内容。

## 编辑器API

- `GridArtWindow.OpenForGrid(size,reference)`：关卡入口，新图继承格尺寸；自动由适配器刷新轮廓，不再依赖Hierarchy选择。新图同Scene存为`<scene>_Art.asset`。关卡ScenePainter在美术绘制时退出事件处理，避免一笔画两种物体。
- `GridArtLevelSource.Read(Scene)`：由Assembly-CSharp-Editor的PlanningArtAdapter注册委托，返回世界XY的GridArtLevelSnapshot/Group，避免GridArt.Editor反向引用关卡程序集。优先DocumentForScene，次选同名planning.json，最后读取非道具PlacedBlock占格；规划Y向下坐标只在适配器转换。包含房间/通道/装配补丁的Solid，排除玩法道具、水及区域范围。
- `GridArtLevelSource.NotifyChanged()`：关卡加载、笔画结束、规划撤回发出失效通知；工作台延后0.35秒合并读取。Hierarchy事件只失效，不在回调中全场扫描。Synchronize只替换联动层，内容相同不改版本/列表，不覆盖手工层。没有工作台订阅时不采集快照。
- `Shared()`：创建／加载共享库；首次从当前tail图裁20纹样与白线边框，不改源图、不引用旧开发图集。
- `Synchronize(library)`：追加SurfaceTilePalette切片，保留格数和裁区，排除Plugins／Development／测试。窗口聚焦同步共享库，也可手动，不每帧扫描项目。
- `Import(library,source,pixelsPerCell)`：单Sprite或已切片图片全部Sprite；不猜切片，不改源图导入设置。
- `Connect(map,destination)`：先校验失效编号，复制原库定义、重映射所有图层并录Undo；来源库GUID＋原槽位去重，保留自定义尺寸／规则／法线，不修改原库。不要直接换已有绘图的palette引用。
- `GridArtGeometry.Cells(bounds,size,limit)`：轴对齐包围盒格中心覆盖、去重、十万格上限；合并前后相同轮廓等价，不是非矩形精确投影。
- `BuildQuads(layer,palette)`：同笔刷八邻格／四象限选图，缺内角回中心；同层支持多纹理，拒绝打包Sprite、错误法线尺寸。
- `Apply(map,scene,recordUndo=true,saveMap=true)`：先验证，再生成自己子树。自动预览传recordUndo=false，保留未变层/块对象，比较网格和材质，只持久化变化资产；不增加额外Undo步数。显式API默认保留旧生成层Undo。saveMap=false保留未保存的Map/Palette。输出路径仍为`Art/Generated/GridArt/<sceneGuid>/<mapGuid>`，32×32格按颜色纹理＋法线分块；无全局SaveAssets，Scene只标脏，旧未引用文件不自动删。
- `GridArtScenePainter.IsPainting`：工具互斥。启动时关闭SurfaceTiles绘制，旧工具再启动则退出新绘制。捕获鼠标控制权，Esc／关闭／切图取消当前未完成笔画，松开完成一笔并排队保存/预览。每框4096格、每图十万格。

GridArtWindow.Automation负责0.65秒延后自动保存/预览，两个开关默认true、按项目/Scene记忆。保存只限Map/Palette，关闭/重编译/切Scene补存完成笔画；进入Play前刷新源。Scene变化后不得向新Scene应用旧Map。SaveAll显式保存Scene，自动流程不保存整Scene。关闭实时预览时保留手动刷新入口。美术资产磁盘签名冲突停止覆盖并备份Library/GridArtRecovery。

窗口不每帧重建；滑条只使画布失效，不销毁正在拖动的控件；笔画结束只更新画布/状态，不重建侧栏。未变图层缓存、视口剔除、复用绘制列表。GridArtLayer.Set/Erase使用非序列化格索引，直接改cells列表或通过Undo恢复后须InvalidateLookup；多格图的擦除仍覆盖整个占格。保留素材滚动位置、Foldout与每图视图。素材表单直接在工作台编辑，库Inspector保留为维护入口。

## 渲染与兼容

SetCamera、RebaseParallax、ParallaxOffset API保持。LateUpdate10000只读跟随后的相机；绝对偏移delta*(1-factor)，不改Z、不累计漂移，可像素吸附。换相机重新建参考。

默认URP Unlit透明，ZWrite Off、Cull Off。lighting3D走URP Lit，带Normals/Tangents，同UV颜色／法线，关闭受光清理旧keywords。不是URP 2D Light路径，也不自动接TA Shader／HSV／像素化标记，不能将逻辑图层当GameObject Layer。

仅固定XY。联动底图随白块更新，但手工美术不自动随房间移动；未实现背景循环、缩放补偿、转向重投影、动画、多地形过渡；不可恢复退役总拼框架。

SurfaceTiles六面保留：AuthoringService/ScenePainter/MeshBuilder拒绝持久化Prefab源和PrefabStage，记录实例覆盖，不ApplyPrefabInstance。Bake先保存Scene、以Scene GUID隔离；仍要求单源图集，统一多源走GridArt。

## 交付与验证

Scene及同名planning.json、Map、Library、原图、生成资源和meta一起交付，不只交场景，不提交Library日志。本轮不修改正式玩法场景。

GridArt/SurfaceTiles EditorTests覆盖边角、负坐标、裁区、多图集、迁移、合并轮廓、材质隔离、Scene所有权、法线、持久化、Undo/redo、Toolkit。资产夹具为唯一临时目录；主编辑器有未保存场景时跳过资产测试，使用隔离项目验证。GPU与窗口尺寸也在隔离项目验证，实际策划制作体验仍需验收。
