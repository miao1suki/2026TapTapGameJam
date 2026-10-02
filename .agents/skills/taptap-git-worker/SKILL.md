---
name: taptap-git-worker
description: 在 2026TapTapGameJam 中克隆、更新、创建任务分支、提交并推送自己的 Unity 任务；只作分支工作者，不合并或直接推送 main。
---

# 分支工作者

唯一远程：`https://github.com/miao1suki/2026TapTapGameJam.git`。
本机项目示例：`D:\.unity\2026TapTap`。其他电脑使用自己的克隆目录；必须有 Assets/Packages/ProjectSettings。
严禁在 `2026Test` 项目实施任务或向它推送。这不是允许 worker 合并的技能。

每次任务开始和提交前重新读根 AGENTS.md，以及 Docs/Agent 下 PROJECT_CONTEXT、WORKING_AGREEMENTS、ACTIVE_WORK、SYSTEM_INDEX；制作编辑器 UI 时另读 EDITOR_UI_STANDARD。文件可能持续更新，不使用旧聊天摘要替代。

## 克隆与更新

新克隆：`git clone https://github.com/miao1suki/2026TapTapGameJam.git <你的目录>`。
在实际工作目录检查 `git remote get-url origin`、`git status --short`、`git branch --show-current`、ProjectVersion。
远程不同立即停止，询问整合者；不要猜路径或改 origin 规避问题。
`git fetch origin --prune`。干净新任务：从最新 origin/main 创建 `codex/<成员>/<任务>`，例如 `git switch -c codex/alice/input-ui origin/main`。
已有任务：切回自己的分支。只对自己的干净分支使用 `git pull --ff-only`；分叉或冲突交给整合者，不自行合并、rebase或强推。
有未提交更改先核实归属，不能 reset/clean/stash 他人的文件。Unity未保存Scene也算待保留的工作，先提醒本人保存。

## 开发与推送

Unity固定6000.3.12f1，遵循模块及场景目录；保持.meta，场景独立制作。
提交只显式暂存任务文件；`git diff --cached` 检查没有Library/Temp/Logs/Builds、凭据、旧玩法和他人的更改。
运行相关验证；更新功能文档与自己的ACTIVE_WORK条目。`git commit -m "feat(scope): description"`。
推送前再次确认 origin精确地址及当前分支不是main；`git push -u origin <自己的分支>`。
不直接推main，不自行合并，不自动建PR（除非用户要求）。
交接：分支、SHA、修改范围、Unity版本、验证结果、需整合者处理的依赖/冲突。
