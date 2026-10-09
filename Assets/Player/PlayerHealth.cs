using System;
using UnityEngine;

namespace Project.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float maxHealth = 5f;
        [SerializeField, Min(0f)] private float startingHealth = 5f;
        private float currentHealth;
        private PlayerController controller;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;
        public bool IsDead => currentHealth <= 0f;
        public event Action<float, float> Changed;
        public event Action Died;
        public event Action Revived;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            currentHealth = Mathf.Clamp(startingHealth, 0f, maxHealth);
            if (currentHealth <= 0f)
            {
                currentHealth = maxHealth;
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || IsDead) return;
            SetHealth(currentHealth + amount);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f ||
                IsDead ||
                (controller != null && controller.IsInvulnerable))
            {
                return;
            }

            SetHealth(currentHealth - amount);
        }

        public void ResetHealth()
        {
            SetHealth(maxHealth);
        }

        private void SetHealth(float value)
        {
            float next = Mathf.Clamp(value, 0f, maxHealth);
            if (Mathf.Approximately(next, currentHealth)) return;
            bool wasDead = IsDead;
            currentHealth = next;
            Changed?.Invoke(currentHealth, maxHealth);
            if (!wasDead && IsDead)
            {
                Died?.Invoke();
            }
            else if (wasDead && !IsDead)
            {
                Revived?.Invoke();
            }
        }
    }
}
