# 最近 Git 整合路线（2026-10-20）

```text
main 9725435（R1/R2/R3最终Scene＋单向平台）
  └─1722c59（远端main／boss_last最新Boss与开场工作）
      ├─ebf3693 光照＋喷泉 ────────────────┐
      ├─73044f4 浅草54项 ─────────────────┤
      ├─381b4ed 最新Gaming＋R2定向接收 ───┼─ 整合main 49d7047
      └─3835ca6 单向平台回归／Scene审计 ───┘

TA f7cbb62 ── 单独完整备份，未合其新版Gaming
```

| 来源 | 本轮采用 | 说明 |
| --- | --- | --- |
| `origin/main`、`boss_last` | `1722c59` | 拉取已有Boss/开场工作，未宣称Boss演出已通关 |
| xianyu66web `codex/codex/gaming-camera-size-8` | `5e88e7c` | 21房2604原物体，追加Start共2605；main已有美术根保留 |
| ILorR1012 `codex/ilorr/r1-level` | `ff785ab` | 最终Scene保留，规划旧记录不能重建最终布局 |
| ILorR1012 `codex/ilorr/r2-level` | `d3fac6a` | 新最终Scene保留，手调与删除不由旧JSON覆盖 |
| ILorR1012 `codex/ilorr/r3-level` | `48cb1e4` | 最终墙调整保留，同样需要精确回采 |
| Irotin-dyx `codex/codex/gaming-art-layer-20261019-main-sync` | `f7cbb62` | `codex/backup/irotin-gaming-art-20261020`完整备份；新版Gaming暂不合 |

“定向接收”只接明确的场景/存档/依赖，不等于把来源分支所有旧工具代码合并。后续美术替换和地图增量仍要保持已处理区域；本轮没有实现无损分数位姿规划格式迁移。

补充接收 `5a30690`：从策划494d10f移植参数窗口关闭保存/右键编辑，增加编译/进Play不自动回写保护；实际窗口25断言通过，未覆盖main平台挂载或新版Boss演出。

本轮验证：整合代码Unity编译及GPU光照65断言通过；Pick实际Play7断言通过；4个最终Scene实际加载共6项检查，零缺脚本且Gaming保留美术Map。浅草13938断言两次通过；单向平台32项单测及编辑态/Play各98项通过。未新打包、未验收安卓真机性能与全图通关。

本地修改备份：`Library/LocalBeforeRemote-20261020`；原喷泉/浅草导入器备份：`Library/OriginalArtBeforeMerge-20261020`。这些仅本机恢复用，不提交Git。
