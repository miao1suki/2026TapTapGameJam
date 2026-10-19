# 2026TapTap 协作入口

Unity **6000.3.12f1**，禁止自动升级 Editor、Packages 或渲染管线。
比赛版本禁止非 TapTap SDK 的游戏运行时联网、局域网发现及遥测；当前未接入 TapTap SDK，游戏仍无网络权限，不得使用 Development/Profiler/Script Debugging 构建。唯一网页例外为用户明确要求的隐私政策：仅玩家主动点击时用系统浏览器打开固定 HTTPS 政策页，不使用 WebView 或自动请求。见 `Docs/Developer/OFFLINE_BUILD.md`；不能借此例外引入其他联网组件。
颜色钥匙与玩家资源最新约定：必须读取 `Docs/Agent/Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md`。同色首把只解锁场景颜色，第二把才解锁玩家能力；唯一玩家来源为 `Assets/_Project/Content/Player/Player.prefab`，禁止重建旧的两份玩家。
唯一工作仓库：**https://github.com/miao1suki/2026TapTapGameJam.git**。
本机主项目：`D:\.unity\2026TapTap`；其他成员可使用自己的克隆目录。

每次任务开始，以及提交前，必须重新读取以下持续更新的文件，不使用聊天记忆替代：

1. `Docs/Agent/PROJECT_CONTEXT.md`
2. `Docs/Agent/WORKING_AGREEMENTS.md`
3. `Docs/Agent/ACTIVE_WORK.md`
4. `Docs/Agent/SYSTEM_INDEX.md`
   正式关卡工作另须读取 `Docs/Agent/Integration/FORMAL_LEVEL_DELIVERY.md`：唯一正式玩法为新版 `Gaming.unity`，配套 `Gaming.planning.json` 与 `Content/LevelMaps/正式关卡.json`。仍使用旧 Gaming 的成员须先同步这次 main 基线，再继续制作；禁止以旧场景、旧存档或 Library 缓存覆盖新版本。正式关卡工作者只拉取、制作并推送自己的分支，只有用户当前整合窗口负责合并和推送 main。
5. 开发颜色系统、颜色物体、万能方块、钥匙、房间重置或交互图迁移时，必须先读 `.agents/skills/taptap-color-gameplay/SKILL.md`，并完整读取其中指定的 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。
6. 做编辑器 UI 时另读 `Docs/Agent/EDITOR_UI_STANDARD.md`。
7. 修改玩家屏幕提示、按键说明、搬运提示、复活提示或 `WordNote_UI` 时，必须先读 `.agents/skills/taptap-word-note/SKILL.md`，并完整读取其中指定的 `Assets/_Project/Code/Systems/WordNote/SKILL.md`。
8. 新增或修改任何游戏内文字、UGUI/TMP 文本、开始菜单、暂停界面、HUD、字幕、提示、成就、设置或动态生成文本时，必须先读 `.agents/skills/taptap-typography/SKILL.md`。
9. 修改场景启动、MainMenu、Gaming、场景跳转、关卡入口、玩法相机随行或暂停预制体接线时，必须先读 `.agents/skills/taptap-scene-flow/SKILL.md`。
10. 新增、迁移或修改 Boss 出场、Boss 死亡、处决、真假结局及其 Timeline Playable 时，必须先读 `.agents/skills/taptap-boss-timeline-migration/SKILL.md`。Timeline 演出只允许使用触发器/Director 的局部坐标；现有通用轨道不足时，在 Playable 轨道放自制脚本，不改 Timeline 工具包来写 Boss 专属逻辑。

先验证当前目录是 Unity 项目且 `git remote get-url origin` 为上述仓库；不匹配立即停止写入和推送。
不在旧项目实现功能，不向旧仓库推送，不复制整个旧 Assets/ProjectSettings。
分支工作者读取 `.agents/skills/taptap-git-worker/SKILL.md`，只提交并推送自己的分支，由整合者合并。
交付或更新音乐、音效资源的成员还须读取 `.agents/skills/taptap-audio-worker/SKILL.md`。
任务结束更新相关功能文档和 `ACTIVE_WORK.md` 中自己的交接记录；不得覆盖其他人的记录。

关卡交付必须包含可编辑存档：场景及 `.meta`、同名 `.planning.json` 及 `.meta`、新增资源依赖一起提交，不能只提交生成后的场景。存档不得只留在 Library；提交前运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`。小关卡通过“协作与片段”导出/导入，操作见 `Docs/Developer/LEVEL_EDITOR_COLLABORATION.md`。

维护源码、场景和 `.meta` 一起提交。不得提交私钥、凭据、Library、Temp、Logs、Builds 或本地用户设置。
多人各自创建自己的场景，不采用旧地图总拼、代理同步或对象锁方案。
Boss 场景的所有专属逻辑、组件、Editor 工具和测试脚本统一放在 `Assets/Boss_Logic`；Boss 场景与 `.planning.json` 只能引用该目录中的 Boss 专属脚本。项目公共框架和可复用系统脚本保持原归属，不因 Boss 使用而搬入该目录。
<!-- UNITY CODE ASSIST INSTRUCTIONS START -->
- Project name: 2026TapTapGameJam
- Unity version: Unity 6000.3.12f1
- Active game object:
  - Name: PauseScreen
  - Tag: Untagged
  - Layer: Default
<!-- UNITY CODE ASSIST INSTRUCTIONS END -->
