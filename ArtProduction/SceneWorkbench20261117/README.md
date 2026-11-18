# Scene 工作台美术来源

`ProgressSources.zip` 保存首轮五张原始生成图、提示词和审图意见；`Round2Sources.zip` 保存第二轮五张原始图、提示词、Claude 意见与 manifest。来源使用既有固定 n8n 生图与 Claude Code 审图工作流。请求图像模型为工作流默认值，实际图像供应端模型未独立验证。

选用：moss-terrain-half-safe-r4 的外边/核心、moss-inner-wide-r2 的四个凹角、moss-compact-r1 的紧凑连接片。band-r3 与 inner-corners-r1 为被淘汰候选。概念图仅作风格参考，不是已搭建场景截图。

解压 Round2Sources.zip 后，对 sources 目录运行 `python prepare_atlases.py <sources目录> <输出目录>`，需要 Pillow。处理只包括测量网格裁切、洋红去底、最近邻采样和暗核心对比收敛，不用代码绘制替代生成美术。输出两张 PNG：13×32 宽片与 13×16 紧凑片。32px 宽片在 Unity 中覆盖两格，不改变 16px/格的全局标准。

NativeSources 的两个 `.aseprite` 文件由最终 PNG 经 LibreSprite 1.3 导出，都是单个可编辑图层，不是多层原始绘画文件。宽片与紧凑片往返导出后可见RGBA均一致；归档原图按脚本重建的两张PNG与提交图RGBA一致。

最新完整审图 20261118-093940-626 结论是返工：紧凑条的厚度/颗粒和宽片不同、孔洞过渡仍需修复。082314-421 与 090946-716 输出不完整，不作为通过依据。场景与素材均为进度交付。

PendingCompact保存新紧凑图请求的提示词和安全失败记录：HTTP500、没有输出、未自动重试，供应端是否处理未知。UIReviewR7保存真实工作台截图的完整Claude审阅及输入哈希；结论仅涉及UI布局可继续，不能作为地形美术通过依据。

Round3RejectedSources.zip记录紧凑草边r3/r4：均已成功生成，完整Claude复审均为返工，未替换Unity素材。包含原PNG/提示词/请求来源、裁切候选和采样预检；预检不是Unity截图。
