# 2026TapTap 协作入口

Unity **6000.3.12f1**，禁止自动升级 Editor、Packages 或渲染管线。
唯一工作仓库：**https://github.com/miao1suki/2026TapTapGameJam.git**。
本机主项目：`D:\.unity\2026TapTap`；其他成员可使用自己的克隆目录。

每次任务开始，以及提交前，必须重新读取以下持续更新的文件，不使用聊天记忆替代：

1. `Docs/Agent/PROJECT_CONTEXT.md`
2. `Docs/Agent/WORKING_AGREEMENTS.md`
3. `Docs/Agent/ACTIVE_WORK.md`
4. `Docs/Agent/SYSTEM_INDEX.md`
5. 做编辑器 UI 时另读 `Docs/Agent/EDITOR_UI_STANDARD.md`。

先验证当前目录是 Unity 项目且 `git remote get-url origin` 为上述仓库；不匹配立即停止写入和推送。
不在旧项目实现功能，不向旧仓库推送，不复制整个旧 Assets/ProjectSettings。
分支工作者读取 `.agents/skills/taptap-git-worker/SKILL.md`，只提交并推送自己的分支，由整合者合并。
交付或更新音乐、音效资源的成员还须读取 `.agents/skills/taptap-audio-worker/SKILL.md`。
任务结束更新相关功能文档和 `ACTIVE_WORK.md` 中自己的交接记录；不得覆盖其他人的记录。

维护源码、场景和 `.meta` 一起提交。不得提交私钥、凭据、Library、Temp、Logs、Builds 或本地用户设置。
多人各自创建自己的场景，不采用旧地图总拼、代理同步或对象锁方案。
