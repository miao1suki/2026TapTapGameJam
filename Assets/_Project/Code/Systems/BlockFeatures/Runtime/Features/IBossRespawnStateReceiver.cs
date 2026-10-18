using System.Collections.Generic;
using UnityEngine;

namespace Project.BlockFeatures
{
    public interface IBossRespawnStateReceiver
    {
        bool IsBossRespawnStateAvailable { get; }
        float CurrentHealth { get; }
        Vector3 WorldPosition { get; }
        void ApplyBossRespawnState(float health, Vector3 worldPosition);
    }

    public static class BossRespawnStateRegistry
    {
        private static readonly List<IBossRespawnStateReceiver>
            Receivers =
                new List<IBossRespawnStateReceiver>();

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Receivers.Clear();
        }

        public static void Register(
            IBossRespawnStateReceiver receiver)
        {
            if (receiver == null ||
                Receivers.Contains(receiver))
            {
                return;
            }

            Receivers.Add(receiver);
        }

        public static void Unregister(
            IBossRespawnStateReceiver receiver)
        {
            if (receiver != null)
            {
                Receivers.Remove(receiver);
            }
        }

        public static IBossRespawnStateReceiver
            FindAvailable()
        {
            for (int index = Receivers.Count - 1;
                 index >= 0;
                 index--)
            {
                IBossRespawnStateReceiver receiver =
                    Receivers[index];
                if (receiver == null ||
                    receiver is Object obj && obj == null)
                {
                    Receivers.RemoveAt(index);
                    continue;
                }

                if (receiver.IsBossRespawnStateAvailable)
                {
                    return receiver;
                }
            }

            return null;
        }
    }
}
