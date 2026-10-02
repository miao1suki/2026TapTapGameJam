using System.Collections.Generic;

namespace Project.InputAbstraction
{
    public interface IAchievementSignalProvider
    {
        IReadOnlyList<string> GetAchievementSignalIds();
    }
}
