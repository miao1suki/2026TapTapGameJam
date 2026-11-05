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

        private bool active;

        public void Bind(
            Camera camera,
            Vector3 shotPosition,
            Quaternion shotRotation,
            float shotOrthographicSize)
        {
            targetCamera = camera;
            SetShot(
                shotPosition,
                shotRotation,
                shotOrthographicSize);
        }

        public void SetShot(
            Vector3 shotPosition,
            Quaternion shotRotation,
            float shotOrthographicSize)
        {
            position = shotPosition;
            rotation = shotRotation;
            orthographicSize =
                Mathf.Max(.01f, shotOrthographicSize);
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
            targetCamera.orthographic = true;
            targetCamera.transform.rotation = Quaternion.Euler(0f, 0f, rotation.eulerAngles.z);
            targetCamera.orthographicSize = orthographicSize;
        }
    }
}
