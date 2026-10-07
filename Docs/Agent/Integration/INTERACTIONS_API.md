# 颜色物体直接组件 API

交互图相关类型已经删除。不要再引入 `InteractionManager`、`InteractionObjectDefinition`、`InteractionObjectCatalog`、GraphView 或反射方法调用。

## 核心接口

```csharp
IColorObject
    string BaseColorTypeId
    bool IsActive
    Activate()
    Deactivate()

IColorApplicationTarget
    CanApplyColor(colorId, actor)
    ApplyColor(colorId, actor)

IRoomColorResettable
    ResetForRoom()
```

## 运行时组件

- `ColorObject`：新固定颜色物体。
- `BlockAbilityHost`：薄启停宿主，只处理同一物体上已经存在的 `BlockFeature`。
- `BlockRuntime`：单物体内部信号、命令、状态和调试。
- `UniversalColorBlock`：自身显示纯色，并向左右上下四个紧邻目标广播颜色。
- `ColorRuntimeService`：颜色解锁、房间重置和固定颜色物体注册。
- `HSVColorFadeManager`：颜色层渐变。

## 接入新物体

1. 预制体挂 `ColorObject`，设置固定 `baseColorTypeId`。
2. 添加 `BlockRuntime` 和 `BlockAbilityHost`。
3. 添加实现具体能力的 `BlockFeature`，例如水体、攀爬或机关。
4. 需要接收玩家颜色时，实现 `IColorApplicationTarget`。
5. 需要万能方块代理时，在目标紧邻位置放置 `UniversalColorBlock`；它会自动检测四方向紧邻的 `IColorApplicationTarget`。
6. 不创建图定义、Catalog 条目或 GraphView 连线。

## 固定形态

同一个颜色组可以有多个具体组件，但组件不会互相切换：

```text
WaterSourceFeature         永远是水源
DirectionalCurrentFeature  永远是水流
BuoyancyColumnFeature      永远是浮力柱
```

`DefaultColorId` 只表示钥匙解锁组。

## 事件所有权

占用和接触事件由具体功能组件负责，使用 `HashSet` 或状态字段按 Actor 去重。Enter/Exit 必须成对清理；多 Collider 不能导致重复生效。禁止在全场景逐帧扫描、字符串方法调用、反射或每次事件启动无意义协程。

## 迁移完成后

图资产、图编辑器、`InteractionObject`、Definitions 和 Catalog 不再存在。旧玩法组件只有在完成固定形态替换后才可删除或改名。
