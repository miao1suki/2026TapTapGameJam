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
        [BlockParameter(Label = "流动岩浆视觉", Group = "表现接口", Order = 0, Tooltip = "仅替换岩浆外观，不改变危险碰撞。留空使用共享TA配置。")]
        [SerializeField] private GameObject flowArtworkPrefab;
        [BlockParameter(Label = "冷却蒸汽", Group = "蓝交互", Order = 1, Tooltip = "冷却块暴露顶面的一次纯视觉雾团，不产生推力。")]
        [SerializeField] private GameObject blueReactionSteamPrefab;
        private Project.FlowMagma.FlowMagmaVisual flowArtwork;
        private readonly List<Vector3> coolingOrigins = new List<Vector3>();
        public bool HasFlowArtwork => flowArtwork != null && flowArtwork.gameObject.activeInHierarchy;

        public GameObject BlueReplacementPrefab =>
            blueReplacementPrefab;

        private readonly LavaContactOcclusion contactOcclusion = new LavaContactOcclusion();

        protected override void OnAttach()
        {
            base.OnAttach();
            contactOcclusion.Bind(this);
            CreateFlowArtwork();
        }

        protected override void OnDetach()
        {
            base.OnDetach();
            if (flowArtwork != null) { flowArtwork.gameObject.SetActive(false); Destroy(flowArtwork.gameObject); }
            flowArtwork = null;
        }

        protected override void OnResetForRoom()
        {
            DisconnectAllPlayers();
            if (flowArtwork != null) flowArtwork.Restart();
        }

        private void CreateFlowArtwork()
        {
            if (!Application.isPlaying || flowArtwork != null) return;
            GameObject prefab = flowArtworkPrefab != null ? flowArtworkPrefab : Project.FlowMagma.ReactionArtworkSettings.Load()?.magmaPrefab;
            if (prefab == null) return;
            var shape = GetComponent<Project.LevelEditor.LevelEditorMergedShape>();
            BoxCollider box = GetComponent<BoxCollider>();
            Bounds local = shape != null ? shape.LocalBounds : box != null ? new Bounds(box.center, box.size) : new Bounds(Vector3.zero, Vector3.one);
            Vector3 front = transform.TransformPoint(new Vector3(local.center.x, local.center.y, local.min.z));
            // This is an underlay in the same cell, not a front-facing decal on its
            // contents. Put it behind opaque walls and the block's front artwork.
            front.z += .02f * GridCellWorldSize;
            GameObject instance = Instantiate(prefab, front, Quaternion.identity, transform);
            Vector3 scale = transform.lossyScale;
            instance.transform.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            flowArtwork = instance.GetComponent<Project.FlowMagma.FlowMagmaVisual>();
            if (flowArtwork == null) { Destroy(instance); return; }
            int layer = gameObject.layer;
            foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = layer;
            var rectangles = new List<Rect>();
            void AddBounds(Bounds bounds)
            {
                Vector3 a = instance.transform.InverseTransformPoint(transform.TransformPoint(bounds.min));
                Vector3 b = instance.transform.InverseTransformPoint(transform.TransformPoint(bounds.max));
                rectangles.Add(Rect.MinMaxRect(Mathf.Min(a.x,b.x), Mathf.Min(a.y,b.y), Mathf.Max(a.x,b.x), Mathf.Max(a.y,b.y)));
            }
            if (shape != null) foreach (RectInt rectangle in shape.Rectangles) AddBounds(shape.RectBounds(rectangle));
            else AddBounds(local);
            flowArtwork.ConfigureCoverage(rectangles, GridCellWorldSize, true);
        }

        private void SpawnCoolingSteam(GameObject replacement)
        {
            GameObject prefab = blueReactionSteamPrefab != null ? blueReactionSteamPrefab : Project.FlowMagma.ReactionArtworkSettings.Load()?.coolingSteamPrefab;
            if (prefab == null || replacement == null) return;
            coolingOrigins.Clear();
            if (flowArtwork != null) flowArtwork.CollectSurfaceOrigins(coolingOrigins, 8);
            if (coolingOrigins.Count == 0)
            {
                var box = GetComponent<BoxCollider>();
                Bounds bounds = box != null ? new Bounds(box.center, box.size) : new Bounds(Vector3.zero, Vector3.one);
                Vector3 point = transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.min.z));
                coolingOrigins.Add(point);
            }
            var staging = new GameObject("Cooling Steam Staging"); staging.SetActive(false);
            foreach (Vector3 point in coolingOrigins)
            {
                GameObject effect = Instantiate(prefab, point + Vector3.up * GridCellWorldSize * .02f, Quaternion.identity, staging.transform);
                var steam = effect.GetComponent<Project.Steam.CoolingSteamEffect>();
                if (steam != null) steam.Configure(GridCellWorldSize);
                effect.transform.SetParent(replacement.transform, true);
            }
            Destroy(staging);
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
                player.EnterDeath(
                    PlayerDeathCause.Lava,
                    this);
                return;
            }
            if (!health.IsDead)
            {
                AudioCuePlayer.Play(AudioCueIds.LavaCool);
                health.TakeDamage(
                    health.MaxHealth,
                    PlayerDeathCause.Lava,
                    this);
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
            SpawnCoolingSteam(replacement);
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
