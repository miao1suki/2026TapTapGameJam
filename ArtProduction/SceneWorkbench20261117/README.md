# Scene 工作台美术来源

`ProgressSources.zip` 保存首轮五张原始生成图、提示词和审图意见；`Round2Sources.zip` 保存第二轮五张原始图、提示词、Claude 意见与 manifest。来源使用既有固定 n8n 生图与 Claude Code 审图工作流。请求图像模型为工作流默认值，实际图像供应端模型未独立验证。

历史已提交样板使用：moss-terrain-half-safe-r4 的外边/核心、moss-inner-wide-r2 的四个凹角、moss-compact-r1 的紧凑连接片。band-r3 与 inner-corners-r1 为被淘汰候选。概念图仅作风格参考，不是已搭建场景截图。新制作流程改为下述单张连续母图，尚未用候选替换这些历史素材。

解压 Round2Sources.zip 后，对 sources 目录运行 `python prepare_atlases.py <sources目录> <输出目录>`，需要 Pillow。处理只包括测量网格裁切、洋红去底、最近邻采样和暗核心对比收敛，不用代码绘制替代生成美术。输出两张 PNG：13×32 宽片与 13×16 紧凑片。32px 宽片在 Unity 中覆盖两格，不改变 16px/格的全局标准。

NativeSources 的两个 `.aseprite` 文件由最终 PNG 经 LibreSprite 1.3 导出，都是单个可编辑图层，不是多层原始绘画文件。宽片与紧凑片往返导出后可见RGBA均一致；归档原图按脚本重建的两张PNG与提交图RGBA一致。

最新完整审图 20261118-093940-626 结论是返工：紧凑条的厚度/颗粒和宽片不同、孔洞过渡仍需修复。082314-421 与 090946-716 输出不完整，不作为通过依据。场景与素材均为进度交付。

PendingCompact保存新紧凑图请求的提示词和安全失败记录：HTTP500、没有输出、未自动重试，供应端是否处理未知。UIReviewR7保存真实工作台截图的完整Claude审阅及输入哈希；结论仅涉及UI布局可继续，不能作为地形美术通过依据。

Round3RejectedSources.zip记录紧凑草边r3/r4：均已成功生成，完整Claude复审均为返工，未替换Unity素材。包含原PNG/提示词/请求来源、裁切候选和采样预检；预检不是Unity截图。

## 单张连续弯曲母图（当前制作流程）

用户提供的两张水/沙/草图只说明母图形状与连接拓扑，不是风格参考。程序先建立一块连续、错位弯曲且四边开口的结构模板，覆盖四向凸角/凹角/横边/竖边/内部共20类象限。生成一张苔草与深蓝土芯母图后，以同一像素尺度配准、去底、裁切，再组装既有13片合同；不分别生成每种转角，不用代码绘制叶簇。

`Round4WindingMotherSources.zip` 包含三张原始母图、提示词/安全请求来源、结构模板、取样合同、Python/Pillow脚本、四份完整Claude意见、候选与实际Unity GPU图。`Round4Provenance.json` 与包内 `CandidateRuns.json` 记录复现命令、哈希和候选状态。源r1拓扑错误；r2b和r3c因程序平均产生规则绿格/草齿而淘汰，即使边界数值相等也不记视觉通过。

r3b从同一新母图提取，实际Unity展示复审为有条件可继续；孔洞长条的宽/紧凑片过渡需返工。r3d只清除44个洋红污染像素，保留其他取样。候选在独立本地Scene运行，已提交的MossAqueduct/Palette未改变。`extraction.json`写于导入前，其`unityImported:false`是历史状态，实际运行以`CandidateRuns.json`及GPU图为准。

解压后按`CandidateRuns.json`复现。显式命令`SceneWorkbenchWindingLab.CreateFromCommandLine`会在新的`WindingMotherLabR<轮次>`目录生成可编辑Scene/Map/Ownership/Palette/纹理，并在Library导出相机截图；目录已存在则拒绝覆盖。当前仍是候选阶段，未声称最终审图通过、原生多层绘画源或完整可游玩关卡。
