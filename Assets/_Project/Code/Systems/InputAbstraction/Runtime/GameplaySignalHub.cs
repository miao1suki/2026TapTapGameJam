using System;
using UnityEngine;

namespace Project.InputAbstraction
{
    public readonly struct GameplaySignal
    {
        public GameplaySignal(
            string signalId,
            GameObject source,
            int count = 0,
            float progress = 0f)
        {
            SignalId = signalId ?? string.Empty;
            Source = source;
            Count = count;
            Progress = progress;
        }

        public string SignalId { get; }
        public GameObject Source { get; }
        public int Count { get; }
        public float Progress { get; }
    }

    public static class GameplaySignalHub
    {
        public static event Action<GameplaySignal> Signal;

        public static void Emit(
            string signalId,
            GameObject source,
            int count = 0,
            float progress = 0f)
        {
            if (string.IsNullOrWhiteSpace(signalId) ||
                source == null)
            {
                return;
            }

            Signal?.Invoke(
                new GameplaySignal(
                    signalId,
                    source,
                    count,
                    progress));
        }
    }
}
