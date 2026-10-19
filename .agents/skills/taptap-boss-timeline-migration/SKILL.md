---
name: taptap-boss-timeline-migration
description: 2026TapTap Boss 与结局 Timeline 的迁移和演出规范；新增、迁移或修改 Boss 出场、Boss 死亡、处决、真假结局及其 Playable、相机和生成位置时必须使用。
---

# Boss Timeline 迁移

## 适用场景

- Boss 出场、普通死亡、处决死亡、真结局、假结局。
- 上述 Timeline 中的相机、生成、移动目标、钥匙/收集品演出和脚本 Playable。
- 为整张地图、Boss 场景或 Timeline 触发器做平移、旋转或迁移。

## 坐标系硬约束

1. Timeline 中任何空间数据都必须是**表演坐标系局部值**，禁止写世界坐标。
2. 表演坐标系按以下顺序解析：
   - `PlayableDirector` 所在物体上的 `ContactTimelineTrigger` 的 `Transform`。
   - 若脚本驱动的 Timeline 没有 `ContactTimelineTrigger`，使用 `PlayableDirector.transform`。Boss 死亡轨因此以 Boss 自身为基准。
3. 固定点使用 `frame.TransformPoint(localPoint)`。
4. 相对角色的大小、方向或偏移使用 `frame.TransformVector(localOffset)`，再加到角色当前位置。
5. 禁止把 `transform.position`、场景查找结果或 `GameObject.Find...position` 直接写入 `targetOffset`。
6. 找不到 owning `PlayableDirector` 或表演坐标系时，必须输出明确错误并停止该演出，不能静默使用世界原点。
7. 所有时长、速度、距离、偏移、生成数量、缓动和停留时间都应序列化到 Timeline/Playable Inspector；代码只保留 `0`、`1`、归一化夹取和浮点容差等数学常量。

## 架构选择

现有通用 Timeline 轨道能完成时，直接使用现有轨道并填局部坐标。

现有轨道表达不了演出时，按以下顺序处理：

1. 在 Timeline 的 **Playable 轨道**放入 `PlayableAsset`。
2. 用它的 `PlayableBehaviour` 实现场景专属演出。
3. 不在 Boss 场景中为单次演出修改 `Assets/GJ_Tools/TimelineTools3D` 的通用工具脚本。
4. 只有多个场景都需要的通用能力，才允许扩展 Timeline 工具包，并另做回归。

## 实现流程

1. 从 `PlayableGraph.GetResolver()` 或 `owner` 解析 owning `PlayableDirector`。
2. 通过项目统一 helper 解析表演坐标系。
3. 将 Playable Inspector 中的点/偏移解释为局部值。
4. 用 `TransformPoint` 或 `TransformVector` 转成运行时坐标。
5. 播放结束时释放相机、控制权和临时对象。

## Boss 死亡收束

- 自然死亡与处决死亡必须共用同一个死亡 Playable/状态机，不要维护两套字幕、钥匙和结局逻辑。
- 自然死亡从 Boss 故障闪烁节点开始完整流程；处决受击后只保留自己的镜头/右移入口，然后切入同一个故障节点。
- Boss 动画、玩家传送、相机、六钥匙和 Player Thinking 交接都通过公开 API 或 Boss 专用 Playable 完成。
- 删除 Boss 前先启动独立场景级 Player Thinking Director，避免结局 Timeline 随 Boss 删除而中断。

## 迁移验证

- 将 Timeline 触发器与整段舞台一起平移、旋转后，演出相对位置不变。
- 搜索 Timeline 资产和 Playable 源码，确认不存在绝对世界坐标常量。
- 检查所有显式演出数值可在 Inspector 或 Timeline Clip 中编辑。
- 普通死亡与处决死亡仍归还相机和玩家操作权。
- 真/假结局仍使用 `Start`/`End` 锚点 ID，不写地图坐标。
- 不修改通用 Timeline 工具包来绕过 Boss 专属需求。
