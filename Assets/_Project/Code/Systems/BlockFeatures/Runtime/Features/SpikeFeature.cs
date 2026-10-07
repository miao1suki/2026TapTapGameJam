using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "尖刺",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Hazard,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Heat })]
    public sealed class SpikeFeature : PlayerContactFeature
    {
        [BlockParameter(
            Label = "单次伤害",
            Group = "尖刺设置",
            Order = 0)]
        [SerializeField, Min(0f)] private float damage = 2f;

        [BlockParameter(
            Label = "伤害间隔",
            Group = "尖刺设置",
            Order = 1)]
        [SerializeField, Min(.05f)] private float damageInterval = .5f;

        private readonly Dictionary<int, float> nextDamageTimes =
            new Dictionary<int, float>();

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            ApplyDamage(player);
        }

        protected override void OnPlayerStayed(
            PlayerController player)
        {
            int id = player.GetInstanceID();
            if (nextDamageTimes.TryGetValue(
                    id,
                    out float nextTime) &&
                Time.time < nextTime)
            {
                return;
            }

            ApplyDamage(player);
        }

        protected override void OnPlayerExited(
            PlayerController player)
        {
            nextDamageTimes.Remove(player.GetInstanceID());
        }

        protected override void OnResetForRoom()
        {
            nextDamageTimes.Clear();
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue(
                "单次伤害",
                damage));
        }

        private void ApplyDamage(PlayerController player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null || health.IsDead)
            {
                return;
            }

            nextDamageTimes[player.GetInstanceID()] =
                Time.time + damageInterval;
            health.TakeDamage(damage);
        }
    }
}
