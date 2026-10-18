using System;
using System.Collections.Generic;
using DG.Tweening;
using Project.Audio;
using Project.BlockFeatures;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum DoorState { Locked, Unlocked }
    public enum DoorTimelineTrigger
    {
        [InspectorName("关闭")] Disabled = 0,
        [InspectorName("每次按钮按下")] ButtonPressed = 1,
        [InspectorName("全部按钮达成开门时")] DoorOpening = 2
    }

    [DisallowMultipleComponent]
    public sealed class DoorController : MonoBehaviour
    {
        [SerializeField, InspectorName("门轴")] private Transform hinge;
        [SerializeField, InspectorName("阻挡碰撞箱")] private Collider blockingCollider;
        [SerializeField, InspectorName("绑定按钮")] private List<DoorButton> buttons = new List<DoorButton>();
        [SerializeField, InspectorName("提示方块")] private DoorHint hint;
        [SerializeField, InspectorName("横门")] private bool horizontal;
        [SerializeField, InspectorName("开门旋转轴")] private Vector3 openEuler = new Vector3(0, 90, 0);
        public const int MaximumButtons = 3;
        [BlockParameter(Label = "开门时间", Group = "开门", Order = 0)]
        [SerializeField, Min(.01f)] private float openDuration = .6f;
        [BlockParameter(Label = "图案展示时间", Group = "开门", Order = 1)]
        [SerializeField, Min(0f)] private float unlockDelay = .8f;
        [BlockParameter(Label = "松开后关门延迟（秒）", Group = "关门", Order = 0,
            Tooltip = "持续压住按钮松开后仍保留通行时间；计时内重新按齐按钮会取消关门。")]
        [SerializeField, Min(0f)] private float closeDelay = .6f;
        [BlockParameter(Label = "Timeline触发", Group = "演出", Order = -2,
            Tooltip = "默认关闭，旧场景也不自动播放。按钮模式只响应一次真实按下，不响应初始化、松开或重复检测。")]
        [SerializeField] private DoorTimelineTrigger timelineTrigger;
        [BlockParameter(Label = "演出锁定玩家", Group = "演出", Order = -1,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2 })]
        [SerializeField] private bool lockPlayerDuringTimeline = true;
        [BlockParameter(Label = "镜头移动时间", Group = "演出", Order = 0,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2 })]
        [SerializeField, Min(.1f)] private float cameraMoveDuration = 1f;
        [BlockParameter(Label = "镜头停留时间", Group = "演出", Order = 1,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2 })]
        [SerializeField, Min(0)] private float cameraHoldDuration = 1.2f;
        [BlockParameter(Label = "镜头返回时间", Group = "演出", Order = 2,
            VisibleWhenField = "timelineTrigger", VisibleWhenValues = new[] { 1, 2 })]
        [SerializeField, Min(0)] private float cameraReturnDuration = .7f;
        [BlockParameter(Label = "全局进度重置时关门", Group = "重置", Order = 0)]
        [SerializeField] private bool resetWithRoom = true;
        private Quaternion closedRotation;
        private Tween opening;
        private MechanismCameraShot cameraShot;
        private int queuedButtonShots;
        private GameObject pendingShotActor;
        private bool pendingOpen;
        private float pendingOpenRemaining;
        private bool pendingClose;
        private float pendingCloseRemaining;
        private bool resetting;
        public DoorState State { get; private set; }
        public float OpenDuration => openDuration;
        public float UnlockDelay => unlockDelay;
        public bool IsWaitingToOpen => pendingOpen;
        public float CloseDelay => closeDelay;
        public bool IsWaitingToClose => pendingClose;
        public IReadOnlyList<DoorButton> Buttons => buttons;
        public bool IsHorizontal => horizontal;
        public DoorTimelineTrigger TimelineTrigger => timelineTrigger;
        public bool IsTimelinePlaying => cameraShot != null && cameraShot.IsPlaying;
        public int PressedCount { get { int count = 0; foreach (var button in buttons) if (button != null && button.IsPressed) count++; return count; } }
        public event Action<DoorState> StateChanged;

        private void Awake()
        {
            if (blockingCollider == null) blockingCollider = GetComponent<Collider>();
            closedRotation = hinge != null ? hinge.localRotation : Quaternion.identity;
            State = DoorState.Locked;
            SetButtons(buttons);
        }
        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EventMgr.OnRoomColorReset += HandleRoomReset;
            NotifyButtonPressed(null);
        }
        private void OnDisable()
        {
            pendingOpen = false;
            pendingClose = false;
            EventMgr.OnRoomColorReset -= HandleRoomReset;
            opening?.Kill();
            CleanupShot();
        }
        public void SetButtons(IReadOnlyList<DoorButton> values)
        {
            var next = values != null ? new List<DoorButton>(values) : new List<DoorButton>();
            var previous = buttons.ToArray();
            buttons.Clear();
            foreach (var button in previous) if (button != null && button.Owner == this) button.SetOwner(null);
            foreach (var button in next)
                if (button != null && !buttons.Contains(button)) { buttons.Add(button); button.SetOwner(this); }
            RefreshHint();
            if (Application.isPlaying && isActiveAndEnabled) NotifyButtonPressed(null);
        }
        private void OnDestroy()
        {
            if (!Application.isPlaying) return;
            var owned = buttons.ToArray();
            buttons.Clear();
            foreach (var button in owned)
                if (button != null && button.Owner == this) { button.SetOwner(null); Destroy(button.gameObject); }
        }
        public void ForgetButton(DoorButton button)
        {
            if (!buttons.Remove(button)) return;
            if (button != null && button.Owner == this) button.SetOwner(null);
            RefreshHint();
            if (Application.isPlaying) NotifyButtonPressed(null);
        }
        public void RefreshHint()
        {
            if (hint == null) hint = GetComponentInChildren<DoorHint>(true);
            if (hint == null)
            {
                var item = new GameObject("门提示");
                item.transform.SetParent(transform, false);
                item.transform.localPosition = horizontal ? new Vector3(.5f, .8f, -.1f) : new Vector3(0, 1.8f, -.1f);
                hint = item.AddComponent<DoorHint>();
            }
            hint.Bind(this);
        }
        public void NotifyButtonPressed(GameObject actor)
        {
            RefreshHint();
            if (!Application.isPlaying || !isActiveAndEnabled || resetting) return;
            bool allPressed = buttons.Count > 0, hasHeld = false;
            foreach (var button in buttons)
            {
                if (button == null || !button.isActiveAndEnabled || !button.IsPressed) allPressed = false;
                if (button != null && button.Mode == DoorButtonMode.Held) hasHeld = true;
            }
            if (!allPressed)
            {
                if (pendingOpen) CancelPendingOpen();
                if ((hasHeld || buttons.Count == 0) && State == DoorState.Unlocked && !pendingClose)
                {
                    pendingClose = true;
                    pendingCloseRemaining = Mathf.Max(0f, closeDelay);
                    if (pendingCloseRemaining <= 0f) CloseFromPressureRelease();
                }
                return;
            }
            pendingClose = false;
            if (State != DoorState.Locked || pendingOpen) return;
            pendingOpen = true;
            pendingOpenRemaining = Mathf.Max(0f, unlockDelay);
            if (timelineTrigger == DoorTimelineTrigger.DoorOpening)
            {
                cameraShot ??= new MechanismCameraShot(this);
                if (cameraShot.TryPlay(actor, new Vector3(0f, .5f, 0f), cameraMoveDuration,
                    unlockDelay + openDuration + cameraHoldDuration, cameraReturnDuration,
                    lockPlayerDuringTimeline, () =>
                    {
                        if (pendingOpen) pendingOpenRemaining = Mathf.Max(0f, unlockDelay);
                    }))
                    pendingOpenRemaining = Mathf.Max(0f, unlockDelay) + cameraMoveDuration;
            }
        }
        // Separate edge notification: the legacy method also handles releases, rebuilds and initial state.
        public void NotifyButtonStateChanged(DoorButton source, GameObject actor)
        {
            if (Application.isPlaying && isActiveAndEnabled && !resetting && source != null &&
                source.Owner == this && source.IsPressed && buttons.Contains(source) &&
                timelineTrigger == DoorTimelineTrigger.ButtonPressed)
            {
                queuedButtonShots++;
                pendingShotActor = actor;
            }
            NotifyButtonPressed(actor);
        }
        private void Update()
        {
            if (timelineTrigger == DoorTimelineTrigger.Disabled && (IsTimelinePlaying || queuedButtonShots > 0)) CleanupShot();
            if (timelineTrigger == DoorTimelineTrigger.ButtonPressed && queuedButtonShots > 0 && Time.deltaTime > 0f)
            {
                cameraShot ??= new MechanismCameraShot(this);
                if (!cameraShot.IsPlaying)
                {
                    queuedButtonShots--;
                    cameraShot.TryPlay(pendingShotActor, new Vector3(0f, .5f, 0f), cameraMoveDuration,
                        cameraHoldDuration, cameraReturnDuration, lockPlayerDuringTimeline);
                }
            }
            AdvanceTimers(Time.deltaTime);
        }
        private void AdvanceTimers(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            if (pendingClose)
            {
                pendingCloseRemaining -= deltaTime;
                if (pendingCloseRemaining <= 0f) CloseFromPressureRelease();
            }
            if (!pendingOpen) return;
            foreach (var button in buttons)
                if (button == null || !button.isActiveAndEnabled || !button.IsPressed) { CancelPendingOpen(); return; }
            if (buttons.Count == 0) { CancelPendingOpen(); return; }
            pendingOpenRemaining -= deltaTime;
            if (pendingOpenRemaining > 0f) return;
            pendingOpen = false;
            State = DoorState.Unlocked;
            if (blockingCollider != null) blockingCollider.enabled = false;
            opening?.Kill();
            if (hinge != null)
                opening = hinge.DOLocalRotateQuaternion(closedRotation * Quaternion.Euler(openEuler), openDuration)
                    .SetEase(Ease.OutCubic);
            StateChanged?.Invoke(State);
            AudioCuePlayer.Play(AudioCueIds.DoorOpen, .9f);
        }
        private void CancelPendingOpen()
        {
            pendingOpen = false;
            if (timelineTrigger == DoorTimelineTrigger.DoorOpening) CleanupShot();
        }
        private void CloseFromPressureRelease()
        {
            pendingClose = false;
            opening?.Kill();
            if (timelineTrigger == DoorTimelineTrigger.DoorOpening) CleanupShot();
            State = DoorState.Locked;
            if (blockingCollider != null) blockingCollider.enabled = true;
            if (hinge != null) opening = hinge.DOLocalRotateQuaternion(closedRotation,openDuration).SetEase(Ease.OutCubic);
            StateChanged?.Invoke(State);
            AudioCuePlayer.Play(AudioCueIds.DoorClose, .85f);
        }
        public void ResetDoor()
        {
            resetting = true;
            pendingOpen = false;
            pendingClose = false;
            opening?.Kill();
            CleanupShot();
            State = DoorState.Locked;
            if (hinge != null) hinge.localRotation = closedRotation;
            if (blockingCollider != null) blockingCollider.enabled = true;
            foreach (var button in buttons) if (button != null) button.ResetButton();
            resetting = false;
            RefreshHint();
            StateChanged?.Invoke(State);
        }
        /// <summary>Release temporary hiding without rolling back door/button progress.</summary>
        public void RefreshBlockingState()
        {
            if (blockingCollider != null)
                blockingCollider.enabled = State == DoorState.Locked;
        }
        private void HandleRoomReset() { if (resetWithRoom) ResetDoor(); }
        private void CleanupShot()
        {
            queuedButtonShots = 0;
            pendingShotActor = null;
            cameraShot?.Dispose();
            cameraShot = null;
        }
    }
}
