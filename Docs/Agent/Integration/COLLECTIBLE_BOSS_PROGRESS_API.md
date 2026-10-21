# 普通收集品进度与Boss流程

## 唯一进度源

`Project.Items.CollectibleProgressService`是当前运行会话的统计来源，不读写磁盘。`CollectiblePickup.OnCollectionFinished`先设置`IsCollected`并更新统计，再发送原有奖励事件。未完成的收集飞行继续沿用原生命周期，R/死亡/离区不取消；重复结算和静默恢复都不会重复奖励。

```csharp
CollectibleProgressSnapshot all = CollectibleProgressService.GetSceneProgress(scene);
CollectibleProgressSnapshot world = CollectibleProgressService.GetWorldProgress();
CollectibleProgressSnapshot room = CollectibleProgressService.GetRegionProgress(scene, regionId);
// all.Collected / Total / Remaining / Ratio / IsComplete
bool enough = all.MeetsRatio(.6f); // 按float有效十进制阈值计算整数需求，不用Ratio做边界判定
CollectibleProgressService.Changed += RefreshMap;
// 禁用时退订
```

- 总数按`Amount`求和，包含inactive作者物体；房间按作者`RoomResetObject.RegionId`归属，不随物理推动迁移。
- 门槛使用`MeetsRatio`：float阈值按其七位有效十进制转decimal，再取`Ceiling(Total*ratio)`比较整数数量，避免0.6f的二进制尾数让6/10误失败；没有扩大允许59%通过的误差。小总数按向上取整，例如总3份需2、总5份需3。
- `GetWorldProgress()`汇总本次会话访问过的场景目录，普通目录暂时卸载时也保留，不会因开发Boss场景没有普通收集品就把已有分母变为0；正式游戏仍统一在Gaming。Boss入口和结局使用此全局查询，房间地图仍用指定Scene/Region查询。
- 每次场景加载/房间规划版本变化只做一次含inactive的收集品目录扫描，不在Update或物理回调全场扫描。
- 规划作者使用已有Scene路径+PlanningBoxId+PlanningLocalCell稳定键；重复作者实例只计一次。缺少规划身份的物体仅有会话内运行时身份，不伪装成可存档键。
- Timeline克隆必须在inactive父物体下生成，启用前调用`SetExcludedFromProgress(true)`；不可只禁用Collider来排除统计。
- 未来存档所有者可调用`CopyCollectedStableIds(scene, destination)`和`RestoreCollectedStableIds(scene, ids)`；后者静默幂等，不播放动画或奖励。本轮不新增磁盘保存，退出应用后清空会话。

## Boss入口与结局

`BossCollectibleEntryGate`挂在Boss入场的`ContactTimelineTrigger`同物体，顶部`gateEnabled`总开关默认开启。关闭时直接放行Timeline，不执行此组件的条件/返回逻辑（正式 Gaming 的条件仍由 `enter_boss` 自行检查）。启用时默认最低收集比例0.3、返回ID为`Start`，并强制要求红绿蓝每色两把、共六把颜色钥匙。两项均满足才放行；缺任一项拒绝本次Timeline，不消费Once机会。返回当前玩家同场景的Start，也允许明确覆写同场景Transform/锚点。没有同场景目标时只拒绝并输出明确警告，不猜位置、不加载其他场景。挂接通过可复用的`IContactTimelineCondition`，无条件组件的既有触发器行为不变。

`Content/Boss/Timelines/enter_boss.playable` 是带字幕演出的替代入口：低进度时 Timeline 会自行播放提示、收黑并传回 `Start`。不能在同一个触发器上同时启用 `BossCollectibleEntryGate`，否则条件组件会在 Timeline 播放前直接拒绝。接入见 [enter_boss API](ENTER_BOSS_TIMELINE_API.md)。

锚点返回统一调用`PlayerController.TryTeleportToStoryAnchor`，由控制器清理绳/攀爬/积水等运动状态、通知房间生命周期、保持外部控制锁、定位相机并将下一次死亡的会话回退点绑定到目标。默认Start不会只移动Transform后还保留旧Boss重生点；明确配置的旧Transform覆写路径保持兼容。

结局在`BossDefeatTimelinePresenter`切换`player_thinking`前读取真实普通收集进度：`>=60%`成功→End，其余→Start。入口门槛为30%且六钥匙齐全；结局60%规则不变。`BossEntranceRequirements` 统一两种入口判定；六钥匙按已保存的三色场景解锁与三色能力解锁共六个阶段读取，不只检查三种场景颜色、不引入新存档协议。未配置/总数为0不会默认成功。成功演出的六份临时画板明确不计数。Boss吸收六钥匙仍由既有Boss死亡演出负责，不把它们当普通成就收集品。成就1009/1010及旧5/10信号标识保留兼容，触发比例与显示文字已统一为30%/60%。

## 地图可用限制

```csharp
BossTravelState boss = BossTravelProgress.GetState(scene);
// HasEnteredBoss / IsBossFightActive / HasDefeatedBoss / BossRegionId
bool unlocked = true; // 地图开局可用，目的地仍需激活
BossTravelProgress.Changed += RefreshMap;
// 明确作者配置时可覆写排除房间；Boss追逐不能调用此接口：
BossTravelProgress.SetBossRegion(scene, regionId);
// 专用Boss场景禁止打开传送地图：
BossTravelProgress.SetDedicatedBossScene(scene, true);
```

BossController启用时记录第一次位置和当前 Scene 的存活状态，血条/受伤系统激活进入战斗，真实EnterDeath记录击败；禁用不是击败。`HasLivingBoss(scene)`只在指定 Scene 仍有启用且未死亡的Boss时返回true。GetState的进入/战斗/击败标志为全会话共享并由存档恢复；BossRegionId是查询Scene的本地排除区。传送开局开放，战斗中及Boss房内禁止使用；目的地仅允许已激活的普通重生点，Boss重生点不列入。

`IsBossScene`在场景名为`Boss`、被明确`SetDedicatedBossScene`标记，或区域管理器全部有效房间都是该Boss房间时为true；这类场景以及玩家当前位于`BossRegionId`时不能打开传送地图。

当前开发资产`Assets/_Project/Scenes/Boss.unity`配套`Boss.planning.json`，单房间`room_0f1f793f`，入场触发`box_aa7a24dc`（局部17,39，Boss_appear），End目标`box_1ba29225`（局部309,39）。最终Boss与所有迁移关卡统一合入正式Gaming，不引入新的跨场景加载架构。`player_thinking`的Start/End仅允许当前玩家同场景锚点；独立Boss开发测试无Start时明确警告并正常揭幕，不切换场景。统计服务的世界/房间查询保留，但不意味着自动迁移场景。

关卡编辑器只保存既有组件的参数，不能凭`componentOverrides`新建组件。Boss入口应使用含`BossCollectibleEntryGate`的独立触发器Prefab Variant及palette条目；`box_aa7a24dc`改用新条目而保留ID/位置/Timeline overrides。直接给生成Scene实例加组件会在下次同步丢失。门外返回锚点也必须进入planning；不应把场景Transform引用当作可跨重建持久化配置。

## 验证与场景交付

`Project.BossLogic.Editor.CollectibleBossProgressAudit.Run`仅在`Library/FlowRetirementProbe -noaudio`执行，覆盖inactive统计、房间剩余、30%/60%边界、R保留、稳定ID导出恢复、六份演出排除及真实Boss激活/死亡状态。真实收集Trigger与飞行沿用`CollectibleContactAudit`、`CollectiblePersistenceAudit`，不能把纯条件断言描述成完整剧情通关。

关卡作者还需明确配置门外返回位置、Start与End目标；提交Scene及同名planning.json、meta和新增依赖。共享入口只负责功能，不擅自猜测Boss房布局或全量重建正式地图。
