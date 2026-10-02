using System.Collections.Generic;

namespace Project.CameraModes.Editor
{
    internal sealed class CameraModeEditorRequester :
        ICameraViewModeRequester
    {
        public string CameraModeRequesterName =>
            "Camera Mode Editor";

        public int CameraModeRequestPriority =>
            CameraControlPriorities.Gameplay;
    }

    internal static class CameraModeEditorRequests
    {
        private static readonly CameraModeEditorRequester Requester =
            new CameraModeEditorRequester();

        private static readonly Dictionary<
            CameraModeController,
            CameraViewModeRequestHandle> Handles =
            new Dictionary<
                CameraModeController,
                CameraViewModeRequestHandle>();

        public static void RequestMode(
            CameraModeController controller,
            CameraViewMode mode,
            bool immediate)
        {
            if (controller == null)
            {
                return;
            }

            Release(controller, immediate);
            Handles[controller] = controller.RequestMode(
                Requester,
                mode,
                immediate);
        }

        public static void RequestMode(
            CameraModeController controller,
            CameraViewMode mode,
            float side2DYawDegrees,
            bool immediate)
        {
            if (controller == null)
            {
                return;
            }

            Release(controller, immediate);
            Handles[controller] = controller.RequestMode(
                Requester,
                mode,
                side2DYawDegrees,
                immediate);
        }

        public static void Release(
            CameraModeController controller,
            bool immediate)
        {
            if (controller == null ||
                !Handles.TryGetValue(
                    controller,
                    out CameraViewModeRequestHandle handle))
            {
                return;
            }

            if (handle.IsValid)
            {
                handle.Release(immediate);
            }
            Handles.Remove(controller);
        }

        public static void ReleaseAll()
        {
            foreach (CameraViewModeRequestHandle handle in Handles.Values)
            {
                if (handle.IsValid)
                {
                    handle.Release(true);
                }
            }
            Handles.Clear();
        }
    }
}
