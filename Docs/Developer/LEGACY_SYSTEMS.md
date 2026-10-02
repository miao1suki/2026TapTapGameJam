# 保留的旧系统

## 成就

另一位程序实际完成的是成就系统，并未实现对话。
成就编辑器创建定义和条件，Graph编辑器编辑逻辑组合；保存后更新catalog。
新项目catalog为空，不带旧测试成就。基础玩家、关卡终点、UI及Timeline可提供标准信号。
持久化存档位于运行环境persistentDataPath，不提交个人测试存档。
详细旧说明在 `Assets/_Project/Code/Systems/Achievements/README.md`；梯子/平台信号只保留名字，不提供相关玩法。

## Timeline演出与战斗

保留Timeline轨道、ActSO动作定义、TimelineActorHost、命中框、效果音频、伤害/闪白及相机机位。
代码位于 `Assets/GJ_Tools/TimelineTools3D`，这是兼容旧功能路径，不含旧demo关卡。
制作TimelineAsset并配置ActSO，在Player动作绑定中接入，或者使用PlayableDirector播放独立演出。
相机演出通过TimelineCamRig连接CameraControlManager，不直接修改Camera与模式组件。
美术、动画和音频需要后续自行导入；框架存在不代表已经配置好新的游戏演出。

## 输入与基础音频

重绑定编辑工具保留，运行时通过InputBindingService接入。PC与手机布局独立保存，手柄两端共用。
基础音频服务常驻；Music/SoundEffects音源分开，新项目不带旧音效素材。
