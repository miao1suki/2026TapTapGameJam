# R1／R2／R3 最终场景接收

2026-10-19 按用户最新授权接收 ILorR1012 的最终已保存场景，不从旧规划重建。

| 关卡 | 作者分支版本 | 权威内容 |
| --- | --- | --- |
| R1 | `codex/ilorr/r1-level` / `ff785ab` | `Assets/Scenes/R1/R1.unity`，包含专属弹性植物镜头及中文 Inspector |
| R2 | `codex/ilorr/r2-level` / `308c65f` | `Assets/Scenes/R2/R2.unity` |
| R3 | `codex/ilorr/r3-level` / `48cb1e4` | `Assets/Scenes/R3/R3.unity`，包含作者最后保存的墙体调整 |

三关均保留作者 Scene 字节、同名 planning 和 meta，未改 Gaming、构建入口或 main 的公共玩法修复。这次接收不表示三关已经拼入 Gaming。

## 重要：JSON 仍不是最终场景的可重建副本

作者交付已明确：R1/R2/R3 含 Inspector 手工位置、非整数尺寸、复制后重复来源 ID、删除记录和规划未同步的参数。最终 Scene 为布局权威；同名 JSON 作为历史编辑快照随附，**不可直接应用／片段导入来重建最终布局**。不要在打开后启用旧规划自动应用。R1/R2/R3 各自文档列有具体差异。

为避免五分钟急件中猜测重标、取整或丢失平台局部锚点，本次没有声称完成 Scene → planning 回采。后续导入别人关卡前，应在隔离副本中逐项回采：唯一稳定 ID、真实 Transform／尺寸、实际存在集合、实例参数、玩家出生、平台附着及机关引用；比对 Scene 不变后再导出协作片段。现有“修复同步”只按标记 Cell 取格，不能视为精确手调回采。

## 本轮验证

- 三个 Scene 序列化 fileID 均唯一；R1 484、R2 366、R3 367 个对象文档。
- 场景外部 GUID 均可在项目或锁定 URP 包中解析；同名 planning 均可解析，双方 meta 已存在。
- 配对提交门禁通过；未执行 Unity 编译、Play、通关或打包，不把作者历史专项当作当前完整验收。
