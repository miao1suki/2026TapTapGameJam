# 颜色进度与标准玩家

## 颜色进度

每种颜色独立拥有「场景颜色」和「玩家能力」两套会话状态。默认都锁定。

- 同色第一把钥匙：激活所有对应颜色场景物体，启动既有外观/HSV演出；不授予玩家染色能力。
- 同色第二把钥匙：授予对应玩家能力，轮盘更新；不重复场景颜色恢复。
- 后续同色钥匙可收集，但不重复解锁事件。
- 单把钥匙多 Collider、重复触发、禁用或销毁均不得推进第二次。结算只发生在 DropItemBase.OnCollectionStarted。
- R、死亡和离开房间保留两种状态；明确的新游戏/ResetProgress 才清空两者和选色。

ColorRuntimeService API：

| 调用 | 职责 |
|---|---|
| IsUnlocked(id) / Unlock(id) | 只查询/解锁场景颜色，保留旧对象功能、HSV和ColorUnlocked事件语义 |
| IsAbilityUnlocked(id) / UnlockAbility(id) | 只查询/授予玩家能力；不改场景物体或HSV |
| CollectColorKey(id) | 每次真实拾取原子推进一个阶段，返回None / SceneColor / PlayerAbility |
| AbilityUnlocked / EventMgr.OnColorAbilityUnlocked | 能力授予通知，轮盘只订阅此通知 |
| SetAllUnlockedForCurrentSession | 只调试场景颜色，不授予玩家能力 |
| UnlockAllAbilitiesForCurrentSession | 独立调试玩家能力，不激活场景颜色 |

编辑器调试入口：`Tools/2026TapTap/调试/颜色与收藏品调试`。窗口仅在 Play Mode 允许修改：可一键调用 `SetAllUnlockedForCurrentSession(true)` 与 `UnlockAllAbilitiesForCurrentSession()` 解锁六把钥匙；收藏品区域显示当前已获得数量和最大可获得数量，并提供目标数量输入、应用数量、设为 0、设为最大和刷新按钮。调试地图按当前 Scene 所有 `RespawnAnchorFeature` 与已注册 `TeleportAnchorFeature` 的世界 XY 相对位置布点，支持 `1×～4×` 缩放和双向滚动；重生点按钮会先确保目标已激活，再调用 `PlayerController.TryRestoreLoadedRunPosition` 走真实复活、房间重置和 Boss 重生链，传送方块按钮通过现有 `TeleportObject` API 移动玩家。

场景物体、三色搬运块、功能组件的原有激活门控保持 IsUnlocked；PlayerColorWheel 与 PickWheelPrototype 的显示/选色必须用 IsAbilityUnlocked。不要将全部 IsUnlocked 调用全局替换，三色物体自身的接触反应不依赖玩家能力。

两种进度本轮均为当前运行会话进度，不新增跨运行存档协议。新游戏沿用现有 ResetProgress 调用。

未解锁选色通过 `PlayerColorWheel.AbilitySelectionLocked` 通知输入驱动，统一WordNote提示“此能力未解锁”。事件仅用于反馈，不修改任一解锁集合；第一把钥匙仍只能激活场景颜色，第二把才开放玩家能力。不要在UI选色时直接调用Unlock或UnlockAbility。

## 唯一标准玩家

唯一资源：`Assets/_Project/Content/Player/Player.prefab`，GUID `850f455fc20459b48987783f0ab1ab59`。以原正式 Gaming 玩家整理；RuntimePlayer 和 Player(暂定) 两个旧路径退役，不得重新生成。

- 根节点 Player，Tag=Player，单位缩放、零旋转；预制体自身位置为原点。
- 胶囊中心为零、半径0.5、高1，仅一个实体 Collider，不再有旧高胶囊或额外大触发箱。
- 统一 PlayerController、PlayerInputDriver、PlayerActionRunner、PlayerHealth、PlayerFallDamage、PlayerInteractionSensor、PlayerColorWheel、GreenHookAbility、PlayerSpritePresentation、PickWheelPrototype 与 TA InteractionLinkEffect。
- 精灵按现有原图比例显示为1.5×1.5格，不改变原图采样密度；基础偏移 `verticalOffsetCells=-0.0625` 加额外 `additionalVerticalOffsetPixels=-3`，总计下移4个美术像素（16有效像素/格），抵消待机/跑步脚下透明区。只移动精灵，不移动玩家根节点、胶囊或交互中心；Timeline适配保留，但默认无测试Timeline且不自动播放。
- 搬运逻辑继续使用实际胶囊中心；禁止重新以某一旧预制体偏移修补占格。
- 场景可配置出生/重生点和相机，不复制玩家Prefab、不对核心碰撞/输入/能力组件保留另一套场景覆盖。

现有 Gaming、Pick、Test、SampleScene、RoyTestScene、RoomRespawnTest、MVMapImportTest 已统一来源和核心配置。MainMenu等无玩家场景不新增玩家。相机跟随/焦点和LevelSceneContext的生成预制体同步到标准玩家。

相机归场景所有，不放进标准玩家Prefab。一次性迁移必须保留旧玩家下的场景相机，暂时脱离后再绑定到新实例，不能随清除旧Prefab覆盖项删除。Pick保留原跟随玩家的场景相机；验证要求每个玩法/测试玩家场景至少有一台启用相机。

编辑器入口统一取 StandardPlayerAsset.Path；EnsureInScene 仅在当前场景无玩家时实例化，有玩家则复用，多个玩家时报错。新建空存档调用 LevelEditorPlayerService.CreateAndBindAtOrigin，在世界(0,0,0)放置并绑定玩家；无房间时使用 hasUnassignedPlayerStart/playerStartWorld 保存出生位置，进入房间后仍按房间ID与局部位置持久化。LevelEditorState按当前活动场景恢复玩家引用，不跨场景串绑。

迁移通过Unity的Prefab替换/转换API重映射场景引用，非单纯替换GUID；原场景位置保留。历史迁移曾将旧Gaming出生点调整为(1.5,-28,0)，避免中心归零后开局进入红钥匙磁吸范围；此坐标不再是正式关卡约束。2026-10-16已采用策划新版Gaming，布局、钥匙和玩家位置以当前正式场景/存档为准，禁止再次运行旧迁移把它改回旧图。正式交付与同步规则见[FORMAL_LEVEL_DELIVERY.md](FORMAL_LEVEL_DELIVERY.md)。迁移前本地备份位于Library/PlayerConsolidationBackup-20261016，既有非玩家内容不全量重建。MigrateProject仅允许隔离验证工程显式执行，不在导入或运行时自动执行。

## 交付

保持项目关卡协作规则：场景、同名planning.json、各自meta和依赖一起交付。不要把编辑器可修改存档只留在Library。玩法验证入口 ColorProgressPlayerVerification（隔离工程），分别检查两阶段真实钥匙、独立API、重置、唯一预制体、场景接线与空存档出生点。
