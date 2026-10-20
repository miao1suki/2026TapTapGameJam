# 二维遮挡光照与喷泉

## 当前连通空间模式

正式 Gaming 与 `Development/MapVisibilityTest/MapVisibilityTest.unity` 均启用 Main Camera 上的“连通空间可见”。保持原画亮度与既有 HSV/颜色解锁，只隐藏范围内没有通路连到玩家的空间；有通路可以绕过转角，无须面对目标或布置照明灯。封死的脚下房间、隔壁房间保持隐藏，开门连通后可见，关门重新隐藏。普通地形碰撞、机关与存档不受影响。

- 默认半径24格，前85%范围保持完整亮度，外侧15%渐隐。通路必须能在这个局部圆形范围内连通；不会沿通路无限揭示整张地图。
- 连通性按世界XY半格网格、四向邻接计算，不穿对角墙缝。最小可辨通路约半格；这不是玩家寻路，不模拟跳跃、攀爬或钥匙能力，也不以作者房间ID判断可见性。细于采样尺度的特殊结构需要另行检查。
- 规划实体墙与关闭的门自动阻断。水、玩家、搬运物、普通平台不默认阻断。其他遮挡物可挂 `VisibilityOccluder2D`，指定非Trigger的BoxCollider。
- 进入玩法2D相机后生效；透视菜单相机和普通Scene视图旁路。Overlay菜单与HUD不变暗。

操作：在主相机 `WorldVisibility2D` 中文 Inspector 保持“连通空间可见”开启，调“视线范围（格）”；支持2～48格。半格采样跟随单格世界尺寸。正式场景默认关闭编辑态预览，进入Play观察效果。关闭整个组件可对比原画面。

### 移动端成本

这个模式不使用玩家灯、`VisibilityLight2D`、实时阴影或新增Unity Light。直接跳过所有光源多边形计算，不分配灯光叠加纹理，也不执行横纵高斯模糊。只生成一张半分辨率R8单通道遮罩和一次全屏合成；不是用多光源把场景照亮。其他系统原有灯光不由本模块修改。

墙体静态轮廓按版本缓存、8单位区块查询；洪泛缓冲区复用，可见格按连续行段合并网格。玩家停留或半格内移动不重建；普通移动最多每秒10次，墙/门变化及较远传送立即更新。初次进入或显式刷新会重建墙缓存。半径越大，连通采样与网格成本越高。

### 地图修改后更新

不使用Lightmap，无须烘焙或等待地图定稿。修改规划墙体后重新进入Play即可；规划实体的版本变更自动更新。自定义代码直接修改静态墙碰撞或动态创建新门后，调用 `WorldVisibility2D.InvalidateAll()`，也可点Inspector的“刷新墙体遮挡缓存”。正式布局文件和物体绑定保持原样。

传送地图同样运行时生成，目的地仍是已激活的普通重生点；剧情Start/End属于另一类入口。手动测试副本操作见该目录README，后续正式地图改动不会自动覆盖此快照。

### 兼容的旧灯光模式

关闭“连通空间可见”才使用旧直线视线、环境亮度、玩家灯及附加灯参数。这些参数在连通模式下不生效，Inspector会隐藏。旧模式的1.5格高斯柔化需要额外两次滤波Pass；当前正式/测试场景均不执行这条渲染分支。旧滤波方案参考 [NVIDIA GPU Gems](https://developer.nvidia.com/gpugems/gpugems2/part-iii-high-quality-rendering/chapter-27-advanced-high-quality-filtering)。

## 喷泉

`Assets/_Project/Content/Art/Fountain.prefab`，也可通过 `Tools/2026TapTap/美术/定位喷泉预制体` 定位。只有表现组件，无碰撞、Boss 判定或结局逻辑。

八帧完整 82×66，严格导出倍率 1、16 有效像素/格，因此自然大小 **5.125×4.125 格**。底部中心固定、保留黑色内部与透明留白、不逐帧裁包围盒。Point、FullRect、无 mip、无压缩、NPOT 不缩放；父缩放由原像素换算补偿。

默认每秒 10 帧可调整；运行自动循环，可用 `FountainArtwork.SetPlaying(bool)` 暂停/恢复。编辑态显示首帧，不用常驻实时粒子模拟。未来玩法脚本可引用这个预制体，不应把结局逻辑塞进表现组件。

## 验证与限制

连通模式以Unity6000.3.12f1隔离工程、真实URP/D3D11 GPU检查绕角、封闭房间、开闭门、传送、范围渐隐与原画亮度，额外放置32个VisibilityLight2D确认不生成光源网格且不揭示封闭空间；同时检查正式Gaming及手动测试副本的实际接线。共58项通过。桌面编辑器正式图冷启动21.291ms、缓存检查0.067ms、移动重建1.734ms；现有1个Unity方向光保持原样。结果见 `Library/ConnectedSpaceFinal.log` 与 `Library/ConnectedSpaceFinal-result.txt`，正式图静态GPU画面在 `Library/ConnectedSpaceProbe/Library/Connected_Formal.png`。这些CPU数据不等同手机帧率。

尚未验收Android真机GPU时间、显存及完整Gaming通关。现有旧灯光审计仅覆盖兼容模式，不能用旧直线视线断言替代连通模式验收。
