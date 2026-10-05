# 音频资源交接

仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。资源来自 `codex/codex/taptap-music` 分支（最终提交 `b22df5b`），位于 `Assets/_Project/Content/Audio`。整合时由 Unity 6000.3.12f1 为每个文件生成并提交同名 `.meta`。

## 已交付资源

- `Music/bgm_wn_opening_01.wav`：开场主题曲，供主菜单／新游戏流程选用；48 kHz、24 bit。循环点仍须试听确认。
- `Music/*.mp3`：另外 13 首 BGM，文件名为 `bgm_alien_sacrifice_01`、`bgm_ambient_01_loop`、`bgm_atmosphere_amin_03`、`bgm_atmosphere_dsmin_02`、`bgm_atmosphere_emin_loop_02`、`bgm_atmosphere_fmin_loop_02`、`bgm_banjo_01`、`bgm_choices_01`、`bgm_kb280_01`、`bgm_music_box_01`、`bgm_robot_sad_piano_01`、`bgm_vocal_chop_01`、`bgm_wake_up_loop_01`，扩展名均为 `.mp3`。
- `SFX/sfx_footstep_land_walk_01`–`03.wav`：陆地脚步变体。
- `SFX/sfx_footstep_water_walk_01`–`04.wav`：水中脚步变体。
- `SFX/sfx_gp_water_enter_01.wav`、`sfx_gp_water_splash_01`–`03.wav`：入水与水花。
- `SFX/sfx_ui_button_select.wav`、`sfx_ui_notice_success_01`–`04.wav`：UI 选择和成功提示。

文件名按类别前缀和 snake_case 组织。当前只导入资源，尚未将这些 AudioClip 绑定到场景、脚步、入水或 UI 事件；不要把“资源已导入”当成“游戏里已播放”。

## 程序接入

现有 `Project.GameFlow.GameAudioService` 是实例服务，使用 `GameAudioService.Instance`，并在有效实例存在时调用：

```csharp
GameAudioService.Instance.PlayMusic(musicClip, loop: true);
GameAudioService.Instance.PlaySound(effectClip, volume: 0.8f);
```

具体触发点由玩法／UI 负责人接线：开场曲可用于主菜单；脚步按陆地／水中状态和步频选择变体；进入水体时播放入水音效；按钮和完成提示由 UI 事件调用。音量、Mixer 路由及 2D／3D 空间化尚未定稿，不在本次资源合并中预设。

## 整合验证与后续

- Unity 导入：14 首 Music、16 个 SFX 均生成 `.meta`，编辑器 Console 0 警告／0 错误。
- 未验收：逐条试听、循环点、移动端压缩与最终包体大小；这些需要音频成员和整合者在接线／打包时复核。
- `land` 是当前陆地脚步分类，未来若区分石／草／木材，可新增命名明确的变体。
