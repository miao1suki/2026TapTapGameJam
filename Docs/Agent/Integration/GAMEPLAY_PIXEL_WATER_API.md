# 玩法水体表现接入

`WaterVisualFeature` 优先加载 `Resources/ColorBlocks/WaterPresentation`，生成 `GameplayWaterPresentation`。旧字段 `waterPrefab/waterLocalOffset/waterScale` 仅在新配置缺失时作为旧 InteractiveWater 后备路径；当前标准资源齐全时不再创建旧波纹水、顶板或反射相机。无需迁移既有 Scene 或规划 JSON。

## 所有权与资源

- WaterSourceFeature、DirectionalCurrentFeature、BuoyancyColumnFeature 继续拥有水域/推力/玩家接触和红色蒸汽反应，不在表现脚本重新实现玩法。
- `GameplayWaterPresentation` 只在初始化、体积/相邻水体变化、方向/密度/速度变化时重建；注册表限当前 Scene。按 Collider 世界边界和 `GridCellSizeUtility` 定义大小，缓存每格尺寸；大块最多3种后片Mesh和1个前片Mesh，不为每格创建GameObject。
- 相邻判断使用全部已激活水族体积：最上沿Surface，下一行Body，更深Interior。面片在XY平面，当前横版相机从-Z观察；不承诺旧四视角视差玩法。
- 源图16×17，UV去掉底部一行透明像素，实际16×16覆盖一格。原始TA图片和独立预制体不改。
- 气泡复用 TA `DirectionalWaterBubbleStream`，新增Down=3而不修改Up=0/Left=1/Right=2。按10格长度、最多3格宽度分段，长度与世界尺寸匹配；普通水域无气泡，定向水流四向，浮力柱向上延伸至连通水域顶面。
- `WaterVisualFeature.ShowFlowBubbles/FlowBubbleDensity/FlowBubbleSpeed` 为只读配置访问，字段使用现有BlockParameter中文Inspector及规划参数保存。

## 材质与管线

全部正式水和气泡材质通过 `ColorAppearanceManager.GetSharedMaterial(source,"blue",true)` 获取；同一来源材质共享，无逐物体逐帧改材质、无逐帧实例化。实际透明度由集中管理器写 `_ColorRevealOpacity`。气泡自身游动透明度与该值相乘，不覆盖解锁渐显。

后片使用既有ColorVineReveal透明网格Shader、TA图片；气泡使用既有Pixel Sprite Reveal；前片继续使用TA PixelWaterDistortion，新增 `_ColorRevealOpacity` 与原 `_Opacity` 相乘。TA独立预制体默认Reveal=1兼容。

所有面片/气泡保留blue普通Layer与Real标记。后片排序-100、气泡0、玩家10；前片在水体前沿，专属PixelWaterRefraction通道于AfterRenderingTransparents取样/绘制，因此包括角色与气泡。WaterUnderlayRendererFeature按唯一LightMode筛选，不再要求普通Water Layer；SelectiveHsvRendererFeature的mask增加此LightMode，HSV继续在像素化之后运行。PC/Mobile Renderer均已具备所需Feature。不把游戏物体Layer改为PixelEffect，不引入另一套全局管线设置。

生成根归WaterVisualFeature所有，禁用时先SetActive(false)再延迟Destroy，不在禁用回调SetParent；同帧重新挂接不复用旧表现。注册表与捕获请求成对注销。区域重置走现有Host重新挂接，保留蓝色解锁状态。

## 验证入口

持久测试 `GameFlow/Tests/Editor/PixelWaterPresentationTests.cs` 验证资源三分组、透明参数、四向枚举稳定性及双端Renderer依赖。实际GPU/Play检查在本机忽略的Library/FlowRetirementProbe隔离项目中进行，不覆盖用户场景；结果与未验收项记录于ACTIVE_WORK。
