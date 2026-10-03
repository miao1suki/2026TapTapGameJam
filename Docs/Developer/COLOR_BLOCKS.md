# 可变色方块：制作与验证

从 Unity 菜单 `Tools/2026TapTap/颜色/颜色工作台` 进入。第一次打开前运行 `Tools/2026TapTap/颜色/初始化或修复颜色资源`；它创建 RGB 目录、白底黑边及彩色材质、颜色方块/钥匙 Prefab、Timeline 演出，并将 Selective HSV Renderer Feature 安装到 PC 与 Mobile Renderer。再次运行不会覆盖已有目录或 Prefab。

## 策划流程

1. 在颜色工作台左侧选红、绿、蓝。其余三个暗色按钮只是预留位，点击才真正新增类型。
2. 场景内直接拖入 `Assets/_Project/Content/ColorBlocks/Prefabs` 下的 `ColorBlock_red/green/blue`；或选择现有带 Renderer 的方块，点击“选中物体 → 可变色方块”。后者保留原方块碰撞体和位置，添加颜色组件并设为初始白底黑边。
3. 拖入相应 `ColorKey_*` Prefab，或在工作台点“在场景创建颜色钥匙”。钥匙 Collider 为 Trigger，PlayerController 碰到后解锁颜色并播放 Timeline。可在 Timeline 编辑器修改镜头片段；需要一个 `CameraControlManager`。
4. 选择颜色后在右侧交互图拖动节点、拉线；按 Delete 删除选中的图元素。图会保存到目录资产，但混色玩法暂不执行。
5. 颜色设置中的“解锁事件 ID”是稳定的脚本事件名。层号必须在 8–31 且不与别的颜色共享；删类型不会自动改写已放置的方块/钥匙。
6. Play Mode 时可展开“运行时预览”，手动解锁选中颜色或反复触发褪色/恢复，不必每次让玩家拾取钥匙。

运行开始时颜色方块使用白色黑边材质且当前类型为空。取得对应钥匙后，颜色属性立即切换，屏幕上的该层从低饱和度恢复到真实材质颜色。表现是屏幕空间效果，独立于状态变化。若开始新游戏，调用 `ColorWorldManager.Instance.ResetProgress()`；跨关卡不应调用。

颜色钥匙也以无色材质出现。钥匙只解锁对应基础色；玩家获得主动调色能力时，再由能力系统调用 `ColorWorldManager.Instance.GrantRecolorAbility()`。交互图目前只是设计数据，不能把节点连线视为已实现的混色行为。

## 美术与性能

默认材质是功能白模，不强迫最终美术贴图。自定义材质可在工作台的“目标材质”选择，但请确认颜色层配置与不透明队列。`SelectiveHsvRendererFeature` 在 RenderGraph 中先按该层与场景深度生成遮罩，再执行 HSV 饱和度 Blit。它不是直接改 `Renderer.material.color`。类型越多，遮罩/Blit 越多；当前只启用 RGB。

透明贴图、美术特效、多个不同色物体共用同层以及任意网格的描边暂未覆盖。交互图目前仅编辑配置，不代表混色逻辑完成。

接入细节见 [COLOR_BLOCKS_API.md](../Agent/Integration/COLOR_BLOCKS_API.md)。
