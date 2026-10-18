# 项目上下文（持续更新）

项目：2026TapTap；远程：`https://github.com/miao1suki/2026TapTapGameJam.git`。
Unity 6000.3.12f1，URP、新 Input System。模板项目的设置保留，未导入旧项目设置。

场景美术统一16个有效美术像素对应1格。编码尺寸需扣除已验证的导出倍率（tail为4倍），不拉伸以迁就碰撞；24像素玩家外观为1.5格而物理仍1格。修改物体、关卡预览、贴花或背景前读 [美术换算约定](../Developer/ART_GRID_CONVENTIONS.md) 和 [工作台换算](../Developer/SCENE_ART_PIXEL_CONVERSION.md)。基座默认有重力，可实例关闭；MRoom2第二版三基座保持无重力。新增颜色装饰仅归属颜色层，不是可交互颜色物体，不能授予能力或参与房间玩法重置。

当前是一份从旧项目筛选而来的基础框架，不是旧游戏整体复制。
关卡自动同步的删除会话缓存必须与采集时文档版本绑定；重导入或共享存档更新不能沿用旧版ID/格推断删除。生成实例重建先重新识别，避免墙、水和机关引用再次误删。MRoom2第二版已从完整副本恢复；打开旧内存场景时先重新加载，不保存覆盖。详见 `Integration/PLANNING_COLLABORATION_API.md`。
正式关卡已增量整合策划分支 `codex/level/main-20261017-03` 的 `eb76949` 交付（17房间，房间12已有内容，新增13～17）：`Scenes/Playing/Gaming.unity`、同名 `Gaming.planning.json` 与 `Content/LevelMaps/正式关卡.json` 成套维护（均在 `Assets/_Project` 下）。保留main原有修复、房间9的平台去重及独立单格16格路线，不全量重建。Gaming 是正式打包游玩场景，不是临时测试关卡；后续变动在整合main继续。旧 Gaming 使用者须同步新版场景和两份存档，不可用策划原始旧文件覆盖本地修复；仅当前整合窗口合并 main，其他成员只拉取和推送任务分支。完整约定见 [正式关卡交接](Integration/FORMAL_LEVEL_DELIVERY.md)。
当前唯一标准玩家为 `Assets/_Project/Content/Player/Player.prefab`（1×1、中心归零），各场景共用；旧RuntimePlayer/Player(暂定)已退役。同色第一把钥匙只解锁场景物体，第二把授予玩家颜色能力，两种状态独立、房间重置保留。接入必读 `Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。
保留：启动与多场景生命周期、场景导航、基础玩家控制、输入与重绑定、双端 UI 预设、相机 Manager/跟随/模式转换、成就、Timeline 演出/战斗、表面瓦片工具、方块功能组件契约、不规则精灵切片。
相机转换不再由 PlayerInputDriver 申请；Tab 的调色动作只控制染色轮盘，F 负责交互目标与搬运，Q/E 负责左右放下，左键负责已选颜色的染色，R / 手柄右摇杆对应“重生”，存活或死亡均可返回最近触碰的锚点并恢复相关房间/通道。门、按钮、已拾取钥匙及解锁进度保留。能源方块和万能方块共用 CarryableFeature；输入仍只通过 PlayerInputDriver 调用 PlayerController，Timeline 优先级不变。

门按钮默认一次按下保持，持续压住模式则在松开、R 或离区时重置，取消持续信号会关门但保留其他保持按钮进度。三色接触方块/可搬运方块禁止跨布置区域迁移，跨区放下被拒绝、越界回到作者位置；玩家离区取消携带并复原来源区。
Timeline 现提供独立对话轨道：轨道不绑定对象，直接向常驻 `WordNote_UI` 申请使用权，与原 API 消息共用同一画布、位置队列、堆叠和动画系统。Timeline 只负责申请和释放本次消息，不接管玩家、颜色系统或房间状态。
Boss 入场先由覆盖全程的 `PlayerTimelineTrack` 按 `Player` Tag 锁住玩法操作，相机轨延迟 0.1 秒再启动，并由末尾 Playable 同时激活血条和 `BossCameraSize`。视野扩大通过 `CameraControlManager` 的独立镜头倍率请求平滑生效；Timeline 相机优先级更高时不覆盖演出，玩家死亡复活不撤销。Boss 被击败后由 `Boss_defeat.playable` 先保持原镜头发呆 1 秒，再用 1 秒平滑框住玩家与 Boss 的中点，随后播放 3.5 秒六钥匙吸收并接死亡位置停留与回程；普通外部删除不触发。
重生点组件可直接标记为 Boss 关卡重生点，并保存复活后的 Boss 血量与相对 X。复活链在房间重置前先发 Boss 重置请求；Boss 由 Timeline 后生成，不建立启动时引用：控制器启用时自注册，重生点先保存待应用状态，Boss 出现后自动应用并回读血量/坐标。关卡参数窗口生成通过稳定 ID 连接的规划标记格，标记格只允许 X 拖动。实际 Scene 附属标记无实体，删除后自动补建。
Boss 战复活另有兜底：同场景存在活动 Boss 且当前没有已解锁的 Boss 关出生点时，玩家复活会改选同场景 X 最小的启用 Boss 关出生点；该点无需预先解锁，但仍执行原有安全落位与 Boss 状态重置。
Boss HUD 使用 `BossDisplayName` 在分段血条左侧显示 Cubic_11 SDF 名字；同 Canvas 的 `BossMonitorUI` 通过独立正交相机和 RenderTexture 在屏幕左上角显示约 `23.3%×35%` 屏幕的实时 Boss 窗口，从中心展开、倒放退场，并在死亡延迟 5 秒后与血条同步隐藏。监控相机渲染除 UI 外全部图层，窗口比例、纹理尺寸和渲染分辨率均按 Canvas 实际尺寸自适应。
Boss 从中心向玩家连接火线、水线、藤蔓线三套现有样式；三份独立运行时材质分别设置 `_Style=1/2/3`。距离越近越粗越明显，最近达到普通线条 1.5 倍。连线随血条激活生成，藤蔓沿线生长，Boss 死亡时按交互线同样的噪波溶解退场。Boss 接触玩家会直接触发玩家死亡。

基础迁移时排除：绳/线、梯子、移动平台玩法、视差平台投影碰撞/深度吸附、2D小拼/总拼/3D合并、旧地图代理同步、旧关卡、旧测试脏数据、美术/音效素材、瓦片库与烘焙贴画、QQ bot 运维源码和私密配置。之后由音频成员在新项目单独交付的 `Assets/_Project/Content/Audio` 不属于“旧素材迁移”，已纳入当前仓库。
成就中的旧玩法信号名字保留仅为兼容枚举/协议，不代表玩法可用。不要重新引入对应实现。
例外：关卡编辑器开发目录中的 `TileLibrary/26TAPTAP.png` 是用户明确要求恢复的源图，只允许用于该开发工具，不扩散到正式美术目录。

## 目录约定

- `Assets/_Project/Code/Systems/<Feature>`：功能源码，Runtime/Editor/Tests 分开。
- `Assets/Player`：基础玩家及 Timeline 动作适配。
- `Assets/GJ_Tools/TimelineTools3D`：保留的旧 Timeline 工具（兼容路径）。
- `Assets/_Project/Scenes/Start/MainMenu.unity`：仅旧编辑器兼容入口，新美术界面预览，运行重定向 Gaming，不进入正式构建。
- `Assets/_Project/Scenes/Playing/Gaming.unity`：唯一正式启动与玩法场景，持有玩家、玩法相机和共享暂停 UI；启动前帧自动安装共享 `Resources/UI/WorldMainMenu.prefab`。
- 正式流程为同一 Gaming 世界内的 MainMenu/Gaming 两个逻辑态。开始、返回、再次开始都不加载或卸载场景，不重置进度；相机通过同一 Manager 租约平滑入退。Bootstrap、旧五个 Systems 场景、旧 `Flow` 目录、Level_01～03 和 Ending 均退役，不再恢复。
- `Assets/_Project/Development/<Feature>/<AuthorOrTask>`：独立实验；不默认加入正式构建。
- `Assets/_Project/Content`：正式流程的 prefab、catalog 和配置。
- `Assets/_Project/Content/Audio/Music` 与 `SFX`：游戏实际使用的音频及其 `.meta`；音频成员按 `.agents/skills/taptap-audio-worker/SKILL.md` 交付。
- `Assets/_Project/Art/ArtSource`：未来导入的原始美术。
- `Assets/_Project/Art/Generated`：工具产物；生成后检查并提交依赖。
- `Docs/Agent`：agent 的统一交流、约定和状态；每次重新读取。
- `Docs/Agent/Integration`：面向 agent 的接入说明。
- `Docs/Developer`：面向开发者/策划的使用说明。

Unity 模板的 `SampleScene` 暂保留为模板参考，不属于正式流程。
