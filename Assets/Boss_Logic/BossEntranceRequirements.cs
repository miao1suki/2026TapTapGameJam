using Project.ColorBlocks;
using Project.Items;

namespace Project.BossLogic
{
    /// <summary>Shared by both the contact gate and the entrance Timeline; no new save state.</summary>
    public static class BossEntranceRequirements
    {
        public const int RequiredColorKeys = 6;
        private static readonly string[] Colors = { "red", "green", "blue" };

        // Each color has two independently saved stages: the scene key and the player-ability key.
        // Checking only scene colors would incorrectly allow entrance after three keys.
        public static int CollectedColorKeyCount
        {
            get
            {
                var progress = ColorRuntimeService.Existing;
                if (progress == null) return 0;
                int count = 0;
                foreach (string color in Colors)
                {
                    if (progress.IsUnlocked(color)) count++;
                    if (progress.IsAbilityUnlocked(color)) count++;
                }
                return count;
            }
        }

        public static bool CanEnter(CollectibleProgressSnapshot progress, int colorKeyCount, float minimumRatio = .3f) =>
            colorKeyCount >= RequiredColorKeys && progress.MeetsRatio(minimumRatio);

        public static string RequirementHint(CollectibleProgressSnapshot progress, int colorKeyCount, float minimumRatio = .3f) =>
            $"进入需收集至少{minimumRatio:P0}的调色板，并集齐6把颜色钥匙（当前调色板{progress.Collected}/{progress.Total}，钥匙{colorKeyCount}/6）";
    }
}
