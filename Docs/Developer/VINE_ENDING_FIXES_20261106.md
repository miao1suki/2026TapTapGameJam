# 藤蔓与结局入场相机专项修复

日期：2026-11-06。父分支 `codex/2d-long-term-migration`，基线 `843db45`；交付分支 `codex/2d-vine-ending-fixes`。本次没有合入 main，没有制作正式安装包，不操作用户 Unity 和真实存档。

## 1. 顶部需要按两次跳跃

隔离测试复现于横向藤蔓顶面。`HorizontalCubeVine` 会给顶面和底面接触都注册水平攀附；`RequestJump` 只检查 `IsHorizontalClimbing`，第一次按键统一清空跳跃缓冲并写入下落速度。顶部玩家便需要再按一次才能走普通跳跃。

修复先核对当前有效攀附源的真实 `Collider2D`：玩家足底确在顶面、有水平重叠、没有向上飞离时，释放攀附并保留本次跳跃缓冲与真实支撑的 coyote 窗口。底面、侧面和未到顶的转角仍按第一次跳跃脱手下落。复现夹具同时验证普通竖直藤蔓顶部一直可以正常起跳，没有把侧面攀爬改成无限空中跳跃。

## 2. 生长顶人和速度

旧 `VineGrowthLift.Collect` 明确拒绝直接站在藤蔓上的玩家，只接受站在载物上的玩家；`HasBodies` 又只考虑实体刚体。因此直接顶面玩家被当成障碍物，生长停止。

修复记录真实顶面玩家租约，并将玩家计入载荷。预检全格上移路径及玩家头顶空间，逐物理步重查障碍。直接顶面的玩家使用 `QueueVineGrowthSupport` 提交位移与当前一步支撑，实际刚体写入仍在 `PlayerController.FixedUpdate`；不新建假平台、不把玩家挂为子节点、不把玩家改为 Kinematic。玩家跳离、走离、被锁操作或由绿绳接管时不续租，原有载物上的玩家保持 `QueuePlatformDelta` 路径。

| 参数 | 原默认 | 新默认 |
|---|---:|---:|
| 逐块生长间隔 | 0.18 秒 | 0.09 秒 |
| 藤蔓表现时长 | 2.3 秒 | 1.15 秒 |

带载荷生长原先最低 0.1 秒／格，会让新的 0.09 秒参数失效；现改为不低于一个 FixedUpdate。仍然是分步托举，先检查空间再生成完整节段。

速度字段已同步到母／子藤蔓 Prefab、7 份被引用的 AuthoredSections Prefab、Gaming 直接组件、MapVisibilityTest 直接组件及新放置使用的 Palette 初值。部分旧实例曾有自定义表现时长，本次按用户“已放置的也相同”统一为 1.15 秒。场景内实际解析到的全部母／子藤蔓参数已通过真实 Gaming 加载检查。

只改变对应组件的两个速度字段；数量、最大生长高度、初始节段列表、Transform、Collider2D、机关引用与美术资源不变。没有重建场景、修改旧 planning JSON 或覆盖其中的历史签名。

## 3. 结局刚传入就偏上

用户确认问题发生在喷泉演出之前。直接从出生位置传送到 `End` 的普通镜头，稳定后没有持续偏上；但会暂时从旧房间追赶，传送首帧错误。进一步启动正式图真实的 `camera_down` 前置镜头后，稳定复现持续偏上：

- 该 Timeline Clip 长达 3600 秒，申请相对倍率 1.3 和向下 5 格的焦点偏移。
- `TryTeleportToStoryAnchor` 没有结束这段镜头拥有的请求。
- 角色已传入结局房，旧偏移仍有效，玩家处于视口高度约 0.644；相机半高仍为 10.4。

新增 `ICameraFollowRelocationSource.CameraFollowCutRequested`，仅剧情黑幕内传送发送。跟随组件收到后 `Snap` 到新目标，`CameraDownBehaviour` 收到后释放自己拥有的倍率与偏移句柄，并经 `CameraControlManager.ApplyViewModeAuthorityImmediately` 当场刷新输出，避免只清数值却把旧画面留到下一帧。事件随目标绑定／解绑和 Playable 生命周期清理。

修复后传送当场、半秒后和稳定后均为视口高度约 0.375，相机半高恢复 8，旧偏移归零。普通死亡与手动重生仍发送原来的 `CameraFollowRelocated`，保留平滑回位。没有挪动玩家出生点、结局地形或 `End` 锚点，也没有改喷泉演出的参数和美术。

原 camera_down Director 可能仍在播放其剩余时间，但它的镜头句柄已全部释放；不强停可能还有其他轨道的 Director。重新进入它的正常触发生命周期时仍由原触发器管理。

## 4. 验证与证据

全部 Unity 验证在 `Library/Native2DValidation/Library/FlowRetirementProbe` 执行，使用隔离公司／产品身份及 `-noaudio`；未清除真实存档、未打开用户主 Unity。

| 验证 | 结果 |
|---|---|
| 新增实际物理 `VineEndingRegressionAudit.Run` | 8 项通过：真实顶面站立、竖藤顶部首次跳跃、横藤顶部首次跳跃、底面脱手、连续两格生长、平滑托起恰好两格、头顶阻挡、玩家存活 |
| 新增真实 Gaming `RunEnding` | 8 项通过：全部实例速度、真实前置镜头、正式 End 锚点、剧情传送首帧及稳定构图 |
| `PlayerMotionFeelAudit.Run` | 61 项通过，含绿绳／攀附所有权及过期接触跳跃 |
| `ClimbCornerVerification.Run` | 48 项通过，含竖藤链顶、实体外转角和上下绕边 |
| 完整 GPU EditMode | 310／310，通过；0 失败、0 跳过 |
| 资源保护 | 对所有修改的 Scene／Prefab 进行字节归一化比较，仅允许两个速度字段不同；Palette 仅修改对应两个初值。GridArt 与旧关卡 JSON 不变 |

基线新增夹具明确失败：横藤顶部首次起跳、直接顶面连续生长和托举距离三个断言失败；正式前置镜头入结局时首帧及稳定构图两个断言失败。相同夹具修复后全部通过。

### 单独记录的旧专项失败

补跑旧 `TimelineVineLiftAudit.Run` 时，其 x=56 的重力基座托举断言失败，位移约 0.010 格；普通重力块、三色块与堆叠的前置断言已通过。把隔离工程中的 `VineGrowthLift` 与 `ClimbableVineFeature` 换回本次父提交版本后，**同一个基座断言仍以相同位移失败**。这是原生 2D 基线中已有的独立基座载物问题，本次没有扩大修改基座控制器，也没有把这个旧专项记作通过。

日志和原始结果保留在本机 `Library/VineEndingFixes/`，不提交 Library。对应文件：`baseline-physics.log`、`baseline-camera-boss.log`、`final-Run.log`、`final-RunEnding.log`、`PlayerMotionFeelAudit.log`、`ClimbCornerVerification.log`、`TimelineVineLiftAudit.log`、`baseline-loads.log`、`editmode.xml`。

没有进行安卓实机、完整结局通关或整套正式包构建；本轮验证范围为上述物理、真实入场链路、编辑态测试和数据保护。

## 5. 参考图分析

七张 SilenceGarden 截图的逐图观察、带图细节、同格／四分块／双网格区分，以及当前 Scene＋GridArt 的分阶段编辑器改进方案见 [SILENCE_GARDEN_TERRAIN_WORKFLOW_REVIEW.md](SILENCE_GARDEN_TERRAIN_WORKFLOW_REVIEW.md)。本次仅交付分析，没有修改关卡编辑器面板或自动转换正式关卡。
