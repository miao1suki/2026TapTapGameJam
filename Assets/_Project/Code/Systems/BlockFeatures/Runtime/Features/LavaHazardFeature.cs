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

        [BlockParameter(
            Label = "冷却蒸汽特效",
            Group = "蓝交互",
            Order = 1,
            Tooltip = "蓝色成功作用于岩浆并生成固体块时播放的一次性蒸汽表现。")]
        [SerializeField] private GameObject blueReactionSteamPrefab;

        public GameObject BlueReplacementPrefab =>
            blueReplacementPrefab;

        protected override void OnPlayerEntered(
            PlayerController player)
        {
            if (player != null && !player.IsDead)
                AudioCuePlayer.Play(AudioCueIds.LavaCool);
            KillPlayer(player, false);
        }

        protected override void OnPlayerStayed(
            PlayerController player)
        {
            KillPlayer(player);
        }

        private static void KillPlayer(PlayerController player, bool playSound = true)
        {
            if (player == null || player.IsDead || player.IsInvulnerable)
            {
                return;
            }

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null)
            {
                if (playSound) AudioCuePlayer.Play(AudioCueIds.LavaCool);
                player.EnterDeath();
                return;
            }
            if (health != null && !health.IsDead)
            {
                if (playSound) AudioCuePlayer.Play(AudioCueIds.LavaCool);
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

            GameObject replacement = Instantiate(
                blueReplacementPrefab,
                transform.position,
                transform.rotation);
            SpawnCoolingSteam(replacement);
            AudioCuePlayer.Play(AudioCueIds.LavaCool);
            if (!Project.Rooms.RoomResetObject.HideForReaction(gameObject, replacement)) Destroy(gameObject);
            return true;
        }

        private void SpawnCoolingSteam(GameObject replacement)
        {
            if (replacement == null || blueReactionSteamPrefab == null)
            {
                return;
            }

            Vector3 origin = GetCoolingSteamOrigin(replacement);
            GameObject steam = Instantiate(
                blueReactionSteamPrefab,
                origin,
                Quaternion.identity,
                replacement.transform);
            steam.name = $"{replacement.name} 冷却蒸汽";
        }

        private static Vector3 GetCoolingSteamOrigin(GameObject replacement)
        {
            Collider collider = replacement.GetComponentInChildren<Collider>();
            if (collider != null)
            {
                Bounds bounds = collider.bounds;
                return new Vector3(
                    bounds.center.x,
                    bounds.max.y + 0.02f,
                    bounds.center.z);
            }

            Renderer renderer = replacement.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                return new Vector3(
                    bounds.center.x,
                    bounds.max.y + 0.02f,
                    bounds.center.z);
            }

            return replacement.transform.position + Vector3.up * 0.52f;
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
            values.Add(new BlockDebugValue(
                "冷却蒸汽",
                blueReactionSteamPrefab != null
                    ? blueReactionSteamPrefab.name
                    : "未设置"));
        }
    }
}
