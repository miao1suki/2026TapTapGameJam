# TA 新效果接入（2026-10-18）

## 来源与范围

筛选接入 `codex/ta/flow-magma@95da62e`（包含 `lava-cooling-steam-burn@cccab8c` 及后续三次喷溅调优）、`codex/ta/rgb-glitch@6625198`、`codex/ta/rain-sprite-field@cb5831a`，以及本轮拉取新增的 `codex/ta/firefly-field@14b0c57`。保留 TA 源目录/GUID，不整分支覆盖正式代码。`223eb8d` 只调独立预览实例，未将其流速0/寿命等覆盖值强加正式预制体。未引入 `_Recovery`、Test 场景改动、字体、ProjectSettings 或 TA 的 Renderer 全量替换；PC/Mobile 只追加 RGB Feature，原水、HSV、像素化设置不变。

## 玩法表现

- `LavaHazardFeature` 创建 `FlowMagmaVisual`，优先实例配置，旧场景留空则读 `Resources/TAReactions/ReactionArtworkSettings`。同一逻辑岩浆使用一个精确占格 Mesh 和一个固定 96 容量的喷溅 Mesh；保留孔洞，只在暴露顶面起波/喷溅。视觉不产生碰撞、伤害或推力，不改原 `LavaContactOcclusion`。
- 蓝色冷却仍替换原黑曜石，最多从八处暴露顶面产生 `CoolingSteamEffect`；不是红色作用水面的原蒸汽喷发玩法。蒸汽挂到冷却结果下，房间恢复时随替换物一起清理。
- 植物障碍沿用同区域四邻域连通烧毁，一组一次音效；弹性植物燃烧开始立即停止弹跳/碰撞，留图至溶解完成。藤蔓继续使用既有 192 帧、母根单 Renderer、自上而下逐段烧毁及托举链，不回退实时藤蔓粒子。
- `PlantBurnEffectPlayer` 在已有 Renderer 的 MPB 写烧毁参数；正式 Sprite 与 HSV Mask 共用 `PlantBurnSampling.hlsl`。取消、Detach、房间重置清理参数，不能在全局共享材质写每株进度。玩法组件继续掌握损毁/恢复时机，视觉不自行发颜色命令。

流动、蒸汽和燃烧均以每格 16 个有效像素采样，抵消作者父级缩放，不改变关卡占格、尖刺/实体碰撞或美术比例。

正式岩浆是底层环境：`LavaHazardFeature` 的面片位于实体前表面之后，`FlowMagmaVisual` 本体/喷溅排序分别为 -10/-9，不能以更靠前的透明队列覆盖填入的搬运方块。这里仅调整绘制顺序，伤害仍由实际暴露接触及 `LavaContactOcclusion` 判断。房间重置即使复用面片，也须解除旧玩家接触。

岩浆喷溅采用 TA 新版持续随机间隔/偶发簇发、实心颗粒、四阶段冷却变暗，不沿用旧的周期静默窗口/空心环。大小可有整数像素层级，宽高和边缘仍按16像素/格对齐，新生至少2像素，缩小最少1像素；不是任意小数缩放素材。正式横流速度保留0.28。

## 死亡 / Boss RGB

唯一标准 `Content/Player/Player.prefab` 增加 `TapTap.TA.RgbGlitch.RgbGlitchSceneDriver`。驱动监听真正死亡和 `PlayerController.RespawnCompleted`；慢速加载内部搬回重生点不代表可提前解除死亡层，存活 R 不播放死亡效果。离开玩家/场景会清理全局效果，旧 TA 预览驱动不能抢占标准玩家。

`RgbGlitchEffect.BeginBossFear(seconds)` / `EndBossFear(seconds)` 请求标准驱动启停轻度恐惧，返回是否有有效驱动。实际 `BossRightMoveExecutor.MotionStateChanged` 经 `Assets/Boss_Logic/BossRgbFearPresentation` 接入；仅 Boss 存在不启动，真正右移追逐才开启。多 Boss 合并所有权；停止最后一个才淡出，死亡强度优先，复活仍可恢复正在追逐的轻度效果。

PC/Mobile Renderer 均显式引用 `RgbGlitchRendererFeature` 及 Shader；仅 Play 中 Game/Base 相机且强度大于零时提交 RenderGraph Pass。没有引入另一套相机控制或 Timeline 锁。

## 雨区与协作

`Tools/2026TapTap/关卡美术/创建雨区` 创建独立 Scene 根对象；选择三色、范围、位置、密度和固定种子。不是全关默认下雨，也不是关卡生成根内的玩法物体；当前工作台只做静态图层，不假称雨区已经写入 planning/工作台画笔。

`RainSpriteField.Configure(color, areaCells, dropsPerCell, seed)`；每区一个 Mesh、最多 512 颗、每滴固定 16×16 源像素/1 格，不随机缩放。固定种子和父级缩放补偿，移动不重抽分布。雨仅是颜色 Layer 装饰，初始褪色随场景颜色解锁恢复；不加 `ColorObject`、能力、交互线、物理或房间玩法重置。

HSV 遮罩采样图集 Alpha × 顶点 Alpha × 材质 Alpha，裁切 0.01，与画面同轮廓。源图 447×336，Default/max512/Point/Clamp/无 Mip/无压缩/NPOT None。

交付雨区或其他关卡变动必须提交 **Scene + 同名 planning.json + 各自 meta + 新资源依赖**，不能只交 Scene 或只交 planning；雨区存在 Scene 中。使用存档片段导入不会自动复制独立 Scene 美术根，跨关卡同时移交雨区 Prefab/参数。

## 萤火虫区域

`Tools/2026TapTap/关卡美术/创建萤火虫区域` 创建独立 Scene 美术根；同样支持 Undo、Prefab 参数覆盖和保存重载，不擅自在现有地图布置。不导入 TA 的测试移动目标或预览场景。

每区最多128只、一个动态网格/两个子网格（硬光点与柔光），固定布局/游动/呼吸三个种子。硬光点固定为每格16像素中的一个完整像素，世界网格对齐并抵消父缩放；柔光是照明，不按任意比例缩小硬光点。编辑态未改变时不逐帧重建网格，动态Mesh不写入Scene资产。实时补光默认0盏，可配置但最多4盏无阴影点光，移动端建议关闭。

它是中性美术，不因绿色光晕而当作绿色能力物体，不加颜色Layer、ColorObject、碰撞、能力线或房间重置。只读玩家位置用于靠近驱散，不接管玩家运动。保存和片段交付边界与雨区相同：Scene+planning成套，独立美术根不包含在玩法存档片段中。

## 验证入口

只在静音 `Library/FlowRetirementProbe` 隔离工程运行，不修改用户正在编辑的场景。

- `Project.FlowMagma.Editor.TAReactionArtworkAudit.Run`：真实 GPU、精确岩浆孔洞、冷却蒸汽、植物联烧/藤蔓/弹跳、燃烧中及完成后房间恢复。
- `TapTap.TA.RgbGlitch.Editor.RgbGlitchIntegrationAudit.Run`：标准玩家/旧驱动优先级、死亡/Boss 分层、加载复活、退出清理。
- `Project.BossLogic.Editor.BossRgbFearAudit.Run`：真正追逐事件、多 Boss、死亡优先及停止。
- `TAEffectsGpuAudit.Run`：PC/Mobile 两套实际 RenderGraph 画面与 RGB 开/关对照，不等同于安卓真机验证。
- `Project.Rain.Editor.RainIntegrationAudit.Run`：三色帧、像素比例、确定种子、512 上限、HSV 透明轮廓、Undo、Scene 保存重载。
- `Project.Fireflies.Editor.FireflyIntegrationAudit.Run`：中性归属、单硬像素真实GPU、缩放旋转补偿、编辑缓存、128只/4灯预算、Undo和Scene保存重载。

新效果不增加任何联网、局域网发现、遥测或构建权限。
