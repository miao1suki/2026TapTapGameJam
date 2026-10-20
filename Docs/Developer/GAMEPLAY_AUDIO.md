# 玩法音效

音效统一由 `AudioCuePlayer` 播放，目录为 `Assets/_Project/Content/Audio/Resources/AudioCatalog.asset`。不需要在已放置的物体上重新挂 AudioSource，也不需要重建关卡；现有预制体和场景实例使用更新后的组件逻辑。

## 背景音乐

- 主菜单继续固定播放 `music.menu.opening`，循环。
- 进入 Gaming 逻辑态后从 7 首区域/氛围 BGM 中随机选曲：`music.ambient.loop`、四个 `music.atmosphere.*`、`music.banjo`、`music.keyboard`。
- 每首强制单次播放，播放结束后静默 `randomMusicGapSeconds`（默认 8 秒），再随机下一首。
- 使用洗牌袋：一轮内每首只出现一次，并避免相邻两首重复。
- `music.boss` 仍由 `BossMusicRequest` 作为最高优先级音乐覆盖，Boss 存活期间循环并且随机轮播暂停；Boss 死亡或禁用后释放覆盖，恢复底层随机 BGM 流程。
- 主菜单 OP、Boss 曲，以及清单中定位为开场、抉择、解谜反馈、叙事事件或过渡的曲目不进入随机池。

## 播放条件

| 事件 | 条件 |
|---|---|
| 搬起、放下 | 搬运命令实际成功时播放；非法放置、重复请求和重生复原不播放 |
| 机械启动、停止 | 基座检测红色能源后的通电/断电状态变化；持续接触不重复 |
| 曲柄停止 | 松开、转满或制动时，实际转动循环结束后播放一次；持续停住不重复，重置/卸载不补播 |
| 玩家死亡 | 统一进入死亡状态时播放 `sfx_gp_plr_die.wav`；生命值耗尽和直接调用死亡接口均有效，同次死亡不重复 |
| 平台移动循环 | 实际发生位移时播放；支持自动、信号和曲柄平台；端点停留、阻挡、断电、重置、禁用停止 |
| 气泡循环 | 气泡柱正在托举玩家时播放；最后一个玩家结束托举、死亡、重置或禁用时停止 |
| 浮力进入、退出 | 玩家总体浮力由无到有、由有到无时各播放一次；不是每帧播放 |
| 落地撞击 | 从空中最高处下落至少6格后真正落地；格尺寸读取关卡配置；途中游泳或攀爬不算这次重落地，不增加伤害 |
| 植物燃烧 | 藤蔓根系开始燃烧时一次；植物障碍成功破坏时一次 |
| 岩浆冷却 | 蓝色交互成功替换岩浆时播放；存活玩家碰到岩浆也使用此声，死亡后持续接触不重复 |
| 菜单确认 | 主菜单和暂停菜单确认操作；键鼠、手柄与触摸沿用同一菜单回调；同帧多层回调只播放一次 |

复活与按R重生仍保留原有的默认2秒无敌时间，配置在 PlayerController 的 `respawnInvulnerabilitySeconds`。无敌期间碰岩浆可以听到触碰声，但不会死亡；持续留在岩浆中，保护结束后仍会死亡。

## 调整与剩余资源

音效受总音量和音效音量控制。背景音乐受总音量和音乐音量控制，可在 AudioCatalog 的对应条目调整 `volumeScale`。全部沿用2D声音，不增加距离衰减或3D音源。

解谜完成、发现秘密尚无确定的玩法触发点，暂不自动播放。水中步行变体保留，游泳继续使用游泳音效；环境声与特殊事件音乐仍等待区域/事件规划。

Agent接入入口与完整Cue表见 [音频接入](../Agent/Integration/AUDIO_HANDOVER.md)。

## 验证

2026-10-21：随机音乐轮播、单次播放与 Boss 覆盖状态已完成 Unity 6000.3.12f1 静态编译，覆盖 `Project.Audio.Runtime`、`Project.GameFlow.Runtime`、GameFlow Editor/PlayMode 测试程序集及主程序集。未启动第二个 batchmode PlayMode，实际随机、8 秒间隔、Boss 覆盖和恢复仍需在主编辑器试听验收。

2026-10-15：隔离Unity6000.3.12f1项目125项EditMode测试通过；32项实际Play检查通过，覆盖高处落地、平台/气泡循环启停与重置/禁用、搬运成功与重复请求、燃烧、岩浆保护/致死/冷却和UI同帧去重。测试日志位于本机 `Library/GameplayAudioFinalTests.xml`、`Library/GameplayAudioFinalPlay2.log`。

未进行本轮PC/Android重新打包与逐条人工试听。隔离水体禁用仍有既存的SetParent日志，不将此次音频检查视为整关无警告验收。

死亡/曲柄补充回归：125项EditMode及41项实际Play检查通过，覆盖直接死亡、生命值死亡、重复死亡去重，以及曲柄松手、转满、制动与静默重置。日志：`Library/DeathCrankAudioTests.xml`、`Library/DeathCrankAudioPlay.log`。
