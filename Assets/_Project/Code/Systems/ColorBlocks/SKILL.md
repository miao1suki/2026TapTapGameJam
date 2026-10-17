---
name: taptap-color-gameplay
description: 2026TapTap 颜色大洗牌后的固定颜色物体、钥匙解锁、房间重置、万能方块代理、直接组件交互和交互图退役规范。
---

# 颜色大洗牌玩法与组件规范

> 2026-10-17 挂接与向上转角：合法点击绿钩目标当帧挂接/开始运动，出线仅视觉；标准发射5格、挂接额外3格冗余、连续超距0.4秒才断，回范围清零，仍不能跨区域。普通藤蔓及单格/竖向绿块补齐真实露顶的向上交接，上方真实节段或实体遮挡时不提前翻顶；保留横向绿块完整上下绕角和跳跃脱手。源码和验证见 GREEN_HOOK_API、CONTACT_CUBES_API；覆盖旧等待出线命中和挂接严格5格断绳描述。

> 2026-10-17 平台路线：`pathMode=0`兼容原route/travelBlocks/reverseRoute；新增四向单程和以中心左右/上下范围，独立方向距离、中心偏移及首程方向。Scene、规划与运行共用MovingPlatformPath，不能再单独推导预览方向。作者位置始终是初始化/重置位置，中心不得使解锁瞬移；曲柄0%仍是作者位置。完整序列化和曲柄范围语义见Docs/Agent/Integration/MECHANISM_SIGNAL_API.md。

> 双阶段解锁与唯一玩家：同色首把钥匙只激活全部对应场景物体，第二把才开放玩家对应颜色能力；两种状态独立，R/离区保留，新游戏ResetProgress清空。IsUnlocked/Unlock只表示场景颜色；IsAbilityUnlocked/UnlockAbility只表示玩家能力，轮盘不得用场景解锁状态。钥匙经CollectColorKey同步且幂等结算，不在OnDisable补解锁。唯一标准玩家为Content/Player/Player.prefab，1×1、碰撞中心归零，各场景共用。完整API、场景迁移与空存档原点生成规则见Docs/Agent/Integration/COLOR_UNLOCK_AND_STANDARD_PLAYER.md；本条覆盖后文旧的单阶段解锁描述。

> 2026-10-16 连续能力与转角：选色持续保留，染色或绿钩子成功不再清空；中央确认取消仍清空。横向绿块攀附按跳跃脱手下落，短暂禁止重抓；真实横藤碰撞外缘转角由玩家运动器交接，失效/重置清理。此条覆盖旧自动清色与横爬禁跳描述。触摸按键持有到对应手指抬起，设置支持持久化安全区布局，见 Docs/Developer/MOBILE_CONTROLS.md。

> 2026-10-16 朝向交互：附近按键交互、搬运目标与角色共享 PlayerController.FacingDirection，排除背后并保持相近目标稳定；默认逻辑占格始终1×1。Q/E保留左右放下并转向对应侧，运行时虚线框复用完整落点校验（含合并体积与房间边界）。轮盘中央确认可清空能力，显式取消触发AbilityDeselectedByPlayer与“当前未使用颜色能力”提示；成功染色后的自动清空不冒发手动取消提示。屏幕点击染色不套朝向筛选。水体停用只停用并延迟销毁，不在父节点禁用中SetParent。

> 2026-10-15 玩法水体表现：WaterVisualFeature默认使用Resources/ColorBlocks/WaterPresentation，按相邻体积生成TA水面/水中/水下面片与前方透明折射板，不生成旧InteractiveWater顶板/波纹。水、气泡均用ColorAppearanceManager共享材质渐显，保留blue Layer；PixelWaterRefraction专属通道不限定Water Layer，并纳入HSV遮罩。定向水流自动四向气泡，浮力柱沿上方连通水域向上，普通水域不加流向气泡。旧waterPrefab等字段只作缺配置后备。此条覆盖下文旧水体默认生成说明。接入先读Docs/Agent/Integration/GAMEPLAY_PIXEL_WATER_API.md。

> 2026-10-14 标注图水流：DirectionalCurrentFeature 增加向上/向下，保留 Left=0、Right=1 的旧存档值，Up=2、Down=3；定向水流仍是同一固定功能的方向参数，不是水域/气泡柱形态切换。编辑器规划参数必须持久保存方向。

> 2026-10-13 曲柄拆分：CrankPlatformFeature 保留脚本身份但只做独立曲柄控制器，不带平台子物体、不写平台 Transform。新建曲柄配套创建独立 Red_CrankLift，消费者 MovingPlatformFeature.BindCrank 以自己的作者位置、路线/距离跟随共享进度；支持一曲柄多平台，任一载人加快整组下降，曲柄绑定基座通电制动。规划绑定 crankOwnerId 是持久对象 ID，合并要求相同绑定，合并/解除及增量重建重新解析，不存 instanceID。曲柄不可合并，平台合并成一个模型与一套逻辑。此条覆盖下文曲柄自带升降平台的旧说明。

> 2026-10-13 机关与跳跃定稿：三色 ContactCube 现在挂 ColorObject 接入钥匙/HSV/搬运门控，初始未解锁，解锁对应色才参与接触反应；仍不是玩家点击染色目标。红块实现 IEnergySource，任意已解锁红块可供能任意基座，旧 Red_Battery 保留 GUID 并替换为红块。移动平台默认信号驱动，绑定基座；曲柄平台通电制动、按住交互升高、空载慢降/载人快降。地图绑定存基础对象 ID 与相对格偏移，不存 instanceID。玩家默认一格宽高，普通跳跃上升4格（第1格中心到第5格中心）；植物普通3格，落差超过4格且按住跳跃为8格，搬运时站到植物获得一次4格跳跃。此条覆盖下文接触方块不受钥匙及携带一律禁跳的旧规则。接入读 Docs/Agent/Integration/MECHANISM_SIGNAL_API.md。

> 2026-10-13 绿接触方块实体规则：ContactCube.Green 及其一体伸长部分始终与玩家碰撞，没有水柱命令时锁水平位移，落稳锁竖直位移；母块及长出实体两侧可攀爬，横向长出部分保留上下攀附禁跳。底部支撑检测不可按整体高度比例缩小。此条覆盖绿块空中忽略玩家的旧规则；红/蓝仍保持原有空中规则。

> 2026-10-15 按钮统一压力触发：DoorButton 与 ButtonLockFeature 均不绑定 F/E，只有玩家脚部或三色 ContactCube 底部压住才触发，不能以玩家头部触碰触发。默认 Latched（一次保持），R/离区保留；Held（持续压住）离开压力、禁用、R/离区复位。红色按钮仍受红色解锁控制。按钮不实现 IInteractionTarget；曲柄/搬运按键不变。门信号不齐时关闭且不清除其他保持按钮。三色方块按作者根位姿恢复。这条覆盖旧按钮键盘触发、拉杆切换和一律持久描述。

> 2026-10-16 绿钩子更新：瞄准与出线随指针自由转向，与红蓝线共用起点，确认命中才接管玩家；只有拉物/拉人施力限制四向，不能将此限制重新加到发射。红蓝 ContactCube/基座逐物理步优先拉物、受阻改拉玩家；绿色 ContactCube/生长体、藤蔓、弹性植物为切向输入荡绳锚点，连接中直接点击可换点且保留动量。成功挂接不因接触、受阻或超时脱钩，仅现有跳跃/交互主动结束；死亡、重生、离区、目标失效清理。保留范围、双阶段解锁、房间边界和额外演出锁，线可穿墙而实体不可穿墙。接入见 `Docs/Agent/Integration/GREEN_HOOK_API.md`，本条覆盖旧四向发射、发射即冻结和自动结束规则。

> 2026-10-13 独立接触方块例外：ContactCubes 中的红/绿/蓝是三个固定类型道具，ColorObject 仅桥接钥匙解锁与外观，不接收玩家点击染色，不能挂 IColorApplicationTarget 或生长等旧 BlockFeature。彼此只走有限接触状态机（绿蓝增长、绿红缩短、红蓝同时锁长、红蓝水柱），对既有颜色物体仍通过其接色契约转发。反应只发推动请求，ContactCubeMotion 已统一负责重力/水柱运动/落稳锁定/空中碰撞；搬运已通过 CarryableFeature + ICarryMotionParticipant 接入，动画期间挂起运动和反应，完整实体校验落点；黏住待接入，不得并行写刚体。横向长出部分通过玩家 API 攀附。接入先读 Docs/Agent/Integration/CONTACT_CUBES_API.md，不套旧万能方块规则。

> 2026-10-12 区域生命周期定稿：Rooms/RoomLifecycleManager 按世界图实际占格管理房间/通道。离开或死亡/R重生仅恢复所属物体的布置状态与位置，保留门、按钮、已拾取钥匙和全局解锁；已解锁物体恢复刚解锁的行为而非白色锁定。ResetForRoom 不清空颜色、不全局广播；仅 ResetProgress 明确清空进度。R 返回最近触碰的锚点，取消携带并恢复区域，不再只是回上一次拿起位置。此条覆盖下文旧房间清锁与搬运重置说明。接入必须读 Docs/Agent/Integration/ROOM_LIFECYCLE_API.md。

> 水体透明渲染：水面/横截面使用 MeshRenderer，Shader Graph 使用 URP Unlit 透明子目标，不使用依赖 SpriteRenderer 翻转/颜色参数的 Sprite Lit。保留原水效节点，透明度只由水体原始 Alpha 与 `_ColorRevealOpacity` 相乘；生成水体注销先停用并延迟销毁，禁止在父节点禁用中移出宿主；同帧重新绑定不复用停用旧实例。

> 分类表现解耦：`HSVColorFadeManager.SetHsv(id, saturation, whiteAmount, seconds)` 只控制全屏 HSV，不读解锁、不操作材质或 Renderer。`ColorAppearanceManager.Play(id, unlocked, duration)` 统一编排基础方块、水、藤蔓的外观；按源材质/颜色组/基础或正式阶段共享运行时材质，物体只注册、注销，不逐帧写透明度。原生缓存不在组件构造时创建。遮罩只裁剪透明度零值，不再次将 Alpha 乘进 HSV，避免漏出原色。藤蔓在运行时用独立透明拖尾 Shader，保留源纹理/颜色，源资产不变。此条覆盖下文关于 HSV 负责交接、GetOpacity 和自动扫描 Renderer 的旧描述。

> 2026-10-09 透明交接与手感：基础外观变白同时淡出，完全透明时交接正式外观再渐显。`GetOpacity` 与 HSV 饱和度分离，几何 Alpha 由材质处理，HSV 遮罩尊重同一透明度；像素化仍先于 HSV。藤蔓中段向外蹬跳，接近根系顶部改为向内更快、更高起跳，并短暂慢落。气泡出水、植物弹跳、红蓝蒸汽的额外速度和短暂控制保护统一由 PlayerController 管理，持续蒸汽不能逐帧累计速度。这条优先于下文旧的纯白交接与固定向外蹬跳描述。

> 2026-10-09 藤蔓/像素化：水和藤蔓保留颜色普通 Layer，通过 Rendering Layer 位 7（128）接像素遮罩。母根统一持有 TA 粒子，30.13→32.43 连续采样，初始一格/长成根系自适应高度；未解锁不生成。命中任何节段的红色交互（含万能方块广播）仅沿该所有者根系顶到底逐节燃烧，倒放缓存粒子，预置节段休眠以支持房间重置。子节段仍不能自行蓝色生长。此条覆盖旧的只 Destroy 命中单节藤蔓行为。

> 2026-10-09 外观交接：Game 不允许使用颜色目录 targetMaterial 的编辑边框识别外观。未解锁使用黑白基础材质，恢复时先推至纯白，交接纯白正式外观后再恢复颜色；水系只显示真实水体。正式状态材质留空时隐藏识别 Renderer，或使用功能组件为该 Renderer 配置的专属材质。层级 HSV 通道包含透明水体，功能启停仍与表现进度解耦。此条覆盖旧的直接切编辑彩色材质描述。

> 2026-10-09 攀爬输入补充：W 或朝藤蔓方向的水平输入均为上爬，反向水平输入用于离开；朝藤蔓方向加跳跃触发向外上方蹬跳并短暂脱离，单独按住跳跃仍可定住。此条覆盖下文旧的“AD 均用于离开”描述。

> 2026-10-07 更新：万能方块作为前期辅助代理保留，自身显示纯色，并向左右上下四个紧邻的 `IColorApplicationTarget` 广播同一颜色。当前固定功能组件已落到 `BlockFeatures/Runtime/Features`，并开始通过组件自身的 `IColorApplicationTarget` 接色入口实现三色反应。

> 2026-10-11 搬运重做：能源方块和万能方块共用 `CarryableFeature`，不再使用长按右键、鼠标吸附拖拽和旧的 `IPlayerPointerDrag`。玩家按 1×1 默认占格检测上下左右紧邻目标并维护拾取队列，按 `F` 固定队列首项，冻结 0.1 秒后由 DOTween 移到玩家头顶。携带期间只能移动和冲刺，不能跳跃、调色、交互、染色或普通动作；`Q` / `E` 分别请求在自身左侧 / 右侧放下，只检测玩家占格方向的紧邻格，非法时保持携带。能源方块的 `CarryableFeature` 受同物体 `IColorObject.IsActive` 门控；万能方块没有颜色对象，常态可搬运。持续携带提示和非法落点提示统一通过 `WordNoteUI` 显示。

`CarryableFeature` 是共享运行时能力，不实现 `BlockFeature` 的颜色形态生命周期；它通过同物体 `IColorObject` 的存在和激活状态自动决定是否进入玩家拾取队列，因此同一组件可以同时挂在能源方块和万能方块上。

可搬运物必须保存拿起前的原始世界位置。`Respawn` 绑定显示名称改为“复活/重置搬运”：携带中按下它，或玩家在携带期间死亡时，目标都通过 DOTween 飞回原始位置；动画完成后恢复 Collider、结束携带状态并释放玩家控制。携带提示由 `WordNoteUI` 显示，内容为“按 当前左/右键 将方块放置在左/右侧，按 当前复活/重置键 重置方块”；它位于下方分区，参与堆叠，显隐动画各 `0.5` 秒。非法落点也在下方分区，显示“位置非法，请留出足够空间”并存活 `2` 秒，显隐与左移组合入场、显隐与右移组合退场，各 `0.5` 秒；两者统一白字黑描边。

## 2026-10-09 掉落物生命周期基类

所有可拾取掉落物统一继承 `Project.Items.DropItemBase`。颜色钥匙和以后新增的钥匙、道具、收集物都必须遵守这一层，不允许各自实现一套启停、销毁和 Tween 生命周期。

固定流程：

```text
OnEnable
→ ResetItemState
→ PlayAppearAnimation

感知范围
→ TryBeginAttraction
→ PlayAttractAnimation
→ 追向 CollectionTarget 并缩小
→ 真正碰到玩家
→ CompleteAttraction
→ TryBeginCollection
→ OnCollectionStarted
→ PlayCollectAnimation
→ 解锁、销毁和其他拾取表现
→ OnCollectionFinished
→ RecycleItem
```

职责约束：

- `OnEnable` 只负责重置掉落物状态和播出现动画。
- 玩家进入感知范围只启动磁吸，不结算颜色、不销毁钥匙。
- 磁吸动画通过 `CollectionActor` / `CollectionTarget` 获取玩家目标；颜色钥匙的目标表现是逐渐缩小并追随玩家。
- 真正碰到玩家后才调用 `CompleteAttraction`，再进入 `TryBeginCollection` 和拾取结算。
- `OnCollectionStarted` 负责关闭碰撞、解锁颜色、隐藏钥匙并发起镜头演出。
- `OnCollectionStarted` 通过 `IPlayerControlLockTarget.AcquireControlLock` 锁定玩家全部操作；`OnCollectionFinished` 或对象禁用时必须释放同一句柄。
- `PlayAppearAnimation`、`PlayAttractAnimation`、`PlayCollectAnimation`、`StopItemAnimations` 是统一动画钩子。DOTween 导入后在这里实现出现、磁吸、收集和清理，不在每个子类里重复写 Tween。
- `PlayCollectAnimation` 必须保证 `onComplete` 只回调一次，回调完成后才允许基类真正回收物体。
- 派生类不得在拾取入口直接 `Destroy(gameObject)`、`SetActive(false)` 或提前关闭 Renderer，否则收集动画会不可见。
- `OnDisable` 只停止动画和清理状态，不能在这里等待消失动画。
- 对象池掉落物必须重写 `RecycleItem` 走对象池归还；没有对象池时保持默认销毁。
- 预制体复用时必须恢复 Collider、Renderer、Transform、材质和逻辑状态，不能保留上一次拾取的 `collectionRequested` 状态。

当前未导入 DOTween，因此禁止引用 `DG.Tweening`。基类只保留空动画钩子和生命周期；钥匙的镜头 Timeline 与颜色渐显属于拾取演出，可以和基类收集动画并行，但必须都结束后才回收。

当前接入：

- `ColorKeyPickup` 继承 `DropItemBase`。
- 玩家进入感知范围后钥匙开始磁吸；真正碰到玩家后才关闭 Collider、解锁颜色并隐藏钥匙。
- 三把颜色钥匙的 Timeline 必须强制正交 2D、固定前方朝向，机位使用 `(0, 1, -10)`，关闭看向角色并锁住相机纵深；只允许平面平移和推拉缩放，避免 3D 场景侧壁露出。
- `ColorKeyPickup` 在前段 Timeline 到达揭示进度后立即 `PlayableDirector.Stop()` 并释放玩家控制锁；相机回位与颜色渐显并行，拾取视觉和回收继续异步收尾，不能延迟输入解锁。
- 以后新增掉落物不得直接继承 `MonoBehaviour` 写拾取生命周期。

## 2026-10-07 点击染色与气泡柱收口

- 左键屏幕射线直接查找 `IColorApplicationTarget`，不再只查找 `IInteractionTarget`。
- `BlockFeature` 统一实现 `IColorApplicationTarget`，具体功能通过重写 `OnColorApplied` 处理颜色作用。
- `PlayerColorWheel` 的交互范围圈只在玩家当前持有选中颜色时显示，并持续慢速呼吸；未持有颜色时完全隐藏。闪烁周期和透明度上下限在 Inspector 暴露。
- 范围圈出现和退场都必须渐入/渐出到 alpha 0，不能瞬切；颜色成功应用到目标后，当前选色立即清空，范围圈随之呼吸退场。
- 游戏内常态隐藏鼠标；持有待用颜色时显示鼠标，颜色成功应用或选色被清空后隐藏。ESC、主菜单等菜单态优先显示鼠标，暂停期间颜色系统不得覆盖菜单的光标状态。
- 持有待用颜色时点击无效目标，在居中下方提示 `这个物体好像不能与这个颜色反应`；目标超出交互范围提示 `无法触碰到这个物体`；颜色成功应用后提示 `染色成功`。三条反馈与非法放置提示共用 2 秒显隐、左入右出、白字黑描边和堆叠规则。
- 左键颜色交互先按鼠标世界命中点判断是否超出交互圈，再判断命中物体能否接收颜色；圈外普通物体也必须提示 `无法触碰到这个物体`。
- 交互圈外的点击统一提示 `无法触碰到这个物体`，不区分空气或实体：空气按玩家世界平面换算点击点，实体按射线命中点计算距离。
- 已接反应：红色清除绿色物、蓝色让藤蔓生长、红色作用于任意蓝色物生成蒸汽；蓝色作用于岩浆由岩浆自身配置处理。
- 红色作用于任意蓝色物时使用 `WaterVisualFeature` 的蒸汽配置：可选择瞬间一股热气或持续蒸汽，持续模式可配置时长，另可配置推力、作用高度和热浪特效资源。
- 浮力水柱不再要求“上方已有蓝系物体”，也不再有最高顶起格数。
- 气泡柱持续跟踪玩家：气泡在玩家下方，且玩家碰撞体与任意蓝色物体的垂直重叠超过自身高度一半时保持上浮；露出超过一半后立即移除气泡水状态、清空浮力并清除剩余向上速度，重新下沉时立即恢复上浮，横移离开气泡列或落到气泡下方才清除状态。

## 2026-10-07 功能组件补充定稿

以下内容优先于后文旧的万能方块/染色描述：

- 岩浆触碰即死，是全局唯一死亡方式；不保留玩家血条组件，也不保留摔落伤害或摔落免疫参数。
- 机关基座只检测贴在一起的能源方块并作为 `IMechanismSignalSource` 输出；只有能源方块可以迁移位置。移动平台等消费者从自己这一侧绑定基座、按钮或曲柄，基座不反向绑定机关。
- 按钮没有实体阻挡，检测玩家脚部/方块底部压力；脚部容差以格为单位，不按交互半径响应按键。
- 曲柄接收玩家长按交互键，按住期间缓慢转动并推进开启进度。
- 红色机关组件至少允许调节：路线类型、游荡格数、单程时间、端点停留、自动或按钮驱动、是否载人、可载玩家标签。
- 蓝色水域、定向水流、浮力水柱先共用现有蓝色系材质。
- 蓝色水域、定向水流、浮力水柱统一继承 `WaterVisualFeature`，默认实例化 `ColorCatalog.blueWaterPrefab`；水体预制体、本地偏移和缩放都可在 Inspector 调整。
- 水族生成水体时按关卡格子中心对齐并按格子体积配置顶部/正面网格，避免水体资源从局部原点偏移。
- 定向水流左右/上下方向、推动速度、是否只在游泳时生效都必须在 Inspector 中可调。
- 浮力水柱只有在其上方存在其他蓝色系物体时才把玩家向上推；向上检测高度和横向检测宽度都按关卡格数配置，上浮速度和最高顶起格数可调，玩家识别使用 Unity Tag。
- 藤蔓是实体阻挡物，玩家只能紧贴两侧攀爬；进入攀爬距离按格暴露，支持 W 上爬、无输入自然下滑、S 加速下滑、空格固定，AD 可离开攀爬范围。
- 弹性植物是实体阻挡物，只在玩家从上方落上或满足跳跃触发条件时弹跳一次，不能像游泳一样持续上飘。
- 植物障碍默认实体阻挡；破坏接口先留出，后续可接火焰、藤蔓反应和销毁动画。
- 所有固定功能组件都必须暴露尽可能多的参数、调试状态和适用范围开关。
- 所有固定功能组件统一继承 `IFeatureVisualTarget` 的“专属材质 / 材质目标渲染器”表现接口，后续可像水域一样给任何一种颜色道具接入专属材质，不影响基础颜色材质和失效恢复。
- 跨色反应先保留接口和参数位：
  - 红色接触任意绿色物：清除绿色物；预留销毁动画、延迟、特效和参数。
  - 蓝色接触藤蔓：在原地向上生成绑定的生长物预制体；推荐使用 `Green_LadderSon`。每块高度直接读取关卡编辑器格子世界尺寸，参数包括生长物预制体、最大生长块数、瞬间长完 / 逐块出现和生长间隔。新块启用时播放自身出现动画，瞬间长完时只让最上方的新块播放。
  - 红色接触任意蓝色物：替换为可配置占位方块；预留替换预制体、延迟和动画参数。
  - 蓝色接触岩浆：在岩浆上方生成向上气流，参数预留高度、推力、持续时间和层；不要求附近先存在水体。

## 2026-10-07 功能组件第二轮校正

本节再次覆盖前文所有旧参数命名和旧布尔开关：

- 交互动作固定同时支持点击和长按，不能在按键映射里改为仅点击或仅长按。玩家输入驱动同时分发 `IInteractionTarget` 和 `IInteractionHoldTarget`。
- 玩家除岩浆外不会死亡或受伤；`PlayerFallDamage` 只负责给弹跳植物计算当前/最近坠落高度，不结算伤害，也不提供摔落免疫。
- 水泡柱必须可调：最高顶起格数、上浮推力；不使用 LayerMask 检测玩家，玩家识别走 Unity Tag。
- 水流只有“向左 / 向右”枚举，并暴露推搡速度。
- 弹性植物必须可调：基础弹跳力度、低落差回弹比例、高落差回弹比例、高落差参考高度（格）、最大弹速、最短触发间隔、连续弹跳衰减比例、衰减重置时间和最低弹跳高度（格）。回弹高度按“落差 × 当前回弹比例”计算；落差越高，回弹比例从低落差比例插值到高落差比例，高落差默认最低保留一半，且不会超过原落差。连续弹跳再乘既有衰减。玩家侧管理本次下落高度、最近落地落差和跨植物连续弹跳衰减；玩家真实落地时通过 `IPlayerBounceSurface` 从脚下植物请求弹跳速度并应用。
- 可攀爬藤蔓必须可调：攀爬倍率、生长物预制体、最大生长块数、瞬间长完 / 逐块出现、生长间隔。母藤蔓生成 `LadderSonFeature` 生长物；生长物继承攀爬和出现动画，但禁用蓝色浇灌生长，只能被红色烧毁。每块高度直接读取关卡编辑器格子世界尺寸，不单独暴露每块高度、出现动画时长或缩放曲线。新块启用时播放自身出现动画，逐块生长时每块都播放，瞬间长完时只让最上方的新块播放。
- 搬运规则：能源方块和万能方块共用 `CarryableFeature`。玩家按 1×1 默认占格检测上下左右紧邻目标，`F` 固定拾取队列首项，冻结 0.1 秒后目标关闭 Collider、置顶并移动到玩家头顶；携带期间只允许移动和冲刺；`Q` / `E` 分别请求左侧 / 右侧放下，只检查玩家占格方向的紧邻格。
- 按钮可调一次保持/持续压住、初始开启、状态材质与脚部容差；玩家按键不切换按钮状态。
- 曲柄不使用圈数：只暴露“每秒转动进度”，满进度固定 100%；可调无人后是否回弹、等待多久回弹、每秒回弹多少；必须暴露转动动画和回弹动画资源。
- 机关基座不做物理检测间隔、不做层检测。只暴露左、右、上、下四个方向的网格检测开关；选中方向邻接能源方块时持续输出，能源方块类型由接口指定。
- 岩浆仅接触即死，不保留每秒血量/伤害数值；暴露蓝色交互后替换成的 `GameObject`。
- 移动平台：距离按方块格数，不使用 Vector 或每格大小；必须承载玩家；承载标签使用 Unity Tag 选择器；机关操控时必须能绑定按钮、基座、曲柄等信号源，并可选择“任一激活”或“全部激活”。
- 移动平台游荡时做前向碰撞检测，碰到非玩家实体方块后立即反向。
- 移动平台只承载真正站在顶面的玩家；平台位移通过 `PlayerController.QueuePlatformDelta` 交给玩家 Rigidbody 执行，不直接改玩家 Transform，也不把玩家挂成平台子物体。

## 2026-10-07 功能组件第三轮校正

本节再次收口所有与关卡网格和藤蔓相关的旧参数：

- 关卡编辑器在放置和生成时为 `LevelEditorPlacedBlock` 记录 `cellWorldSize`，并实现 `IGridCellSizeProvider`。
- 需要按格计算距离、高度或检测范围的功能组件必须通过 `GridCellSizeUtility` / `BlockFeature.GridCellWorldSize` 读取该值，不再在组件 Inspector 暴露“每格大小 / 每格高度”这类字段。
- 曲柄仍接受一格范围内的长按交互。搬运目标只依赖 `IPlayerCarryHost` 命令口，不直接依赖 `PlayerController`。
- 机关基座四方向检测、移动平台游荡格数、水泡最高顶起格数全部以关卡格子世界尺寸换算。
- 藤蔓克隆自身生成后续块，不把 `Sequence` 之外的已有藤蔓或视觉子物体带走；生成物由藤蔓组件记录并在 Reset / Detach 时清理。
- 机关控制方向统一为“消费者绑定信号源”：移动平台在 `linkedControls` 中选择基座、按钮、曲柄并订阅 `IMechanismSignalSource`；基座、按钮和曲柄不保存机关列表，也不主动寻找消费者。

## 2026-10-07 功能组件第四轮校正

- 浮力水柱的上方蓝系检测统一为网格参数：`向上检测高度（格）` 和 `横向检测宽度（格）`。
- 检测盒从气泡上边缘开始，垂直高度为格数乘关卡格子世界尺寸，横向总宽度也为格数乘关卡格子世界尺寸；不再暴露世界单位的检测距离或半径。

## 2026-10-07 功能组件第五轮校正

- `PlayerColorWheel` 左键不再使用交互圈内最近目标。
- 左键先做屏幕射线，命中最靠前的可交互物体，再检查它是否在 `PlayerInteractionSensor.ScanRadius` 交互圈内；超出圈外或点击 UI 时不执行颜色交互。
- 万能方块作为可点击代理参与该链路：玩家选中颜色后点击屏幕上的万能方块，万能方块自身显示对应纯色，并向四个紧邻目标广播颜色。

## 2026-10-07 万能方块校正

- 万能方块自身使用纯色材质，不套红蓝绿功能物体材质。
- 广播方向固定为左右上下四个紧邻格，距离由 `IGridCellSizeProvider` 的格子世界尺寸换算。
- Inspector 暴露“持续染色 / 过一会褪色”模式；选择延迟褪色时暴露褪色延迟秒数，并提供当前颜色、褪色倒计时和广播目标数调试。
- 持续染色保留到房间重置或下一次广播；延迟褪色只恢复万能方块自身纯色，不会替邻接目标撤销它们自己的状态。

## 2026-10-07 功能组件第六轮校正

- `Tab` 对应的“调色”动作只负责开关染色轮盘，可在按键映射中选择点击或长按触发。点击模式按一次打开，再按一次或选色时关闭；长按模式按住期间打开，松开或选色时关闭。`F` 负责交互和搬运。
- 曲柄/搬运继续使用现有统一交互键 F；按钮不接入 `IInteractionTarget`。
- 玩家驱动不再申请相机 2D/3D 切换；相机模式仍由相机系统/Timeline 自行管理。
- `PlayerInputDriver` 只读取输入并调用 `PlayerController` 的颜色轮盘命令；`PlayerColorWheel` 不直接读取 `GameInput`。
- 藤蔓新增实体碰撞、按格进入距离、自然下滑速度、S 加速下滑速度和空格固定行为。
- 按钮使用按格脚部容差；平台碰方块反向；水族和弹跳植物的表现/碰撞按上方实体边界修正。

## 2026-10-11 搬运与放下校正

- 能源方块和万能方块共用独立 `CarryableFeature`；能源方块在颜色未解锁时不可搬运，万能方块常态可搬运。
- 玩家脚本按默认 1×1 占格检测上下左右紧邻目标，进入范围后注入拾取队列。按 `F` 暂定队列首项，玩家冻结 0.1 秒后开始搬运。
- 搬起后目标 Collider 关闭，Renderer 临时置顶，目标通过 DOTween 飞到玩家头顶并持续跟随。
- 携带期间只能移动和冲刺，不能跳跃、调色、交互、染色或普通动作。`Q` / `E` 请求左侧 / 右侧放下；只检测玩家占格方向的紧邻格，非法落点保持携带。
- 左右放下前必须先沿放下方向从玩家中心做 `1.1` 格射线；命中玩家和当前搬运物之外的 Collider 直接判非法，避免一层薄墙被目标格检测绕过。
- 目标格必须先查 `LevelEditorPlacedBlock.ContainsCell` 的关卡网格占用；同格已有方块或道具时直接非法，物理 Collider 只能作为补充检测，不能绕过网格占用。
- 忽略玩家或当前搬运物自身时只能比较自身层级；不能使用 `transform.root`，因为规划生成的地图方块可能全部共用一个父根，会误跳过整个地图。
- 搬运目标只通过 `IPlayerCarryHost` 读取玩家锚点和中心点，不直接依赖 `PlayerController`。持续携带提示和非法位置提示通过 `WordNoteUI` 显示，默认白字黑描边。
- `WordNoteUI` 入退场动画使用四条独立组合通道：显隐、水平移动、竖直移动、缩放；可分别启用并编辑方向、距离、时长、初末速度和变速指数。
- 可搬运物记录原始位置；携带中按“复活/重置搬运”或玩家死亡时，目标 DOTween 回原位，动画结束后恢复 Collider 并释放控制。携带提示通过 `WordNoteUI` 显示实际当前按键。

## 适用范围

本规范适用于颜色钥匙、红蓝绿场景物体、万能方块、房间重置、颜色物体之间的直接反应，以及交互图移除后的迁移工作。

当前 `origin/main` 仍是旧的交互图和动态颜色结构。本文件描述目标架构。迁移期间可以暂时保留旧路径做对照，但不得把新功能重新接回交互图。

## 最终玩法结论

1. 红、蓝、绿场景物开局全部是白色失效状态。
2. 玩家拾取对应颜色钥匙后，该颜色的全部物体恢复原色并启用自身固定功能。
3. 离开房间后，全部颜色物体重新变白失效，钥匙效果清空。
4. 物体在放下时已经决定自身形态，不会跨颜色变化，也不会在同颜色内切换形态。
5. 蓝色族的水源、定向水流、浮力柱是三个不同组件和预制体，不共用一个运行时模式枚举。
6. 每个功能只由一个组件负责，组件自己管理触发、条件、状态、表现、生成物和清理。
7. 不再用通用交互图描述物体关系。
8. 物体之间的反应使用类型安全的直接接口调用。
9. 万能方块只负责自身纯色表现和向四方向紧邻目标广播颜色，本身不拥有玩法能力。

## 栏目归属

关卡编辑器中的栏目职责必须严格区分：

- 方块栏目只用于地图墙体、地形块和几何碰撞，不放策略玩法物体。
- 道具栏目承载全部红、蓝、绿玩法预制体。
- 颜色玩法预制体统一位于 `Assets/_Project/Content/ColorBlocks/Prefabs`。
- 玩法预制体命名使用颜色前缀加功能名：`Red_*`、`Blue_*`、`Green_*`。
- 关卡编辑器新增、生成、覆盖和删除时必须继续保持这条边界。

本轮已配置的道具预制体包括：

```text
Red_Lava
Red_Battery
Red_Button
Red_Crank
Red_Foundation
Red_Platform

Blue_WaterSource
Blue_WaterWay
Blue_WaterBubble

Green_Ladder
Green_JumpPlant
Green_PlantObstacle

Universal_Block
```

`Universal_Block` 是万能方块代理的基础预制体。它只挂 `UniversalColorBlock`，不挂 `ColorObject`、`BlockRuntime` 或 `BlockAbilityHost`，也不属于任何颜色解锁组。

这些 prefab 当前只是渲染、碰撞和颜色骨架，具体组件尚未接入。后续约束建议：

| Prefab | 类别 | 主要交互 |
|---|---|---|
| `Red_Lava` | Hazard | PlayerContact、ObjectContact |
| `Red_Battery` | Mechanism | ObjectContact、AppliedColor |
| `Red_Button` | Mechanism | PlayerContact、ObjectContact |
| `Red_Crank` | Mechanism | PlayerContact、ObjectContact |
| `Red_Foundation` | Mechanism | ObjectContact、AppliedColor |
| `Red_Platform` | Movement | ObjectContact、AppliedColor |
| `Blue_WaterSource` | Water | PlayerContact |
| `Blue_WaterWay` | Water | PlayerContact |
| `Blue_WaterBubble` | Water | PlayerContact |
| `Green_Ladder` | Plant | PlayerContact |
| `Green_JumpPlant` | Plant | PlayerContact |
| `Green_PlantObstacle` | Plant | ObjectContact |

所有组件默认还必须声明 `RoomReset`。

## 术语

- `A`：玩家当前选中的、已经解锁的颜色 ID。
- `B`：实际接收颜色并执行自己固定逻辑的物体、机械或机关。
- `ColorObject`：拥有固定基础颜色和解锁状态的场景物体。
- `ColorObjectFeatureHost`：根据颜色解锁和房间重置，统一启停物体上已有的功能组件。
- `UniversalColorBlock`：固定在场景中的颜色交互代理，自身显示纯色并向四方向紧邻目标广播 A。
- `Feature`：一个具体功能组件，例如水源、水流、岩浆、移动平台、藤蔓。

## 颜色与形态边界

颜色只表示钥匙解锁分组。

```text
BaseColorId = red / blue / green
```

形态由预制体和组件决定。

```text
WaterSourceFeature
DirectionalCurrentFeature
BuoyancyColumnFeature
```

这三个组件都可以有 `DefaultColorId = blue`，但运行时始终分别执行自己的功能。

禁止以下设计：

- 水源运行时变成水流。
- 水流运行时变成浮力柱。
- 蓝色物体运行时变成红色物体。
- 同一个蓝色功能组件用枚举在三种形态之间切换。
- 通过染色把 A 形态改成 B 形态。
- 用 `CurrentColorId` 决定物体是什么功能。

允许的状态变化只有：

```text
白色失效
→ 恢复原色并启用固定功能
→ 离开房间重新失效
```

## 万能方块

万能方块是固定交互代理，不是颜色源、不是功能本体，但和能源方块共用可搬运组件。

交互流程：

```text
玩家选择颜色 A
→ 点击 UniversalColorBlock
→ UniversalColorBlock 自身显示 A 的纯色
→ 检查左右上下四个紧邻格
→ 对每个 IColorApplicationTarget 广播 ApplyColor(A)
```

目标不区分颜色来自玩家直接点击还是万能方块广播。

万能方块必须满足：

- 固定在场景中。
- 不拥有红色、蓝色、绿色玩法能力。
- 不参与红绿、蓝绿、红蓝物体反应。
- 不通过交互图或反射找方法。
- 不保存单个目标 B；按关卡格子尺寸检测左右上下四个紧邻目标并广播颜色。
- 离开房间后不保留跨房间的颜色效果。

## 标准接口

颜色应用目标由 B 实现：

```csharp
public interface IColorApplicationTarget
{
    bool CanApplyColor(string colorId, GameObject actor);
    bool ApplyColor(string colorId, GameObject actor);
}
```

玩家交互流程：

```text
PlayerColorController.SelectedColorId
→ 左键屏幕射线命中前景可交互物体
→ PlayerInteractionSensor 检查目标是否在交互圈内
→ 目标是 IColorApplicationTarget：直接 ApplyColor
→ UniversalColorBlock：自身纯色，并向四方向紧邻目标广播 ApplyColor
```

接触事件使用直接接口：

```csharp
IPlayerContactReceiver.OnPlayerEnter/Exit/Stay
IObjectContactReceiver.OnObjectEnter/Exit
IColorReactionReceiver.CanReact/React
IColorApplicationTarget.CanApplyColor/ApplyColor
IRoomColorResettable.ResetForRoom
```

不要使用：

```text
InteractionManager.Trigger
InteractionObjectDefinition
InteractionNodeKind
InvokeMethod
字符串方法名
反射调用
```

## 组件契约

继续沿用现有项目组件规范：

- 一个组件只负责一个功能。
- 组件必须提供中文显示名。
- Editor 界面首行必须显示粗体中文名，脚本名和组件名不变。
- 使用 UI Toolkit Inspector。
- 参数按语义分组、Foldout、条件显隐。
- 可调参数必须暴露。
- 运行时状态只读展示。
- 调试按钮只在合理状态可用。
- `OnAttach` 建立状态。
- `OnDetach` 清理状态。
- `OnTick` 只处理真正需要逐帧运行的功能。
- 生成物必须记录拥有者并能在 Reset/Disable/Destroy 时清理。
- 多 Collider 按物体实例去重，Enter/Exit 按 Actor 去重。
- 不在高频路径中全场景查找、反射或启动无意义协程。

`DefaultColorId` 的新语义：

```text
物体固定属于哪个钥匙解锁组
```

它不再表示：

```text
当前颜色
运行时可切换的能力配置
```

## ColorObjectFeatureHost

Host 只做薄层启停，不再动态给物体添加全部能力。

正确流程：

```text
预制体已经挂好固定功能组件
→ Host 读取 ColorObject.BaseColorId
→ 对应颜色未解锁：disable 功能
→ 对应颜色已解锁：enable 功能
→ 房间退出：disable、Reset、清理生成物
```

禁止流程：

```text
读取 CurrentColorId
→ 扫描 BlockAbilityCatalog
→ 给物体添加所有功能
→ 动态切换当前颜色对应的功能
```

`BlockAbilityCatalog` 已删除；固定功能组件直接由预制体作者挂载。

当前代码中过渡期仍使用 `BlockAbilityHost` 作为薄宿主名称，行为等价于目标规范里的 `ColorObjectFeatureHost`。它只收集预制体上已有的 `BlockFeature`，不扫描 Catalog、不添加组件、不切换形态。

## 功能组件约束

`BlockFeature` 是具体玩法组件的基类。新增组件必须同时满足：

1. `DisplayName` 写中文功能名。
2. `DefaultColorId` 填写该组件所属的固定颜色解锁组。
3. `Category` 标记功能类别。
4. `Interactions` 声明它参与哪些交互。
5. 组件形态由脚本类型和所属预制体固定，不能用运行时枚举切换水源、水流、气泡柱等不同形态。
6. 组件必须在 `OnAttach`/`OnDetach` 成对管理状态。
7. 组件实现直接接口，不调用 `InteractionManager` 或反射方法名。
8. `Interactions` 声明必须和实际实现的接口一致，编辑器校验会拒绝只声明不实现。

当前可用类别：

```text
Mechanism / 机关
Hazard / 危险物
Water / 水域
Plant / 植物
Movement / 移动
Reaction / 反应
Utility / 通用
```

当前可用交互标记：

```text
PlayerContact / 玩家接触
ObjectContact / 物体接触
AppliedColor / 接收颜色
RoomReset / 房间重置
```

直接接口：

```csharp
IPlayerContactReceiver.OnPlayerEnter/Exit/Stay
IObjectContactReceiver.OnObjectEnter/Exit
IColorReactionReceiver.CanReact/React
IColorApplicationTarget.CanApplyColor/ApplyColor
IRoomColorResettable.ResetForRoom
```

## 现有项目迁移映射

### 保留并改造

- `ColorRuntimeService`
  - 统一管理所有 `ColorObject`。
  - 增加房间级解锁和重置入口。
  - 不再自动生成或聚合并水体视觉。

- `ColorCatalog` / `ColorTypeDefinition`
  - 保留颜色定义。

- `HSVColorFadeManager`
  - 保留白色失效与恢复原色的渐变。

- `ColorKeyPickup`
  - 保留直接入口并继承 `DropItemBase`。
  - 拾取开始时立即关闭碰撞和解锁颜色，收集动画完成后才隐藏和回收。
  - 不依赖交互图。

- `InteractiveWater`
  - 作为水源、水流、浮力柱组件的表现基础；当前没有颜色系统自动生成的水体实例。

- `PlayerController`
  - 保留游泳、攀爬、受伤、击退、移动能力。

- `PlayerHealth` / `PlayerFallDamage`
  - 保留。

- `PlayerInteractionSensor` / `IInteractionTarget`
  - 保留目标查找和点击入口。

- `PlayerColorWheel`
  - 可保留为选色 UI。
  - 只保存未锁颜色与当前选色。
  - 不再触发 `Manual` 图。

- `BlockRuntime`
  - 可作为单物体内部信号、命令、状态和调试总线。
  - 不再承担跨物体通用图职责。

### 退役或重做

- `BlockAbilityHost`
  - 改成薄层激活器。
  - 不按当前颜色查目录和切换全部功能。

- `BlockWaterFeature`、`BlockVineFeature`、`BlockPowerFeature`
  - 旧具体功能组件已经删除。
  - 当前 `ColorBlocks/Prefabs` 中的颜色玩法预制体只是配置占位符，后续按固定形态重新挂功能组件。

- 旧喷流、下一跳、弹跳、藤蔓生长触发组件
  - 已删除；新固定反应后续按直接接口重写。

### 删除

- `InteractionManager`
- `InteractionGraph`
- `InteractionObjectDefinition`
- `InteractionObjectCatalog`
- `ColorBlock` 过渡适配器和 `ColorBlock_*` 预制体
- `InteractionManagerWindow`
- `InteractionGraphView`
- `InteractionEditorService` 中的图同步职责
- `Assets/_Project/Content/Interactions/Definitions`
- `Assets/_Project/Resources/Interactions/InteractionObjectCatalog.asset`
- 预制体和关卡栏目上的 `interactionDefinition`
- `InteractionObject` 的图来源实现
- 所有 `InteractionManager.Register/Raise/Trigger`
- `GetCompiledGraph`
- 节点队列、逐次协程和 `InvokeMethod`

## 建议功能组件拆分

红色：

```text
LavaHazardFeature
MechanismBaseFeature
MovingPlatformFeature
LiftFeature
SpikeFeature
LadderFeature
ButtonLockFeature
CrankPlatformFeature
```

蓝色：

```text
WaterSourceFeature
DirectionalCurrentFeature
BuoyancyColumnFeature
```

绿色：

```text
ClimbableVineFeature
BouncePlantFeature
PlantObstacleFeature
```

固定反应：

```text
BurnPlantReaction
VineGrowthReaction
HardPlatformReaction
SteamPushReaction
```

## 固定反应规则

红色加绿色：

```text
岩浆或高温接触植物障碍
→ 植物烧毁
→ 清除碰撞
→ 打开路径
```

蓝色加绿色：

```text
水流接触藤蔓或植物
→ 藤蔓延伸
→ 生成新攀爬路径
```

红色加蓝色：

```text
岩浆接触水体
→ 生成硬质通行平台

高温接触水体
→ 生成蒸汽推力
→ 推动玩家或机关
```

反应组件必须负责：

- 重复触发保护。
- 生成物拥有者。
- 离开、禁用、销毁和房间重置时清理。
- 参数暴露，不把距离、持续时间、力度硬编码在全局服务。

## 房间重置

房间重置至少处理：

- 保留已解锁颜色，按世界图 ID 仅恢复相关区域。
- 已解锁 ColorObject 保持激活、恢复基础行为与作者位置；未解锁仍失效。
- BlockAbilityHost 清理临时能力并按当前解锁挂接，不全局停用。
- 清除硬平台、蒸汽、藤蔓、喷流等生成物。
- 清除玩家身上的水体、攀爬、击退和临时速度。
- 清除万能方块转发状态。
- 不清理永久存档数据，除非策划明确要求。

门、按钮、已拾取钥匙和锚点不恢复。解锁保留是本次运行的进度，不代表新增跨运行存档。

## 迁移顺序

1. 盘点 Definitions、Catalog、预制体、场景和编辑器入口引用。
2. 新增 `ColorObject`、房间重置入口和固定颜色激活流程。
3. 将一个具体玩法迁移成单功能组件，与旧图路径对照验收。
4. 迁移万能方块代理和玩家选色入口。
5. 迁移三色固定反应。
6. 删除所有图运行时引用。
7. 删除 Definitions、Catalog、GraphView 和编辑器同步。
8. 更新文档、System Index、API 文档和本 Skill。

## 验收重点

### 颜色和房间

- 开局所有颜色物体白色失效。
- 钥匙只解锁对应颜色组。
- 解锁后物体恢复自身固定形态。
- 离开房间后全部失效并清除效果。
- 重新进入房间不会残留状态。

### 万能方块

- 可以搬运；与能源方块共用 `CarryableFeature`，没有颜色对象所以常态可搬。
- 点击后只把颜色转给关联的 B。
- B 的反应与直接点击 B 相同。
- 万能方块自身不产生玩法效果。

### 固定形态

- 水源永远是水源。
- 水流永远是水流。
- 浮力柱永远是浮力柱。
- 不跨颜色变化。
- 不在同颜色内换形态。

### 直接反应

- 红绿只烧植物。
- 蓝绿只触发水流滋养和藤蔓生长。
- 红蓝只生成约定的平台或蒸汽。
- 非目标颜色不触发。
- 重复接触不堆叠生成物。
- 禁用和房间重置后清理完整。

### 静态检查

- Runtime 和 Editor 编译 0 错误。
- `git diff --check` 通过。
- 项目中不再存在图类型和 GraphView 入口。
- 没有 Missing Script、空 GUID、重复定义。
- 没有高频反射、无意义协程和全场景逐帧扫描。
- 所有可拾取掉落物继承 `DropItemBase`，且没有在子类绕开基类直接销毁、禁用或提前隐藏 Renderer。
- 未导入 DOTween 前没有新增 `DG.Tweening` 引用。

## 最终原则

```text
颜色是解锁分组。
形态在放置时固定。
钥匙只改变失效和生效。
一个功能一个组件。
万能方块自身纯色并向四方向紧邻目标广播颜色。
物体反应直接走类型安全接口。
交互图整套退役。
```
