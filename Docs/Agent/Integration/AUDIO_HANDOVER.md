# 音频资源交接说明

当前仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。
分支：`音乐`。
音频文件位于 `Assets/_Project/Content/Audio/`，命名按 RVSA 规范（`snake_case`、类别前缀、受控词表）。资源目录为 `Music/`、`SFX/`，运行时 Cue 目录为 `Resources/AudioCatalog.asset`；`amb_` 前缀的环境声暂存于 `Music/`。

## 资源清单

### AMB — 环境声（暂存于 Music/）

| 文件 | 用途 |
|---|---|
| `amb_env_machine_hum_01.wav` | 机器运转低鸣，机关 / 工业区域底噪 |
| `amb_env_plant_rustle_01.wav` | 植物枝叶摩擦，植被区域底噪 |
| `amb_env_water_drip_01.wav` | 滴水声，潮湿 / 洞穴区域底噪 |
| `amb_env_wind_gust_01.wav` | 阵风声，开阔 / 高处区域底噪 |

循环属性已登记到 `AudioCatalog`，Unity 内仍需实际试听循环点。

### BGM — 音乐

| 文件 | 用途 | 循环 |
|---|---|---|
| `bgm_wn_01_opening.wav` | 开场主题曲，开始界面 / 新游戏流程 | 是 |
| `bgm_ambient_loop_01.mp3` | 环境铺底氛围 | 是 |
| `bgm_atmosphere_amin_03.mp3` | 氛围 A 小调，场景气氛垫 | 否 |
| `bgm_atmosphere_dsmin_02.mp3` | 氛围 升D 小调，场景气氛垫 | 否 |
| `bgm_atmosphere_emin_02_loop.mp3` | 氛围 E 小调 | 是 |
| `bgm_atmosphere_fmin_02_loop.mp3` | 氛围 F 小调 | 是 |
| `bgm_banjo_01.mp3` | 班卓琴曲，区域背景音乐 | 否 |
| `bgm_choices_01.mp3` | 抉择 / 菜单场景音乐 | 否 |
| `bgm_kb280_01.mp3` | 键盘旋律曲，场景背景音乐 | 否 |
| `bgm_music_box_01.mp3` | 八音盒主题，解谜 / 收集反馈 | 否 |
| `bgm_robot_sad_piano_01.mp3` | 悲伤钢琴，叙事 / 情感场景 | 否 |
| `bgm_vocal_chop_01.mp3` | 人声切片，节奏层 / 过渡 | 是 |
| `bgm_wake_up_01_loop.mp3` | 醒来 / 开场序列 | 是 |
| `bgm_alien_sacrifice_01.mp3` | 外星祭祀主题，重要事件场景 | 否 |

路径均为 `Assets/_Project/Content/Audio/Music/`。

### SFX — 音效

| 文件 | 用途 |
|---|---|
| `sfx_footstep_land_walk_01~03.wav` | 陆地行走脚步，3 个变体 |
| `sfx_footstep_water_walk_01~04.wav` | 水中行走脚步，4 个变体 |
| `sfx_gp_plr_jump.wav` | 玩家跳跃 |
| `sfx_gp_plr_climb_01.wav` / `_02.wav` | 玩家攀爬，2 个变体 |
| `sfx_gp_plr_die.wav` | 玩家死亡 |
| `sfx_gp_plr_respawn.wav` | 玩家重生 |
| `sfx_gp_plr_swim_01~03.wav` | 玩家游泳，3 个变体 |
| `sfx_gp_plr_water_enter.wav` | 玩家入水 |
| `sfx_gp_plr_water_exit.wav` | 玩家出水 |
| `sfx_gp_door_open.wav` | 开门 |
| `sfx_gp_door_close.wav` | 关门 |
| `sfx_gp_plant_grow.wav` | 植物生长 |
| `sfx_gp_steam_burst.wav` | 蒸汽喷发（瞬时） |
| `sfx_gp_steam_loop.wav` | 蒸汽持续（循环） |
| `sfx_gp_paint_acquire_initial.wav` | 首次获得上色能力（同路径修订） |
| `sfx_gp_paint_select.wav` | 选色 / 上色确认 |
| `sfx_gp_block_paint_complete.wav` | 方块上色完成 |
| `sfx_gp_crank_turn_loop.wav` | 曲柄 / 机关转动，循环 |
| `sfx_notice_deny.wav` | 操作被拒绝提示 |

路径均为 `Assets/_Project/Content/Audio/SFX/`。

## 运行时接入

`AudioCatalog.asset` 把 45 个音频登记为 37 个 Cue。代码不直接依赖文件名，而是传入稳定的 `AudioCueIds`：

```csharp
using Project.Audio;

AudioCuePlayer.Play(AudioCueIds.PlayerJump);
AudioCuePlayer.Play(AudioCueIds.NoticeDeny, .7f);
AudioCuePlayer.PlayLoop(AudioCueIds.SteamLoop, this);
AudioCuePlayer.StopLoop(this);
```

`AudioCuePlayer` 是玩法侧的统一入口；`GameAudioService` 也提供 `PlayCue`、`PlayCueLoop`、`StopLoop`、`IsLoopPlaying` 和 `GetLoop`。如需在 Inspector 中指定非默认目录，可给 `GameAudioService` 设置 `audioCatalog`；留空时自动读取 `Resources/AudioCatalog`。

通道规则：

- `Music`：开始菜单、区域 BGM、氛围音乐，受总音量和背景音乐滑杆影响。
- `SoundEffect`：玩家动作、UI、机关、环境 SFX，受总音量和音效滑杆影响。
- 循环 SFX 使用 owner 管理，owner 推荐传组件实例，避免同一机关叠音。
- 当前全部为 2D 声音，不使用 `spatialBlend`、距离衰减或 3D 跟随。

### 已接事件

| 触发时机 | Cue | 入口 |
|---|---|---|
| 开始菜单出现 | `music.menu.opening` | `StartMenuController.OnEnable` |
| 进入非菜单流程 | `music.ambient.loop` | `GameAudioService.HandleActiveSceneChanged` |
| 玩家跳跃 | `sfx.player.jump` | `PlayerController` 实际跳跃分支 |
| 玩家落地脚步 | `sfx.footsteps.land` | `PlayerController` 固定帧移动检测 |
| 玩家游泳 | `sfx.player.swim` | `PlayerController` 固定帧移动检测 |
| 玩家攀爬 | `sfx.player.climb` | `PlayerController` 进入及移动检测 |
| 玩家入水 / 出水 | `sfx.player.water_enter` / `sfx.player.water_exit` | `PlayerController` 水域状态切换 |
| 玩家死亡 / 重生 | `sfx.player.die` / `sfx.player.respawn` | `PlayerController.OnDied` / `RequestRespawn` |
| 开关门 | `sfx.door.open` / `sfx.door.close` | `DoorController` 状态切换 |
| 曲柄转动 | `sfx.crank.turn_loop` | `CrankPlatformFeature` 按住循环 |
| 蒸汽喷发 / 持续 | `sfx.steam.burst` / `sfx.steam.loop` | `WaterVisualFeature.TriggerSteamReaction` |
| 藤蔓生长 | `sfx.plant.grow` | `ClimbableVineFeature.GrowTo` |
| 获得颜色 | `sfx.paint.acquire` | `ColorKeyPickup.OnCollectionStarted` |
| 选色 / 上色成功 / 非法 | `sfx.paint.select` / `sfx.paint.complete` / `sfx.notice.deny` | `PlayerColorWheel` |

环境区域音、非菜单 BGM 切换和 UI 点击目前仍保留占位音，等关卡正式进入区域规划后再接。

## 待验证项

- [ ] Unity 导入检查：在 Unity 6000.3.12f1 中确认所有音频正确导入，无报错
- [ ] `.meta` 文件：首次导入后在 Unity 中生成，与音频文件一并提交
- [ ] 试听验证：逐条试听，确认音质与循环点衔接自然
- [ ] 环境声与 `sfx_gp_steam_loop`、`sfx_gp_crank_turn_loop` 的循环点确认
- [ ] 打包检查：确认音频压缩设置与项目打包配置一致
- [x] 播放接口扩展：随机变体、按 owner 循环、停止/查询循环音接口已加入 `AudioManager` 和 `GameAudioService`
- [x] Cue 目录：45 个音频登记为 37 个 Cue，Music / SoundEffect 通道已分流
- [x] 运行时接线：玩家动作、门、曲柄、蒸汽、藤蔓、颜色钥匙与选色反馈已接入

## 未解决问题

- `amb_` 前缀的环境声暂存于 `Music/`，如需严格按类别分桶可另开 `Ambience/` 目录
- `bgm_wn_01_opening` 的分段词不在 RVSA 允许的 `intro` / `loop` / `tail` 三件套内，后续如做正式交付建议改为 `bgm_wn_01_intro`
- 早期版本移除的 `sfx_ui_button_select`、`sfx_ui_notice_success_*`、`sfx_gp_water_enter_01`、`sfx_gp_water_splash_*` 已不在仓库，接入方需使用新命名
- 部分音效的导入压缩与音量需在 Unity 中实测后微调
- 不启用 3D 空间化；后续若需要空间音，必须先更新本交接和 skill 再改架构

---

*本文档随音频资源同步更新。*

整合说明：重命名资源沿用原 `.meta` GUID，新增资源补齐 `.meta`。既有场景引用不随文件名变化而丢失。
