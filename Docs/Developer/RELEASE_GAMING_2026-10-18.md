# Gaming 双端紧急测试包

地图采用策划最新56126c3，仅接入地图布局及其存档；新版透明主菜单、同场景开始/返回和摄像机动画保留本地实现。两个构建的唯一启动场景都是Assets/_Project/Scenes/Playing/Gaming.unity。不打包旧MainMenu，不通过换场景开始/返回。

## 产物

- Android：Builds/形之彩-20261018-Gaming正式测试/Android/形之彩.apk，79,741,776字节，ARM64 IL2CPP，0错误/9警告。
- Windows：Builds/形之彩-20261018-Gaming正式测试/形之彩-Windows.zip，100,248,545字节，0错误/5警告。解压后运行Windows/形之彩.exe，不能只复制exe。
- APK SHA256：DE852BC84624FBD14A0E3548B734B05BF0754111CEDE0FB687AFB9B15613BB51。
- ZIP SHA256：509741B84415A2A16B3F2FC6395E1E1CF499BB35ED4C770ED8AD54B7E22B0973。

移动端采用美术新图左侧方形方向/跳跃键（30×30），动作键保持原画比例；正常/按下状态已接入，不改变触摸容差、布局或多指输入。双端图标使用Resources/临时ICON.png。接触方块切片逐像素与原图一致，四边均为原画1像素暗底，本轮不擅改源画。

## 验证边界

两端均非Development，无Profiler/ScriptDebugging。最终APK经aapt确认没有uses-permission，debuggable=false，usesCleartextTraffic=false，隐私Activity为Launcher，Unity活动不导出；apksigner验证v2签名通过。Windows实际启动烟测无异常日志。没有Android真机、完整关卡通关或新20房间图的完整性能验收。

Windows测试快照使用1280×720可缩放窗口，项目默认显示设置未被该隔离测试设置覆盖。警告主要为旧RenderPipeline回调弃用、已有Shader编译提示；不是0警告构建。

旧6套“形之彩-*”包已从Builds移动至Library/PreviousBuilds-20261018，不再与新包混放，仍可恢复。验证日志在Library/RoomPhysics-20261018。

## 后续事项

- Map14有7个未绑定按钮及尚未明确的新增信号，按策划交付保留，见MVMAP14_LAYOUT.md。
- 新横/竖1/3/5格平台形状基本验证通过；连续单格平台合并专项在记录阶段被拒，原因待后续定位，不能宣称已通过。
- 蓝色点击绿块使用安全占位暂停策略；旧推挤策略已在出包前移除。终版专项第四轮220项通过，包含4方向×3格尺寸、实际鼠标命中、锁轴/单格改向、真实玩家阻挡与清障、R/死亡/离区。此前失败记录保留，最后一轮仅修夹具，未改包内生产代码。
- 用户未提交MRoom2/MRoom3/Pick、字体、Boss Timeline及其他无关文件不纳入发布提交。关卡编辑后仍须交Scene、同名planning、meta与命名正式存档成套。
