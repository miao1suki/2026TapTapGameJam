# 移动端操作与攀爬转角

## 操作

- 左侧：左右移动、跳跃。右侧：跑、跳跃、选色。两侧跳跃映射同一个动作，可同时按住；轮盘按钮目前默认开放用于测试，颜色能力仍须钥匙解锁。
- 游泳/攀爬/绿钩子出现上下方向；附近有可用朝向交互或搬运目标时显示交互；携带时显示左右放下；死亡时显示重生。
- 各按钮由对应手指独立持有，手指滑出图形不松开，抬起/暂停/失焦/切后台释放。界面按键不会占用另一手指点击场景的能力指针。
- 开始菜单或暂停菜单的“设置 → 触控布局”：拖动按键，点选后缩小/放大，保存生效；取消不保存，恢复默认也需保存确认。布局按安全区比例和各按键尺寸持久化到本机，不修改场景/预制体。只在移动平台显示此入口。

## 攀爬

横向绿块伸长部分支持从顶部绕外缘到下方、侧面接顶部/下方。过渡沿真实实体边界移动，碰撞仍由刚体处理，不穿过障碍。绕过边缘后保持附着，反向输入沿另一面继续爬。横爬或转角中按跳跃脱手下落，短暂防止重新抓住同一表面。垂直攀爬原蹬跳不变。

能力选色不再是一次性：成功染色与绿钩子保留当前颜色，下次直接使用；轮盘中央仍可取消。

## 接入

`IMobileGameplayContext` 是输入层的只读情境契约；`PlayerInputDriver` 发布当前玩家，HUD 不反射查玩家、不直接依赖 Assembly-CSharp、不每帧扫描场景。触摸仍通过 `VirtualInputState → GameInput → PlayerInputDriver`。

`UnityInputSource` 的触摸主动作使用 `TouchGameplayPointer`，按接触 ID 区分界面起始触摸与世界触摸；不是 `primaryTouch` 代表所有手指。UI 模块保留独立指针，双跳跃按钮通过动作所有者集合合成。

`MobileControlLayout` 保存版本键 `ShapeOfColor.TouchLayout.v1`，归一化安全区坐标、尺寸80–200参考单位；非法数据回退默认并夹紧边界。设置通过事件打开布局，避免 StartMenu/GameFlow 互相引用。布局预览不派发任何玩法动作。

`HorizontalCubeVine` 只登记真实已解锁/落稳的攀爬表面；`PlayerController.ProtectClimbCorner`、`CancelClimbCorner` 与 `TickClimbCorner` 统一写运动速度。表面禁用、移动/尺寸变化、重置、死亡与控制锁均中断转角。不要由功能脚本直接搬玩家 Transform。

## 验证入口

隔离工程 `Library/FlowRetirementProbe` 可执行 `Project.Player.Editor.ClimbMobileVerification.Run`，覆盖真实绿块转角、脱手、持续选色和布局保存；输入编辑测试覆盖三指和双跳跃所有权。安卓包仅 MainMenu/Gaming，原生隐私流程见 `ANDROID_PRIVACY.md`；手感和隐私边界需真机验收。
