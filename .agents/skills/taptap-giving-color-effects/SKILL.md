---
name: taptap-giving-color-effects
description: 在 2026TapTap 中将已有红蓝绿临时染色覆盖效果接入新场景或目标、配置参数，或排查没有效果、紫色 Shader、贴图透明区域不匹配时使用；不用于修改正式颜色解锁、物体反应或持久染色逻辑。
---

# TA 染色覆盖效果接入

此 Skill 帮程序员 AI 在已有场景中正确复用 `Assets/ta_source/GivingColor`，并先判断失效原因再接线。它描述的是成功染色后的短暂视觉反馈，与正式颜色玩法分开。

## 开始前

1. 重新读取根 `AGENTS.md` 和 `Docs/Agent/PROJECT_CONTEXT.md`、`WORKING_AGREEMENTS.md`、`ACTIVE_WORK.md`、`SYSTEM_INDEX.md`。
2. 阅读 `Assets/ta_source/GivingColor/使用说明.md`，并检查当前分支的 `GivingColorEffectController.cs`、`GivingColorEffectTarget.cs`、`GivingColorDye.shader` 与 `Resources/GivingColorDye.mat`。场景和代码会更新，不要照搬旧场景结论。
3. 如果任务涉及正式染色判定、颜色能力/钥匙、颜色物体、万能方块或反应逻辑，还要先完整读取 `.agents/skills/taptap-color-gameplay/SKILL.md` 和其中指定的 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。

## 不可越过的边界

- 本模块只观察原有染色成功结果并绘制临时覆盖层；不负责解锁能力、选色、判定目标、改变玩法状态或保存最终颜色。
- 不修改玩家开局获得三种能力的流程、`PlayerColorWheel` 的染色判定、目标的 `IColorApplicationTarget` 实现、颜色反应、房间状态、原 Renderer/材质、URP Renderer 配置或无关场景结构。
- 用户要求场景接入时，只做该场景必需的增量接线；先检查现有 Prefab/组件，避免重复组件和重复控制器。用户只要求分析时，不写入场景或 Prefab。
- 不用 `GivingColorTestReceiver` 替代正式玩法目标，也不为让动画出现而绕过颜色解锁或成功判定。
- 若目标是正式水体/蒸汽、植物/藤蔓生长、岩浆生成的深蓝固体，按当前控制器的排除规则保持无覆盖效果。

## 场景接入判断

先在用户指定的 Scene、玩家 Prefab 和运行时生成流程中核实以下条件：

1. 场景加载时是否已有玩家 `PlayerColorWheel` 和同一对象上的 `PlayerInteractionSensor`；染色交互还需要可用的 `Camera.main`。
2. 原有玩家染色是否会触发 `ColorApplicationSucceeded`，目标和颜色能否在成功事件前被控制器识别。
3. 目标是否满足下方的自动触发/显式加入条件，以及是否有受支持且启用的 Renderer。

`GivingColorEffectController` 在 `AfterSceneLoad` 及后续 `sceneLoaded` 时查找 `PlayerColorWheel`。找到时会创建一个隐藏的运行时监听器；找不到时直接返回，不会持续轮询。若玩家在场景加载事件之后才被延迟生成，验证控制器是否随玩家生成；若需补接，优先在用户指定的玩家 Prefab/常驻对象上增量添加一个控制器并引用现有组件，不改玩家逻辑代码。场景中已有控制器时不可再建第二个。

当前 `Assets/Scenes/Test.unity` 不是自动接线依据。使用前检查当前版本的序列化 Scene 和 Prefab：如果没有玩家或运行时玩家生成入口，单独运行它不会因染色 Skill 或 Shader 文件存在而自动产生效果。只在用户明确要求测试布置时才为测试场景添加最小所需对象/组件。

## 目标资格规则

| 目标 | 触发条件 |
|---|---|
| `UniversalColorBlock` | 原有玩家染色成功后自动播放；控制器会在目标上运行时添加 `GivingColorEffectTarget` |
| 正式的其他 `IColorApplicationTarget` | 在实现接色接口的同一个 GameObject 上预先添加 `GivingColorEffectTarget`，且原接色逻辑成功后播放 |
| 独立测试物体 | 仅测试时添加 `GivingColorTestReceiver`；需有受支持 Renderer、可用 `ColorRuntimeService` 且所选颜色已解锁。该接收器不持久改变外观 |
| 水、蒸汽、植物、藤蔓、岩浆反应 | 控制器显式排除，不播放覆盖层 |
| 物体接触触发的反应 | 不经过玩家染色成功事件，默认没有覆盖层 |

`GivingColorEffectTarget` 只是视觉覆盖层的入口和生命周期组件，不是玩法接色接口。仅在确认目标属于应有视觉反馈的对象时添加。控制器检查标记组件时使用接收接口组件所在对象上的 `GetComponent`，不要只把标记放在子对象上；可见 Renderer 可以位于目标对象或其子对象。

## 贴图与渲染适配

- 当前覆盖 Shader 是 URP 透明 Shader。加载顺序为 `Resources/GivingColorDye.mat` 的 Shader，再按 Shader 名称查找；不可用时控制器记录错误并跳过，不应把 Shader 替换到源材质上。
- `SpriteRenderer` 覆盖层复制 Sprite、UV、翻转、排序和源纹理 Alpha。
- `MeshRenderer` 使用相机朝向的矩形投影，要求同对象有 `MeshFilter` 和网格；读取首个源材质中的 `_BaseMap` 或 `_MainTex` 及其 Alpha。没有可识别纹理时会回退成白色完整矩形，因此先核对纹理、UV、Alpha 和材质属性。
- 其他 Renderer 类型不创建覆盖层。复杂多材质、特殊 UV、相机可见面与包围盒不一致等情况要先报告限制并定位，不要通过修改原材质掩盖问题。
- Shader 按原纹理的有效 Alpha 绘制；效果结束后覆盖层淡出并销毁，最终显示由原玩法逻辑决定。测试接收器本身不改变最终外观。

## 参数与资源

- 三色动画、像素化、边缘与噪波参数位于 `Assets/ta_source/GivingColor/Resources/GivingColorDye.mat`；控制器在下一次成功染色时读取材质设置。
- 自动创建的控制器使用代码默认时长。只有需要调整 Inspector 中的基础时长或高级覆盖参数时，才在已存在的玩家根对象/场景常驻对象上手动添加一个 `GivingColorEffectController`。
- 不要复制 Shader、材质或脚本到第二个目录；保留现有 `.meta` 和 GUID。

## 排查顺序

没有效果时，依次确认：

1. 原玩法是否真的报告染色成功；失败点击、未解锁颜色、能力球获取、物体接触反应都不会启动覆盖层。
2. 控制器是否在正确时机创建并绑定 `PlayerColorWheel`；`PlayerInteractionSensor` 是否在同一玩家对象上；`Camera.main` 是否存在。
3. `colorId` 是否为 `red`、`blue` 或 `green`，点击位置与事件位置是否仍对应同一目标。
4. 目标是否符合资格表；自定义目标是否把 `GivingColorEffectTarget` 挂在接色接口所在的 GameObject 上。
5. Renderer 是否启用且类型受支持；检查源纹理、Alpha、UV、MeshFilter 和首个材质的贴图属性。
6. 查看 Unity Console 第一条 Shader 编译错误；检查活动管线是否为 URP，以及材质是否仍引用本目录的 Shader。控制器检测到 Shader 不可用时会跳过效果，不能用粉紫错误材质替换源物体材质。

按用户要求的范围做验证：确认成功点击只在目标上播放对应颜色、失败点击不播放、覆盖层结束后原玩法外观显露、房间重置/目标禁用能清理叠加物。不要为单纯文档或分析任务修改场景、运行测试或提交改动。
