# 像素雨区

纯视觉美术对象，无碰撞、积水、地面溅射或玩家颜色交互。红、绿、蓝三套预制体共用一个动态网格脚本、材质和 Shader；不生成单颗雨滴 GameObject。每区最多 512 颗。

## 布置

通过 `Tools/2026TapTap/关卡美术/创建雨区` 选择颜色、范围和位置后创建；也可以将本目录 `Rain_Red`、`Rain_Green`、`Rain_Blue` 预制体拖入 Scene。

选中雨区会显示范围。Inspector 可调整宽高、密度、速度、四帧动画、噪波与固定种子。编辑时显示静止首帧，运行时下落并错峰切换四帧。移动雨区和重新打开场景不重抽初始分布；调整参数或种子才重新生成。创建与参数修改支持 Undo，运行时和 Prefab 编辑模式不允许通过窗口创建。

雨区是普通 Scene 根对象，不放在关卡生成根之内，因此“应用到场景”不会重建或清除它。它不写入 planning 文件；协作交付仍须同时提交 Scene、对应 planning 存档及资源依赖。项目未引入 TA 雨效果示例场景。

## 像素与范围

统一使用 `GridArtworkMetrics`：16 个有效源像素为 1 格。四帧各为 16×16，显示为 1×1 格；固定大小，不允许每颗随机缩放。父物体缩放不会改变雨滴像素尺寸，中心按世界像素步长对齐。范围、速度分别以格、格/秒计，默认每格世界尺寸为 1；放在实现 `IGridCellSizeProvider` 的父级下时使用父级格尺寸。

宽度乘密度决定颗数（上限 512）；高度决定下落距离。速度随机幅度、到底概率、噪波、错峰等待及轻摆保留 TA 实现。固定作者种子不使用临时 instanceID，不污染 Unity 全局随机状态。

## HSV 和渲染

根据 `ColorCatalog` 自动归属红、绿、蓝颜色 Layer，按场景装饰的灰白→彩色流程恢复。不注册颜色物体、不解锁能力，也不参与颜色状态机、房间重置或交互连线。它不申请像素化 Rendering Layer，避免对原生像素素材再次像素化。

HSV 遮罩同时使用图集透明度、网格顶点透明度与材质透明度；和雨 Shader 使用相同 UV、0.01 alpha 裁切边界。不会把透明格子或淡出的雨滴当作实心方块处理。

原图为 447×336；Default 纹理、最大 512、Point、Clamp、无 Mipmap、无压缩、NPOT 不缩放。图集坐标以左上角为原点：列起点 320/352，红色行 160/192，蓝色行 224/256，绿色行 288/320，每格 16×16。`RainSpriteField.FrameUV` 是统一取帧接口。PNG 只作为共享图集采样，不重新切片或重排帧。

## 接口与验证

`RainSpriteField.Configure(color, areaCells, dropsPerCell, seed)` 配置区域；其他随机和下落参数通过序列化 Inspector 调整。`RainRegionWindow.CreateRegion` 是带 Undo、Prefab 覆盖记录、场景标脏及 Play 保护的创建入口。

在隔离 Unity 验证项目执行 `Project.Rain.Editor.RainIntegrationAudit.Run`，结果写入 `Library/RainIntegrationAudit.txt`。检查三色 Prefab、纹理设置、12 个帧矩形、相同种子的确定性、移动不重抽、父级缩放/格尺寸、顶点透明遮罩、零密度、512 上限、创建 Undo 和 Scene 保存/重载。测试只允许在 Library 下的验证项目运行，不修改正式场景和 planning。
