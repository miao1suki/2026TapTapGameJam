# 项目设置与程序交接

这份说明帮助新接手的程序员打开项目、运行基础流程并找到主要代码入口。它记录的是当前项目约定，不是 Unity 设置修改指南。

## 项目基线

| 项目项 | 当前值 |
|---|---|
| Unity Editor | `6000.3.12f1` |
| 渲染管线 | URP，Package `com.unity.render-pipelines.universal` 版本 `17.3.0` |
| 输入 | Unity Input System，Package `com.unity.inputsystem` 版本 `1.19.0` |
| Timeline | Package `com.unity.timeline` 版本 `1.8.11` |
| UI | UGUI，Package `com.unity.ugui` 版本 `2.0.0` |
| 唯一 Git 仓库 | `https://github.com/miao1suki/2026TapTapGameJam.git` |

不要升级 Unity Editor、Packages 或渲染管线，除非任务明确要求并经过项目负责人确认。不要从旧项目复制 `Assets` 或 `ProjectSettings`。

## 获取并打开项目

1. 克隆唯一工作仓库：

   ```bash
   git clone https://github.com/miao1suki/2026TapTapGameJam.git
   ```

2. 在 Unity Hub 中添加仓库根目录。根目录应包含 `Assets`、`Packages` 和 `ProjectSettings`。
3. 使用 Unity `6000.3.12f1` 打开项目，等待首次资产导入和 Package Manager 解析完成。
4. 打开项目后先检查 Console。出现导入或编译错误时，先记录第一条错误和受影响文件，不要通过升级包或改项目设置来掩盖问题。

## 启动与调试

### 正常游戏流程

1. 打开 `Assets/_Project/Scenes/Bootstrap/Bootstrap.unity`。
2. 点击 Play。Bootstrap 会依次初始化 Core、Input、Audio、Camera 和 UI 常驻场景，然后显示主菜单。
3. 从主菜单开始后进入第一关；关卡终点按流程进入第二关、第三关和结束场景。

Build Settings 中的启用场景顺序由 `ProjectSettings/EditorBuildSettings.asset` 管理。当前正式流程包括 Bootstrap、系统场景、主菜单、Level_01～03 和 Ending。修改启动流程或场景列表前，先核对 `Docs/Developer/GAME_FLOW.md` 与实际 Build Settings。

### 当前场景调试

通过菜单 `Tools/2026TapTap/场景/场景导航与关卡入口` 打开场景导航工具。需要直接调试当前场景时选择“当前场景”入口；这条路径会补齐系统场景并激活当前关卡。

`Assets/Scenes/Test.unity` 是测试场景，不属于正式关卡流程。新测试优先使用独立场景；不要把测试物体、临时验证脚本或旧地图拷入正式关卡。Test 场景中的既有改动也要先检查 Git 差异和归属，避免覆盖其他人的测试。

## 输入与运行时入口

桌面默认控制：

| 操作 | 默认输入 |
|---|---|
| 左右移动 | `A` / `D`，或手柄摇杆 |
| 跳跃 | `Space`，或触摸跳跃 |
| 打开/关闭调色轮盘 | `Tab`，或手柄 `Select` |
| 交互/拾取 | `F` |
| 向左/右放下携带物 | `Q` / `E` |
| 复活/重置搬运物 | `R`，或手柄右摇杆对应动作 |
| 暂停 | `Esc` |

输入从 `GameInput` 进入；`PlayerInputDriver` 负责将动作转成 `PlayerController` 命令，`PlayerController` 不直接读取设备输入。相机实际输出由 `CameraControlManager` 管理，游戏场景流程由 `GameFlowController` 管理。Timeline 演出具有更高的相机优先级。

## 目录地图

| 路径 | 职责 |
|---|---|
| `Assets/_Project/Code/Systems/<Feature>` | 项目系统代码，按功能目录组织；Runtime、Editor、Tests 分开 |
| `Assets/Player` | 基础玩家逻辑和 Timeline 动作适配 |
| `Assets/_Project/Scenes/Bootstrap` | 启动场景 |
| `Assets/_Project/Scenes/Systems` | Core、Input、Audio、Camera、UI 常驻系统场景 |
| `Assets/_Project/Scenes/Flow` | 主菜单、流程与结束场景 |
| `Assets/_Project/Scenes/Levels/Level_XX` | 正式关卡场景 |
| `Assets/_Project/Content` | 正式流程使用的预制体、目录和配置资产 |
| `Assets/_Project/Development/<Feature>/<Task>` | 独立实验与开发工具，不默认加入正式构建 |
| `Assets/ta_source` | TA 独立表现、Shader 和迁入资源；按各目录的使用说明接入 |
| `Docs/Agent` | 协作约定、系统索引、状态与接口交接文档 |
| `Docs/Developer` | 面向程序员和策划的项目及功能使用说明 |

修改功能前先从 `Docs/Agent/SYSTEM_INDEX.md` 找到当前有效实现和接入文档。随源码保留的旧 README 只能作参考，遇到冲突以当前项目文档和实际代码为准。

## 颜色玩法与 TA 表现模块

- 颜色系统、颜色物体、万能方块、颜色钥匙和房间重置遵循 `.agents/skills/taptap-color-gameplay/SKILL.md`，并阅读其中指定的 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。
- 下表是交接检索入口。AI 或程序员应先按 `effect_id` / 组件名定位模块，再阅读对应的模块说明和源码；不要从场景里的视觉结果反推玩法逻辑。

| effect_id / 检索关键词 | 入口与资源 | 安装位置 / 自动接入 | 触发与结果 | 可调参数和边界 |
|---|---|---|---|---|
| `dye_feedback`；`GivingColorEffectController`、`GivingColorEffectTarget` | `Assets/ta_source/GivingColor/GivingColorEffectController.cs`；`GivingColorEffectTarget.cs`；Shader `GivingColorDye.shader`；材质 `Resources/GivingColorDye.mat`；完整步骤见 [`使用说明.md`](../../Assets/ta_source/GivingColor/使用说明.md) | 有 `PlayerColorWheel` 和 `PlayerInteractionSensor` 的场景会在加载后自动创建运行时监听器；无需改场景或玩家预制体。手动添加 Controller 仅用于需要在 Inspector 配置时长等参数。成功的允许目标会临时添加 `GivingColorEffectTarget`。 | 仅响应玩家原染色接口报告成功后的事件；使用成功染色目标、选中色和点击位置播放一次短暂覆盖。红色为燃烧扩散，蓝色为多次水彩叠染，绿色为分叉生长；完成后覆盖层渐隐，显露玩法系统设置的最终外观。 | 调 `GivingColorDye.mat`：红/蓝/绿速度各为 0.25–4 倍、像素化、红橙边宽、三色独立噪波。无需改原 Renderer/材质。排除水体蒸汽反应、植物反应、藤蔓/植物生长、岩浆与水反应生成的深蓝固体。不得触碰开局能力、解锁、接色判定或物体最终颜色逻辑。 |
| `steam_burst`；`SteamBurstEffect`、`steamVfxPrefab` | `Assets/ta_source/SteamEfect/SteamBurstEffect.cs`；`SteamBurst.shader`；`SteamBurst.mat`；`SteamBurst.prefab`；完整参数见 [`README.md`](../../Assets/ta_source/SteamEfect/README.md) | 蒸汽脚本挂在一次性 `SteamBurst.prefab` 上。`Blue_WaterSource`、`Blue_WaterWay`、`Blue_WaterBubble` 三个蓝色水体预制体的 `WaterVisualFeature.steamVfxPrefab` 已引用它；水体在 Collider 顶面中心实例化。Test 场景的既有对应物体从这些预制体继承配置。 | 水体收到红色染色反应时由现有水体逻辑实例化；仅做向上喷发并在约 0.82 秒内消散的视觉。推力、反应时间和是否触发仍属于原 `WaterVisualFeature` 逻辑。 | 调 `SteamBurst.mat`：效果大小 0.5–3（默认 1.6）、喷发高度 0.5–4（默认 1）、雾气浓度（默认 0.92，影响不透明度和粒子数）、像素化程度 0–1（默认 0.1）。`SteamBurstEffect` Inspector 可调生命周期、水格尺寸和三层粒子数。不要为了蒸汽改染色解锁或水体反应判定。 |
| `target_link_preview`；`InteractionLinkEffect` | `Assets/ta_source/LinkEffect/InteractionLinkEffect.cs`；材质 `InteractionLink.mat`；完整接入步骤见 [`连接效果使用说明.md`](../../Assets/ta_source/LinkEffect/连接效果使用说明.md) | 挂在玩家根物体（与 `PlayerController`、`PlayerInteractionSensor` 同对象）。引用 Sensor、`PlayerColorWheel`、`ColorCatalog` 和连线材质；部分同对象组件可自动查找。运行时线条由组件创建，不给目标物逐个挂脚本。 | 玩家选中红/绿/蓝时，只对交互范围和目标 Layer 内、能够响应当前颜色的目标显示预览。它只读检测可行性；实际点击染色仍走原玩家染色流程。 | 组件 Inspector 可调线条生成时长、断开溶解时间、起点锚点/偏移和目标 Layer；`InteractionLink.mat` 可调链条粗细、像素大小、枝条噪波、效果速度和蓝色水纹外围强度。Test 场景已有 `TestPlayer` 接入配置可作参照。不得从连线组件执行或替代染色。 |

### 面向 AI 的模块调用约定

以下记录是检索元数据，不是可调用 API；实际 API 以对应源码签名为准。AI 接手任务时按以下字段阅读：

```yaml
dye_feedback:
  entry: TaSource.GivingColor.GivingColorEffectController
  observe: PlayerColorWheel.ColorApplicationSucceeded
  explicit_visual_api: GivingColorEffectController.PlayOn(target, colorId, color)
  requires: [successful_original_dye, supported_renderer]
  forbidden_write_targets: [ability_unlock_flow, original_dye_logic, target_gameplay_color, original_renderer_material]
steam_burst:
  entry: Project.SteamEfect.SteamBurstEffect
  prefab: Assets/ta_source/SteamEfect/SteamBurst.prefab
  configured_by: WaterVisualFeature.steamVfxPrefab
  trigger_owner: existing_water_reaction
  visual_lifetime_seconds: 0.82
target_link_preview:
  entry: InteractionLinkEffect
  mount: player_root
  reads: [PlayerInteractionSensor, PlayerColorWheel, ColorCatalog, target_color_response]
  writes: [temporary_runtime_line_visual]
  gameplay_authority: existing_player_dye_flow
```

`Assets/Scenes/Test.unity` 当前用于调试连线预览（`TestPlayer` 上已有 `InteractionLinkEffect`）。染色覆盖通过运行时自动监听接入，不要求在 Test 场景保存组件；蒸汽则随三个蓝色水体预制体字段接入。不要为展示这些效果而改写 Test 场景、能力获取步骤或原玩法组件。需要改任何一个效果时，先读上表对应的模块说明，并检查该目录源码、Prefab/材质引用和本地工作区差异。

- 如需求明确要求保护原程序逻辑和场景、只新增隔离组件，使用 `.agents/skills/taptap-readonly-extension/SKILL.md`。

## 当前未开发或不属于本项目的功能

- 对话系统尚未开发，不存在可供接入的对话模块。
- 旧项目中的绳/线、梯子、移动平台玩法、投影视差碰撞/深度吸附和旧地图拼合未迁入。
- 旧关卡、旧测试脏数据、旧素材和 QQ bot 运维源码不属于本项目。
- `SampleScene` 是 Unity 模板参考，不属于正式游戏流程。

不要根据旧项目代码或场景推断这些功能已存在，也不要为了让旧示例通过而重新加入被排除的依赖。

## 开发与交付约定

1. 开始任务时确认仓库根目录、`origin`、当前分支和工作区状态；先读根目录 `AGENTS.md` 及其中指定的持续更新文档。
2. 使用个人任务分支 `codex/<成员或任务>/<范围>`。不直接推 `main`，不合并他人分支，不覆盖工作区既有改动。
3. 修改源码、场景或 Unity 资产时保留同路径 `.meta`。不要提交凭据、`Library`、`Temp`、`Logs`、`Builds` 或本地编辑器配置。
4. 场景按关卡或任务独立编辑。多人不要共享修改同一个场景；正式场景变更先确认归属。
5. 完成后按功能要求编译和验证，运行时修改做相关测试，并在风险匹配时验证打包。说明未验证项目和限制。
6. 更新对应功能说明和 `Docs/Agent/ACTIVE_WORK.md` 中自己的交接记录；只暂存当前任务明确涉及的文件。

更多运行流程见 [GAME_FLOW.md](GAME_FLOW.md)，项目架构和功能入口见 [系统索引](../Agent/SYSTEM_INDEX.md)，协作规则见 [AGENTS.md](../../AGENTS.md)。
