# 接触触发 Timeline API

## 源码

`Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/ContactTimelineTrigger.cs`

默认道具预制体：

`Assets/_Project/Content/ColorBlocks/Prefabs/ContactTimeline_Block.prefab`

## 契约

组件直接继承 `MonoBehaviour`，不是 `BlockFeature`，不要求 `ColorObject`、`BlockRuntime` 或 `BlockAbilityHost`，可挂到任意道具或场景物件。

## 运行规则

- `OnAttach` 缓存 Renderer 状态、解析 `PlayableDirector` 并隐藏 Renderer。
- `ContactTimelineTrigger` 的 Inspector 直接绑定所挂物体的 Collider SerializedObject，可直接编辑启用状态、Trigger、BoxCollider 尺寸和中心。
- `OnTriggerEnter` / `OnTriggerExit` 和 `OnCollisionEnter` / `OnCollisionExit` 都参与接触记录。
- 支持 `AnyCollider`、`Player`、`Tag` 三种筛选。
- `triggerMode` 支持只触发一次或可重复触发；可重复模式使用 `repeatCooldownSeconds` 控制最短间隔。
- `requestPlayerControlLockDuringTimeline` 为真时，播放前通过触发对象或场景中的 `IPlayerControlLockTarget` 申请控制锁；播放到总时长末尾、Director 停止、重置或禁用时释放，暂停阶段保留锁。组件不保存 `PlayerController` 引用。
- 接触使用根对象实例 ID 去重，离开后再次进入才可再次触发。
- Inspector 将触发对象、碰撞体、触发规则和表现设置分开显示，并显示当前模式与剩余冷却。
- `OnResetForRoom` 清除触发记录、停止 Timeline 并重新隐藏。
- `OnDetach` 停止 Timeline 并恢复原 Renderer 状态。

## 验证

- Runtime 与 Editor 程序集静态编译通过。
- `git diff --check` 通过。
