# 岩浆冷却蒸汽

独立的一次性 URP 像素蒸汽效果，表现蓝色作用于红色岩浆后冷却成固体方块的反应。它只画蒸汽，不负责接色判定、岩浆碰撞、冷却音效或固体块生成。

## 资源

- `CoolingSteam.shader`：透明 URP 粒子 Shader，带轻微流动噪声与屏幕像素化边缘。
- `CoolingSteam.mat`：默认浅蓝白蒸汽材质；像素化程度默认 `0.22`。
- `CoolingSteamEffect.cs`：生成低速扩散的雾团、雾芯和雾边，默认约 `1.2` 秒消散。
- `CoolingSteam.prefab`：可独立实例化的一次性特效。

## 运行时接入

`Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab` 的 `LavaHazardFeature` 将 `CoolingSteam.prefab` 指定为“冷却蒸汽特效”。蓝色成功替换岩浆时，特效出现在替换块顶面附近并作为替换块的子物体；运行时发射点对齐到主镜头可见的块面，粒子排序高于默认层级，以便雾气覆盖黑曜石表面。Shader 深度测试仍会让更靠近镜头的物体遮挡蒸汽。`RoomResetObject` 清理临时替换块时会一并清理蒸汽。接色判定、原岩浆复位、冷却音效和 `CoolLava_Block` 的外观均沿用现有系统。

雾团会在方块上方轻柔展开并缓慢上浮，默认上升幅度小于一个方块高度，不会形成 `SteamEfect` 中的高喷柱。尺寸、浓度、上浮速度、边缘柔化、流动和像素化程度可在 `CoolingSteam.mat` 的 Shader 属性中调整；粒子生命周期和各层数量可在 `CoolingSteamEffect` Inspector 中调整。

## 像素化

`_Pixelation` 范围为 `0–1`，默认 `0.22`，只作用于该蒸汽材质。`0` 为柔和雾边；增大数值会增大屏幕像素块，不改变项目其他材质或全局像素化设置。
