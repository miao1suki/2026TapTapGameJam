using Project.Player;
using UnityEngine;

namespace Project.WorldItems
{
    [DisallowMultipleComponent]
    public sealed class NeutralSpike : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);
        private void OnCollisionEnter(Collision collision) => Hit(collision.collider);
        private void Hit(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead && !player.IsInvulnerable) player.EnterDeath();
        }
    }
}
