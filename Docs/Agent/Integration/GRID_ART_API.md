# GridArt 接入与维护

状态：独立试用版；不替代 SurfaceTiles、PlanningDocument 或玩法碰撞。正式关卡尚未自动挂载。

## 数据所有权

- `GridArtMap`：每关单独 ScriptableObject，存格尺寸、独立美术层、笔刷索引与已画格。世界 XY 原点为0，CellSize继承当前关卡编辑器。负坐标使用 Floor，不是截断。房间图屏幕 Y 向下与场景世界 Y 向上在源关卡编辑器中已经转换，本工具直接使用场景 Cell。
- `GridArtLayer`：稳定 Guid、显示、锁定、排序、深度、颜色和视差；`cells` 为局部美术格。不新增 RoomResetObject、IColorObject 或 Collider。
- `GridArtPalette/GridArtBrush`：共享 Sprite/九宫格与内角定义；当前地图保存笔刷索引，因此不得重排、删除已使用的库项目。需要替换美术时原槽位改图；新增只能追加。正式扩展为稳定 brush Guid 的迁移前，不引入任意删除功能。
- `GridArtScene`：唯一输出根，保存地图与持久化 Mesh/材质引用；运行时仅按层更新 Transform。作者不要把手工物体放进 `美术层_` 生成子树，它会在应用时被替换；玩法及其他根不会被删除。

## API

- `GridArtWindow.OpenForGrid(float size, IEnumerable<Rect> reference=null)`：关卡编辑器适配入口。只读 Scene 中 LevelEditorPlacedBlock 的 `Cell/SizeCells`，传黄色参考轮廓；新图继承格尺寸。既有图尺寸不一致仅提示，不改图。新建通过用户选择路径完成。
- `GridArtBuilder.BuildQuads(layer,palette)`：编辑器四象限规则解析；相同笔刷才邻接，角对角邻格仅在两个正交邻格均存在时使用。缺失内角退到中心；SpriteAtlas 打包拒绝。
- `GridArtBuilder.Apply(map,scene)`：显式完整重建该美术输出，保存到 `Art/Generated/GridArt/<mapAssetGuid>`。32×32格按源纹理分块，材质按层/源纹理复用。先验证资源再替换自己的生成层，不动玩法；完成后必须保存 Scene。最多十万格、单次框选4096格。
- `GridArtScene.SetCamera(Camera)`：选跟随相机，空则使用有效 Camera.main；只读取，不直接修改相机，也不占 Manager 控制权。
- `GridArtScene.RebaseParallax()`：重置背景层至布置位置，并以当前摄像机建立新参考。摄像机被替换、场景启用会重新建立参考。普通移动/重生不累计积分，因此回到初始相机位置背景也会回原位置。
- `GridArtScene.ParallaxOffset(delta,factor,pixelUnit)`：纯函数；offset=delta*(1-factor)，不处理Z；pixelUnit>0时绝对位移吸附，避免累计误差。LateUpdate顺序10000，读取跟随后的相机。

## 持久化与渲染

素材、palette（含Sprite子资产）、map、所有Mesh/Material、Scene及.meta一起交付。源码 Runtime 不依赖 Editor，Player 不读取源码图片文件或重建瓦片。移走或删除源图会丢失材质依赖。

透明 URP Unlit Shader，ZWrite Off、Cull Off，保留原图 Alpha。排序 `layerIndex-100`，层深度由作者设置；玩家 Sprite 仍走自己的深度/水下Shader链。该输出不擅自写颜色层、Stencil 或 Rendering Layers。不同玩法镜头方向、透明排序及前景遮挡需要专项测试。

应用会复用有相同map引用的 Scene 输出和已有网格资产；删除过的图层/网格块留下的未引用资产尚无自动清理，避免误删被其他Scene使用的资源。清理需依赖审计/用户授权。烘焙资产不是Undo对象；绘图Undo后应显式再次Apply。

## 扩展路线

优先：brush稳定Guid迁移、局部更新、区域搬移、缺角可视验证、自动周期图片层、关卡墙体只读派生美术。然后再考虑多地形连接规则及按房间绑定；不可用InstanceID作存档引用。暂不增加插件依赖或改变旧地图格式。

## 验证

EditMode程序集 `Project.GridArt.EditorTests` 覆盖五种角关系、负坐标、多格擦除、混合笔刷、删格重建、序列化、视差吸附与UI Toolkit入口。隔离测试场景在Development/GridArt/TileArtTrial；不把测试相机或示意图写入正式Scene。实际窗口鼠标操作及目标设备表现仍需用户验收。
