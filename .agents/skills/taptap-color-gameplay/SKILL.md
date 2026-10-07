---
name: taptap-color-gameplay
description: 2026TapTap 颜色大洗牌开发规范，覆盖固定颜色物体、钥匙解锁、房间重置、万能方块代理、固定形态、直接组件交互和交互图退役。适用于修改 ColorBlocks、色物体能力、万能方块、颜色反应或交互图迁移时。
---

# 颜色大洗牌开发入口

本 Skill 是颜色系统开发的强制入口。

## 开始前必须读取

每次涉及颜色系统开发前，必须完整读取：

`Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`

该文件保存完整目标规范。本文件只负责让 Codex 在项目中发现并进入该规范，不替代完整内容。

## 适用范围

以下任务必须先应用本 Skill：

- 修改 `Assets/_Project/Code/Systems/ColorBlocks`
- 新增或修改红色、蓝色、绿色场景物体功能
- 修改颜色钥匙、颜色解锁或房间重置
- 新增万能方块、颜色代理或 `IColorApplicationTarget`
- 修改 `ColorBlock`、`ColorObject`、`BlockAbilityHost` 或 `BlockFeature`
- 拆除 `InteractionManager`、GraphView、Definitions 或 Catalog

## 不可违反的结论

- 颜色是钥匙解锁分组，不是物体的动态当前颜色。
- 物体的具体形态在放置时固定，不会跨颜色或同颜色切换形态。
- 一个功能只由一个组件负责。
- 功能组件直接处理触发、状态、表现、生成物和清理。
- 万能方块只是 B 的代点击入口，负责把玩家选中的颜色转交给关联目标。
- 万能方块不是颜色源、不是能力本体、不能搬运。
- 物体之间的固定反应使用类型安全接口，不经过交互图。
- 高频路径禁止反射、字符串方法调用、全场景扫描和无意义协程。
- 新增或修改自制组件时，必须提供中文 UI Toolkit Inspector、Foldout、条件显隐和可调试状态。

## 工作流程

1. 完整读取 `Assets/_Project/Code/Systems/ColorBlocks/SKILL.md`。
2. 按根 `AGENTS.md` 读取项目持续文档。
3. 盘点改动涉及的预制体、场景、颜色资产和现有图引用。
4. 先补直接组件路径，再删除旧图路径。
5. 运行时验证锁色、解锁、房间重置、重复进入和清理。
6. 同步更新功能文档与 `Docs/Agent/ACTIVE_WORK.md`。

## 完成检查

- Runtime 和 Editor 编译 0 错误。
- `git diff --check` 通过。
- 没有新增 `InteractionManager`、`InteractionObjectDefinition`、GraphView 或反射依赖。
- 没有运行时颜色切换或同色形态切换。
- 房间退出后颜色物体、万能方块和生成物全部重置。
