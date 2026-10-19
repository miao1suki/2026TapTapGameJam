# R1 按钮至弹性植物镜头提示

源码：`Assets/Scenes/R1/Runtime/R1SpringCameraGuide.cs`；中文UI Toolkit Inspector和显式审计在同关Editor目录。仅挂R1原Main Camera，不安装到其他Scene、玩家Prefab或正式Gaming。

## 所有权与状态

- `buttonAnchor` 指向原U26稳定ID `r1_button_middle`，`landingPlantAnchor` 指向原Q41:S41稳定ID `r1_spring_lower`。使用镜像后的真实Transform，不从Excel或整数planning重建位置。增量编辑后引用丢失时，限频查询同场景`LevelEditorPlacedBlock.ActiveInstances`按稳定ID恢复；不逐帧扫Scene。
- 空闲 → 跟随玩家自身实体中心进入按钮附近的可调矩形 → 申请拉远并锁存 → 指定植物顶面落地/回弹 → 释放本次租约。离开按钮不是结束条件，按钮是否已锁存与此提示无关。只追踪当前相机实际跟随的同场景标准玩家。
- 代码初始区域宽10格、高6格、上移0.5格、Size20；当前Scene保留策划修改后的宽3格、高6格、上移0.5格和Size16，不重写其余值。格长读取按钮`LevelEditorPlacedBlock.CellWorldSize`；进入0.45秒、恢复0.6秒。SceneView选中相机显示金色范围和起终点连线，不新增碰撞。
- 新增序列化`autoFramePlant`，默认false，R1 Scene显式存false。**手动模式**每帧请求`max(expandedSize, 当前基础Size)`，不调用自动构图、不保留历史最大值，可在活动提示中实时改大/改小；切换自动→手动也会解除历史最大值。小幅输入修改同样更新handle；尺寸变化沿用进入时间。只在提示活动时控制镜头，不改变整关默认Size。
- **自动模式**将`expandedSize`作为下限，保留原本次最大值锁存。帧序600在跟随500之后、输出1000之前，基于`CameraModeController.TryGetCameraState`的未放大机位、实际植物实体Bounds和输出宽高比计算完整入镜半高；`framingMarginCells`只在此模式参与计算。两种模式的倍率分母均来自`TryGetProjectionState`的基础Size，不从已放大Camera反推，避免指数扩大。
- Inspector中文开关“自动保证植物入镜”；关闭时尺寸字段为“拉远尺寸”，开启时为“最小拉远尺寸”，说明与高级安全边距按模式显隐；支持多选混合值和标准序列化Undo。运行只读状态显示手动/自动及本次请求值，不伪装成可编辑参数。
- 唯一Camera写入口仍是`CameraControlManager.RequestLensMultiplier`。普通请求取最大、不相乘，保留跟随；恢复仅释放自身handle，不硬写12，不清除其他区域或Boss镜头。若作者之后改变基础Size，恢复服从新基础配置。演出优先级沿用Manager现有规则。
- 订阅现有`BouncePlantFeature.BounceTriggered`（额外校验当前玩家位于该植物顶面附近）与`AppearanceReset`，不修改植物或玩家。搬运/按下方向不回弹时，脚底与指定实体顶面接触且非上升也可结束；侧擦/悬空经过不算。死亡、R/房间复原、传送、菜单预览、引用失效或关闭组件清理并可重新触发。订阅在关闭/换目标时成对解除。
- 只读`GuideActive`、`AutomaticFraming`、`RequestedSize`和`ApproachBounds`供Inspector/专项使用。`ResetGuideForTesting()`只释放本次租约；若仍在按钮范围，下一帧可再次激活。不新增输入或进度存档。

## 验证与交付

`R1SpringCameraGuideAudit.Run`仅允许关闭R1编辑器后的显式batchmode：先只读打开R1核对真实引用，再在临时Scene用现有植物契约、标准玩家胶囊及Manager做编辑态检查。最终74项通过：原自动50项回归保留，新增手动16不被自动下限覆盖、实时大小与小幅数值变化、宽高比不强制放大、模式双向切换、动态基础下限、实时缩小中间帧、落地恢复、Unity原生序列化参数复制及中文条件显隐。临时Transform无持久场景ID，不使用EditorJson文件往返测试它们。不进入Play、不保存作者Scene，最终核对Scene/planning文件字节未变。本轮日志仅留`Library/r1-manual-camera-size-audit.log`。实际跳跃路线、触发范围和视觉手感由用户验收，不能把专项说成完整通关。

本任务前的Scene/planning字节备份在`Library/R1BeforeSpringCamera-20261019`。本次相对该Scene仅新增1个组件引用和组件参数块，所有关卡位姿与用户已有改动保留，planning字节不变。参数保存在Scene相机而非规划物体覆盖；普通增量编辑不会重建相机，但从空Scene只导入planning不会创建本专属提示，交付须包含Scene、同名planning、双方meta及新增代码/meta。共享片段不携带场景相机组件，正式Gaming接入需要整合者另行明确配置，不能覆盖整个正式地图。

本轮手动模式修改前另备份在`Library/R1BeforeManualCameraSize-20261019`；相对本轮备份，镜头修复只增加`autoFramePlant: 0`一行，不覆盖策划Size16、宽3等新微调或重建地图。2026-10-19用户授权以“R1 final version”交付该镜头及已保存关卡，范围与规划/Scene差异见R1_LEVEL最终版记录；仅推R1分支，未自动合入最新main。此前74项编辑态专项不代表最终布局通关，参数和实际路线仍由策划验收。
