# 关卡预览、物体美术与颜色装饰

接手先读 `Docs/Developer/ART_GRID_CONVENTIONS.md`、`SCENE_ART_PIXEL_CONVERSION.md`、`EDITOR_INTERACTION_UPDATE.md` 和 `GAMEPLAY_REFINEMENTS.md`。场景/规划交付仍遵守 `FORMAL_LEVEL_DELIVERY.md`，正式Gaming不能被测试地图覆盖。

## 像素和预览

统一 `Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell = 16`。tail已逐像素验证为4倍导出，不能把64文件像素再显示成4格。玩家24×24自然显示1.5格，脚底按原偏移对齐，标准胶囊仍1×1。重生点32×32为2×2外观，不扩大原激活范围。

`ObjectArtworkPlanningPreview.TryGet` 返回当前正式2D素材描述，包含UV、自然格数、旋转、镜像和平铺；栏目与网格复用，不能各自生成不同的3D缩略图。水复用已有正式水面/水中/水底素材，藤蔓预览采用缓存采样，不逐次读图。旧存档尺寸与碰撞不因新默认平台3×1而静默重排。

左连墙梯为新独立条目；旧侧梯ID/GUID保留为右连墙梯。参数 `NeutralLadder.wallSide` 保留存档值。

## 新图用途

`471dca30b9bcb2527867589ad677e3e3.png`：暗红/亮红U分别为基座未激活/激活；旁边深色裂纹图为黑曜石，接入现有岩浆蓝交互冷却替换，R/离房恢复岩浆；绿色团状为植物障碍；七种告示牌为字幕激发道具图片。不要另建玩法来实现这些外观。

下半部红蓝绿连线、团簇、竖柱是静态背景/装饰；雨滴只为后续区域下雨提供原始素材，不是已接入雨系统。成就收集表未交付，本轮不处理。

`GridArtSupplementLibrary.ImportShared()` 窄入口将装饰追加到统一素材库，保持原索引和稳定引用；重复导入幂等，原图最后15像素宽区域按真实尺寸显示而非拉成16。不要执行重建所有资源或全量重建地图替代窄生成。

装饰归属保存在 `GridArtBrush.colorId`，图层可覆盖或显式选择无色；Builder按实际颜色目录Layer拆合批。颜色目录通过 `PlanningArtAdapter` 注入独立GridArt编辑器程序集，不反射Assembly-CSharp、不硬编码Layer名。不生成ColorObject、不实现玩家染色接口。

`GridArtScene`保存颜色渲染绑定，OnEnable一次性重建遮罩MPB：原图alpha、材质透明度、Group=-1。MPB本身不会随Scene保存，不能只在Editor Builder中设置一次。HSV沿用现有像素化之后的全屏SelectiveHsvRendererFeature，与首次钥匙场景解锁联动；第二钥匙能力解锁不能重播装饰恢复。没有逐物体Update或材质复制。

## 物理与机关

`MechanismBaseFeature.useGravity`（参数“物理/启用重力”）默认true；关闭为悬浮，仍可搬运/绿钩子拉动。搬运与钩子独占期间不得抢写刚体，R/离房复原遵守实例值。MRoom2两基座在Scene和planning中显式false；后续修改需成套交付，不能仅改临时instanceID或Library缓存。

`DoorController`开门前图案展示与松开后关门缓冲独立。`BouncePlantFeature`普通、高跳、搬运高度独立；旧8/8/4默认保留。藤蔓下一格先检测实体阻挡，水与本根不阻挡。

## 验证

仅在 `Library/FlowRetirementProbe` 隔离工程执行：`ArtGridRefinementAudit.Run`（真实GPU外观/HSVmask）、`PlayerPixelScaleAudit.Run`、`GameplayRefinementAudit.Run`、`DecorationsHsvAudit.Run`（实际URP保存重载/双阶段解锁）、`EditorInteractionAudit.Run`、`FragmentSafetyAudit.Run`。项目EditMode包含像素换算、合批颜色、存档重开、素材库幂等及超限导入测试。GPU用例不可加 `-nographics`。

本轮D3D11通过：美术179、玩家51、玩法83、装饰URP47、窗口50、片段16、地图协作36、水形状197及EditMode225/225。后续经用户授权，MRoom2左侧竖平台与原定右侧基座恢复持久绑定，Scene、规划和六个合并快照保持一致；保存重开与35项真实播放回归通过，不再在审计中临时注入绑定。两基座无重力及原布局保留。尚未做Android真机或完整通关验证。
