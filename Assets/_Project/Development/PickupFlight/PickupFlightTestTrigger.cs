using Project.Pickups;
using Project.Player;
using UnityEngine;

namespace Project.Development.PickupFlight
{
    /// <summary>仅供 Pick 场景测试动画，正式拾取逻辑不依赖此脚本。</summary>
    [RequireComponent(typeof(Collider2D), typeof(PickupFlightAnimator))]
    public sealed class PickupFlightTestTrigger : MonoBehaviour
    {
        private PickupFlightAnimator flightAnimator;

        private void Awake()
        {
            flightAnimator = GetComponent<PickupFlightAnimator>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null) flightAnimator.Play(player.transform);
        }
    }
}
