# 本机多 AI 美术工作流

此目录只服务开发期美术制作，不进入 Unity Assets、运行时或发布包。软件和凭据数据库位于 `%LOCALAPPDATA%/XingZhiCai/ArtWorkflow`；仓库没有 API Key。

## 启动

```powershell
./Ops/ArtWorkflow/Install-ArtWorkflow.ps1
./Ops/ArtWorkflow/Start-ArtWorkflow.ps1 -ImportWorkflows
./Ops/ArtWorkflow/Start-ArtWorkflow.ps1
```

打开 `http://localhost:5791/`，首次创建自己的本机管理员账户。不对外开放端口，不配置定时任务。`-ImportWorkflows` 仅用于首次导入；重复导入相同 ID 会覆盖该流程。已有配置请先停止 n8n，再执行 `Start-ArtWorkflow.ps1 -UpdateWorkflows`：先备份本机工作流，再升级审查/原型两条定义，保留节点的凭据引用和位置。发现未知自定义节点停止覆盖，交给人工合并。原始导出保存在本机 `workflow-backups`，不提交 Git。

本轮固定 n8n 2.46.6、Claude Code 2.1.34。npm 未发布 2.1.35。后者由 `Start-ClaudeCode.ps1` 启动，检查固定版本并设置 `DISABLE_AUTOUPDATER=1`，不修改用户全局客户端配置。登录 Claude Code 与配置工作流 API 凭据是两个独立步骤，客户端登录不代表其他 API 已配置。

若 Windows 已启用无认证的本机环回代理，启动器为本次进程继承其 HTTP/HTTPS_PROXY，已有环境变量优先；localhost 始终直连。不修改系统代理、不访问代理密码。浏览器能访问供应商而 n8n 连接超时时，先核对本机代理与进程路由，不能直接认定 Key 错误。

Windows 安装跳过通用第三方生命周期脚本，仅给本流程所需节点装配已校验的官方 SQLite 预编译模块；Git Bash 使用官方 Portable Git，经 7-Zip 解压，免全局安装，不运行下载的自解压安装器。此安装方式验收 HTTP/Form/Code/Merge；Kafka、SSH、额外原生图像处理节点不在验收范围。软件依赖下载与版本清单保存在本机安装目录，不进入项目 Git。

## 一次性配置凭据

在 n8n 本机 Credentials 页面配置常规审查使用的 OpenAI、Google Gemini(PaLM) API 凭据。Image 2 使用独立 OpenAI 类型凭据，名称为 `工作流Image2`，原型的两个请求节点共用它。`Organization ID` 只能填实际组织 ID，没有则留空，不能用作备注。已建的 Anthropic 凭据保留，但流程不使用；Claude 只通过本机客户端。

本次接入用户指定的随想供应商 `https://sui-xiang.com`。n8n 通用 HTTP 节点不会自动采用凭据里的 Base URL/Host，因此两条流程都有显式的 `服务配置` 节点，集中设置 `openaiBaseUrl`、`geminiHost` 和型号。换线路时凭据与此节点一起核对。准备节点规范化 `/v1`、`/v1beta` 后生成实际 URL，不回退官方服务。供应商 Key 不得发到官方端点。OpenAI 凭据的 Base URL 通常以 `/v1` 结尾，Gemini Host 和 Claude Code 则用域名根地址。

凭据加密密钥由启动脚本随机生成，保存为仅当前 Windows 用户可读写的 `n8n-key.local`。备份或迁移本机 n8n 数据时必须同时保护这份密钥，否则旧凭据无法解密。流程关闭执行内容持久保存；仍需避免分享含本次素材和模型回复的执行截图/完整导出。程序只在本机监听。

模型 ID 在 `服务配置` 节点集中设置。常规预设 `gpt-6.1-sol`、`gemini-3.5-flash`，须与本人供应商分组权限相符。账号可用性仍需实际验证；失败不自动换服务商、型号或重试付费请求。升级脚本只读凭据 ID/名称/类型来绑定节点，不查询密钥内容。

## 三个流程

| 流程 | 如何执行 | 产物与边界 |
| --- | --- | --- |
| 离线流程验收 | 打开后手动执行 | 不调用模型、不收费，仅检查 n8n 执行链路；不能当作真实 API 通过 |
| 常规美术审查（Claude 按需） | 编辑器中监听测试表单，填制作说明、PNG/JPEG/WebP 图片、可选动画时序，再提交 | 向所配置供应商发送 OpenAI/Gemini 两次审查；在“下载审查报告”输出 Binary 下载 Markdown。没有 Claude HTTP 节点 |
| Image 2 原型生图 | 监听测试表单，填需求，可选一张参考图，提交 | 无参考图走生成接口，有参考图走编辑接口。固定单张、1024×1024、medium；在“下载原型图”输出 Binary 下载 PNG。不自动写入 Unity |

表单测试需要本机用户主动点击“监听测试事件”。所有流程默认未激活，不发布公开表单。上传图限制 5 MB。请求失败停止，不自动循环付费。生成流程不能保证严格像素、连续动画、可平铺瓦片或物理正确的材质图。

## Claude Code 定点复审

供应商要求只用 Claude Code 客户端，不能从 n8n 请求 Anthropic Messages。固定客户端 2.1.34，不自动更新。运行以下配置脚本，在本机密码框填写该服务商 Key，不从 n8n 解密取出已有 Key：

```powershell
./Ops/ArtWorkflow/Configure-ClaudeCode.ps1
```

供应商教程指定 `ANTHROPIC_BASE_URL=https://sui-xiang.com`、`ANTHROPIC_AUTH_TOKEN`。配置器把令牌用 Windows DPAPI 绑定当前账户加密保存，型号预填供应商示例 `claude-sonnet-5-5`，可按自己的分组修改。未配置时启动器停止，不回退官方 API。密码框保存不发模型请求；配置存在不代表连通性已验收。

常规资产先做本地规则检查和 OpenAI/Gemini 审查。只有重要定稿、结论冲突或关键风格问题才让 Claude 看明确列出的少量问题和一张图/联系表，不遍历项目、不重复其他模型已确认的内容、不做批量初审：

```powershell
# 仅检查输入，不调用模型。
./Ops/ArtWorkflow/Invoke-ClaudeArtReview.ps1 -BriefFile C:/Art/review-questions.md -ImageFile C:/Art/contact-sheet.png
# 确定需要复审后，显式执行一次。
./Ops/ArtWorkflow/Invoke-ClaudeArtReview.ps1 -BriefFile C:/Art/review-questions.md -ImageFile C:/Art/contact-sheet.png -RunReview
```

复审说明最多 10000 字符，图片最多 5 MB；一次客户端进程、1 个 agent turn、输出最多 1600 tokens，关闭工具/MCP/Hook、持久会话、额外思考预算和自动重试。不自动降级或换型号，结果保存在本机 `reviews`。这些限制任务规模，不保证中转商实际账单金额或上游内部路由次数；账单以供应商记录为准。令牌只在本机交给客户端，不输出日志。

## 制作和验收

1. **需求**：资产用途、相机尺度、碰撞轮廓、源像素尺寸、调色板、明暗方向、运动状态、帧数/时长、材质响应和可读性目标。
2. **原型**：Image 2 生成构图、配色、造型或效果关键画面。先少量选方向，再围绕选定参考迭代；不一次生成大量无约束候选。
3. **定稿约束**：将候选缩成轮廓/色块方案，确定源像素网格和调色板。不能仅用最近邻缩小大图就宣称得到合格像素画。
4. **源像素制作**：Aseprite 维护 `.aseprite` 分层、帧、标签、时长、锚点；PSD 用于必要合成处理，不能成为只有合成大图的唯一来源。Unity 仍按项目已确认的 16 个有效像素/格和转换规则适配，禁止只改 PPU 修偏移。
5. **审查**：静态看轮廓、像素簇、色数、接缝；动画看实际时长播放、循环接缝、脚底漂移、轮廓抖动和状态过渡；特效看预兆/主作用/余波、危险范围与碰撞、透明叠加和强弱层次。常规两模型先查，必要时 Claude 定点复审；分歧由负责人决定并记录。
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
- [随想 Claude Code 接入](https://sui-xiang.com/claude-code)、[供应商文本协议](https://sui-xiang.com/docs?cat=api-examples&page=chat)
