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
`CameraModeSwitch` 不再由 `PlayerInputDriver` 消费为相机切换；它现在承担“调色”入口，用于开关染色轮盘。相机模式请由场景或 Timeline 演出自行申请，Timeline 的 Cutscene 要求仍保持最高优先级。

`Carry` 是锁定长按的“搬运”动作，默认键盘鼠标右键、手柄北键；PlayerInputDriver 只把按住状态和指针交给 PlayerController，实际拿起、拖拽和放下由目标实现。

## 公共自动发现

`Project.ProjectDiscovery` 是项目级多态发现入口。需要被自动查找的组件实现
`IProjectDiscoverySource`，用 `DiscoveryId`、`DiscoveryTags` 和
`DiscoveryOrder` 声明身份，并统一使用：

- `FindAll<T>()` / `FindFirst<T>()`：按基类、具体组件类型或接口查找场景实例；默认排除未激活对象，需要包含时显式传入 `true`。运行时 Type 场景使用 `FindFirst(Type)`。
- `FindTagged<T>("blue")`：按发现标签过滤；`BlockFeature` 使用唯一的默认颜色 ID 作为发现标签。
- `GetComponents<T>(root)`：从指定物体收集实现同一基类或接口的组件。
- `FindImplementations<T>()`：编辑器内扫描所有具体派生类型，供目录和模板工具复用。
- `FindType(string)`：按完整类型名解析已加载程序集中的类型。

不要在业务模块自行复制 `FindFirstObjectByType` 或 `TypeCache.GetTypesDerivedFrom`
的排序、去重和过滤逻辑；但有特殊 ECS、资源目录或第三方生命周期约束的地方可以保留局部实现。

## 字幕管理器

`Project.Subtitles.SubtitleManager` 统一管理“中间大字”和“下方小字”的字体、
字号、颜色、位置、停留时间和进出动画。它使用独立的 `ScreenSpaceOverlay` 画布，
不依赖、申请或写入相机组件，也不参与 CameraControlManager 的控制权竞争。

运行时通过 `ShowLarge`、`ShowBottom`、`Show(SubtitleCue)` 和 `HideAll` 调用。
`SubtitleTrigger` 只负责碰撞触发和提交 `SubtitleCue`，默认隐藏挂载物体及子物体的
Renderer，同时保留 Collider。需要单独覆盖样式时，关闭
`SubtitleCue.useManagerDefaults`。

字幕服务通过 `ISubtitleService` 暴露，注册在 `SubtitleServiceRegistry` 中。
相机端实现共享的 `Project.Contracts.ICameraViewportSource`，
`CameraControlManager` 只提供只读视口信息；字幕管理器通过接口查询，
不直接依赖具体相机类。两者都不要求对方存在，普通屏幕空间字幕无需相机。

## 相机唯一写入者

实际 Camera、Transform、projectionMatrix 只能由 `CameraControlManager` 写。
控制源实现 `ICameraControlSource.TryGetCameraState(context, out CameraState)`；通过 `RequestControl(source, priority, interruptionPolicy, transition)` 取得句柄。
句柄 `HasControl` 不等于 `IsValid`；非持有者不能假定输出已应用。`ReleaseControl`/`Retarget`/`SetPriority`/`SetInterruptionPolicy` 控制生命周期。
禁用/销毁时释放自己的句柄；不由外部协程同时插值实际Camera。Manager统一处理输出过渡和抢占。
模式请求实现 `ICameraViewModeRequester`，调用 `CameraModeController.RequestMode(requester, mode, [yaw], [transition])`，持有并释放请求句柄。
`CameraFollowController.SetTarget` 设置角色目标；模式控制与跟随解耦。
Timeline使用 `TimelineCamRig.Acquire/Release/SetShotTransform` 通过Manager接管；保留旧演出API，不给玩家自动开放手动视角切换。

## 玩家（基础子集）

`Project.Player.PlayerController` 只负责马的物理与动作能力：Rigidbody + CapsuleCollider，固定XY平面移动、跳跃、冲刺，普通3D物理碰撞，无投影碰撞或深度校正。
输入由 `Project.Player.PlayerInputDriver` 读取 GameInput，再通过 `SetMoveInput`、`SetSprintInput`、`RequestJump`、`TryPlayAction` 等命令驱动 PlayerController；`PlayerInputDriver` 同时也是相机模式的 `GameplayAbility` requester。
API：`Motor`、`IsGrounded`、`IsControlLocked`、`CurrentStateId`、`TryPlayAction(ActSO)`、`SetControlLocked(bool)`、`ReceiveTimelineSignal()`。
控制锁采用引用计数；每次加锁必须同一调用方配对解锁，禁用会清空。
`PlayerActionRunner` 播放Timeline，动作遵循优先级及 Interruptible，LockMovement只锁基础motor。
事件：`ActionStarted/ActionCompleted/ControlLockChanged/TimelineSignalReceived`。
`PlayerInteractionSensor` 扫描 `IInteractionTarget` 并消费Interact；没有对话实现。
游泳状态中，横向输入控制左右游泳；未按跳跃键时按 `swimSinkSpeed` 缓慢下沉，按住跳跃键时按 `swimRiseSpeed` 缓慢上浮，纵向变化由 `swimVerticalAcceleration` 平滑。
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
烘焙生成Mesh/Material/PNG资产并绑定引用；保存场景或prefab并提交全部依赖。运行时优先使用持久化结果；
没有烘焙时由Runtime根据已序列化的Placement生成显示Mesh，因此绘制数据可直接进入Play。方块基础颜色由
`LevelEditorPlacedBlock`序列化并在运行时重应用，不再染到贴画输出层。
Shader位于 `Assets/_Project/Rendering/Shaders/SurfaceTiles`，名称前缀2026TapTap；不要省略它们。

## 2D 关卡编辑器（Development）

`Assets/_Project/Development/LevelEditor/Orpheus0829` 是实验工具，不属于正式流程。栏目数据在
`LevelEditorPalette`/`LevelEditorBlockEntry`；条目有 `Custom` 与 `Prefab` 两种模式。`Custom` 保存颜色、
贴画和组件模板，放置时应用这些数据；`Prefab` 只保存预制体引用，放置时不应用颜色、贴画或组件模板。
组件模板使用 `LevelEditorComponentEditorWindow` 生成到 `GeneratedBlocks`，通过 `customTemplate` 关联；
运行时不依赖编辑器窗口，场景仍只序列化最终实例。`LevelEditorState.GenerationParent` 保存当前场景的
生成父物体；放置时仅在新实例与父物体同场景时挂载，否则回退到编辑器内容根节点。
正式接入前需确认该工具的生命周期、场景临时对象和打包边界。

## 方块功能组件

`Assets/_Project/Code/Systems/BlockFeatures` 提供固定颜色物体的功能组件契约。功能组件继承
`BlockFeature`，由 `BlockRuntime` 统一发现、校验、排序和 Tick。组件通过 `BlockContext`
使用能力查询、信号、命令和调试接口；链接与表现接口是预留接口，正式服务接入前不得当作已实现能力；组件编辑窗口保存模板前会运行
`BlockFeatureValidationUtility` 校验依赖、冲突和重复数量。

旧的水体、藤蔓和动力具体组件已经删除。后续按固定形态分别实现
水源、水流、气泡柱、梯子/藤蔓、弹性植物、植物障碍、岩浆和机关组件。
每个组件继承 `BlockFeature`，声明固定颜色组，并使用
`IPlayerContactReceiver`、`IObjectContactReceiver`、`IColorReactionReceiver`
等直接接口处理行为。

`BlockAbilityHost` 挂在颜色物体根节点，作为固定颜色组的薄启停宿主。
它只检查预制体上已经存在的 `BlockFeature`，根据
`BlockFeatureAttribute.DefaultColorId` 与物体固定基础颜色匹配后启停，
并校验 `Requires/Conflicts/MaxPerBlock`。它不再扫描 Catalog、添加组件、
运行时切换形态或读取动态当前颜色。

## 验证

EditMode覆盖相机、输入、重绑定、流程配置和贴画/切片。
PlayMode覆盖Bootstrap、唯一Camera/AudioListener/EventSystem、主菜单、暂停、123关与Ending。
修改玩法motor应另测普通地面移动跳跃与打包，不用旧绳梯测试场景代替。
