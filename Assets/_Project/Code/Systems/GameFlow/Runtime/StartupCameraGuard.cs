using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class StartupCameraGuard : MonoBehaviour
    {
        private Camera guardedCamera;

        public static StartupCameraGuard CreateIfNeeded(Transform parent)
        {
            if (Camera.allCamerasCount > 0)
            {
                return null;
            }

            GameObject guardObject = new GameObject("__StartupCameraGuard");
            if (parent != null)
            {
                guardObject.transform.SetParent(parent, false);
            }

            Camera camera = guardObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.05f, 0.075f, 1f);
            camera.cullingMask = 0;
            camera.depth = -1000f;
            return guardObject.AddComponent<StartupCameraGuard>();
        }

        private void Awake()
        {
            guardedCamera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            Camera[] cameras = Camera.allCameras;
            for (int index = 0; index < cameras.Length; index++)
            {
                Camera candidate = cameras[index];
                if (candidate != null && candidate != guardedCamera &&
                    candidate.isActiveAndEnabled)
                {
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }
}
