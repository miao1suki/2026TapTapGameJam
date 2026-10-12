# 交互预览链条

`InteractionLinkEffect` 挂在玩家上，独立扫描 `PlayerInteractionSensor.ScanRadius` 内最近的 `IInteractionTarget`。它读取 `PlayerColorWheel.SelectedColorId`，从 `ColorCatalog` 取红、绿、蓝色卡，并使用 `InteractionLink.mat` 在运行时生成一条链。原有玩家、交互和渲染管线脚本不用修改。

- 目标实现 `ILinkEffectPreviewTarget` 时，以它的 `CanLink` 和 `LinkAnchor` 作为预览结果与终点；否则使用已有 `IInteractionTarget.CanInteract` 与 Collider 最近点。
- 可行时链条完整且流动；不可行时断续变暗。链条只做预览，不执行交互。
- 没有选中色时，正式场景应隐藏链条。`Assets/Scenes/Test.unity` 为了快速预览，把“未选色时使用测试色”设为红色；玩家选色后总是优先使用真实选色。
- Test 场景的 `TestPlayer` 旁新增一个红色测试目标。红色链完整，选择绿色或蓝色后链变断续。测试目标仅用于 Test 场景。
- 材质由 Inspector 直接引用；Shader 随材质一起导入，不需要在交互目标或 `PlayerController` 中加载 Shader。

在 Unity 6000.3.12f1 中打开 Test 场景，进入 Play Mode，检查链条是否从玩家连向旁边的测试方块。测试其他颜色时可以在运行时切换玩家选色，或暂时修改链条组件的测试色字段。
