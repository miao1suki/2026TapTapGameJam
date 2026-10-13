---
name: taptap-audio-worker
description: 为 2026TapTapGameJam 交付和更新音乐、音效资源时使用；在自己的 Git 分支中导入 Unity 音频与 .meta，核对二进制文件和播放效果，再交给整合者。不得直接推 main 或自行接线游戏场景。
---

# 音频资源分支工作者

适用于音乐、音效的新增、替换和导入设置调整。唯一仓库是 `https://github.com/miao1suki/2026TapTapGameJam.git`，Unity 固定 **6000.3.12f1**。先完整阅读项目根目录 `AGENTS.md`、其中列出的持续更新文档，以及 `.agents/skills/taptap-git-worker/SKILL.md`；本 skill 只补充音频规则，不取代通用分支流程。远程、项目或版本不符就停止。

## 交付边界

- 游戏实际使用的文件放在 `Assets/_Project/Content/Audio/Music/` 或 `Assets/_Project/Content/Audio/SFX/`；需要更细分类时先沿用已有目录，不按人员名字或日期分目录。首次导入时用 Unity 创建目录和 `.meta`。
- 同一声音的修订沿用原路径和文件名，保留对应 `.meta` 与 GUID；确需改名或移动时在 Unity 中操作，并检查引用。不要把 `最终版2`、`new` 一类临时版本一起提交。
- 可提交游戏需要的音频文件及其 `.meta`、经约定的音频配置。DAW 工程、分轨、原始录音、试听稿和批量导出缓存默认不进 Unity 仓库；需要团队长期保存时先与整合者约定独立存储和版本标识。
- 不自行改 `Systems_Audio.unity`、Timeline、关卡场景或 AudioMixer 接线。当前声音统一登记在 `Assets/_Project/Content/Audio/Resources/AudioCatalog.asset`，通过 `AudioCuePlayer.Play(cueId)`、`AudioCuePlayer.PlayLoop(cueId, owner)` 和 `AudioCuePlayer.StopLoop(owner)` 调用；`GameAudioService` 提供同样的 Cue API 并保留直接 AudioClip 接口。
- 音频分为 `Music` 与 `SoundEffect` 两个通道：音乐只受总音量和音乐滑杆影响，音效和循环机关声只受总音量和音效滑杆影响。开始界面和 ESC 设置里的滑杆直接控制实际运行时音量，不再走预览数据。
- 当前项目不使用 3D 空间音。不要给现有 Cue 流程补 `spatialBlend`、距离衰减、跟随声源或 AudioMixer 3D 路由；玩法循环声按 owner 停止即可。

## 二进制与版本控制

- 当前仓库使用普通 Git 管理小型音频，不启用 LFS。导入前查看单文件大小和本次总量；大型无损文件、频繁改动的大文件，或接近 GitHub 普通 Git 的 50 MiB 警告 / 100 MiB 拒收界限时，先与整合者决定是否启用 LFS，**不要自行修改 `.gitattributes` 或迁移历史**。
- LFS 必须先由整合者统一配置并告知所有协作者，再提交受其跟踪的文件；不能把指针文件当成实际音频交接。
- 只显式暂存本任务的音频文件、对应 `.meta` 和获准修改的配置/文档；检查 `git diff --cached --stat`、`git diff --cached --name-status`。不提交 Library、Temp、Logs、构建产物、凭据或其他人的场景与资源。

## 验收与交接

在 Unity 中确认新文件能导入和试听；替换时核对引用未丢失，并在 `AudioCatalog.asset` 中同步 Cue 名称、通道、循环和音量。音乐说明是否循环及循环衔接，音效说明触发用途；报告峰值、音量、导入压缩等已知问题，但不擅自统一修改全项目设置。提交前按通用 skill 复查工作树、远程、项目文档及自己的 `ACTIVE_WORK` 记录，只推自己的任务分支。

交给整合者：分支、提交 SHA、资源路径清单、每条声音的用途及预期触发点、循环要求、Unity 试听结果、待接线项和未解决问题。请整合者合并并完成场景/代码接入；不要直接推 `main`、强推、合并或改写历史。
