using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "曲柄平台",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Mechanism,
        Interactions = BlockFeatureInteraction.RoomReset,
        Provides = new[] { typeof(IMechanismSignalSource) },
        Writes = new[] { BlockChannel.Power, BlockChannel.Motion })]
    public sealed class CrankPlatformFeature : BlockFeature,
        IInteractionTarget,
        IInteractionHoldTarget,
        IMechanismSignalSource
    {
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
        private float progress;
        private float idleStartedAt;
        private AnimationClip playingClip;

        public float Progress => Mathf.Clamp01(progress);
        public bool IsSignalActive =>
            progress >= .999f && CanRunFeature;
        public event Action<bool> SignalChanged;

        protected override void OnAttach()
        {
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
            engaged = false;
            progress = 0f;
            playingClip = null;
        }

        protected override void OnTick(float deltaTime)
        {
            bool wasComplete = IsSignalActive;
            if (engaged)
            {
                progress += progressPerSecond * deltaTime;
                idleStartedAt = Time.time;
                PlayClip(turningAnimation);
            }
            else if (reboundWhenIdle &&
                     progress > 0f &&
                     Time.time - idleStartedAt >=
                     idleDelayBeforeRebound)
            {
                progress -= reboundProgressPerSecond * deltaTime;
                PlayClip(reboundAnimation);
            }

            progress = Mathf.Clamp01(progress);
            if (wasComplete != IsSignalActive)
            {
                SignalChanged?.Invoke(IsSignalActive);
            }
        }

        protected override void OnResetForRoom()
        {
            engaged = false;
            progress = 0f;
            idleStartedAt = Time.time;
            playingClip = null;
            SignalChanged?.Invoke(false);
        }

        public bool CanInteract(GameObject interactor)
        {
            if (!CanRunFeature || interactor == null)
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
        }

        public void SetSignal(bool active, Component source)
        {
            if (!CanRunFeature)
            {
                return;
            }

            progress = active ? 1f : 0f;
            engaged = false;
            idleStartedAt = Time.time;
            SignalChanged?.Invoke(IsSignalActive);
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
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
    }
}
