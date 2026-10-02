---
name: taptap-project-handoff
description: 接手 2026TapTap 的基础系统和旧项目可复用能力，定位功能、读取持续更新的协作文档并避免恢复被排除的玩法。
---

# 项目接手

工作仓库只允许 `https://github.com/miao1suki/2026TapTapGameJam.git`；本机项目 `D:\.unity\2026TapTap`。
每次先重新读取AGENTS.md和其列出的Docs/Agent文件；使用SYSTEM_INDEX定位源码，使用Integration/FOUNDATION_API.md读取接入契约。
源自旧项目的工具/API标为旧功能，不意味着旧测试场景、资源、玩法或旧git地址可继续使用。
保留启动流程、相机管理/转换、输入/重绑定、基础玩家、成就、Timeline演出战斗、瓦片绘制/烘焙和不规则切片工具。
不包含对话系统（尚未开发）、绳梯平台玩法、投影视差碰撞、旧地图总拼、旧素材和QQ bot源码。
CameraControlManager是相机输出唯一写入者；GameInput是输入入口；流程由GameFlowController管理。
基础PlayerController不提供旧梯子/投影/平台API；不要为满足旧示例悄悄增加这些依赖。
旧模块README仅参考，先确认最新迁移报告和功能文档；不清楚的能力先询问用户。
git操作按taptap-git-worker技能；整合权限仅属于用户指定整合者。
