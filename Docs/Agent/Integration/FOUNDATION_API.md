# 基础系统接入（面向 agent）

本文件列出迁移后契约。模块 README 是旧功能实现细节参考，不要照搬被移除的旧场景/玩法。

## 启动与流程

`Project.GameFlow.GameFlowController` 管理 Bootstrap、常驻系统与一个活动流程场景，使用 catalog 路径，不硬编码 SceneManager 到处卸载场景。
`GameSceneCatalog` 保存启动、系统列表与 MainMenu/Level01/02/03/Ending 映射。
`LevelSceneContext.Configure/SetPlayerPrefab/SetPlayerSpawn/Activate` 在自己的场景生成一个玩家并连接跟随目标。
`LevelGoal.Configure(nextScene, endsGame, delay)` / `BeginCompletion` 提供流程跳转；出生点 `PlayerSpawnPoint`。
`GameSystemSceneRoot` 按 `IGameSystemService.InitializationOrder` 初始化子服务。系统不放进关卡复制。
`GameUiRouter.StartGame/SetPaused` 负责主菜单与暂停；UI场景常驻，关卡中主菜单全屏底板不激活。
`GameAudioService` 提供基础音源，具体音频素材不迁移；不假定旧生成音频仍存在。
初始化工具 `TapTapFoundationSetup.Generate` 创建清洁基础场景与普通白膜，不含旧机关。

## 输入

使用 `GameInput.ReadVector2(InputActionId.Move)`、`IsPressed`、`WasTriggeredThisFrame`，不在业务脚本读 Keyboard/Gamepad。
`InputService` 可替换 `IInputSource`，便于测试。`VirtualJoystick` / `VirtualInputButton` 提供触摸输入。
`PlatformUILayoutController` 按 Desktop/Mobile 保存与切换布局；两平台均允许手柄。
`InputBindingService` 与 `InputBindingBootstrap` 管理重绑定，编辑窗口为旧功能保留。
`CameraModeSwitch` 保留枚举/API，但默认无绑定、无触摸按钮、无玩家消费逻辑；以后要启用需单独需求。

## 相机唯一写入者

实际 Camera、Transform、projectionMatrix 只能由 `CameraControlManager` 写。
控制源实现 `ICameraControlSource.TryGetCameraState(context, out CameraState)`；通过 `RequestControl(source, priority, interruptionPolicy, transition)` 取得句柄。
句柄 `HasControl` 不等于 `IsValid`；非持有者不能假定输出已应用。`ReleaseControl`/`Retarget`/`SetPriority`/`SetInterruptionPolicy` 控制生命周期。
禁用/销毁时释放自己的句柄；不由外部协程同时插值实际Camera。Manager统一处理输出过渡和抢占。
模式请求实现 `ICameraViewModeRequester`，调用 `CameraModeController.RequestMode(requester, mode, [yaw], [transition])`，持有并释放请求句柄。
`CameraFollowController.SetTarget` 设置角色目标；模式控制与跟随解耦。
Timeline使用 `TimelineCamRig.Acquire/Release/SetShotTransform` 通过Manager接管；保留旧演出API，不给玩家自动开放手动视角切换。

## 玩家（基础子集）

`Project.Player.PlayerController`：Rigidbody + CapsuleCollider，固定XY平面移动、跳跃、冲刺，普通3D物理碰撞，无投影碰撞或深度校正。
API：`Motor`、`IsGrounded`、`IsControlLocked`、`CurrentStateId`、`HasActionBinding`、`TryPlayAction(ActSO)`、`SetControlLocked(bool)`、`ReceiveTimelineSignal()`。
控制锁采用引用计数；每次加锁必须同一调用方配对解锁，禁用会清空。
`PlayerActionRunner` 播放Timeline，动作遵循优先级及 Interruptible，LockMovement只锁基础motor。
事件：`ActionStarted/ActionCompleted/ControlLockChanged/TimelineSignalReceived`。
`PlayerInteractionSensor` 扫描 `IInteractionTarget` 并消费Interact；没有对话实现。
旧梯子、绳网、PlatformRider、投影平台接口已移除，不为兼容示例重新添加。

## 成就与 Timeline（旧功能）

`GameplaySignalHub.Emit(signalId, source, count, progress)` 转发标准信号，`IAchievementSignalProvider` 列出提供者能力。
基础玩家发出跳跃、落地、动作开始/完成信号；LevelGoal发出到达终点信号。
`AchievementSignalBridge` 转交成就条件，`AchievementManager.Instance` 提供查询/触发/事件/持久化。
新catalog为空，旧测试成就不导入；用成就编辑器创建数据。旧梯子/平台信号标识保留仅协议兼容，无对应功能。
TimelineKit保留 `ActSO`、actor host、hitbox、effect/audio、Camera tracks、DamageableHealth与HitFlash。
旧Inspector和帮助文本是兼容代码，不代表旧测试场景已迁移。新UI遵循UI Toolkit标准。

## 瓦片工具（旧功能）

`SurfaceTileBlock` 的palette、cellSize、placements是可编辑数据；支持多层、rotation/flip、以格为单位微调、透明底。
`SurfaceTileAuthoringService` 是Editor编辑/生成入口；修改需Undo/标脏。
`SurfaceTileSheetImporterWindow` 提供ObjectField源图、缩放、选区、自动识别完整块与追加碎片，调用现有Generator生成稳定瓦片ID。
配方与原图放一起；输出图集/库按配方路径。工具没有内置美术素材。
烘焙生成Mesh/Material/PNG资产并绑定引用；保存场景或prefab并提交全部依赖。运行时使用持久化结果，不依赖Editor。
Shader位于 `Assets/_Project/Rendering/Shaders/SurfaceTiles`，名称前缀2026TapTap；不要省略它们。

## 验证

EditMode覆盖相机、输入、重绑定、流程配置和贴画/切片。
PlayMode覆盖Bootstrap、唯一Camera/AudioListener/EventSystem、主菜单、暂停、123关与Ending。
修改玩法motor应另测普通地面移动跳跃与打包，不用旧绳梯测试场景代替。
