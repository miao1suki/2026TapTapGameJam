# 岩浆冷却蒸汽表现

`Assets/ta_source/Steam` 提供蓝色作用于红色岩浆后播放的一次性蒸汽表现。蒸汽在镜头可见的冷却块上部表面展开，并以较高粒子排序覆盖黑曜石外观，表现其正在被蒸发包裹；仍保留深度测试，因此不会穿过更靠近镜头的遮挡物。蒸汽缓慢上浮，默认上升不到一个方块高度，约 `1.2` 秒后消散。材质保留独立的屏幕像素化效果。

## 接入

`Assets/_Project/Content/ColorBlocks/Prefabs/Red_Lava.prefab` 的 `LavaHazardFeature` 在“蓝交互”参数组引用 `Assets/ta_source/Steam/CoolingSteam.prefab`。蓝色成功替换岩浆时，组件在冷却块 Collider 顶部附近实例化蒸汽并将其挂到替换块下；蒸汽组件再沿主镜头视线将发射点对齐到冷却块可见表面。现有 `RoomResetObject` 清理临时替换块时会一并清理蒸汽；没有区域管理器时仍按原规则销毁岩浆，短暂特效由自身生命周期清理。

该连接只提供视觉反馈。蓝色资格和接色结果仍由 `LavaHazardFeature` 决定，原岩浆隐藏/复位、冷却音效和 `CoolLava_Block` 生成行为不变。

## 调整

- `CoolingSteam.mat`：雾团大小、雾气浓度、上浮速度、边缘柔化、雾边细节、流动速度、中心亮度和像素化程度。
- `CoolingSteamEffect`：生命周期、方块世界尺寸，以及雾团/雾芯/雾边三层粒子数；蒸汽粒子排序高于默认层级。
- `CoolingSteamEffect` Inspector 使用 UI Toolkit。默认粒子材质缺失时会显示提示。

改变材质像素化只影响该效果，不改全局像素化或其他材质。完整资源说明见 [`Steam/README.md`](../../Assets/ta_source/Steam/README.md)。

## 验收步骤

1. 进入含已解锁 `Red_Lava` 的场景，选择蓝色并成功作用于岩浆。
2. 确认原岩浆按既有规则被替换为 `CoolLava_Block`，块顶出现浅蓝白像素蒸汽，蒸汽短暂上浮并自行消散。
3. 在启用房间重置管理器的场景中复位区域，确认临时冷却块和其蒸汽一起清理，原岩浆恢复。

此提交没有改动 `Assets/ta_source/SteamEfect` 或 `Assets/Scenes/Test.unity`。
