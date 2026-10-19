# 2026-10-19 支线整合

本次由用户当前窗口授权合并与推送；用户明确采用MVMap17地图，保留main修复。其他工作者继续只拉取、制作及推送自己的分支，不自行合并main。

## 整合范围

- `codex/codex/gaming-camera-size-8` / `b2d0e18`：采用最新MVMap17正式Gaming，20房间、1406原物体；补回新出生位置的无碰撞Start，最终1407条。保留main单Gaming菜单、4096字体、贴图、物理、重置、收集/Boss门槛和相机实现。合并参数窗口的Boss复活标记与平台宽度/移动参数，二者均保留。
- `codex/ilorr/r3-level` / `e04e422`：新增独立R3及可选平台附着物，配套Scene/planning/meta/来源齐备；保留main逐实例重力与作者运动学设置。R3不加入正式构建、不自动拼进Gaming。
- `codex/ilorr/r1-level` / `16fb26f`：横向镜像、工具与源文件此前已接入；本次补齐合并祖先关系，保留main规范化签名，不再次镜像或重建R1。
- TA `firefly-field` / `f184939` 连同其祖先 `flow-magma`、`lava-cooling-steam-burn`、`rgb-glitch`：运行时代码与素材此前已选择性接入，并已有世界格/像素密度、HSV、透明交接和美术区域编辑器修复。本次按保留main的冲突策略记录合并关系；补缺失的独立雨、萤火虫预览及岩浆预览参数。不用旧源码覆盖这些后续修复，不改正式构建列表。
- TA `rain-sprite-field` / `cb5831a`：编辑态静止帧与实际Play动画分离已在main实现；保留main确定性布局、网格比例和HSV接入，不恢复旧的任意粒子缩放。
- TA `water-platform-demo` / `84e6194`：Grow序列帧及正式藤蔓表现已接入；只补独立Animation预览。拒绝旧Recovery场景、VSCode本机设置、Packages/TagManager及退役流程的回退。
- `interaction-graph-removal-handover` / `afbc5b7`：早期拆图提案已被现行直接组件实现取代；合并历史按main已完成的退役实现解决，不恢复旧交互图或旧待办计划。

## 使用与限制

所有正式关卡制作者先同步本次main，关闭旧规划窗口后从磁盘重开Gaming，不能保存旧内存地图覆盖MVMap17。MVMap17作者保留的未绑定按钮/机关仍见MVMAP17_ROOM_SPLIT.md，不在整合中猜测接线。Boss独立场景尚未自动拼入Gaming；Start/End仍只同场景传送。

整合在隔离Git工作树完成，用户主目录MRoom2/MRoom3/Pick、Timeline、插件及其他未提交内容不参与提交。本轮不构建；编译与专项验证结果记录在ACTIVE_WORK。合并提交并不表示所有关卡均已人工通关。
