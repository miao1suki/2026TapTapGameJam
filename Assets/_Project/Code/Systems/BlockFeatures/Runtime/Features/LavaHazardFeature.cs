using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    [BlockFeature(
        DisplayName = "岩浆",
        DefaultColorId = "red",
        Category = BlockFeatureCategory.Hazard,
        Interactions =
            BlockFeatureInteraction.PlayerContact |
            BlockFeatureInteraction.AppliedColor |
            BlockFeatureInteraction.RoomReset,
        Writes = new[] { BlockChannel.Heat })]
    public sealed class LavaHazardFeature : PlayerContactFeature,
        IColorReactionReceiver
    {
        [BlockParameter(
            Label = "蓝交互替换预制体",
            Group = "蓝交互",
            Order = 0,
            Tooltip = "蓝色物体作用于岩浆后替换成的 GameObject。")]
        [SerializeField] private GameObject blueReplacementPrefab;

        public GameObject BlueReplacementPrefab =>
            blueReplacementPrefab;

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(health.MaxHealth);
            }
        }

        public bool CanReact(GameObject other)
        {
            if (!CanRunFeature ||
                blueReplacementPrefab == null ||
                other == null)
            {
                return false;
            }

            ColorObject color =
                other.GetComponentInParent<ColorObject>();
            return color != null &&
                   string.Equals(
                       color.BaseColorTypeId,
                       "blue",
                       System.StringComparison.OrdinalIgnoreCase);
        }

        public void React(GameObject other)
        {
            if (!CanReact(other))
            {
                return;
            }

            ReplaceForBlueReaction();
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (!string.Equals(
                    colorId,
                    "blue",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return ReplaceForBlueReaction();
        }

        private bool ReplaceForBlueReaction()
        {
            if (blueReplacementPrefab == null)
            {
                return false;
            }

            Instantiate(
                blueReplacementPrefab,
                transform.position,
                transform.rotation);
            Destroy(gameObject);
            return true;
        }

        public override void CollectDebugValues(
            List<BlockDebugValue> values)
        {
            values.Add(new BlockDebugValue(
                "接触玩家",
                PlayerCount));
            values.Add(new BlockDebugValue(
                "蓝交互替换",
                blueReplacementPrefab != null
                    ? blueReplacementPrefab.name
                    : "未设置"));
        }
    }
}
