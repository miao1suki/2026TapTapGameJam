using UnityEngine;

namespace Project.BossLogic
{
    [DefaultExecutionOrder(10000)]
    internal sealed class BossAppearCameraGuard :
        MonoBehaviour
    {
        private Camera targetCamera;
        private Vector3 position;
        private Quaternion rotation;
        private float orthographicSize;
        private float fieldOfView;
        private bool active;

        public void Bind(
            Camera camera,
            Vector3 shotPosition,
            Quaternion shotRotation,
            float shotOrthographicSize,
            float shotFieldOfView)
        {
            targetCamera = camera;
            SetShot(
                shotPosition,
                shotRotation,
                shotOrthographicSize,
                shotFieldOfView);
        }

        public void SetShot(
            Vector3 shotPosition,
            Quaternion shotRotation,
            float shotOrthographicSize,
            float shotFieldOfView)
        {
            position = shotPosition;
            rotation = shotRotation;
            orthographicSize =
                Mathf.Max(.01f, shotOrthographicSize);
            fieldOfView =
                Mathf.Clamp(shotFieldOfView, 1f, 179f);
            active = targetCamera != null;
        }

        private void LateUpdate()
        {
            if (!active || targetCamera == null)
            {
                return;
            }

            targetCamera.transform.SetPositionAndRotation(
                position,
                rotation);
            if (targetCamera.orthographic)
            {
                targetCamera.orthographicSize =
                    orthographicSize;
            }
            else
            {
                targetCamera.fieldOfView = fieldOfView;
            }
        }
    }
}
