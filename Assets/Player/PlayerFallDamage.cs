using System;
using UnityEngine;

namespace Project.Player
{
    public static class NextJumpBounceService
    {
        private static float pendingMultiplier = 1f;

        public static void Arm(float multiplier)
        {
            pendingMultiplier = Mathf.Max(pendingMultiplier, multiplier);
        }

        public static float ConsumeMultiplier()
        {
            float result = Mathf.Max(1f, pendingMultiplier);
            pendingMultiplier = 1f;
            return result;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerFallDamage : MonoBehaviour
    {
        private PlayerController player;
        private bool wasGrounded;
        private float highestAirPosition;
        private float fallDistance;

        public float LastFallDistance { get; private set; }
        public float LastLandingTime { get; private set; } =
            float.NegativeInfinity;
        public float CurrentFallDistance =>
            Mathf.Max(0f, fallDistance);
        public event Action<PlayerFallDamage, float> Landed;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            wasGrounded = player.IsGrounded;
            highestAirPosition = transform.position.y;
        }

        private void FixedUpdate()
        {
            if (player == null) return;
            bool grounded = player.IsGrounded;
            float y = transform.position.y;
            if (!grounded)
            {
                highestAirPosition = Mathf.Max(highestAirPosition, y);
                if (player.VerticalVelocity < -0.01f)
                {
                    fallDistance = Mathf.Max(fallDistance, highestAirPosition - y);
                }
            }
            else if (!wasGrounded)
            {
                LastFallDistance = Mathf.Max(0f, fallDistance);
                LastLandingTime = Time.time;
                Landed?.Invoke(this, LastFallDistance);
                fallDistance = 0f;
                highestAirPosition = y;
            }
            else
            {
                highestAirPosition = y;
                fallDistance = 0f;
            }
            wasGrounded = grounded;
        }

        public void ApplyBounce(float verticalSpeed)
        {
            player?.ApplyVerticalBounce(verticalSpeed);
        }
    }
}
