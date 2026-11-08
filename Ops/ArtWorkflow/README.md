# 本机多 AI 美术工作流

此目录只服务开发期美术制作，不进入 Unity Assets、运行时或发布包。软件和凭据数据库位于 `%LOCALAPPDATA%/XingZhiCai/ArtWorkflow`；仓库没有 API Key。

## 启动

```powershell
./Ops/ArtWorkflow/Install-ArtWorkflow.ps1
./Ops/ArtWorkflow/Start-ArtWorkflow.ps1 -ImportWorkflows
./Ops/ArtWorkflow/Start-ArtWorkflow.ps1
```

打开 `http://localhost:5791/`，首次创建自己的本机管理员账户。不对外开放端口，不配置定时任务。导入只在首次或明确更新模板时运行；重复导入相同 ID 会覆盖该流程，不会保留 UI 中对流程的修改，请先导出。

本轮固定 n8n 2.46.6、Claude Code 2.1.34。npm 未发布 2.1.35。后者由 `Start-ClaudeCode.ps1` 启动，检查固定版本并设置 `DISABLE_AUTOUPDATER=1`，不修改用户全局客户端配置。登录 Claude Code 与配置工作流 API 凭据是两个独立步骤，客户端登录不代表其他 API 已配置。

Windows 安装跳过通用第三方生命周期脚本，仅给本流程所需节点装配已校验的官方 SQLite 预编译模块；Git Bash 使用官方 Portable Git，经 7-Zip 解压，免全局安装，不运行下载的自解压安装器。此安装方式验收 HTTP/Form/Code/Merge；Kafka、SSH、额外原生图像处理节点不在验收范围。软件依赖下载与版本清单保存在本机安装目录，不进入项目 Git。

## 一次性配置凭据

在 n8n 本机 Credentials 页面创建 OpenAI、Anthropic、Google Gemini(PaLM) API 三种凭据，把密钥填到密码字段；然后给对应 HTTP 节点选择凭据。OpenAI 原型的两个请求节点使用同一份 OpenAI 凭据。保持官方服务地址，不把密钥填进节点参数、JSON、Git、聊天或截图。

凭据加密密钥由启动脚本随机生成，保存为仅当前 Windows 用户可读写的 `n8n-key.local`。备份或迁移本机 n8n 数据时必须同时保护这份密钥，否则旧凭据无法解密。流程关闭执行内容持久保存；仍需避免分享含本次素材和模型回复的执行截图/完整导出。程序只在本机监听。

模型 ID 在“准备审查”节点集中设置。预设为 `gpt-5.4`、`claude-sonnet-5-5`、`gemini-3.5-flash`，账号可用性需要本人凭据验证；若无权限，换成该账号可访问且支持图片输入的型号，不自动换服务商或自动重试付费请求。

## 三个流程

| 流程 | 如何执行 | 产物与边界 |
| --- | --- | --- |
| 离线流程验收 | 打开后手动执行 | 不调用模型、不收费，仅检查 n8n 执行链路；不能当作真实 API 通过 |
| 三模型美术审查 | 编辑器中监听测试表单，填制作说明、PNG/JPEG/WebP 图片、可选动画时序，再提交 | 图片和说明分别发送三家 API，每家一次；在“下载审查报告”输出 Binary 下载 Markdown。模型输出是待人工复核的意见 |
| Image 2 原型生图 | 监听测试表单，填需求，可选一张参考图，提交 | 无参考图走生成接口，有参考图走编辑接口。固定单张、1024×1024、medium；在“下载原型图”输出 Binary 下载 PNG。不自动写入 Unity |

表单测试需要本机用户主动点击“监听测试事件”。所有流程默认未激活，不发布公开表单。上传图限制 5 MB。请求失败停止，不自动循环付费。生成流程不能保证严格像素、连续动画、可平铺瓦片或物理正确的材质图。

## 制作和验收

1. **需求**：资产用途、相机尺度、碰撞轮廓、源像素尺寸、调色板、明暗方向、运动状态、帧数/时长、材质响应和可读性目标。
2. **原型**：Image 2 生成构图、配色、造型或效果关键画面。先少量选方向，再围绕选定参考迭代；不一次生成大量无约束候选。
3. **定稿约束**：将候选缩成轮廓/色块方案，确定源像素网格和调色板。不能仅用最近邻缩小大图就宣称得到合格像素画。
4. **源像素制作**：Aseprite 维护 `.aseprite` 分层、帧、标签、时长、锚点；PSD 用于必要合成处理，不能成为只有合成大图的唯一来源。Unity 仍按项目已确认的 16 个有效像素/格和转换规则适配，禁止只改 PPU 修偏移。
5. **审查**：静态看轮廓、像素簇、色数、接缝；动画看实际时长播放、循环接缝、脚底漂移、轮廓抖动和状态过渡；特效看预兆/主作用/余波、危险范围与碰撞、透明叠加和强弱层次。三模型意见可分歧，最终由负责人决定并记录。
6. **材质图**：按资产实际需求制作法线、粗糙度/高光和 AO，逐帧对齐同一图集布局。分别检查色彩空间、法线方向、翻转、透明边缘、拼接和光源移动。Renderer2D 材质未接对应通道前，生成一张灰度图不等于效果已接入；当前项目自定义材质对 AO/高光的支持需另做适配。
7. **Unity**：导入到独立测试 Scene，检验真实镜头尺度、排序、脚底/锚点、Tilemap 规则、碰撞、灯光与低端设备开销。原型和审查文件不能自动覆盖正式 Gaming、Palette 或发布材质。

动画至少提供带帧编号的联系表、逐帧毫秒数、标签和真实播放预览。仅审查联系表不能判断播放节奏；法线动画也要随相同帧和标签播放。特效还须保留事件时间点、发射参数、混合方式和可复现的测试 Scene。

每项交付保留来源、许可、人工修改说明、源文件、导出配置、文件哈希、审查意见与 Unity 实际截图。参考其他游戏只分析视觉规律和制作方法，不提取对方资产作为项目源图。

## 离线检查

```powershell
node ./Ops/ArtWorkflow/test-workflows.mjs
```

检查流程图、准备节点、图片限额、参考图分流和代码语法，不发网络请求。真实付费 API 及原型图质量必须在配置凭据后专项验证。

## 官方依据

- [OpenAI Image 2](https://developers.openai.com/api/docs/models/gpt-image-2)、[生成](https://developers.openai.com/api/reference/resources/images/methods/generate)、[编辑](https://developers.openai.com/api/reference/resources/images/methods/edit)
- [Claude Code 安装及固定版本](https://code.claude.com/docs/en/setup)、[Claude 型号](https://platform.claude.com/docs/en/models/overview)
- [Gemini 型号](https://ai.google.dev/gemini-api/docs/models/gemini-3.5-flash)
- [n8n npm](https://docs.n8n.io/hosting/installation/npm/)、[凭据](https://docs.n8n.io/credentials/)、[部署环境变量](https://docs.n8n.io/hosting/configuration/environment-variables/deployment/)
