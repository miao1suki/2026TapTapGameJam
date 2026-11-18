# 苔石水廊展示场景（进行中）

打开 `MossAqueduct.unity`，使用 Scene 内的关卡工作台。Scene、ShowcaseMap、ShowcasePalette、ShowcaseOwnership 及依赖均为可编辑作者资产，不加入正式构建。场景含 366 个碰撞格、6 个美术层、387 个美术锚点及真实双向绑定的门/按钮；没有玩家，不是完整可游玩关卡。

`MossConnections.unity` 是实际 Unity 渲染的连接检验场景，包含实体、单格横竖条、孤岛、孔洞、L/T/十字、阶梯、缺口、斜角、负坐标及跨 x31/32 分块。底部跨块长条的孔洞是有意设置的测试输入。

MossTerrainWide 的 32px 源片覆盖两格，仍为 16px/格；薄条暂用 MossTerrainBands 的 16px 源片。宽轮廓为 Palette 的可选扩展，旧 Palette 保持原路径。四边和孔洞均参与连接，但美术尚未验收：Claude 20261118-093940-626 完整复审要求修复窄条粗细突变与孔洞过渡，不能当作定稿。

原始生成 PNG、提示词、审图意见和来源哈希位于 `ArtProduction/SceneWorkbench20261117`；NativeSources 中的 Aseprite 文件为可编辑单层切片资产。详细用法、验证和待办见 `Docs/Developer/SCENE_AUTHORING_WORKBENCH_20261117.md`。
