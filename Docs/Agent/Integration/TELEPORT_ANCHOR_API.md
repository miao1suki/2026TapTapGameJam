# 传送目标方块

## 资源

- 组件：`Assets/_Project/Code/Systems/BlockFeatures/Runtime/Features/TeleportAnchorFeature.cs`
- 默认预制体：`Assets/_Project/Content/ColorBlocks/Prefabs/Teleport_Block.prefab`
- 道具栏目：`传送目标方块`

## 配置

`TeleportAnchorFeature.teleportId` 是任意字符串 ID。启用时注册到全局静态表，禁用或修改 ID 时注销旧项。重复 ID 以后注册者不覆盖先注册者，并在 Console 输出警告。

预制体默认：

- 不参与物理碰撞。
- 不显示 Renderer。
- 组件启用时再次强制关闭自身及子物体 Collider/Renderer。

## API

```csharp
bool moved = TeleportAnchorFeature.TeleportObject(
    objectToTeleport,
    "target_id");
```

成功时：

- 查找已注册的 `target_id`。
- 将对象放到目标方块的位置和旋转。
- 根对象有 `Rigidbody` 时先写刚体位置，并清空非 Kinematic 刚体速度。

可单独查询目标：

```csharp
bool found = TeleportAnchorFeature.TryGetAnchor(
    "target_id",
    out TeleportAnchorFeature anchor);
```

运行时修改 ID 使用 `SetTeleportId(string)`，不要直接改 Inspector 字段。
