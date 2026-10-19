using Project.InputAbstraction;
using UnityEngine;

namespace Project.Achievements
{
    public static class PlayerDeathAchievementRouter
    {
        private const string ConditionId = "0001";
        private const int FallFirstDeath = 1001;
        private const int SpikeFirstDeath = 1002;
        private const int FallTenDeaths = 1003;
        private const int SpikeTenDeaths = 1004;
        private const int LavaDeath = 1011;
        private const int TotalDeaths = 1012;
        private const int TotalFiveDeaths = 1013;
        private const int LavaTenDeaths = 1014;

        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            if (subscribed)
            {
                GameplaySignalHub.Signal -=
                    OnGameplaySignal;
            }

            GameplaySignalHub.Signal +=
                OnGameplaySignal;
            subscribed = true;
        }

        private static void OnGameplaySignal(
            GameplaySignal signal)
        {
            switch (signal.SignalId)
            {
                case AchievementSignalIds.PlayerFallDeath:
                    Trigger(FallFirstDeath);
                    Trigger(FallTenDeaths);
                    break;
                case AchievementSignalIds.PlayerSpikeDeath:
                    Trigger(SpikeFirstDeath);
                    Trigger(SpikeTenDeaths);
                    break;
                case AchievementSignalIds.PlayerLavaDeath:
                    Trigger(LavaDeath);
                    Trigger(LavaTenDeaths);
                    break;
                case AchievementSignalIds.PlayerDied:
                    Trigger(TotalDeaths);
                    Trigger(TotalFiveDeaths);
                    break;
            }
        }

        private static void Trigger(
            int achievementId)
        {
            AchievementManager.Instance.TriggerById(
                achievementId,
                ConditionId,
                1,
                100f);
        }
    }
}
