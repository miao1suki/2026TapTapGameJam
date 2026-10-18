# 颜色钥匙悬浮与接触水柱

## 钥匙外观

三色钥匙沿世界 Y 轴上下悬浮。默认振幅 2 个有效美术像素、周期 2 秒、启停缓动 0.2 秒。`GridArtworkMetrics` 按 **16 有效像素 / 格** 换算；例如格尺寸 1 时振幅为 0.125 世界单位，格尺寸 2 时为 0.25。只平移正式 Sprite 子层，不缩放、不移动钥匙根或触发体，也不改变第一把解锁场景、第二把解锁能力的规则。

待机由 `DropItemBase` 持有，其他收集品默认不启用。颜色钥匙在正式外观排版后添加悬浮；吸附开始后将悬浮权重缓慢降至 0，进入收集时立即交还给 `IDropItemCollectionAnimation` / `PickupFlightAnimator`。禁用、区域取消吸附及对象复用都会清理残留位移。不应另加独立的钥匙 Tween 或直接移动根节点。

可在颜色钥匙 Inspector 的“待机悬浮（仅外观）”调整振幅、周期与缓动。

## 红蓝接触水柱

接触仍由 `ContactCube` 的真实接触壳判定决定。蓝块接触红块后，沿红块指向蓝块方向喷射；分开、搬运、锁色或区域复原时立即释放水速度来源。红块被绿钩拉取时依旧可以供能。未增加额外控制锁、刚体约束或碰撞体。

- 首次接触当步就发布水速度请求，不等待新水柱对象的下一次 FixedUpdate；后续每步仍检查真实接触。
- `jetSpeed` 统一为格 / 秒，按真实格尺寸转换。原值 5 保持不变；玩家单独默认乘 1.6，即 8 格 / 秒，其他方块 / `ICubeJetPushReceiver` 仍为 5 格 / 秒。
- 玩家速度仍通过 `EnterWater` / `SetWaterVelocity` 交由现有玩家控制器消费，不直接移动或冻结玩家。
- 默认气泡速度 6 格 / 秒、密度 2.5 排 / 格。全长即时预填充，不等待粒子从源头爬满。

正式外观由 `CubeWaterJetArtwork` 读取现有 `WaterPresentationSettings`：TA 水中面片及 6 张气泡原图。水图按 16×16 有效内容裁去 padding；气泡保持原始有效像素大小、像素边缘对齐，不随意缩放图案。水在玩家后、气泡在水前，沿四向流动，气泡自身保持正立。源材质统一从 `ColorAppearanceManager` 获取 blue 共享材质，同时保留 HSV 与透明遮罩契约。

每条水柱固定最多 7 个 MeshRenderer（水 + 6 个气泡帧批次），最多 96 个气泡；更新仅复用网格和列表，不为每粒气泡创建 GameObject、材质或碰撞体。旧 `jetMaterial` 字段仅保留序列化兼容，不再创建旧 3D 占位水柱。

蓝色接触方块 Inspector 的“水柱”分组可调整玩家倍率、气泡密度、气泡速度。修改外观不要同时改动 `ContactCubeMotion` 或玩家控制所有权。

## 验证

仅在静音隔离 `Library/FlowRetirementProbe` 工程、有真实 GPU 的 Unity 中运行：

`Project.ContactCubes.Editor.KeyHoverAndJetArtworkAudit.Run`

覆盖三色钥匙 / 1 与 2 世界单位格尺寸、根与触发体不漂移、吸附和收集交接、禁用复用、接触首步推力、玩家与非玩家倍率区别、气泡预填充 / 上限 / 共享批次、四向 / 真格尺寸、像素边界、分离与区域重置释放，以及真实 Camera 截图。输出 `Library/KeyHoverAndJetArtworkAudit.txt` 与 `.png`。接触壳物理另运行现有 `Project.ContactCubes.Editor.ContactCubeJetContactAudit.Run`，不以美术测试替代真实落地接触回归。
