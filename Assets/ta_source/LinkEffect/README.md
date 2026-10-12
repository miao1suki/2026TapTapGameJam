# 交互像素连线预览

`InteractionLinkEffect` 已挂在 `Assets/Scenes/Test.unity` 的 `TestPlayer` 上。它只读取现有组件的状态，不调用 `TryInteract`、`ApplyColor` 或搬运命令，也不修改玩家、方块或渲染管线脚本。上次新增的红色测试方块已移除。

## 识别范围

- F 交互：读取 `IInteractionTarget.CanInteract`，覆盖按钮锁和曲柄平台；如目标提供 `ILinkEffectPreviewTarget`，可额外指定预览状态和连接锚点。
- 左键颜色：读取 `PlayerColorWheel.SelectedColorId`、`IColorApplicationTarget.CanApplyColor` 和具体功能类型。`BlockFeature` 基类虽然都实现颜色接口，但默认 `OnColorApplied` 返回失败，因此只把有实际颜色处理的弹性植物、植物障碍、岩浆、藤蔓、水体和万能方块列为候选。红/蓝反应及藤蔓剩余生长量也参与可行性预览。
- F 搬运：读取 `PlayerCarryTargetRegistry` 和 `CanCarry`，以玩家默认的 1×1 网格占位检查上下左右相邻格。
- 多 Collider 按所属颜色物体或组件 GameObject 去重，在检测圈内按可行性和距离排序，最多绘制 Inspector 指定数量的连线。无选色时只预览 F 交互与搬运；Test 场景不再使用临时测试色。

## 表现与使用

`InteractionLink.shader` 在透明线条上按屏幕像素格采样噪波，形成颗粒、断边和低帧率闪烁；它不绘制链节。可行目标较亮，不可行目标更暗且有噪波缺口。`InteractionLink.mat` 可调像素大小、噪波强度和闪烁速度；玩家组件可调检测间隔、最多线数、宽度、弧度和透明度。

在 Unity 6000.3.12f1 中打开 Test 场景并进入 Play Mode。选择红、绿或蓝色后，靠近已有颜色玩法物体可查看对应连线；按钮、曲柄和可搬运物可在未选色时查看淡白色连线。目标的实际点击仍由原有玩家脚本决定，连线仅提示候选与当前可行性。
