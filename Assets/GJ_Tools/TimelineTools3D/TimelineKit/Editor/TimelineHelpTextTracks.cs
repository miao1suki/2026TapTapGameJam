using System.Collections.Generic;
using UnityEngine.Timeline;

public static class TimelineHelpTextTracks
{
    public static void Fill(Dictionary<System.Type, string[]> map)
    {

        TimelineHelpText.Register(map, typeof(HitBoxTrack), "HitBoxTrack · 打击判定轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上划一段，就代表「这段时间里存在打击判定」。\n" +
            "和传统做法（代码里定时开关碰撞体）相比，判定窗口跟着时间轴走，改起来更直观。\n\n" +
            "【怎么用】\n" +
            "1) Timeline 里右键 → 添加 HitBoxTrack；\n" +
            "2) 把轨道绑定到一个挂了 TimelineActorHost 的物体（界面上的轨道绑定槽）；\n" +
            "3) 在轨道上创建片段，拖动两端决定判定起止时间；\n" +
            "4) 选中片段，在 Inspector 里配形状、偏移、伤害、击退等参数。\n\n" +
            "【绑定类型】\n" +
            "Transform —— 判定位置以这个物体的朝向为基准换算。\n\n" +
            "【注意】\n" +
            "绑定物体上必须有 TimelineActorHost，否则不会生效，控制台会给出警告。\n\n" +
            "");

        TimelineHelpText.Register(map, typeof(HitBoxClip), "HitBoxClip · 打击判定片段",
            "【这是什么】\n" +
            "扣在 HitBoxTrack 上的判定片段。片段本身只描述「判定长什么样」和「哪段时间有效」，" +
            "真正扫描目标的是物体上的 TimelineActorHost。\n\n" +
            "【判定形状 HitBoxShape】\n" +
            "· Sphere 球：用 boxRadius 当半径；\n" +
            "· Box 盒：用 hitBoxSize 当长宽高；\n" +
            "· Sector 扇形：朝角色前方展开，用 sectorAngle 定张角、" +
            "sectorInnerRadius 定内径（>0 就是空心圆弧，贴身处不判定）、sectorHeight 定竖直高度。\n\n" +
            "【位置与朝向】\n" +
            "· boxOffset：判定中心相对角色的本地偏移，随角色位置与朝向换算；\n" +
            "· boxEuler：判定整体相对角色的本地旋转（欧拉角，三轴自由）。扇形中轴沿本地 +Z。\n\n" +
            "【结算参数】\n" +
            "· damage：命中伤害；\n" +
            "· HitForce：命中击退冲量，0 表示不击退。\n\n" +
            "【判定窗口】\n" +
            "· startTime / endTime：相对片段起点的时间窗（秒），只有落在这个区间内才开启判定；\n" +
            "· useRepeatScan：勾上后在窗口内按 scanInterval 反复扫描，" +
            "同一目标只结算一次，只用来补漏新进入范围的目标。\n\n" +
            "");


        TimelineHelpText.Register(map, typeof(TransformTrack), "TransformTrack · 位移轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上编排物体位移。适合做冲刺、击飞、绕圈这类带演出的移动。\n\n" +
            "【怎么用】\n" +
            "1) 添加 TransformTrack；\n" +
            "2) 绑定要移动的物体（Transform）；\n" +
            "3) 创建片段，在 Inspector 里选位移模式并填参数。\n\n" +
            "【绑定类型】\n" +
            "Transform。\n\n" +
            "");

        TimelineHelpText.Register(map, typeof(TransformTimelineClip), "TransformTimelineClip · 位移片段",
            "【这是什么】\n" +
            "扣在 TransformTrack 上的位移片段，支持五种互斥位移模式：\n" +
            "· 瞬移（穿墙）：进入片段瞬间直接传送到目标点，不检测碰撞；\n" +
            "· 匀速直线：按固定速度朝某方向走一段距离；\n" +
            "· 变速直线：可用速度曲线，也可继续使用旧的起止速度线性过渡；\n" +
            "· 跳跃：独立的弧线位移模式，水平使用速度曲线，垂直使用高度曲线；\n" +
            "· 绕圈：绕一个圆心走一段角度，可做顺时针/逆时针、可变速。\n\n" +
            "【通用参数】\n" +
            "· direction：直线移动方向（本地坐标），会自动去掉垂直分量，留空则用角色面朝方向；\n" +
            "· useCollision：撞墙时是否贴墙滑动（勾上会做碰撞扫掠）；\n" +
            "· castRadius：扫掠检测球半径，越大越「胖」，越早碰到墙。\n\n" +
            "【瞬移模式参数】\n" +
            "· endPos：传送到的终点（本地偏移）；\n" +
            "· endEuler：传送完成后锁定的朝向（本地欧拉角）。\n\n" +
            "【匀速直线参数】\n" +
            "· moveSpeed：速度（米/秒）；\n" +
            "· totalDistance：总距离，走满即停；0 表示不限制，走到片段结束为止。\n\n" +
            "【变速直线参数】\n" +
            "· useSpeedCurve / speedCurve：开启后按片段进度直接采样实际速度；\n" +
            "· startSpeed / endSpeed：关闭速度曲线时使用，片段内线性过渡。\n\n" +
            "【跳跃参数】\n" +
            "· jumpStartTime：相对片段起点的起跳时间；\n" +
            "· jumpDuration：跳跃持续时间，超过片段末尾时自动截断；\n" +
            "· jumpHeight：高度曲线的最大高度基准；\n" +
            "· jumpHeightCurve：X=跳跃进度，Y=高度倍率。水平移动仍使用速度曲线。\n\n" +
            "【Scene 起点承接】\n" +
            "编辑器会追溯同一条 TransformTrack 前一个片段的实际结束位置，" +
            "瞬移、直线、跳跃和绕圈预览都从该位置开始；只有前面没有片段时才使用绑定物体的当前场景位置。\n\n" +
            "【绕圈参数】\n" +
            "· circleCenterLocal：圆心（相对进入片段时的位置）；\n" +
            "· circleRadius：半径；\n" +
            "· circleTotalAngle：总角度（360 = 完整一圈）；\n" +
            "· circleClockwise：俯视顺时针；\n" +
            "· circleVariableSpeed 与 circleStartAngSpeed / circleEndAngSpeed：角速度是否变速。\n\n" +
            "");


        TimelineHelpText.Register(map, typeof(CameraTimelineTrack), "CameraTimelineTrack · 相机运镜轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上编排相机运镜和 2D/3D 投影切换。" +
            "整条轨道通过 TimelineCamRig 向 Project CameraControlManager 统一申请 Cutscene 控制权，" +
            "并通过 CameraModeController 申请 2D/3D 模式。\n\n" +
            "【怎么用】\n" +
            "1) 相机上必须有 Project 的 CameraControlManager 和 TimelineCamRig；\n" +
            "2) 添加 CameraTimelineTrack 并绑定这台相机；\n" +
            "3) 创建片段，在 Inspector 里选运镜模式、设置目标与时长。\n\n" +
            "【2D / 3D 模式申请】\n" +
            "片段不再直接覆盖投影，只能向 Project CameraModeController 申请正交 2D 或透视 3D。" +
            "CameraModeController 是唯一模式权威，CameraControlManager 在写入前强制使用权威投影。" +
            "申请 2D 时还可指定绝对 Yaw，表示转向完成后的平面朝向。\n\n" +
            "【2D 正交轴约束】\n" +
            "正交片段可只允许相机沿 X/Y/Z 中指定轴移动，并可限制每个轴的世界坐标范围。" +
            "例如只保留 X，可得到只能左右推进/跟随的横版相机。\n\n" +
            "【2D 平面转向】\n" +
            "正交片段可选片头或片尾围绕目标平面转向，设置角度、持续时间和速度曲线。" +
            "正角度按平面右转，适合四面平台的视角切换。\n\n" +
            "【轨道策略】\n" +
            "· allowManualCamera：整条轨道是否允许玩家手动拖动视角；\n" +
            "· restoreOriginOnEnd：轨道结束后是平滑交还还是立即交还 Project 玩法相机。\n\n" +
            "【控制权与让位】\n" +
            "轨道存在有效片段时 Timeline 申请高优先级控制权；轨道停止、结束或遇到 Project 更高优先级镜头时，" +
            "Timeline 自动让位并恢复到 Project 当前玩法相机。" +
            "轨道必须显式绑定 TimelineCamRig，未绑定时不会自动查找场景中的相机。" +
            "同一时间只应有一条 CameraTimelineTrack 控制同一个 TimelineCamRig，多轨重叠会输出警告。\n\n" +
            "");

        TimelineHelpText.Register(map, typeof(CameraTimelineClip), "CameraTimelineClip · 运镜片段",
            "【这是什么】\n" +
            "扣在 CameraTimelineTrack 上的运镜片段。\n\n" +
            "【运镜模式】\n" +
            "· 平滑运镜：从当前机位平滑过渡到目标机位；\n" +
            "· 瞬移运镜：立刻切到目标机位，常用于硬切镜头；\n" +
            "· 归位：把相机恢复到轨道开始前的稳定机位。\n\n" +
            "【2D / 3D】\n" +
            "申请模式可选择不申请、申请正交 2D 或申请透视 3D。" +
            "片段只提交申请，是否切换、正交尺寸、FOV 和 2D Yaw 最终由 Project CameraModeController 决定。\n\n" +
            "【2D 约束】\n" +
            "正交投影时可启用轴约束：只勾允许移动的 X/Y/Z 轴，并可给每个轴设置范围，" +
            "用于横版左右运动、固定纵深或限制房间边界。\n\n" +
            "【归位子模式】\n" +
            "归位也分「瞬移」和「平滑」两种过渡方式。" +
            "归位模式只应用归位方式、平滑速度、看向角色和模式申请，其他运动与轴约束参数无效。\n\n" +
            "【运动与环绕】\n" +
            "可开启环绕模式，并分别用半径曲线、高度曲线和运动曲线控制环绕半径、圆心高度与角度进度。" +
            "连续运镜可开启“以上一帧位置为起点”，让当前片段从开始瞬间的相机状态继续。\n\n" +
            "【Scene 轨迹编辑】\n" +
            "选中片段后会显示等时间相机路径、路径节点和视锥。" +
            "起点会追溯同一条轨道前一个片段的实际结束状态，只有前面没有片段时才使用当前 Project 相机状态。" +
            "普通运镜拖拽轨迹终点可改目标机位；环绕运镜拖拽终点可同步修改半径、总角度和高度。" +
            "关闭看向角色后，终点还可直接拖拽旋转。\n\n" +
            "【提示】\n" +
            "片段上还有变速、模式申请、2D 平面转向和轴约束相关参数，" +
            "选中片段后 Inspector 里会全部列出，每个字段都有中文 Tooltip 说明。\n\n" +
            "轨道级的 allowManualCamera 与 restoreOriginOnEnd 在 CameraTimelineTrack 上设置。\n\n" +
            "");


        TimelineHelpText.Register(map, typeof(EffectAudioTrack), "EffectAudioTrack · 音效特效轨道",
            "【这是什么】\n" +
            "自定义 Timeline 轨道：在时间轴上触发音效和特效。" +
            "用它可以把「打击音、命中火花」这类表现和时间轴对齐，不用在代码里掐时间。\n\n" +
            "【怎么用】\n" +
            "1) 添加 EffectAudioTrack 并绑定到挂了 TimelineActorHost 的物体；\n" +
            "2) 创建片段，在 Inspector 里指定音效、特效模板等参数。\n\n" +
            "【音效与特效从哪来】\n" +
            "由 TimelineActorHost 负责实际播放与回收：音效走它身上的 AudioSource，" +
            "特效走它实现的特效生成 / 回收接口（可接对象池）。\n\n" +
            "");

        TimelineHelpText.Register(map, typeof(EffectAudioClip), "EffectAudioClip · 音效特效片段",
            "【这是什么】\n" +
            "扣在 EffectAudioTrack 上的片段，描述「在这个时间点播放什么音效 / 特效」。\n\n" +
            "【常用能力】\n" +
            "· 指定要播放的音效（AudioClip）；\n" +
            "· 指定要生成的特效模板；\n" +
            "· 支持在片段持续时间内重复触发（配合触发间隔参数），" +
            "适合做持续喷射、连击火花这类表现；\n" +
            "· 音调可随机微调（在 TimelineActorHost 上配置范围），避免重复音太机械。\n\n" +
            "【提示】\n" +
            "选中片段后 Inspector 会列出全部字段（含中文 Tooltip）；" +
            "需要更细的表现时，优先在这里调，而不是改代码。\n\n" +
            "");
    }
}
