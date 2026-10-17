# 接触触发 Timeline

## 用途

`ContactTimelineTrigger` 是道具栏可挂的独立触发组件。它不依赖 `ColorObject`、`BlockRuntime`、`BlockAbilityHost`、钥匙解锁或颜色能力门控，Boss、普通关卡和测试场景都可以使用。

项目内提供同规格 1×1 预制体：

`Assets/_Project/Content/ColorBlocks/Prefabs/ContactTimeline_Block.prefab`

## 挂载要求

物体需要：

- 一个 `Collider`
- 一个 `PlayableDirector`
- `ContactTimelineTrigger`

组件激活时会关闭自身和子物体的 Renderer，但保留 Collider，让物体作为隐形触发区域使用。

## 编辑器如何查看

道具栏、关卡画布及非运行状态的 SceneView 使用 `UI/棍母.png` 作等比缩放标记。关卡画布中的已放置标记和半透明放置预览在顶层，不会挡住鼠标选择；照片只用于辨认触发器，不属于正式关卡美术。默认模型不渲染，游戏中既没有照片，也没有占位方块和阴影；触发范围仍以Collider真实大小为准。图片与 `.meta` 已作为资源一起交付。

## 参数

- Timeline 资源：留空时使用 `PlayableDirector` 已配置的资源。
- 管理的碰撞体：选择后，Inspector 直接编辑该 Collider 的真实数据。
- 直接碰撞体字段：启用状态、Trigger 开关、BoxCollider 尺寸和中心。
- 触发对象：接触对象筛选、指定 Unity Tag。
- 触发规则：只触发一次 / 可重复触发、重复冷却、重复接触时重新开始。
- 播放期间申请玩家控制锁：通过 `IPlayerControlLockTarget` 接口申请；不持有具体 `PlayerController` 引用。Timeline 播放到总时长末尾、进入停止状态、重置或禁用时自动释放。
- 表现设置：自动隐藏物体、触发后销毁。

Inspector 顶部会直接显示当前触发模式，可重复模式会额外显示冷却秒数和实时剩余冷却。

Trigger 和 Collision 两种 Collider 接触方式都支持。
