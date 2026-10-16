namespace Project.InputAbstraction
{
    public interface IMobileGameplayContext
    {
        bool IsDead { get; }
        bool IsCarrying { get; }
        bool IsSwimming { get; }
        bool IsClimbing { get; }
        bool IsHorizontalClimbing { get; }
        bool IsGreenHookActive { get; }
        bool ShowAirborneDownControl { get; }
        bool HasContextInteraction { get; }
        int CarryQueueCount { get; }
    }

    public static class MobileGameplayContext
    {
        public static IMobileGameplayContext Current { get; set; }
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Current = null;
    }
}
