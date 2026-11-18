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

`Round5MotherExtractionExperiments.zip`归档后续单套16px母图及提取实验、完整Claude审图、独立20象限/47邻接结构检查和实际GPU。源图、提示词、安全请求、脚本及逐文件SHA均可追溯；`Round5Provenance.json`索引状态。所有新候选均退回，r3d继续是未定稿候选，原样板未替换。本次续做复用了已有源图，没有重复付费生成。

固定裁切r3e丢失窄条；r4/r5单套16px草边变薄；r5b/r5c只在隔离实验临时改采样，前者窄条缺失，后者接头仍不合格，已全部撤回。包内实验C#为文本档案，不能当作生产渲染代码安装。r4限厚版历史GPU仅存缩略图，Library原图在工作树归档时移除。候选CLI的`-compactOnly`会清除宽片引用，只在新建候选Palette使用，默认宽片流程保持。

`Round6MixedBoundarySources.zip`只修采样清单中宽/紧凑片真正相接的30对边界（清单共816对），相对r3d改变586像素。校正依然来自生成像素的局部平均/去污染，没有代码补画叶簇。零边界差不代表视觉连续：实际Unity孔洞长条仍有暗槽、周期草齿。Claude完整审图20261118-173952-726保留A/r3d为基线，B/r6有条件继续，C/r5c返工。`Round6Provenance.json`索引完整输入/复现脚本、实际GPU、审图与依赖闭包，逐文件SHA和ZIP CRC已验证。

本轮另外交付可直接编辑的`Assets/_Project/Development/LevelEditor/WindingMotherLabR6`，含`Aqueduct.unity`、`Connections.unity`、`All47.unity`及独立作者资产和生成依赖。它是未定稿候选，不自动替换原样板或r3d。素材与GPU导出哈希核对一致；YAML依赖闭包只允许本候选及其Scene/Map对应的生成目录，未扩大收录其他本地实验。URP默认材质沿用已有Package。源码未变，复用第11轮148项通过结果，本轮没有前台交互或构建验收。

`Round7TextureTransferExperiment.zip`保留同源纹理转移实验及复现脚本。脚本从原生384px母图选择4块8×8全不透明草纹理，经目标紧凑片的土芯掩码替换RGB，共1985像素；保留alpha与宽图。输出经过目标掩码合成，不能称为完整裁片未经改变的直采，也没有程序绘制叶簇或新增付费生成。384px是本地最近邻处理尺度，未独立证明供应端有效像素/导出倍率。

包内包含独立Aqueduct、Connections、All47实际Unity GPU图，以及r3d/r6/r7同坐标接头对比；SourcePatchReview只是软件图集预览。r7减轻水平暗槽，但草齿仍规则、细柱仍有暗缝，未获得完整Claude复审，`artApproved:false`。本次只归档实验，不提交本地WindingMotherLabR7资产或替换原样板。`texture-transfer.json`的`unityImported:false`为导入前历史值，实际状态以Round7Provenance.json与Unity结果为准。ZIP内逐文件SHA及CRC已核验。
