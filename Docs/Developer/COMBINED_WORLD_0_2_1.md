# 0.2.1 全关卡合图

正式启动仍只有 Gaming，菜单、玩家、相机和 UI 不复制。当前 Android applicationId 为 `com.miao1suki.shapeofcolor`，versionName `0.2.1`，versionCode `201`。2026-10-20 之前的 `com.xingzhicai.game` 包为历史包；更改包名不会自动迁移旧包数据。

## 来源与位置

保留最新实际 Scene 的内部布局、浮点位姿、合并孔洞、材质与引用图；不以旧 JSON 生成替代品。以下为源关卡到正式世界的 Unity XY 平移：

| 来源 | XY 平移 |
| --- | --- |
| R1 | -255, +104 |
| R2 | -289, +49 |
| R3 | -225, +35 |
| MRoom2 | -68, +69 |
| MRoom3 | -62, -34 |
| Boss | -58, -68 |

连接 Gaming↔MRoom2↔R1↔R2↔R3↔Gaming；MRoom3 两个上部口接 Gaming 下方对应开放处；Boss 接长下落井。新通道附墙与必要梯子，仅裁切接口的53格原墙，保留关内内容。用户原先移走的 Gaming 房间21按保存规划保留在暂存位置，没有删除。

MRoom3 新增 `box_9fb493b9`、`box_e0df9bf4`、`box_65075c98` 均保留（正式 ID 加 `MRoom3_` 前缀）。最终4343个物体、28区域，含连接区。连接区采用非矩形独立占格，不覆盖整片世界地图。

## 后续编辑与导入

正式 Gaming Scene、Gaming.planning.json、正式关卡.json 配套。`AuthoredSections` 内 prefab 是实际作者对象完整引用图；规划的 authoredSnapshotGuid/InstanceId/ObjectId 和精确位置偏移用于无损回采。导入片段重新分配实例组 ID，不能缺少快照资源。六份同目录 `.planning.json` 为最终 Scene 回采片段，可经拖动预览选择放置位置，不要使用旧 R 文件重建。

增量应用时快照按引用组原子更新，未改组保留已有实例，避免保留对象仍引用已删除旧组。现有全图重建与参数编辑回归保持位姿/门按钮/平台/曲柄/挂载/藤蔓引用一致。

## Boss 与验证边界

35份普通收集品按真实 Amount 总计；30%需11份，60%需21份。Boss入口使用已有门槛与 Start 返回，同场景 End 与八帧喷泉在末尾空房间，泉水仅表现、不代写结局判定。Start/End 各唯一，胶囊净空。Boss重生点排除地图传送；不把 Gaming 标成专用 Boss 场景。

隔离 Unity 验证：4343对象全量重建位姿与机关引用零差异；不变增量保留4343个实例；修改一组后原子替换并恢复零差异；最终275个通道格静态碰撞无墙阻挡；MRoom3三块齐全；实际渲染截图复核。Play检查唯一玩家/相机/UI/房间服务、35收集品、Boss零收集拒绝与Start返回、九个Boss重生点、End/泉水。最后连接标记调整仅完成静态复核，未人工走通全部关卡，未完整打Boss或演完两条结局；不等同完整通关或安卓真机测试。

最终又合入远端main `4b8a1ad` 单轮存档与开场提交，并重新通过上述Play专项；此前运行报告早于最终标记调整的限制已由这次重跑补齐，但仍不是完整通关。双端均以Gaming唯一场景、非Development正式构建成功，Windows 0 errors/6 warnings，Android 0 errors/9 warnings。D3D11发布包启动无异常。当时 APK 实际包名仍为历史值 `com.xingzhicai.game`、versionName0.2.1/code201、ARM64、debuggable=false、无uses-permission；当前工程已按 TapTap 后台改为 `com.miao1suki.shapeofcolor`，需重新构建验证。

构建目录：`Builds/形之彩-0.2.1-20261020-全关卡`。Windows整目录压缩包约97MiB，APK约77MiB；不要只分发Windows单个exe。构建未提交Git，源与成套依赖已提交。远端字体采用前，本地字体已备份到Library且保留在git stash，其他用户未提交内容未纳入本次范围。
