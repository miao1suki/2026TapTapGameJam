# 音频资源交接说明

当前仓库：https://github.com/miao1suki/2026TapTapGameJam.git。
分支：codex/codex/taptap-music · 提交：2de6944
音频文件位于 Assets/_Project/Content/Audio/，命名遵循 RVSA 规范（snake_case、类别前缀、受控词表）。

## 资源清单

### BGM — 开场主题曲

| 文件 | 路径 | 用途 | 循环 | 格式 |
|---|---|---|---|---|
| gm_wn_opening_01.wav | Assets/_Project/Content/Audio/Music/ | 开始界面 / 新游戏流程 | 是（无缝循环） | WAV 48 kHz / 24 bit，13.2 MB |

### SFX — 脚步

| 文件 | 路径 | 用途 |
|---|---|---|
| sfx_footstep_land_walk_01.wav | Assets/_Project/Content/Audio/SFX/ | 陆地行走脚步 — 变体 01 |
| sfx_footstep_land_walk_02.wav | 同上 | 陆地行走脚步 — 变体 02 |
| sfx_footstep_land_walk_03.wav | 同上 | 陆地行走脚步 — 变体 03 |
| sfx_footstep_water_walk_01.wav | 同上 | 水中行走脚步 — 变体 01 |
| sfx_footstep_water_walk_02.wav | 同上 | 水中行走脚步 — 变体 02 |
| sfx_footstep_water_walk_03.wav | 同上 | 水中行走脚步 — 变体 03 |
| sfx_footstep_water_walk_04.wav | 同上 | 水中行走脚步 — 变体 04 |

### SFX — 水交互

| 文件 | 路径 | 用途 |
|---|---|---|
| sfx_gp_water_enter_01.wav | Assets/_Project/Content/Audio/SFX/ | 入水（从陆地进入水体时触发） |
| sfx_gp_water_splash_01.wav | 同上 | 水花溅射 — 变体 01 |
| sfx_gp_water_splash_02.wav | 同上 | 水花溅射 — 变体 02 |
| sfx_gp_water_splash_03.wav | 同上 | 水花溅射 — 变体 03 |

### SFX — UI

| 文件 | 路径 | 用途 |
|---|---|---|
| sfx_ui_button_select.wav | Assets/_Project/Content/Audio/SFX/ | UI 按钮选中 / 点击反馈 |
| sfx_ui_notice_success_01.wav | 同上 | 成功提示音 — 变体 01 |
| sfx_ui_notice_success_02.wav | 同上 | 成功提示音 — 变体 02 |
| sfx_ui_notice_success_03.wav | 同上 | 成功提示音 — 变体 03 |
| sfx_ui_notice_success_04.wav | 同上 | 成功提示音 — 变体 04 |

## 运行时接入

当前项目提供的播放接口位于 GameAudioService：

`csharp
// 播放音乐（传入 AudioClip，是否循环）
GameAudioService.PlayMusic(clip, loop: true);

// 播放音效（传入 AudioClip，音量倍率 0–1）
GameAudioService.PlaySound(clip, volumeScale: 1f);
`

### 预期接线

| 触发时机 | 文件 | 建议调用 |
|---|---|---|
| 开场场景加载 / 标题画面 | gm_wn_opening_01 | PlayMusic(bgm_wn_opening_01, loop: true) |
| 玩家在陆地移动时（步频发声） | sfx_footstep_land_walk_01-03 | PlaySound 随机变体 |
| 玩家在水中移动时（步频发声） | sfx_footstep_water_walk_01-04 | PlaySound 随机变体 |
| 玩家进入水体 | sfx_gp_water_enter_01 | PlaySound 入水瞬间 |
| 玩家在水中产生溅射 | sfx_gp_water_splash_01-03 | PlaySound 随机变体 |
| UI 按钮点击 | sfx_ui_button_select | PlaySound |
| UI 成功/完成反馈 | sfx_ui_notice_success_01-04 | PlaySound 随机变体 |

## 待验证项

- [ ] Unity 导入检查：在 Unity 6000.3.12f1 中确认所有 .wav 文件正确导入，无报错
- [ ] .meta 文件：首次导入后在 Unity 中生成，与音频文件一并提交
- [ ] 试听验证：在 Unity 中逐条试听，确认音质、循环点衔接自然
- [ ] 打包检查：确认音频压缩设置与项目打包配置一致

## 未解决问题

- 脚步的地面材质分类（land）为占位名，后续可根据游戏实际地面类型（stone/grass/wood 等）拆分更细的脚步声变体
- 部分音效的导入压缩/音量需要在 Unity 中实测后微调
- 音频系统的接线（场景引用、AudioMixer 路由、3D 空间化）由整合者完成，本分支不包含场景修改

---

*本文档随音频资源同步更新。*
