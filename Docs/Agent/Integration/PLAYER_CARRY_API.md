# 玩家搬运与提示

## 物理所有权

`IPlayerCarryTarget` 保持原有拾取、头顶跟随、放下与复位契约。`CarryableFeature` 额外实现可选接口：

```csharp
Vector3 IPlayerCarryCollisionTarget.ConstrainCarryDisplacement(
    Vector3 anchor, Vector3 displacement, float skin);
```

- `anchor` 是本物理步的头顶根位置；`displacement` 是候选世界位移，返回不穿透的位移。
- 由 `PlayerController` 在写 Rigidbody 速度及执行平台位移前调用。普通、冲刺、游泳、攀爬均经同一约束，不增加第二个刚体写入者。
- `CarryTargetMotion` 缓存拿起前启用的实体 Collider。Box 使用实际中心、尺寸、旋转及缩放做无分配 OBB 扫掠，支持合并块及伸长绿块；Sphere/Capsule/Mesh 使用保守包围盒。
- 拾取动画期间按目标头顶位置查询，不依赖 Tween 当前帧或渲染插值位置。沿各轴裁剪，保留沿障碍平行和反向离开的运动；Trigger、不碰撞的 Layer、搬运物自身及当前携带者排除。稠密查询溢出保守阻挡。
- 不启用搬运物 Collider，不给玩家附加临时刚体或父子关系；原 `ICarryMotionParticipant` 挂起/恢复、放下检测、R/死亡/离区逻辑不变。
- 每次拿起重新缓存碰撞体及原始启用状态，避免生长/重置后沿用旧缓存。
- 这是移动扫掠，不会自动把一开始已嵌入墙体的作者物体推出墙；放置时仍须保持空间合法。

## 世界提示

三色 ContactCube、旧能源兼容预制体和万能方块统一绑定 `Cubic_11 SDF`。新挂载组件默认引用同字体；未配置时优先使用场景 `WordNoteUI.DefaultFont`。

队列首项提示使用实体包围中心，独立世界字号，不继承合并物体的拉伸。按现有规范显示黑字白细描边、2.7 字号、最高排序/Always 深度。可见期间刷新设备和重绑定文案，只有首项显示。

屏幕提示仍全部经 `PlayerInputDriver` → `WordNoteUI`，不新增独立业务 Canvas。正式 Gaming 使用 SharedGameUI 中的组件，Test 沿用已有组件，Pick 新增独立的同型 WordNote_UI；每个场景恰好一份，不添加 EventSystem。提示服务晚于玩家初始化时，驻留搬运/复活消息允许下一帧重试。

## 回归

`Project.ContactCubes.Editor.CarryCollisionVerification.Run` 仅允许在 `Library/FlowRetirementProbe` 隔离项目运行。验证三场景接线、长块字体/尺寸/中心、触发器、墙/顶面、反向离开以及真实 PlayerController 冲刺撞头、复位恢复。不可用该命令覆盖作者场景。
