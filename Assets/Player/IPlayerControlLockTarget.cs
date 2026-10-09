using System;

namespace Project.Player
{
    public interface IPlayerControlLockTarget
    {
        IDisposable AcquireControlLock(object owner);
    }
}
