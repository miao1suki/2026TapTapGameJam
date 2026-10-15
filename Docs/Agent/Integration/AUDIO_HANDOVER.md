# 音频资源交接说明

当前仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。
分支：`codex/codex/taptap-music`。
音频文件位于 `Assets/_Project/Content/Audio/`，命名按 RVSA 规范（`snake_case`、类别前缀、受控词表）。仓库目前只有 `Music/` 与 `SFX/` 两个目录，`amb_` 前缀的环境声暂存于 `Music/`。

第一版音频制作已完成，共 63 个文件：环境声 6、BGM 14、SFX 43。

## 资源清单

### AMB — 环境声（暂存于 Music/）

| 文件 | 用途 |
|---|---|
| `amb_env_machine_hum_01.wav` | 机器运转低鸣，机关 / 工业区域底噪 |
| `amb_env_plant_rustle_01.wav` | 植物枝叶摩擦，植被区域底噪 |
| `amb_env_water_drip_01.wav` | 滴水声，潮湿 / 洞穴区域底噪 |
| `amb_env_wind_gust_01.wav` | 阵风声，开阔 / 高处区域底噪 |
| `amb_env_room_loop.wav` | 房间底噪，循环 |
| `amb_env_water_flow_loop.wav` | 水流环境声，循环 |

`room_loop` 与 `water_flow_loop` 已在文件名标注循环；其余四个环境声未标注，需整合时确认。

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
| `sfx_gp_plr_climb_01.wav` / `_02.wav` | 玩家攀爬（通用 / 岩石），2 个变体 |
| `sfx_gp_plr_climb_vine.wav` | 玩家攀爬藤蔓 |
| `sfx_gp_plr_die.wav` | 玩家死亡 |
| `sfx_gp_plr_respawn.wav` | 玩家重生 |
| `sfx_gp_plr_swim_01~03.wav` | 玩家游泳，3 个变体 |
| `sfx_gp_plr_water_enter.wav` | 玩家入水 |
| `sfx_gp_plr_water_exit.wav` | 玩家出水 |
| `sfx_gp_plr_buoy_enter.wav` | 玩家进入浮力状态 |
| `sfx_gp_plr_buoy_exit.wav` | 玩家离开浮力状态 |
| `sfx_gp_plr_impact_01.wav` | 玩家受击 / 撞击 |
| `sfx_gp_door_open.wav` | 开门 |
| `sfx_gp_door_close.wav` | 关门 |
| `sfx_gp_button_press.wav` | 按钮按下 |
| `sfx_gp_button_release.wav` | 按钮回弹 |
| `sfx_gp_machine_start.wav` | 机器启动 |
| `sfx_gp_machine_stop.wav` | 机器停止 |
| `sfx_gp_crank_turn_loop.wav` | 曲柄 / 机关转动，循环 |
| `sfx_gp_steam_burst.wav` | 蒸汽喷发（瞬时） |
| `sfx_gp_steam_loop.wav` | 蒸汽持续（循环） |
| `sfx_gp_bubble_loop.wav` | 气泡上浮，循环 |
| `sfx_gp_platform_move_loop.wav` | 平台移动，循环 |
| `sfx_gp_plant_grow.wav` | 植物生长 |
| `sfx_gp_plant_burn.wav` | 植物燃烧 |
| `sfx_gp_lava_cool.wav` | 岩浆冷却（遇水 / 降温） |
| `sfx_gp_block_pickup_01.wav` | 方块拾取 |
| `sfx_gp_block_drop_01.wav` | 方块放下 / 落地 |
| `sfx_gp_paint_acquire_initial.wav` | 首次获得上色能力（同路径修订） |
| `sfx_gp_paint_select.wav` | 选色 / 上色确认 |
| `sfx_gp_block_paint_complete.wav` | 方块上色完成 |
| `sfx_notice_confirm.wav` | 操作确认提示 |
| `sfx_notice_deny.wav` | 操作被拒绝提示 |
| `sfx_notice_puzzle_complete.wav` | 解谜完成提示 |
| `sfx_notice_secret_reveal.wav` | 隐藏要素揭示提示 |

路径均为 `Assets/_Project/Content/Audio/SFX/`。

## 运行时接入

当前项目提供的播放接口位于 `GameAudioService`：

```csharp
// 播放音乐（传入 AudioClip，是否循环）
GameAudioService.PlayMusic(clip, loop: true);

// 播放音效（传入 AudioClip，音量倍率 0–1）
GameAudioService.PlaySound(clip, volumeScale: 1f);
```

### 预期接线

| 触发时机 | 文件 | 建议调用 |
|---|---|---|
| 开场场景加载 / 标题画面 | `bgm_wn_01_opening` | `PlayMusic(clip, loop: true)` |
| 常驻氛围层 | `bgm_ambient_loop_01` | `PlayMusic(clip, loop: true)` |
| 区域环境音 | `amb_env_machine_hum_01` / `amb_env_plant_rustle_01` / `amb_env_water_drip_01` / `amb_env_wind_gust_01` / `amb_env_room_loop` / `amb_env_water_flow_loop` | 按区域进入 / 离开淡入淡出 |
| 玩家跳跃 / 落地 | `sfx_gp_plr_jump` / `sfx_footstep_land_walk_*` | `PlaySound` |
| 玩家在地面 / 水中移动 | `sfx_footstep_land_walk_*` / `sfx_footstep_water_walk_*` | `PlaySound` 随机变体 |
| 玩家攀爬 | `sfx_gp_plr_climb_01~02` | `PlaySound` 随机变体 |
| 玩家攀爬藤蔓 | `sfx_gp_plr_climb_vine` | 攀附藤蔓时播放 |
| 玩家入水 / 出水 | `sfx_gp_plr_water_enter` / `sfx_gp_plr_water_exit` | `PlaySound` 状态切换瞬间 |
| 玩家游泳 | `sfx_gp_plr_swim_01~03` | `PlaySound` 随机变体 |
| 玩家浮力进出 | `sfx_gp_plr_buoy_enter` / `sfx_gp_plr_buoy_exit` | `PlaySound` 状态切换瞬间 |
| 玩家受击 / 撞击 | `sfx_gp_plr_impact_01` | `PlaySound` |
| 玩家死亡 / 重生 | `sfx_gp_plr_die` / `sfx_gp_plr_respawn` | `PlaySound` |
| 开关门 | `sfx_gp_door_open` / `sfx_gp_door_close` | `PlaySound` 状态切换瞬间 |
| 按钮按下 / 回弹 | `sfx_gp_button_press` / `sfx_gp_button_release` | `PlaySound` |
| 机器启动 / 停止 | `sfx_gp_machine_start` / `sfx_gp_machine_stop` | `PlaySound` |
| 曲柄机关转动 | `sfx_gp_crank_turn_loop` | `PlaySound` 循环播放 |
| 蒸汽机关 | `sfx_gp_steam_burst` / `sfx_gp_steam_loop` | 喷发用 `PlaySound`；持续段循环播放 |
| 水下气泡 | `sfx_gp_bubble_loop` | 潜入水中循环播放，出水停止 |
| 移动平台 | `sfx_gp_platform_move_loop` | 平台启动时循环播放，停止时停 |
| 植物机关生长 / 燃烧 | `sfx_gp_plant_grow` / `sfx_gp_plant_burn` | `PlaySound` |
| 岩浆遇水冷却 | `sfx_gp_lava_cool` | `PlaySound` 冷却触发时 |
| 拾取 / 放下方块 | `sfx_gp_block_pickup_01` / `sfx_gp_block_drop_01` | `PlaySound` |
| 获得上色能力 | `sfx_gp_paint_acquire_initial` | `PlaySound` |
| 选色 / 上色完成 | `sfx_gp_paint_select` / `sfx_gp_block_paint_complete` | `PlaySound` |
| 操作确认 / 拒绝 | `sfx_notice_confirm` / `sfx_notice_deny` | `PlaySound` |
| 解谜完成 / 隐藏要素揭示 | `sfx_notice_puzzle_complete` / `sfx_notice_secret_reveal` | `PlaySound` |

## 待验证项

- [ ] Unity 导入检查：在 Unity 6000.3.12f1 中确认所有音频正确导入，无报错
- [ ] `.meta` 文件：首次导入后在 Unity 中生成，与音频文件一并提交
- [ ] 试听验证：逐条试听，确认音质与循环点衔接自然
- [ ] 循环点确认：四个未标注的环境声，以及 `sfx_gp_steam_loop`、`sfx_gp_crank_turn_loop`、`sfx_gp_platform_move_loop`、`sfx_gp_bubble_loop`
- [ ] 打包检查：确认音频压缩设置与项目打包配置一致

## 未解决问题

- `amb_` 前缀的环境声暂存于 `Music/`，如需严格按类别分桶可另开 `Ambience/` 目录
- `bgm_wn_01_opening` 的分段词不在 RVSA 允许的 `intro` / `loop` / `tail` 三件套内，后续如做正式交付建议改为 `bgm_wn_01_intro`
- 早期版本移除的 `sfx_ui_button_select`、`sfx_ui_notice_success_*`、`sfx_gp_water_enter_01`、`sfx_gp_water_splash_*` 已不在仓库，接入方需使用新命名
- 部分音效的导入压缩与音量需在 Unity 中实测后微调
- 音频系统的接线（场景引用、AudioMixer 路由、3D 空间化）由整合者完成，本分支不包含场景修改

---

*本文档随音频资源同步更新。*