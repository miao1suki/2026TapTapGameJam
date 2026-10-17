# 音频资源交接说明

当前仓库：`https://github.com/miao1suki/2026TapTapGameJam.git`。
分支：`音乐`。
音频文件位于 `Assets/_Project/Content/Audio/`，命名按 RVSA 规范（`snake_case`、类别前缀、受控词表）。资源目录为 `Music/`、`SFX/`，运行时 Cue 目录为 `Resources/AudioCatalog.asset`；`amb_` 前缀的环境声暂存于 `Music/`。

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

循环属性已登记到 `AudioCatalog`，Unity 内仍需实际试听循环点。

主菜单的`music.menu.opening`现使用新`bmg_op.mp3`，按菜单场景循环播放。正式进入Gaming后，`GameAudioService.HandleActiveSceneChanged`播放`music.atmosphere.e_minor`（`bgm_atmosphere_emin_02_loop.mp3`），复用现有Music音量总线及循环配置；暂停返回菜单再进入时也按此切换。

`bgm_boss.mp3`登记为`music.boss`（`AudioCueIds.MusicBoss`），可通过`AudioCuePlayer.Play(AudioCueIds.MusicBoss)`调用；当前默认非循环，未擅自添加Boss触发点。两首新增MP3使用Streaming/Vorbis、质量0.8、不预解码，meta已由Unity生成并交付，文件名保留音频成员原命名。

该服务由`GameFlowController.Start`安装到现有持久流程根并初始化，注册实际`ActiveSceneChanged`事件。仅修改Cue或直接调用私有播放方法不能验证真实入口；验收须从MainMenu按钮进入Gaming，再从暂停返回并重新进入，检查实际MusicSource与时间缩放。

### BGM — 音乐

| 文件 | 用途 | 循环 |
|---|---|---|
| `bgm_wn_01_opening.wav` | 原开场主题曲，资源保留，主菜单现改用新OP | 是 |
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
| `bgm_boss.mp3` | Boss 战 / Boss 出场主题曲，Cue已登记，等待玩法调用 | 当前否，Boss循环需求待确认 |
| `bmg_op.mp3` | 主菜单OP，保留现有文件名 | 是 |

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
| `sfx_gp_checkpoint_active_01.wav` | 新解锁的重生点激活 |
| `sfx_gp_checkpoint_active_02.wav` | 已解锁重生点再次激活 |
| `sfx_gp_block_drop_01.wav` | 方块放下 / 落地 |
| `sfx_gp_paint_acquire_initial.wav` | 首次获得上色能力（同路径修订） |
| `sfx_gp_paint_select.wav` | 选色 / 上色确认 |
| `sfx_gp_block_paint_complete.wav` | 方块上色完成 |
| `sfx_notice_confirm.wav` | 操作确认提示 |
| `sfx_notice_deny.wav` | 操作被拒绝提示 |
| `sfx_notice_puzzle_complete.wav` | 解谜完成提示 |
| `sfx_notice_secret_reveal.wav` | 隐藏要素揭示提示 |

路径均为 `Assets/_Project/Content/Audio/SFX/`。

## 响度标准

`Music/` 下 20 个文件于 2026-10-15 统一做了一次响度归一化。处理方式为 ffmpeg `loudnorm` 两遍测量 + 线性增益，只调整整体电平，不压缩动态。

| 指标 | 目标 |
|---|---|
| 综合响度（integrated） | -28 LUFS |
| True Peak | ≤ -3 dBTP |
| 处理方式 | 线性归一化，保持原始动态范围 |

实测结果：全部落在 -28.55 ~ -27.56 LUFS；True Peak 最大值 -5.73 dBTP，无一越线。

| 文件 | LUFS | True Peak (dBTP) |
|---|---|---|
| `bgm_wn_01_opening.wav` | -28.01 | -16.97 |
| `bgm_ambient_loop_01.mp3` | -28.00 | -16.15 |
| `bgm_atmosphere_amin_03.mp3` | -28.01 | -14.12 |
| `bgm_atmosphere_dsmin_02.mp3` | -28.02 | -16.85 |
| `bgm_atmosphere_emin_02_loop.mp3` | -28.00 | -15.60 |
| `bgm_atmosphere_fmin_02_loop.mp3` | -28.00 | -16.64 |
| `bgm_banjo_01.mp3` | -28.00 | -10.01 |
| `bgm_choices_01.mp3` | -28.00 | -13.20 |
| `bgm_kb280_01.mp3` | -28.00 | -12.76 |
| `bgm_music_box_01.mp3` | -28.00 | -9.48 |
| `bgm_robot_sad_piano_01.mp3` | -28.00 | -10.53 |
| `bgm_vocal_chop_01.mp3` | -28.00 | -15.59 |
| `bgm_wake_up_01_loop.mp3` | -28.50 | -13.07 |
| `bgm_alien_sacrifice_01.mp3` | -28.55 | -11.21 |
| `amb_env_machine_hum_01.wav` | -28.00 | -15.82 |
| `amb_env_plant_rustle_01.wav` | -28.00 | -5.73 |
| `amb_env_room_loop.wav` | -28.00 | -11.65 |
| `amb_env_water_drip_01.wav` | -28.22 | -5.73 |
| `amb_env_water_flow_loop.wav` | -27.56 | -11.75 |
| `amb_env_wind_gust_01.wav` | -27.68 | -10.42 |

环境声与 BGM 目前共用同一目标值。若实际混音中环境声需要垫在音乐之下，应单独设更低的目标并重新归一化，不要在场景里用音量倍率找平衡。

## 运行时接入

`AudioCatalog.asset` 把 60 个音频登记为 54 个 Cue。代码不直接依赖文件名，而是传入稳定的 `AudioCueIds`：

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
| 进入 Gaming | `music.atmosphere.e_minor` | `GameAudioService.HandleActiveSceneChanged` |
| 玩家跳跃 | `sfx.player.jump` | `PlayerController` 实际跳跃分支 |
| 玩家落地脚步 | `sfx.footsteps.land` | `PlayerController` 固定帧移动检测 |
| 玩家游泳 | `sfx.player.swim` | `PlayerController` 固定帧移动检测 |
| 玩家攀爬 | `sfx.player.climb` | `PlayerController` 进入及移动检测 |
| 玩家攀爬藤蔓 | `sfx.player.climb_vine` | `PlayerController` 按当前攀附源区分，重生/退出清理 |
| 按钮踩下 / 回弹 | `sfx.button.press` / `sfx.button.release` | `DoorButton` / `ButtonLockFeature` 实际状态变化，持续压住不重复播放 |
| 玩家入水 / 出水 | `sfx.player.water_enter` / `sfx.player.water_exit` | `PlayerController` 水域状态切换 |
| 玩家死亡 / 重生 | `sfx.player.die` / `sfx.player.respawn` | `PlayerController.EnterDeath` 统一死亡入口去重，兼容生命值事件和直接调用；`RequestRespawn` 播放重生声 |
| 重生点首次解锁 / 切换至已解锁点 | `sfx.checkpoint.unlock` / `sfx.checkpoint.switch` | `RespawnAnchorFeature.ActivateForPlayer`；依据激活前历史状态选声，重复绑定当前点不播放，链式补激活其他点静默，R 原地重生不播放 |
| 开关门 | `sfx.door.open` / `sfx.door.close` | `DoorController` 状态切换 |
| 曲柄转动 | `sfx.crank.turn_loop` | `CrankPlatformFeature` 实际向上转动时循环，达到上限/松开/制动/重置停止 |
| 曲柄停止 | `sfx.machine.stop` | 实际转动后的松开、上限或制动播放一次；房间重置和卸载仅静默清理循环 |
| 蒸汽喷发 / 持续 | `sfx.steam.burst` / `sfx.steam.loop` | `WaterVisualFeature.TriggerSteamReaction` |
| 藤蔓生长 | `sfx.plant.grow` | `ClimbableVineFeature.GrowTo` |
| 获得颜色 | `sfx.paint.acquire` | `ColorKeyPickup.OnCollectionStarted` |
| 选色 / 上色成功 / 非法 | `sfx.paint.select` / `sfx.paint.complete` / `sfx.notice.deny` | `PlayerColorWheel` |
| 搬起 / 放下方块 | `sfx.block.pickup` / `sfx.block.drop` | `CarryableFeature` 成功开始搬起/放下时；非法或重复请求、R复原不播放 |
| 基座通电 / 断电 | `sfx.machine.start` / `sfx.machine.stop` | `MechanismBaseFeature` 检测红色能源后的实际状态边沿；持续接触不重复播放 |
| 平台实际移动 | `sfx.platform.move_loop` | `MovingPlatformFeature.FixedUpdate` 按实际世界位移判断，兼容信号/自动/曲柄；停留、阻挡、断电、重置、禁用停止 |
| 气泡柱实际托举玩家 | `sfx.bubble.loop` | `BuoyancyColumnFeature` 按有浮力玩家集合启停；与蒸汽使用不同owner，离开、死亡、重置、禁用清理 |
| 浮力进入 / 退出 | `sfx.player.buoy_enter` / `sfx.player.buoy_exit` | `PlayerController.SetBuoyancy` 总体浮力源由无到有/由有到无；持续设置同一源不重复 |
| 至少6格下落后落地 | `sfx.player.impact` | `PlayerController` 真实落地分支，读取实际格尺寸；排除本次途中游泳/攀爬，不新增伤害 |
| 藤蔓 / 植物障碍燃烧 | `sfx.plant.burn` | `ClimbableVineFeature.BeginBurn` 每根一次；`PlantObstacleFeature.BreakObstacle` 成功时 |
| 岩浆冷却 / 玩家碰岩浆 | `sfx.lava.cool` | `LavaHazardFeature` 成功蓝色替换、存活玩家进入；无敌期间进入也有声音，Stay仅在保护结束真正死亡时补播，死亡后不重复 |
| 主菜单 / 暂停菜单确认 | `sfx.notice.confirm` | `AudioCuePlayer.PlayUiConfirm` 同帧去重；`GameAudioService`、`StartMenuController`、`EscMenuController` 共用，按键/手柄/触摸走同一入口 |

环境区域音及其余 BGM 暂不自动播放。解谜完成与发现秘密等待实际触发器；水中步行4个变体已登记但尚无独立浅水步行事件，游泳使用游泳Cue。本轮不改变玩家默认复活/R后的2秒无敌。

### 资源使用建议（未全部接线）

| 触发时机 | Clip 文件名 | 建议入口 |
|---|---|---|
| 开场场景加载 / 标题画面 | `bgm_wn_01_opening` | `PlayMusic(clip, loop: true)` |
| OP / 开场演出 | `bmg_op` | 按演出时机播放，循环待确认 |
| Boss 战 / Boss 出场 | `bgm_boss` | 按 Boss 演出时机播放，循环待确认 |
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
| 新解锁重生点 | `sfx_gp_checkpoint_active_01` | 解锁并激活新重生点时播放 |
| 已解锁重生点 | `sfx_gp_checkpoint_active_02` | 再次激活已解锁重生点时播放 |
| 获得上色能力 | `sfx_gp_paint_acquire_initial` | `PlaySound` |
| 选色 / 上色完成 | `sfx_gp_paint_select` / `sfx_gp_block_paint_complete` | `PlaySound` |
| 操作确认 / 拒绝 | `sfx_notice_confirm` / `sfx_notice_deny` | `PlaySound` |
| 解谜完成 / 隐藏要素揭示 | `sfx_notice_puzzle_complete` / `sfx_notice_secret_reveal` | `PlaySound` |

## 待验证项

- [ ] Unity 导入检查：在 Unity 6000.3.12f1 中确认所有音频正确导入，无报错
- [ ] `.meta` 文件：首次导入后在 Unity 中生成，与音频文件一并提交
- [ ] 试听验证：逐条试听，确认音质与循环点衔接自然
- [ ] 替换后整体平衡确认：-28 LUFS 在游戏内与 UI / SFX 的相对关系
- [ ] 循环点确认：四个未标注的环境声，以及 `sfx_gp_steam_loop`、`sfx_gp_crank_turn_loop`、`sfx_gp_platform_move_loop`、`sfx_gp_bubble_loop`
- [ ] 打包检查：确认音频压缩设置与项目打包配置一致
- [x] 播放接口扩展：随机变体、按 owner 循环、停止/查询循环音接口已加入 `AudioManager` 和 `GameAudioService`
- [x] Cue 目录：63 个音频登记为 55 个 Cue，Music / SoundEffect 通道已分流；玩法/确认、两种重生点激活和新主菜单OP已接线，Boss曲等待调用
- [x] 运行时接线：玩家动作、门、曲柄、蒸汽、藤蔓、颜色钥匙与选色反馈已接入

## 未解决问题

- `amb_` 前缀的环境声暂存于 `Music/`，如需严格按类别分桶可另开 `Ambience/` 目录
- `bgm_wn_01_opening` 的分段词不在 RVSA 允许的 `intro` / `loop` / `tail` 三件套内，后续如做正式交付建议改为 `bgm_wn_01_intro`
- 本次新增 `bgm_boss.mp3` 与 `bmg_op.mp3` 按需求暂不重命名，后续正式整理时再统一 RVSA 命名。
- 早期版本移除的 `sfx_ui_button_select`、`sfx_ui_notice_success_*`、`sfx_gp_water_enter_01`、`sfx_gp_water_splash_*` 已不在仓库，接入方需使用新命名
- 部分音效的导入压缩与音量需在 Unity 中实测后微调
- 不启用 3D 空间化；后续若需要空间音，必须先更新本交接和 skill 再改架构

---

*本文档随音频资源同步更新。*

整合说明：重命名资源沿用原 `.meta` GUID，新增资源补齐 `.meta`。既有场景引用不随文件名变化而丢失。
