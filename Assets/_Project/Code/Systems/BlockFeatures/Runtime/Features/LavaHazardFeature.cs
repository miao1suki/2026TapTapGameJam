using System.Collections.Generic;
using Project.ColorBlocks;
using Project.Audio;
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

        private readonly LavaContactOcclusion contactOcclusion = new LavaContactOcclusion();

        protected override void OnAttach()
        {
            base.OnAttach();
            contactOcclusion.Bind(this);
        }

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            TryKillPlayer(player);
        }

        protected override void OnPlayerStayed(
            PlayerController player)
        {
            TryKillPlayer(player);
        }

        private void TryKillPlayer(PlayerController player)
        {
            if (player == null || player.IsDead || player.IsInvulnerable ||
                !contactOcclusion.HasExposedContact(player, GridCellWorldSize))
            {
                return;
            }

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                AudioCuePlayer.Play(AudioCueIds.LavaCool);
                player.EnterDeath();
                return;
            }
            if (health != null && !health.IsDead)
            {
                AudioCuePlayer.Play(AudioCueIds.LavaCool);
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

            // Configure shape before Awake caches the replacement's colliders/renderers.
            var staging = new GameObject("Reaction Staging"); staging.SetActive(false);
            GameObject replacement = Instantiate(
                blueReplacementPrefab,
                transform.position,
                transform.rotation, staging.transform);
            var placed = GetComponent<Project.LevelEditor.LevelEditorPlacedBlock>();
            if (placed != null)
                replacement.transform.localScale = Vector3.Scale(replacement.transform.localScale,
                    new Vector3(placed.SizeCells.x, placed.SizeCells.y, 1f) * placed.CellWorldSize);
            var shape = GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
            if (shape != null)
                replacement.AddComponent<Project.LevelEditor.LevelEditorMergedShape>().Configure(shape.Dimensions, shape.Rectangles);
            replacement.transform.SetParent(null, true); Destroy(staging);
            AudioCuePlayer.Play(AudioCueIds.LavaCool);
            if (!Project.Rooms.RoomResetObject.HideForReaction(gameObject, replacement)) Destroy(gameObject);
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
