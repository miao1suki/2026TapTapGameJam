# 项目上下文（持续更新）

项目：2026TapTap；远程：`https://github.com/miao1suki/2026TapTapGameJam.git`。
Unity 6000.3.12f1，URP、新 Input System。模板项目的设置保留，未导入旧项目设置。

当前是一份从旧项目筛选而来的基础框架，不是旧游戏整体复制。
保留：启动与多场景生命周期、场景导航、基础玩家控制、输入与重绑定、双端 UI 预设、相机 Manager/跟随/模式转换、成就、Timeline 演出/战斗、表面瓦片工具、方块功能组件契约、不规则精灵切片。
相机转换可由 PlayerInputDriver 通过 CameraModeSwitch 申请切换；PlayerController 不读取输入，Timeline 演出保持更高优先级。
对话系统尚未开发，不存在可接入的对话模块。

基础迁移时排除：绳/线、梯子、移动平台玩法、视差平台投影碰撞/深度吸附、2D小拼/总拼/3D合并、旧地图代理同步、旧关卡、旧测试脏数据、美术/音效素材、瓦片库与烘焙贴画、QQ bot 运维源码和私密配置。之后由音频成员在新项目单独交付的 `Assets/_Project/Content/Audio` 不属于“旧素材迁移”，已纳入当前仓库。
成就中的旧玩法信号名字保留仅为兼容枚举/协议，不代表玩法可用。不要重新引入对应实现。
例外：关卡编辑器开发目录中的 `TileLibrary/26TAPTAP.png` 是用户明确要求恢复的源图，只允许用于该开发工具，不扩散到正式美术目录。

## 目录约定

- `Assets/_Project/Code/Systems/<Feature>`：功能源码，Runtime/Editor/Tests 分开。
- `Assets/Player`：基础玩家及 Timeline 动作适配。
- `Assets/GJ_Tools/TimelineTools3D`：保留的旧 Timeline 工具（兼容路径）。
- `Assets/_Project/Scenes/Bootstrap`、`Systems`、`Flow`：启动、常驻系统、流程。
- `Assets/_Project/Scenes/Levels/Level_XX`：按关卡组织，不按人员职位。
- `Assets/_Project/Development/<Feature>/<AuthorOrTask>`：独立实验；不默认加入正式构建。
- `Assets/_Project/Content`：正式流程的 prefab、catalog 和配置。
- `Assets/_Project/Content/Audio/Music` 与 `SFX`：游戏实际使用的音频及其 `.meta`；音频成员按 `.agents/skills/taptap-audio-worker/SKILL.md` 交付。
- `Assets/_Project/Art/ArtSource`：未来导入的原始美术。
- `Assets/_Project/Art/Generated`：工具产物；生成后检查并提交依赖。
- `Docs/Agent`：agent 的统一交流、约定和状态；每次重新读取。
- `Docs/Agent/Integration`：面向 agent 的接入说明。
- `Docs/Developer`：面向开发者/策划的使用说明。

Unity 模板的 `SampleScene` 暂保留为模板参考，不属于正式流程。
