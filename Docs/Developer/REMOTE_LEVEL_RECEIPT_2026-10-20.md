# 2026-10-20 远端关卡接收

## 接收边界

以远端 main `1722c59` 为代码基线，保留其中最新 Boss/开场 Timeline 等功能。策划与美术分支分别有权威内容，不整分支覆盖公共代码。

| 来源 | 本次处理 |
| --- | --- |
| xianyu66web `codex/codex/gaming-camera-size-8` / `5e88e7c` | 21 房间、2604 作者物体的 Gaming Scene 和配套规划；另补 Start，总计 2605 |
| ILorR1012 R1 `ff785ab` | 已在 main，保持不动 |
| ILorR1012 R2 `d3fac6a` | 原样接收最终 Scene，不能从旧 JSON 重建 |
| ILorR1012 R3 `48cb1e4` | 已在 main，保持不动 |
| Irotin-dyx `f7cbb62` | 仅完整树备份 `codex/backup/irotin-gaming-art-20261020`，本次不合新 Gaming/新美术 Map |

策划 `494d10f` 中组件窗口自动保存工具先与地图分开审计，随后以 `5a30690` 接收两文件增量并增加编译/进入Play保护，真实窗口25断言通过；不接其旧Boss出场资产。main现有公共代码、菜单、单场景启动和美术/物理修复不回退。此前main的Gaming_Art根及其119个YAML文档完整保留，源美术Map/生成资源不改；策划分支没有此美术根，直接覆盖整个Scene会意外删掉已接入美术。四个旧的无规划独立Platform根不额外移植到新布局。

## Start 与精度

新版玩家出生在房12 `mvmap17_region_fb7d702fa6`，World `(-72.4614486694336, 39.39635467529297, 0)`。隐形 Start 复用既有正式锚点预制体，Scene 精确跟随该位置，不修改作者玩家、机关或其他物体。规划占局部格 `(15,13)`，Scene 标记格 `(-73,39)`。

现有规划只保存整数格，禁止 Transform 组件覆盖；因此三份规划一致不等于能表达亚格位姿。若旧版本从规划重建，Start 会回到该格中心，X/Y 差约 0.04/0.10 格。最终手调回采系统应支持精确位姿后再消除此限制，不能取整后声称无损。

## R2 仍待精确回采

`d3fac6a` 只提交 Scene 与说明，未提交同步规划：删除入口红块、增加拾取半径为 1，并新增手调 2×1 墙；墙实际位置 `(48,-6.5)` 与旧标记格不同。保留最终 Scene 原始内容，旧 JSON 仍只能作历史参考，不能应用/片段导入重建最终布局。与 R1/R3 同样，必须回采实际实例集合、唯一 ID、精确 Transform 和参数，保持 Scene 不变后才能用于可靠协作导入。

## 验证与交付

- Gaming Scene 内嵌规划、同名 JSON、正式命名 JSON 内容一致，21 房间、2605 条。
- 作者 2604 条物体逐项 JSON 字段相同；原 16457 份 YAML 文档只改变房12 children、内嵌规划文档与 SceneRoots，其余原文不变。
- 既有 GridArt 层级 119 份文档原样保留，无 fileID 冲突。R2 Scene 原文与 `d3fac6a` 相同。
- Scene fileID 唯一；GUID 在项目或锁定 URP 包可解析；新增缺失引用为 0。
- `Ops/LevelMaps/Test-PlanningMapCommit.ps1`通过；后续整合在隔离Unity实际加载Gaming及R1/R2/R3，零缺脚本，Gaming美术根/Map仍存在。未进行Play通关或PC/安卓构建。

`Ops/LevelMaps/Receive-20261020.ps1` 是定向接收脚本：输入工作树须先为作者 `5e88e7c` 的三份文件，默认基线 `1722c59`。它不是自动合并工具，也不会从规划生成 Scene；重复执行会因已存在 Start 而拒绝。后续新交付应先审计再更新脚本，不盲跑在新布局上。
