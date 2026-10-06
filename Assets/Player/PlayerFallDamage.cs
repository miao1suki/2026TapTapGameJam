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
    [RequireComponent(typeof(PlayerController), typeof(PlayerHealth))]
    public sealed class PlayerFallDamage : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float safeFallDistance = 3f;
        [SerializeField, Min(0f)] private float damagePerMeter = 1f;
        [SerializeField] private AnimationCurve damageCurve =
            null;

        private PlayerController player;
        private PlayerHealth health;
        private bool wasGrounded;
        private float highestAirPosition;
        private float fallDistance;
        private int immunityDepth;

        public float LastFallDistance { get; private set; }
        public bool IsImmune => immunityDepth > 0 ||
                                 (player != null && player.IsFallDamageImmune);
        public event Action<PlayerFallDamage, float> Landed;

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            health = GetComponent<PlayerHealth>();
            wasGrounded = player.IsGrounded;
            highestAirPosition = transform.position.y;
        }

        private void FixedUpdate()
        {
            if (player == null || health == null) return;
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
                if (!IsImmune && LastFallDistance > safeFallDistance)
                {
                    float excess = LastFallDistance - safeFallDistance;
                    float scale = damageCurve != null && damageCurve.length > 0
                        ? Mathf.Max(0f, damageCurve.Evaluate(
                            Mathf.InverseLerp(safeFallDistance, safeFallDistance + 8f, LastFallDistance)))
                        : excess;
                    health.TakeDamage(scale * damagePerMeter);
                }
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

        public void SetFallDamageImmune(bool value)
        {
            if (value) immunityDepth++;
            else immunityDepth = Mathf.Max(0, immunityDepth - 1);
        }

        public void ApplyBounce(float verticalSpeed)
        {
            player?.ApplyVerticalBounce(verticalSpeed);
        }
    }
}
