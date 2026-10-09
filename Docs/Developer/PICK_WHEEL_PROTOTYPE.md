# Pick 三色轮盘原型

打开 `Assets/Scenes/Pick.unity` 并进入 Play Mode。场景内的 `TestPlayer` 使用正式玩家预制体与现有 `PlayerInputDriver`。呼出轮盘、确认、取消、移动与重生均沿用项目输入配置；此原型不定义或改动任何键位。鼠标指向扇区或其向外延伸的方向时，该扇区会缩小并硬切到半透明白色；中心留白是取消选择的死区。

未解锁扇区保留位置并使用项目内 Cubic-11 像素字体显示“？”，不能确认。颜色解锁读取正式的 `ColorRuntimeService` 数据；可在场景中放置现有颜色钥匙测试解锁后的状态。原型不提供独立的解锁或重置热键。

轮盘直接使用 `Assets/_Project/Resources/UI/轮盘.psd` 原有的三块黑色扇区；运行时按区域拆出同一张图，不再在其上叠绘另一套彩色扇区。每块以自身实际可见像素的重心等比缩放；指向时硬切为白色半透明，缩放使用 DOTween。`PickWheelPrototype` 作为 `PlayerColorWheel` 的外部视图接入，选色、解锁校验及物体染色仍走正式玩家逻辑。Pick 场景还放有供测试玩家站立的地面；原 `Test` 场景不受影响。

当前只在 Pick 场景绑定这套外观，其他场景仍使用原有轮盘；未复制 UV 仓库代码（仓库链接当前无法访问）。分支交付 DOTween 免费版核心及其原始 readme；本机 DOTween Pro 不进入仓库。正式合入前仍须在另一份克隆中复核 Unity 编译与 Play Mode。
