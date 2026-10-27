# 协作约定

每个任务必须确认项目目录、远程仓库、Unity版本、工作树状态，重新读取项目入口文档。
分支工作者使用 `codex/<姓名或agent>/<任务>` 或已有团队约定独立分支；不得直接推 main、合并其他分支、强推或重写历史。
整合者负责验证与合并。用户在当前窗口要求整合时，可以在验证后将任务快进/合并到 main；不要把这项权限传给其他 worker。

修改前在 ACTIVE_WORK 记录范围、正在编辑的场景和接口；遇到重叠先协调，不覆盖他人的改动。
场景按关卡分目录，每人独立 Scene 工作。不要自动替换已保存的场景，也不要复制旧测试场景进正式关卡。
正式关卡例外与交付基线见 [FORMAL_LEVEL_DELIVERY.md](Integration/FORMAL_LEVEL_DELIVERY.md)：后续正式制作使用新版 Gaming 及其配套存档；仍在旧 Gaming 上工作的成员先同步新版，不能提交旧关卡覆盖正式进度。策划可在自己的任务分支继续维护正式关卡，合并与 main 推送仍仅由用户当前整合窗口完成，不向其他 worker 转授。
避免跨功能强依赖：输入经 GameInput，实际 Camera 写入经 CameraControlManager，游戏流程经 GameFlowController。
未确认的功能迁移先询问；不得把旧玩法顺手带入。旧 API 只有文档明确标注的可用子集有效。

Editor UI 必须 UI Toolkit；场景 HUD 可拖动、折叠、关闭和重新打开；EditorWindow 使用 Unity 原生拖动/关闭、Foldout 管理内容。
运行时 UI 可使用现有 UGUI，不因此强制重做运行时技术栈。
功能介绍描述当前产品行为，不写对话历史、临时争论或“你之前说”。

完成时验证编译；修改 Runtime 时运行相关测试，并在风险匹配时验证打包。
比赛期间游戏禁止非 TapTap SDK 联网。当前 TapTap 成就接入只允许 Android 的 `INTERNET` 与 `ACCESS_NETWORK_STATE`，且必须在原生隐私同意、Unity 启动后初始化；其余网络权限继续移除。构建保持非 Development；不得开启调试发现、Unity 遥测或广告。用户明确授权的唯一网页例外为主动点击隐私政策时调用系统浏览器打开固定 HTTPS 页面，不在游戏内发请求、嵌 WebView 或自动联网。新增 SDK 需专项审计，规则见 `Docs/Developer/OFFLINE_BUILD.md`。
提交说明列出变更、验证、未验证项目与阻塞。文档随代码一起提交。
Git 忽略目录中放本地日志/验证产物；凭据不得进入项目，包括未跟踪文件。

## 关卡协作存档

关卡 Scene 是布局、对象、实例参数和 Unity 引用的唯一编辑权威。交付必须同时提交 Scene/`.meta`、Scene 引用的 GridArt Map 与稀疏区域归属资产/`.meta`、新增 Palette/Prefab/美术依赖；不得把作者数据只留在 Library。旧 `.planning.json` 与命名正式关卡 JSON 仅作历史只读基线，不再同步、不参与构建或提交门禁，禁止据此重建覆盖当前 Scene。区域协作是在同一 Scene 内整体平移区域及所属对象/已认领美术，不再导入导出 JSON 片段。提交前运行 `Ops/LevelMaps/Test-PlanningMapCommit.ps1`（兼容文件名）；新克隆执行 `git config core.hooksPath .githooks`，已有 hook 则组合调用，不覆盖原 hook。当前操作见 [场景关卡编辑器](../Developer/SCENE_LEVEL_EDITOR.md)。
