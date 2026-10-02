namespace Project.Achievements
{
    public interface IAchievementUnlockReceiver
    {
        void OnAchievementUnlocked(string displayName);
    }
}
