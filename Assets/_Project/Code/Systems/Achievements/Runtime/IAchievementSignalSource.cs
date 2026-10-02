using System;

namespace Project.Achievements
{
    public interface IAchievementSignalSource
    {
        event Action<AchievementSignal> AchievementSignal;
    }
}
