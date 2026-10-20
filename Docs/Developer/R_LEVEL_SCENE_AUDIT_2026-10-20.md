# R1／R2／R3 最终 Scene 只读审计

## 范围与结论

Unity 6000.3.12f1 在独立 `NeutralMechanismProbe` 项目直接加载作者最终 Scene，读取实际对象而非从旧 JSON 生成。R1 为 `ff785ab`，R2 为最新 `d3fac6a`，R3 为 `48cb1e4`。三关 Scene 文件读取前后 SHA256 完全一致，没有保存、取整或修改布局。

**本报告不是精确回采完成声明。旧 planning 仍不能重建或作为最终关卡片段导入。** 当前 `PlanningBox` 只保存整数格尺寸和位置；实际 Scene 存在分数位姿、复制来源 ID 和额外组件／引用。要实现无损重建，还需要扩展持久化、增量同步、片段平移和引用映射，并证明往返不变；本轮没有冒险迁移这些基础格式。

| Scene | planning记录 | 实际标记实例 | 规划ID无实例 | 重复来源ID＋局部格组数 | 实际玩家位置 |
| --- | ---: | ---: | ---: | ---: | --- |
| R1 | 77 | 75 | 4 | 2 | 52.5，-34.5，0 |
| R2 | 55 | 55 | 2 | 2 | 57.5，-19.5，0 |
| R3 | 59 | 52 | 9 | 2 | 59.5，-14.5，0 |

“规划 ID 无实例”是集合比较，并不全部等于误删；它可能是作者删除或未生成内容，不能自动补回。重复来源通常是作者手工复制的合法新物体，不能自动去重删除。

## 待回采证据

- R1：旧规划中的 `box_4e3f389c`、`r1_cube_blue_upper`、`r1_cube_green_upper`、`r1_cube_red` 没有对应实例。重复来源为 `r1_wall_10 @ (12,10)` 和 `r1_spring_upper @ (21,8)`，各有两个对象。保留例如地形实际 Y=-26.989…、曲柄实际 Y=-25.9… 等分数坐标，不可四舍五入到整格。
- R2：旧规划中的 `r2_cube_green_upper`、`r2_cube_red_entrance` 没有对应实例。重复来源为 `r2_wall_5 @ (55,8)` 和 `r2_wall_10 @ (34,20)`，各有两个对象。最新复制墙实际中心 `(48,-6.5,0)`，实际缩放 `(2,1,1)`，与旧来源格不一致；另一段墙实际X缩放约7.000463，不能强改成7。入口红块删除保留作者结果。
- R3：旧规划中的 `r3_collectible_CL84`、`r3_cube_red`、`r3_lava_3`、`r3_water_3`、`r3_vine_BL89`～`r3_vine_BL93` 没有对应实例。重复来源为 `r3_wall_13 @ (65,9)` 和 `r3_collectible_AF84 @ (8,9)`。手调墙实际位置约 `(72.330734,-4.67953,0)`、曲柄约 `(61.770416,-21.8799,0)` 必须保留。

审计还列出实际中心与标记格推算中心不同的18／6／11条记录。此数值仅为检查候选，不等于全部为Bug：门的固定足印、附着平台以及作者有意偏移也会产生差异，不能统一吸附。

## 可复现入口与产物

将对应分支源码和最终 Scene 复制到独立测试项目 `D:/.unity/2026TapTap/Library/NeutralMechanismProbe`，以 Unity `-batchmode -nographics -executeMethod FinalScenePlanningAudit.Run` 运行。

输出为 `Library/FinalScenePlanningAudit.txt`、`Library/R1.final-scene-audit.json`、R2/R3同名文件。逐项包含全精度局部／世界Transform、标记格、唯一GlobalObjectId、层级路径、根组件序列化诊断和实际玩家位置。组件诊断JSON含当前会话引用，只用于差异排查，**不是可移植或可导入的备份格式**。完整可恢复权威仍是 Git 中的作者 Scene 和依赖。

Scene SHA256：

- R1：`5BE80A6A812F90F6E2DD7C95B4BFC6F53B7FB6D04C3860A93A2C496E89B03A9E`
- R2：`94D328BD9B9601A0BA02C605041395A739D69389D409F9D27FF4E5F72B6CB411`
- R3：`20A450A581DE99D3083C21096A1317F8F0BFF8C1089DFCDBD5B50027B9B9B8B1`

## 单向平台回归

此前 `eab17b5` 的旧中性单向平台新版美术补接经实际测试：ObjectArtworkTests 32/32通过；中性／红／绿／蓝每种1格与3格，实际Prefab编辑态98项、实际Play98项通过。验证完整16×16帧、PPU16、横向平铺而非拉伸、世界像素方正、原占格及贴图支撑边等于Collider顶边。

Prefab父节点Y缩放0.2，视觉子节点Y缩放5抵消压缩；底枢轴局部Y=-4.5对应世界Y=-0.9，完整1格帧的顶边恰好到Collider的0.1世界单位顶边。这组值正确，不应为了数值好看改成0。测试执行入口为 `OneWayDeliveryAudit.Run`，输出Library下Edit／Play结果；未将无图形模式的Transform验证表述为GPU截图、全关通关或真机验收。
