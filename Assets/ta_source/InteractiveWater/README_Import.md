# 2026TapTap 交互水面接入说明

预制体：`Assets/ta_source/InteractiveWater/InteractiveWater.prefab`。
本项目 Unity 6000.3.12f1、URP 17.3.0；Level_01 使用常驻主相机和 Universal Renderer。
原始迁移包中的 Renderer2D.asset 仍保留作参考，Level_01 主相机不需要切换 Renderer。

## 多个水面

每个预制体在运行时复制各自的波纹模拟纹理、环境波纹纹理、四份相关材质和平面反射纹理。
碰撞水花只写入发生碰撞的那一个水面；原始资源是模板，不再由运行时脚本直接改写。
每增加一个水面都要多占一份纹理显存。需要降低开销时可减小纹理模板的分辨率。

## 水下画面如何绘制

PC_Renderer 和 Mobile_Renderer 中的 Water Underlay Capture 在透明物体绘制之前生成
`_WaterUnderlayTexture`。它先复制相机画面，再用 UnderwaterAlbedo.shader 以无光照方式
重画指定层的可见物体，保留常见的 `_BaseMap`、`_MainTex`、颜色和 Sprite 顶点色。
FrontMesh.shadergraph 在自身面片范围内取样这张纹理，施加波纹扰动、水色和焦散。
FrontMesh.mat 的 Main Color Alpha 为 1；透视效果来自取样后的画面。

Renderer Feature 的 Capture Layers 默认包括除 Water（Layer 4）和 UI（Layer 5）之外的层。
水面正面和顶面放在 Water 层，避免重复进入取样画面。可在两个 Renderer Asset 的
Water Underlay Capture 设置里收窄 Capture Layers，但必须包含需显示在水下的角色层。

这套方法使水片覆盖处最终显示的是无光照取样经水面重新着色的结果。底层物体为了
在水外正常显示，仍会由主相机按自身材质绘制一次。特殊自定义材质若不用 `_BaseMap`
或 `_MainTex` 保存颜色，其水下取样可能只呈现纯色，需要为该材质扩展
UnderwaterAlbedo.shader。两个水面相互重叠的屏幕区域由排序更靠前的水片显示。

## 使用注意

- 场景中保留标记为 MainCamera 的相机；水面预制体根物体缩放保持 (1,1,1)。
- WaterTopMesh、WaterFrontMesh 两个 Sorting Layer 已写入 TagManager，ID 与预制体一致。
- PC 和 Mobile URP Asset 均启用了 Opaque Texture，但本效果的水下取样使用独立捕获纹理。
- 鼠标点击波纹的旧 Input 测试默认关闭；2D/3D Trigger 入口均在 InteractiveWater.cs 中。
- 重新导入旧迁移包会覆盖本项目的运行时隔离和水下渲染改动。
- 尚未在 Unity 中验证编译和最终视觉效果；首次打开项目后请关注 Console。
