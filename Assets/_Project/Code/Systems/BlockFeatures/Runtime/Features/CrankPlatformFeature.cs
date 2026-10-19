using System;
using System.Collections.Generic;
using Project.Audio;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "曲柄",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IMechanismSignalSource) },
        Writes = new[] { BlockChannel.Power })]
    public sealed class CrankPlatformFeature : BlockFeature,
        IInteractionTarget,
        IInteractionHoldTarget,
        IMechanismSignalSource
    {
        [BlockParameter(Label = "载人下降倍率", Group = "平台", Order = 2)]
        [SerializeField, Min(1f)] private float loadedDescentMultiplier = 3f;
        [BlockParameter(Label = "制动基座", Group = "平台", Order = 3, Tooltip = "基座通电时停止升降，包括玩家操作；共享组内任一有效曲柄的基座均可制动整组。")]
        [SerializeField] private MechanismBaseFeature brakeBase;
        private readonly HashSet<MovingPlatformFeature> platforms = new HashSet<MovingPlatformFeature>();
        // A secondary handle shares one authoritative progress value, not a
        // second platform drive. Bindings are persisted by PlanningBox ID.
        [BlockParameter(Label = "共享进度曲柄", Group = "平台", Order = 4,
            Tooltip = "可选：作为另一个曲柄的操作位置；共用进度、回弹和制动。不能串联或循环绑定。")]
        [SerializeField] private CrankPlatformFeature sharedController;
        private readonly HashSet<CrankPlatformFeature> sharedHandles = new HashSet<CrankPlatformFeature>();
        public CrankPlatformFeature SharedController => sharedController;
        public bool BindSharedController(CrankPlatformFeature source)
        {
            sharedHandles.RemoveWhere(handle => handle == null);
            if (source == this || source != null &&
                (source.sharedController != null || sharedHandles.Count > 0)) return false;
            if (sharedController != null)
            {
                sharedController.sharedHandles.Remove(this);
                sharedController.SignalChanged -= ForwardSharedSignal;
            }
            StopCrankAudio();
            engaged = false;
            sharedController = source;
            if (source != null)
            {
                source.sharedHandles.Add(this);
                source.SignalChanged += ForwardSharedSignal;
            }
            return true;
        }
        private void ForwardSharedSignal(bool active) => SignalChanged?.Invoke(active && CanRunFeature);
        public MechanismBaseFeature BrakeBase => brakeBase;
        public IReadOnlyCollection<MovingPlatformFeature> Platforms => platforms;
        // Compatibility for legacy callers. New callers use Platforms/BindCrank.
        public Transform LiftPlatform
        {
            get { foreach (var platform in platforms) if (platform != null) return platform.transform; return null; }
        }
        public bool HasStandingPlayer
        {
            get
            {
                foreach (var platform in platforms)
                    if (platform != null && platform.IsDriveAvailable && platform.HasStandingPlayer) return true;
                foreach (var handle in sharedHandles)
                    if (handle != null && handle.CanRunFeature)
                        foreach (var platform in handle.platforms)
                            if (platform != null && platform.IsDriveAvailable && platform.HasStandingPlayer) return true;
                return false;
            }
        }
        public bool IsDriveAvailable => CanRunFeature;
        [SerializeField, HideInInspector] private bool initialPlatformCreated;
        public bool InitialPlatformCreated => initialPlatformCreated;
        public void MarkInitialPlatformCreated() => initialPlatformCreated = true;
        public void RegisterPlatform(MovingPlatformFeature platform) { if (platform != null) platforms.Add(platform); }
        public void UnregisterPlatform(MovingPlatformFeature platform) { if (platform != null) platforms.Remove(platform); }
        public void BindBase(MechanismBaseFeature source) => brakeBase = source;
        [BlockParameter(
            Label = "每秒转动进度",
            Group = "曲柄设置",
            Order = 0,
            Tooltip = "按住交互键时，每秒增加多少进度。")]
        [SerializeField, Min(0f)] private float progressPerSecond = .35f;

        [BlockParameter(
            Label = "无人后回弹",
            Group = "回弹设置",
            Order = 0)]
        [SerializeField] private bool reboundWhenIdle = true;

        [BlockParameter(
            Label = "回弹等待时间",
            Group = "回弹设置",
            Order = 1,
            VisibleWhenField = "reboundWhenIdle",
            VisibleWhenValue = 1)]
        [SerializeField, Min(0f)] private float idleDelayBeforeRebound = .5f;

        [BlockParameter(
            Label = "每秒回弹进度",
            Group = "回弹设置",
            Order = 2,
            VisibleWhenField = "reboundWhenIdle",
            VisibleWhenValue = 1)]
        [SerializeField, Min(0f)] private float reboundProgressPerSecond = .2f;

        [BlockParameter(
            Label = "转动动画",
            Group = "动画资源",
            Order = 0)]
        [SerializeField] private AnimationClip turningAnimation;

        [BlockParameter(
            Label = "回弹动画",
            Group = "动画资源",
            Order = 1)]
        [SerializeField] private AnimationClip reboundAnimation;

        [BlockParameter(
            Label = "动画控制器",
            Group = "动画资源",
            Order = 2,
            Tooltip = "留空时自动查找子物体上的 Animator。")]
        [SerializeField] private Animator animator;

        private bool engaged;
        private bool crankAudioPlaying;
        private float progress;
        private float idleStartedAt;
        private AnimationClip playingClip;

        public float Progress => sharedController != null && sharedController != this && sharedController.sharedController == null
            ? Mathf.Clamp01(sharedController.progress) : Mathf.Clamp01(progress);
        public bool IsSignalActive =>
            Progress >= .999f && CanRunFeature && (sharedController == null || sharedController.CanRunFeature);
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
            if (sharedController != null && !BindSharedController(sharedController)) BindSharedController(null);
            engaged = false;
            progress = 0f;
            idleStartedAt = Time.time;
            playingClip = null;
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }
        }

        protected override void OnDetach()
        {
            if (sharedController != null)
            {
                sharedController.sharedHandles.Remove(this);
                sharedController.SignalChanged -= ForwardSharedSignal;
            }
            StopCrankAudio(false);
            engaged = false;
            progress = 0f;
            playingClip = null;
        }

        protected override void OnTick(float deltaTime)
        {
            if (sharedController != null) return; // Only the group owner advances time.
            if (IsGroupBraked())
            {
                StopCrankAudio();
                return;
            }
            bool wasComplete = IsSignalActive;
            bool anyEngaged = engaged;
            foreach (var handle in sharedHandles)
                if (handle != null && handle.CanRunFeature && handle.engaged) anyEngaged = true;
            if (anyEngaged)
            {
                bool isTurning = progress < 1f && progressPerSecond > 0f && deltaTime > 0f;
                if (!isTurning) StopCrankAudio();
                else if (!crankAudioPlaying)
                {
                    crankAudioPlaying = AudioCuePlayer.PlayLoop(
                        AudioCueIds.CrankTurnLoop,
                        this,
                        .85f);
                }
                progress += progressPerSecond * deltaTime;
                idleStartedAt = Time.time;
                PlayClip(turningAnimation);
            }
            else
            {
                StopCrankAudio();
                if (reboundWhenIdle &&
                    progress > 0f &&
                    Time.time - idleStartedAt >=
                    idleDelayBeforeRebound)
                {
                    progress -= reboundProgressPerSecond * (HasStandingPlayer ? loadedDescentMultiplier : 1f) * deltaTime;
                    PlayClip(reboundAnimation);
                }
            }

            progress = Mathf.Clamp01(progress);
            if (progress >= 1f) StopCrankAudio();
            if (wasComplete != IsSignalActive)
            {
                SignalChanged?.Invoke(IsSignalActive);
            }
        }

        private bool IsGroupBraked()
        {
            if (brakeBase != null && brakeBase.IsSignalActive) return true;
            foreach (var handle in sharedHandles)
                if (handle != null && handle.sharedController == this && handle.CanRunFeature &&
                    handle.brakeBase != null && handle.brakeBase.IsSignalActive) return true;
            return false;
        }

        protected override void OnResetForRoom()
        {
            StopCrankAudio(false);
            engaged = false;
            progress = 0f;
            idleStartedAt = Time.time;
            playingClip = null;
            if (sharedController != null) return;
            foreach (var handle in sharedHandles)
                if (handle != null) handle.engaged = false;
            SignalChanged?.Invoke(false);
            foreach (var platform in platforms) if (platform != null) platform.SnapToCrankProgress();
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!CanRunFeature || interactor == null ||
                sharedController != null && !sharedController.CanRunFeature)
            {
                return false;
            }

            PlayerController player =
                interactor.GetComponentInParent<PlayerController>();
            return player != null &&
                   Vector3.Distance(
                       transform.position,
                       player.transform.position) <=
                   GridCellWorldSize + .25f;
        }

        public bool TryInteract(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return false;
            }

            engaged = true;
            return true;
        }

        public void HoldInteract(GameObject interactor)
        {
            engaged = CanInteract(interactor);
        }

        public void EndInteract(GameObject interactor)
        {
            engaged = false;
            idleStartedAt = Time.time;
            if (sharedController != null) sharedController.idleStartedAt = Time.time;
        }

        public void SetSignal(bool active, Component source)
        {
            if (!CanRunFeature)
            {
                return;
            }

            if (sharedController != null) { sharedController.SetSignal(active, source); return; }
            foreach (var handle in sharedHandles)
                if (handle != null) handle.engaged = false;

            progress = active ? 1f : 0f;
            foreach (var platform in platforms) if (platform != null) platform.SnapToCrankProgress();
            engaged = false;
            idleStartedAt = Time.time;
            SignalChanged?.Invoke(IsSignalActive);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue("平台载人", HasStandingPlayer ? "有人" : "无人"));
            values.Add(new BlockDebugValue("绑定平台数", platforms.Count));
            values.Add(new BlockDebugValue("共享进度", sharedController != null ? sharedController.name : "本曲柄"));
            values.Add(new BlockDebugValue(
                "曲柄进度",
                $"{Progress:P0}"));
            values.Add(new BlockDebugValue(
                "无人回弹",
                reboundWhenIdle ? "开启" : "关闭"));
            values.Add(new BlockDebugValue(
                "输出状态",
                IsSignalActive ? "已满" : "未满"));
        }

        public override void CollectDebugActions(
            List<BlockDebugAction> actions)
        {
            actions.Add(new BlockDebugAction(
                "直接转满",
                () => SetSignal(true, this),
                Application.isPlaying));
            actions.Add(new BlockDebugAction(
                "重置进度",
                () => SetSignal(false, this),
                Application.isPlaying && progress > 0f));
        }

        private void PlayClip(AnimationClip clip)
        {
            if (animator == null ||
                clip == null ||
                playingClip == clip)
            {
                return;
            }

            playingClip = clip;
            animator.Play(clip.name, 0, 0f);
        }

        private void StopCrankAudio(bool playStopSound = true)
        {
            if (!crankAudioPlaying)
            {
                return;
            }

            crankAudioPlaying = false;
            AudioCuePlayer.StopLoop(this);
            if (playStopSound)
            {
                AudioCuePlayer.Play(AudioCueIds.MachineStop);
            }
        }
    }
}
