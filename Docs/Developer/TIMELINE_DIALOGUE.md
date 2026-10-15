# Timeline 对话气泡

## 使用方式

1. 在 Timeline 中点击“添加轨道”，选择“对话轨道”。
2. 将角色或挂有角色的物体绑定到轨道。
3. 在轨道上创建片段，把片段拉伸到需要显示气泡的时间段。
4. 在片段 Inspector 中填写“对话内容”，其余参数按演出需要调整。

默认会读取：

- 气泡图：`Assets/_Project/Art/Generated/GameUI/SpeechBubble/speech_bubble.png`
- 气泡材质：`Assets/_Project/Art/Generated/GameUI/SpeechBubble/SpeechBubble.mat`
- 字体：`Assets/Test字体/[可商用]Cubic‑11 俐方体 11 号/Cubic_11 SDF.asset`
- 默认配置：`Assets/_Project/Resources/UI/DialogueBubbleDefaults.asset`

## 播放顺序

片段开始时只显示气泡本身，气泡会按照“起始本地偏移 → 结束本地偏移”缓慢上浮并完成缩放/淡入。

上浮完成后，会再等待“上浮后等待”参数指定的时间，然后才从第一个字符开始逐字显示。逐字显示速度由“每秒字符数”控制。

片段结束前按“淡出时长”淡出，随后运行时气泡自动销毁。

## 可调参数

- 文本：内容、字体、字号、最小字号、自动缩放、行距、对齐、颜色、描边、富文本。
- 打字机：是否逐字显示、上浮后的等待时间、每秒字符数。
- 气泡：Sprite、材质、整体颜色、大小、文字留白、比例、九宫格、排序层、跟随、朝向相机。
- 表演：起止本地偏移、上浮时长与曲线、起止缩放、淡入淡出时长与曲线。

所有玩家可见文字必须继续使用项目统一的 `Cubic_11 SDF` 字体，不要在对话轨道中改回 Unity 默认字体。
