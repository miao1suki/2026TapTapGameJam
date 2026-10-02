using System;

namespace Project.CameraModes
{
    public enum CameraViewMode
    {
        Side2D = 0,
        Perspective3D = 1,
    }

    public interface ICameraViewModeRequester
    {
        string CameraModeRequesterName { get; }

        int CameraModeRequestPriority { get; }
    }

    public interface ICameraViewModeAuthority
    {
        CameraViewMode CurrentMode { get; }

        CameraViewMode TargetMode { get; }

        bool TryGetProjectionState(out CameraState state);

        CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            bool immediate = false);

        CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            float side2DYawDegrees,
            bool immediate = false);

        CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            CameraTransition transition);

        CameraViewModeRequestHandle RequestMode(
            ICameraViewModeRequester requester,
            CameraViewMode mode,
            float side2DYawDegrees,
            CameraTransition transition);
    }

    public readonly struct CameraViewModeRequestHandle :
        IEquatable<CameraViewModeRequestHandle>,
        IDisposable
    {
        private readonly CameraModeController controller;
        private readonly int id;

        internal CameraViewModeRequestHandle(
            CameraModeController controller,
            int id)
        {
            this.controller = controller;
            this.id = id;
        }

        internal CameraModeController Controller => controller;
        internal int Id => id;

        public bool IsValid =>
            controller != null &&
            controller.IsModeRequestValid(this);

        public void Release(bool immediate = false)
        {
            controller?.ReleaseModeRequest(this, immediate);
        }

        public void Dispose()
        {
            Release(true);
        }

        public bool Equals(CameraViewModeRequestHandle other)
        {
            return controller == other.controller && id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is CameraViewModeRequestHandle other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((controller != null ? controller.GetHashCode() : 0) * 397) ^ id;
            }
        }

        public static bool operator ==(
            CameraViewModeRequestHandle left,
            CameraViewModeRequestHandle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(
            CameraViewModeRequestHandle left,
            CameraViewModeRequestHandle right)
        {
            return !left.Equals(right);
        }
    }
}
