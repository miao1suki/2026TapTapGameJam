using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;

namespace Project.Achievements
{
    [AddComponentMenu("2026TapTap/Achievements/AchievementSignalBridge")]
    [DisallowMultipleComponent]
    public class AchievementSignalBridge :
        MonoBehaviour,
        IAchievementSignalSource
    {
        [SerializeField]
        private Component signalProvider;

        [SerializeField]
        private string signalId = "AchievementSignal";

        public event Action<AchievementSignal> AchievementSignal;

        public IReadOnlyList<string> GetAvailableSignalIds()
        {
            return signalProvider is
                   IAchievementSignalProvider provider
                ? provider.GetAchievementSignalIds()
                : Array.Empty<string>();
        }

        private void OnEnable()
        {
            GameplaySignalHub.Signal += OnGameplaySignal;
        }

        private void OnDisable()
        {
            GameplaySignalHub.Signal -= OnGameplaySignal;
        }

        public void Emit()
        {
            EmitSignal(
                signalId,
                0,
                0f);
        }

        public void EmitWithName(string value)
        {
            EmitSignal(
                value,
                0,
                0f);
        }

        public void EmitWithCount(int value)
        {
            EmitSignal(
                signalId,
                value,
                0f);
        }

        public void EmitWithProgress(float value)
        {
            EmitSignal(
                signalId,
                0,
                value);
        }

        public void EmitWithValues(
            int valueCount,
            float valueProgress)
        {
            EmitSignal(
                signalId,
                valueCount,
                valueProgress);
        }

        private void EmitSignal(
            string valueSignalId,
            int valueCount,
            float valueProgress)
        {
            AchievementSignal?.Invoke(
                new AchievementSignal(
                    valueSignalId,
                    count: valueCount,
                    progress: valueProgress));
        }

        private void OnGameplaySignal(
            GameplaySignal signal)
        {
            if (string.IsNullOrWhiteSpace(signalId) ||
                signal.SignalId != signalId)
            {
                return;
            }

            if (signalProvider != null &&
                signal.Source != signalProvider.gameObject)
            {
                return;
            }

            EmitSignal(
                signal.SignalId,
                signal.Count,
                signal.Progress);
        }
    }
}
