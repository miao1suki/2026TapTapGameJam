using System;
using System.Collections.Generic;
using Project.InputAbstraction;
using UnityEngine;

[AddComponentMenu("TimelineKit/DamageableHealth")]
public class DamageableHealth :
    MonoBehaviour,
    IDamageable,
    IAchievementSignalProvider
{
    private static readonly string[] AchievementSignals =
    {
        AchievementSignalIds.DamageTaken,
        AchievementSignalIds.EntityDied
    };

    [Header("生命值")]
    [Tooltip("生命上限，用于 Heal/ResetHp 的封顶")]
    public float maxHp = 100f;
    [SerializeField] private float _hp = 100f;

    public event Action<float, float> onDamaged;
    public event Action onDeath;

    public float Hp => _hp;

    public bool IsDead => _hp <= 0f;

    public IReadOnlyList<string> GetAchievementSignalIds()
    {
        return AchievementSignals;
    }

    private void Awake()
    {
        if (_hp <= 0f)
        {
            _hp = maxHp;
        }
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }
        _hp -= amount;
        if (_hp < 0f)
        {
            _hp = 0f;
        }
        onDamaged?.Invoke(amount, _hp);
        GameplaySignalHub.Emit(
            AchievementSignalIds.DamageTaken,
            gameObject);
        if (_hp <= 0f)
        {
            onDeath?.Invoke();
            GameplaySignalHub.Emit(
                AchievementSignalIds.EntityDied,
                gameObject);
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }
        _hp = Mathf.Min(maxHp, _hp + amount);
        onDamaged?.Invoke(-amount, _hp);
    }

    public void ResetHp()
    {
        _hp = maxHp;
    }

    public void Kill()
    {
        TakeDamage(_hp);
    }
}
