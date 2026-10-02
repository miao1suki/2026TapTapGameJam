using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Achievements
{
    public enum AchievementLookupMode
    {
        Id = 0,
        DisplayName = 1
    }

    [Serializable]
    public sealed class AchievementDetectionBinding
    {
        [SerializeField]
        private bool enabled = true;

        [SerializeField]
        private Component source;

        [SerializeField]
        private string signalName = string.Empty;

        [SerializeField]
        private AchievementLookupMode lookupMode =
            AchievementLookupMode.Id;

        [SerializeField]
        private int achievementId;

        [SerializeField]
        private string achievementName = string.Empty;

        [SerializeField]
        private string conditionId = "0001";

        [SerializeField]
        private int count = 1;

        [SerializeField]
        private float progress = 1f;

        public bool Enabled => enabled;
        public Component Source => source;
        public string SignalName => signalName;
        public AchievementLookupMode LookupMode => lookupMode;
        public int AchievementId => achievementId;
        public string AchievementName => achievementName;
        public string ConditionId => conditionId;
        public int Count => count;
        public float Progress => progress;
    }

    [DisallowMultipleComponent]
    public sealed class AchievementDetector : MonoBehaviour
    {
        private sealed class BindingRuntime
        {
            public AchievementDetectionBinding Binding;
            public IAchievementSignalSource CustomSource;
            public Action<AchievementSignal> CustomAction;
            public UnityEngine.Events.UnityAction ButtonAction;
            public UnityEngine.Events.UnityAction<float> FloatAction;
            public UnityEngine.Events.UnityAction<bool> BoolAction;
            public UnityEngine.Events.UnityAction<int> IntAction;
            public UnityEngine.Events.UnityAction<string> StringAction;
            public UnityEngine.Events.UnityAction<Vector2> VectorAction;
        }

        [SerializeField]
        private List<AchievementDetectionBinding> bindings =
            new List<AchievementDetectionBinding>();

        private readonly List<BindingRuntime> runtimeBindings =
            new List<BindingRuntime>();

        public IReadOnlyList<AchievementDetectionBinding> Bindings =>
            bindings;

        private void OnEnable()
        {
            SubscribeAll();
        }

        private void OnDisable()
        {
            UnsubscribeAll();
        }

        private void OnDestroy()
        {
            UnsubscribeAll();
        }

        private void SubscribeAll()
        {
            UnsubscribeAll();
            for (int index = 0;
                 index < bindings.Count;
                 index++)
            {
                AchievementDetectionBinding binding =
                    bindings[index];
                if (!binding.Enabled ||
                    binding.Source == null)
                {
                    continue;
                }

                BindingRuntime runtime =
                    new BindingRuntime
                    {
                        Binding = binding
                    };
                if (binding.Source is
                    IAchievementSignalSource customSource)
                {
                    runtime.CustomSource = customSource;
                    runtime.CustomAction =
                        signal => OnCustomSignal(
                            binding,
                            signal);
                    customSource.AchievementSignal +=
                        runtime.CustomAction;
                    runtimeBindings.Add(runtime);
                    continue;
                }

                if (binding.Source is Button button)
                {
                    runtime.ButtonAction =
                        () => Fire(binding);
                    button.onClick.AddListener(
                        runtime.ButtonAction);
                }
                else if (binding.Source is Slider slider)
                {
                    runtime.FloatAction =
                        _ => Fire(binding);
                    slider.onValueChanged.AddListener(
                        runtime.FloatAction);
                }
                else if (binding.Source is Toggle toggle)
                {
                    runtime.BoolAction =
                        _ => Fire(binding);
                    toggle.onValueChanged.AddListener(
                        runtime.BoolAction);
                }
                else if (binding.Source is Dropdown dropdown)
                {
                    runtime.IntAction =
                        _ => Fire(binding);
                    dropdown.onValueChanged.AddListener(
                        runtime.IntAction);
                }
                else if (binding.Source is InputField inputField)
                {
                    runtime.StringAction =
                        _ => Fire(binding);
                    inputField.onValueChanged.AddListener(
                        runtime.StringAction);
                }
                else if (binding.Source is ScrollRect scrollRect)
                {
                    runtime.VectorAction =
                        _ => Fire(binding);
                    scrollRect.onValueChanged.AddListener(
                        runtime.VectorAction);
                }
                else
                {
                    Debug.LogWarning(
                        "[AchievementDetector] 不支持的源组件：" +
                        binding.Source.GetType().Name,
                        this);
                    continue;
                }

                runtimeBindings.Add(runtime);
            }
        }

        private void UnsubscribeAll()
        {
            for (int index = 0;
                 index < runtimeBindings.Count;
                 index++)
            {
                BindingRuntime runtime =
                    runtimeBindings[index];
                if (runtime.CustomSource != null &&
                    runtime.CustomAction != null)
                {
                    runtime.CustomSource.AchievementSignal -=
                        runtime.CustomAction;
                }

                if (runtime.Binding.Source is Button button &&
                    runtime.ButtonAction != null)
                {
                    button.onClick.RemoveListener(
                        runtime.ButtonAction);
                }

                if (runtime.Binding.Source is Slider slider &&
                    runtime.FloatAction != null)
                {
                    slider.onValueChanged.RemoveListener(
                        runtime.FloatAction);
                }

                if (runtime.Binding.Source is Toggle toggle &&
                    runtime.BoolAction != null)
                {
                    toggle.onValueChanged.RemoveListener(
                        runtime.BoolAction);
                }

                if (runtime.Binding.Source is Dropdown dropdown &&
                    runtime.IntAction != null)
                {
                    dropdown.onValueChanged.RemoveListener(
                        runtime.IntAction);
                }

                if (runtime.Binding.Source is InputField inputField &&
                    runtime.StringAction != null)
                {
                    inputField.onValueChanged.RemoveListener(
                        runtime.StringAction);
                }

                if (runtime.Binding.Source is ScrollRect scrollRect &&
                    runtime.VectorAction != null)
                {
                    scrollRect.onValueChanged.RemoveListener(
                        runtime.VectorAction);
                }
            }

            runtimeBindings.Clear();
        }

        private void OnCustomSignal(
            AchievementDetectionBinding binding,
            AchievementSignal signal)
        {
            if (!string.IsNullOrWhiteSpace(
                    binding.SignalName) &&
                binding.SignalName != signal.SignalId)
            {
                return;
            }

            int count = signal.Count > 0
                ? signal.Count
                : binding.Count;
            float progress = signal.Progress > 0f
                ? signal.Progress
                : binding.Progress;
            Fire(binding, count, progress);
        }

        private void Fire(
            AchievementDetectionBinding binding)
        {
            Fire(
                binding,
                binding.Count,
                binding.Progress);
        }

        private void Fire(
            AchievementDetectionBinding binding,
            int count,
            float progress)
        {
            AchievementManager manager =
                AchievementManager.Instance;
            if (binding.LookupMode ==
                AchievementLookupMode.Id)
            {
                manager.TriggerById(
                    binding.AchievementId,
                    binding.ConditionId,
                    count,
                    progress);
                return;
            }

            manager.TriggerByName(
                binding.AchievementName,
                binding.ConditionId,
                count,
                progress);
        }
    }
}
