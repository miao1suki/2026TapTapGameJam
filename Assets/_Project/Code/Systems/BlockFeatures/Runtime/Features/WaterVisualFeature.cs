using System.Collections.Generic;
using InteractiveWater;
using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    public enum SteamReactionMode
    {
        [InspectorName("瞬间一股热气")] Instant = 0,
        [InspectorName("持续蒸汽")] Continuous = 1
    }

    /// <summary>
    /// Shared water visual bridge for all blue water-family props.
    /// </summary>
    public abstract class WaterVisualFeature : PlayerContactFeature
    {
        [BlockParameter(
            Label = "水体预制体",
            Group = "水体表现",
            Order = 0,
            Tooltip = "留空时使用 ColorCatalog 的 blueWaterPrefab。")]
        [SerializeField] private GameObject waterPrefab;

        [BlockParameter(
            Label = "水体本地偏移",
            Group = "水体表现",
            Order = 1)]
        [SerializeField] private Vector3 waterLocalOffset =
            Vector3.zero;

        [BlockParameter(
            Label = "水体缩放",
            Group = "水体表现",
            Order = 2)]
        [SerializeField] private Vector3 waterScale =
            Vector3.one;

        [BlockParameter(
            Label = "蒸汽触发方式",
            Group = "红交互蒸汽",
            Order = 0)]
        [SerializeField] private SteamReactionMode steamMode =
            SteamReactionMode.Instant;

        [BlockParameter(
            Label = "蒸汽推力",
            Group = "红交互蒸汽",
            Order = 1)]
        [SerializeField, Min(0f)] private float steamPushSpeed = 10f;

        [BlockParameter(
            Label = "持续时长（秒）",
            Group = "红交互蒸汽",
            Order = 2,
            VisibleWhenField = nameof(steamMode),
            VisibleWhenValue = (int)SteamReactionMode.Continuous)]
        [SerializeField, Min(.05f)] private float steamDuration = 2f;

        [BlockParameter(
            Label = "作用高度（格）",
            Group = "红交互蒸汽",
            Order = 3)]
        [SerializeField, Min(1)] private int steamHeightBlocks = 3;

        [BlockParameter(
            Label = "热浪特效",
            Group = "红交互蒸汽",
            Order = 4,
            Tooltip = "在水头顶部生成的热浪资源；留空时只执行推力。")]
        [SerializeField] private GameObject steamVfxPrefab;

        [BlockParameter(
            Label = "瞬间特效停留（秒）",
            Group = "红交互蒸汽",
            Order = 5,
            VisibleWhenField = nameof(steamMode),
            VisibleWhenValue = (int)SteamReactionMode.Instant)]
        [SerializeField, Min(.05f)] private float steamVfxLifetime = 1.5f;

        private InteractiveWater.InteractiveWater waterVisual;
        private GameObject spawnedWaterVisual;
        private readonly Collider[] steamOverlapBuffer =
            new Collider[32];
        private readonly HashSet<int> steamPlayerIds =
            new HashSet<int>();
        private GameObject steamVfxInstance;
        private float steamRemainingSeconds;
        private bool steamContinuous;

        protected bool HasWaterVisual => waterVisual != null;
        protected bool IsBlueUnlocked()
        {
            ColorRuntimeService service =
                ColorRuntimeService.Existing;
            return service != null &&
                   service.IsUnlocked("blue");
        }

        protected override void OnAttach()
        {
            base.OnAttach();
            ResolveWaterVisual();
            SetWaterReveal(1f);
        }

        protected override void OnDetach()
        {
            StopSteamReaction();
            SetWaterReveal(0f);
            base.OnDetach();
            DestroySpawnedWaterVisual();
        }

        protected override void OnTick(float deltaTime)
        {
            UpdateSteamReaction(deltaTime);
        }

        protected override bool OnColorApplied(
            string colorId,
            GameObject actor)
        {
            if (!string.Equals(
                    colorId,
                    "red",
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            TriggerSteamReaction();
            return true;
        }

        protected override void OnResetForRoom()
        {
            StopSteamReaction();
            SetWaterReveal(0f);
            DestroySpawnedWaterVisual();
        }

        protected void SetWaterReveal(float value)
        {
            if (waterVisual != null)
            {
                waterVisual.SetReveal(value);
            }
        }

        private void ResolveWaterVisual()
        {
            waterVisual =
                GetComponentInChildren<InteractiveWater.InteractiveWater>(
                    true);
            if (waterVisual != null)
            {
                return;
            }

            GameObject prefab = waterPrefab;
            if (prefab == null)
            {
                ColorCatalog catalog =
                    ColorRuntimeService.Instance.Catalog;
                prefab = catalog != null
                    ? catalog.BlueWaterPrefab
                    : null;
            }

            if (prefab == null)
            {
                return;
            }

            spawnedWaterVisual = Instantiate(
                prefab,
                transform);
            spawnedWaterVisual.name = "Water Visual";
            float cellSize = GridCellWorldSize;
            InteractiveWater.InteractiveWater interactiveWater =
                spawnedWaterVisual.GetComponent<
                    InteractiveWater.InteractiveWater>();
            interactiveWater?.ConfigureBlockVolume(
                new Vector2(cellSize, cellSize),
                cellSize);
            spawnedWaterVisual.transform.localPosition =
                new Vector3(
                    -cellSize * .5f,
                    cellSize * .5f,
                    -cellSize * .5f) +
                waterLocalOffset;
            spawnedWaterVisual.transform.localRotation =
                Quaternion.identity;
            spawnedWaterVisual.transform.localScale =
                waterScale;
            waterVisual =
                interactiveWater;
        }

        private void DestroySpawnedWaterVisual()
        {
            if (spawnedWaterVisual != null)
            {
                Destroy(spawnedWaterVisual);
                spawnedWaterVisual = null;
            }

            waterVisual = null;
        }

        private void TriggerSteamReaction()
        {
            StopSteamReaction();
            SpawnSteamVfx();
            ApplySteamPush();
            steamContinuous =
                steamMode == SteamReactionMode.Continuous;
            steamRemainingSeconds = steamContinuous
                ? Mathf.Max(.05f, steamDuration)
                : Mathf.Max(.05f, steamVfxLifetime);
        }

        private void UpdateSteamReaction(float deltaTime)
        {
            if (steamRemainingSeconds <= 0f)
            {
                return;
            }

            if (steamContinuous)
            {
                ApplySteamPush();
            }

            steamRemainingSeconds = Mathf.Max(
                0f,
                steamRemainingSeconds - deltaTime);
            if (steamRemainingSeconds <= 0f)
            {
                DestroySteamVfx();
            }
        }

        private void StopSteamReaction()
        {
            steamRemainingSeconds = 0f;
            steamContinuous = false;
            DestroySteamVfx();
        }

        private void SpawnSteamVfx()
        {
            if (steamVfxPrefab == null)
            {
                return;
            }

            steamVfxInstance = Instantiate(
                steamVfxPrefab,
                transform.parent);
            steamVfxInstance.name = $"{name} Steam VFX";
            Collider waterCollider = GetComponent<Collider>();
            float topY = waterCollider != null
                ? waterCollider.bounds.max.y
                : transform.position.y + GridCellWorldSize * .5f;
            steamVfxInstance.transform.position = new Vector3(
                transform.position.x,
                topY,
                transform.position.z);
            steamVfxInstance.transform.rotation =
                Quaternion.identity;
        }

        private void DestroySteamVfx()
        {
            if (steamVfxInstance != null)
            {
                Destroy(steamVfxInstance);
                steamVfxInstance = null;
            }
        }

        private void ApplySteamPush()
        {
            float cellSize = GridCellWorldSize;
            float height = Mathf.Max(1, steamHeightBlocks) * cellSize;
            Collider waterCollider = GetComponent<Collider>();
            Bounds waterBounds = waterCollider != null
                ? waterCollider.bounds
                : new Bounds(
                    transform.position,
                    Vector3.one * cellSize);
            Vector3 center = new Vector3(
                waterBounds.center.x,
                waterBounds.max.y + height * .5f,
                waterBounds.center.z);
            Vector3 halfExtents = new Vector3(
                Mathf.Max(.05f, waterBounds.extents.x),
                height * .5f,
                Mathf.Max(.05f, waterBounds.extents.z));
            int count = Physics.OverlapBoxNonAlloc(
                center,
                halfExtents,
                steamOverlapBuffer,
                Quaternion.identity,
                ~0,
                QueryTriggerInteraction.Collide);
            steamPlayerIds.Clear();
            for (int index = 0; index < count; index++)
            {
                Collider collider = steamOverlapBuffer[index];
                PlayerController player = collider != null
                    ? collider.GetComponentInParent<PlayerController>()
                    : null;
                if (player == null ||
                    !player.gameObject.CompareTag(
                        "Player") ||
                    !steamPlayerIds.Add(player.GetInstanceID()))
                {
                    continue;
                }

                player.ApplyVerticalBounce(steamPushSpeed);
            }
        }
    }
}
