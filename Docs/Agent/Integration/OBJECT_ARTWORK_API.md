# 物体美术接入

`ObjectArtwork/Runtime` 为独立资源/帧索引/比例计算模块；`BlockFeatures/Runtime/WorldObjectSpritePresentation` 只负责显示，不写玩家、颜色解锁或重生绑定，不改变作者碰撞体和稳定规划ID。

## 状态与接口

- 重生点读取 `RespawnAnchorFeature.IsActivated` / `IsCurrent`，分别对应 Locked / Used / Current；切换时重置帧时钟，当前动画循环。
- `BouncePlantFeature.BounceTriggered` 仅在 `TryGetBounceSpeed` 成功后发出；不能从 CollisionStay 重复调用视觉动画。
- `BouncePlantFeature.AppearanceReset` 在 Detach / ResetForRoom 时发出；表现停止临时播放并回静止帧。
- `WorldObjectSpritePresentation.Ensure(owner)` 补齐旧实例，不能替换物体身份或全量重建关卡。重生点 Awake 与植物 OnAttach 提供运行时后备。
- `PlayBounce()` 仅测试/外观用，不产生弹跳速度；真实逻辑仍从 `IPlayerBounceSurface` 进入 PlayerController。
- `Refresh(delta)` 刷新外观；暂停时使用0，不用unscaledDeltaTime推进装置/植物动画。

## 材质与尺寸

植物使用 `ColorAppearanceManager.GetSharedMaterial(source,"green",true)`，共享池统一更新 `_ColorRevealOpacity`，不为每株复制/逐帧修改材质。渲染器位于绿色 Layer，MPB 给原HSV遮罩提供颜色组/正式阶段/精灵Alpha纹理；HSV仍走现有全屏阶段，不另算一份本地HSV。重生点走无色共享材质。

Sprite Tiled使用FullRect切片、Point采样、无压缩/Mipmap；用原BoxCollider尺寸及根lossyScale确定铺设面积，用 `GridCellSizeUtility` 决定一株的世界尺寸。对子节点反向补偿非等比缩放，不能直接把一幅植物图拉成合并长条。静止图片最高不透明像素用于顶面对齐，保证跳跃接触面不悬空。精灵尺寸更新只写表现子物体，不改变碰撞体/物体根姿态。

原占位Mesh保留：编辑时隐藏；运行时由ColorObject负责黑白基础阶段和退场，不能由表现组件提前关闭基础阶段。植物正式阶段不使用颜色目录的编辑边框材质。

## 资产与编辑兼容

`WorldObjectArtworkSetup.Build()` 在编辑器生成资源并绑定两份正式道具Prefab；幂等更新切片、保留源图片/GUID、作者位姿和玩法字段。当前Scene只补表现，不自动保存或重建。导入Objects.asset及打开Scene时补齐既有实例；缺资源时保留原有功能后备，不创建空白美术。

资源位于 Resources/ObjectArtwork 和 Art/Generated/ObjectArtwork，必须与源码、.meta、Prefab同时交付。原始两张装置图和弹簧图未改动。场景文件不因本次绑定强制提交。
