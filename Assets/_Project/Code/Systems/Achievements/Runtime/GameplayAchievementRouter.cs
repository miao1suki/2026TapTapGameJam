using Project.InputAbstraction;
using UnityEngine;

namespace Project.Achievements
{
    public static class GameplayAchievementRouter
    {
        private const string ConditionId = "0001";
        private const int LowCollectibleHome = 1005;
        private const int FakeEnding = 1006;
        private const int TrueEnding = 1007;
        private const int ColorAbilityUsed = 1008;
        private const int CollectibleFivePercent = 1009;
        private const int CollectibleTenPercent = 1010;

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
                case AchievementSignalIds
                    .LowCollectibleReturnHome:
                    Trigger(LowCollectibleHome);
                    break;
                case AchievementSignalIds.FakeEndingReached:
                    Trigger(FakeEnding);
                    break;
                case AchievementSignalIds.TrueEndingReached:
                    Trigger(TrueEnding);
                    break;
                case AchievementSignalIds.ColorAbilityUsed:
                    Trigger(ColorAbilityUsed);
                    break;
                case AchievementSignalIds
                    .CollectibleProgress5:
                    Trigger(CollectibleFivePercent);
                    break;
                case AchievementSignalIds
                    .CollectibleProgress10:
                    Trigger(CollectibleTenPercent);
                    break;
            }
        }

        private static void Trigger(int achievementId)
        {
            AchievementManager.Instance.TriggerById(
                achievementId,
                ConditionId,
                1,
                100f);
        }
    }
}
