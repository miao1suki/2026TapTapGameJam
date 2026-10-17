using UnityEngine;

namespace Project.GreenHook
{
    public enum GreenHookMotionMode
    {
        PullTarget = 0,
        PullPlayer = 1,
        Swing = 2
    }

    public enum GreenHookLaunchResult
    {
        Success = 0,
        Ignored = 1,
        Rejected = 2,
        OutOfRange = 3,
        Busy = 4
    }

    public interface IGreenHookTarget
    {
        GreenHookMotionMode GreenHookMotionMode { get; }
        Transform GreenHookTransform { get; }
        bool CanBeGreenHooked(GameObject actor);
    }

    public interface IGreenHookMotion
    {
        bool IsGreenHookPullActive { get; }
        Vector3 GreenHookPosition { get; }
        void BeginGreenHookPull();
        void SetGreenHookVelocity(Vector3 velocity);
        void EndGreenHookPull(bool snapToGrid, Vector3 separationDirection, float separationDistance);
    }
}
