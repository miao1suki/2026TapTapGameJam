# Camera Close Timeline

资源：`Assets/_Project/Content/Boss/Timelines/camera_close.playable`

该 Timeline 只有一个 `PlayableTrack`，片段的 `CameraClosePlayable` 不绑定对象，直接通过场景中的 `CameraControlManager` 申请镜头缩放租约。

参数：

- `sizeScale`：相对当前玩法镜头尺寸的缩放倍率，默认 `0.5`，必须大于 0。
- `blendDuration`：进入和退出缩放时使用的平滑时间，默认 `1.25` 秒。

## 触发器接线

把该 Timeline 放到 `ContactTimelineTrigger` 的 `timeline`：

- `triggerMode` 使用“进入播放，离开停止”。
- `contactFilter` 按玩法需要选择 Player 或 Tag。
- 玩家进入范围时 Playable 申请缩放，Timeline 持续播放。
- 玩家离开范围时触发器停止 Director；`CameraCloseBehaviour` 除 `OnBehaviourPause/OnGraphStop` 外还直接订阅 `PlayableDirector.stopped`，任一停止路径都会释放同一镜头租约，`CameraControlManager` 按相同平滑时间恢复。

## 相机约束

- Playable 不直接写 `Camera.orthographicSize`。
- `RequestLensScale` 与现有 `RequestLensMultiplier` / `RequestRelativeLensMultiplier` 组合；普通放大请求仍保持倍率不小于 1，缩放请求允许 `0.5` 等正数。
- Timeline 相机控制权优先级为 Cutscene 时，镜头缩放请求保持挂起，不覆盖演出相机。
- 请求持有者消失、Director 停止、Timeline 重播或对象销毁时必须释放，不允许残留缩小视野。
