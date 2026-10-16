# 移动端操作与攀爬转角

## 操作

- 左侧：左右移动、跳跃；左侧跳跃按住可在落地后连续起跳。右侧：行走／奔跑切换、单次跳跃、选色。行走／奔跑通过点击切换，不需要持续按住；暂停后保留选择，失焦或切后台清除奔跑输入。
- 空中、攀爬及左跳跃按住时，右侧上方显示临时左右键，与交互和放下按钮错开。游泳/攀爬/绿钩子显示上下方向；空中和落地后0.3秒也显示下键，用于抑制弹性植物弹跳，按住时不会因落地隐藏。
- 附近有可用朝向交互或搬运目标时显示交互；携带时显示左右放下；死亡时显示重生。轮盘入口可打开，但未获得能力时选择对应颜色会提示“此能力未解锁”，不会授予能力。
- 各按钮由对应手指独立持有，手指滑出图形不松开，抬起/暂停/失焦/切后台释放物理按压。按键有14参考单位的初始触摸容差、按下高亮和缩放反馈；方向键图形轻微跟手、松开回正，实际检测区域不移动。界面按键不会占用另一手指点击场景的能力指针。
- 默认按键已向内、向上调整；已有自定义布局保留，想使用新版位置可在触控布局中“恢复默认 → 保存”。移动端隐藏PC的Space/ESC提示及额外暂停按钮，只显示触控操作。
- 开始菜单或暂停菜单的“设置 → 触控布局”：拖动按键，点选后缩小/放大，保存生效；取消不保存，恢复默认也需保存确认。布局按安全区比例和各按键尺寸持久化到本机，不修改场景/预制体。只在移动平台显示此入口。
- 移动端“手柄按键”只修改手柄绑定；未连接时显示现有绑定但不能开始接听。“重置手柄”不清除键鼠绑定；屏幕按键使用触控布局，不把触屏主触摸误设成手柄按键。

## 搬运放下与相机

落点跟随玩家所在格，不吸附地面；格线附近有0.06格的稳定区间，防止走动小抖动造成相邻格反复切换。玩家与目标相邻格轻微重叠时可以放下，动画期间由玩家运动控制器通过约0.16秒的平滑水平速度让位，不瞬移或直接改Transform；距离最多0.65格。贴另一侧墙时按实际碰撞深度判断，不额外要求玩家占格之外的空隙，允许正常一格通道。沿途有真实障碍或空间确实不足时仍拒绝。预览和执行共用完整物体包围盒校验，墙体、其他物体及物体原属房间限制保留。落点或让位路线中途被阻挡时保留搬运，未完成分离前不恢复物体实体碰撞；取消、重生与离区清理分离速度。

2D跟随焦点默认从玩家上方1格改为玩家中心（相机下移1格），Gaming与Test同步；3D焦点不变。已有其他自定义场景的相机覆盖值不强制修改。

## 攀爬

横向绿块伸长部分支持从顶部绕外缘到下方、侧面接顶部/下方。过渡沿真实实体边界移动，碰撞仍由刚体处理，不穿过障碍。绕过边缘后保持附着，反向输入沿另一面继续爬。横爬或转角中按跳跃脱手下落，短暂防止重新抓住同一表面。垂直攀爬原蹬跳不变。

能力选色不再是一次性：成功染色与绿钩子保留当前颜色，下次直接使用；轮盘中央仍可取消。

## 接入

`IMobileGameplayContext` 是输入层的只读情境契约；`PlayerInputDriver` 发布当前玩家，HUD 不反射查玩家、不直接依赖 Assembly-CSharp、不每帧扫描场景。触摸仍通过 `VirtualInputState → GameInput → PlayerInputDriver`。

`UnityInputSource` 的触摸主动作使用 `TouchGameplayPointer`，按接触 ID 区分界面起始触摸与世界触摸；不是 `primaryTouch` 代表所有手指。UI 模块保留独立指针，双跳跃按钮通过动作所有者集合合成。

`TouchPressFeedback`只负责图形反馈，不写玩法输入。`VirtualInputButton.ConfigureToggle`用于跑步开关，`ConfigureRepeatJump`只用于左跳跃；`GameInput.IsTouchJumpRepeating`仅允许玩家输入驱动在落地时补发起跳，不在空中反复触发蹬跳或取消钩子。`IMobileGameplayContext.ShowAirborneDownControl`提供空中及落地短暂窗口，HUD不读取私有字段。

`IPlayerCarrySeparationHost.ResolveCarryOverlap(bounds,item,apply)`是可选的运动控制器接入口：预览必须传false，不得移动玩家；执行传true只请求分离，不同步修改位置。物体等待`IsCarrySeparationPending(item)`结束并通过`IsCarrySeparated(bounds)`，再恢复碰撞；失败、取消和重置调用`CancelCarrySeparation(item)`。唯一刚体写入者仍为PlayerController；其他类型玩家未实现此接口时使用原有阻挡规则。

`MobileControlLayout` 保存版本键 `ShapeOfColor.TouchLayout.v1`，归一化安全区坐标、尺寸80–200参考单位；非法数据回退默认并夹紧边界。设置通过事件打开布局，避免 StartMenu/GameFlow 互相引用。布局预览不派发任何玩法动作。

`HorizontalCubeVine` 只登记真实已解锁/落稳的攀爬表面；`PlayerController.ProtectClimbCorner`、`CancelClimbCorner` 与 `TickClimbCorner` 统一写运动速度。表面禁用、移动/尺寸变化、重置、死亡与控制锁均中断转角。不要由功能脚本直接搬玩家 Transform。

## 验证入口

隔离工程 `Library/FlowRetirementProbe` 可执行 `Project.Player.Editor.ClimbMobileVerification.Run`，覆盖真实绿块转角、脱手、持续选色和布局保存；`MobileCarryRegressionVerification.Run`覆盖落点稳定、玩家安全分离、动画中途阻挡、两阶段能力锁、布局保存与手柄改键。输入编辑测试覆盖三指、双跳跃所有权、奔跑切换、左跳跃连跳标记、跟手图形和触摸容差。安卓包仅 MainMenu/Gaming，原生隐私流程见 `ANDROID_PRIVACY.md`；手感和隐私边界需真机验收。
