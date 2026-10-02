using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.15f, 0.85f, 1f, 0.9f);
            Vector3 basePosition = transform.position;
            Gizmos.DrawWireSphere(basePosition + Vector3.up * 0.9f, 0.45f);
            Gizmos.DrawLine(basePosition, basePosition + Vector3.up * 1.8f);
            Gizmos.DrawLine(
                basePosition + Vector3.left * 0.45f,
                basePosition + Vector3.right * 0.45f);
            Gizmos.DrawRay(basePosition, transform.forward * 1.2f);
        }
    }
}
