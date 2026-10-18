using System.Collections.Generic;
using InteractiveWater;
using Project.Audio;
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
    public abstract class WaterVisualFeature : PlayerContactFeature, IColorApplicationHitTarget
    {
        [BlockParameter(
            Label = "旧版后备预制体",
            Group = "旧版水体后备",
            Order = 0,
            Tooltip = "仅在像素水体配置缺失时使用；留空时取颜色目录的后备资源。")]
        [SerializeField] private GameObject waterPrefab;

        [BlockParameter(
            Label = "水体本地偏移",
            Group = "旧版水体后备",
            Order = 1)]
        [SerializeField] private Vector3 waterLocalOffset =
            Vector3.zero;

        [BlockParameter(
            Label = "水体缩放",
            Group = "旧版水体后备",
            Order = 2)]
        [SerializeField] private Vector3 waterScale =
            Vector3.one;

        [BlockParameter(Label = "显示流向气泡", Group = "流向气泡", Order = 0)]
        [SerializeField] private bool showFlowBubbles = true;
        [BlockParameter(Label = "气泡密度（排/格）", Group = "流向气泡", Order = 1)]
        [SerializeField, Range(.2f, 3f)] private float flowBubbleDensity = 1.1f;
        [BlockParameter(Label = "气泡速度（格/秒）", Group = "流向气泡", Order = 2)]
        [SerializeField, Range(.1f, 5f)] private float flowBubbleSpeed = 1.4f;
        public bool ShowFlowBubbles => showFlowBubbles;
        public float FlowBubbleDensity => flowBubbleDensity;
        public float FlowBubbleSpeed => flowBubbleSpeed;

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
        private GameplayWaterPresentation pixelWaterVisual;
        private readonly Collider[] steamOverlapBuffer =
            new Collider[32];
        private readonly HashSet<int> steamPlayerIds =
            new HashSet<int>();
        private GameObject steamVfxInstance;
        private float steamRemainingSeconds;
        private bool steamContinuous;
        private Vector3 steamOrigin;
        private Vector2 steamHalfWidthDepth;
        public Vector3 SteamOrigin => steamOrigin;

        protected bool HasWaterVisual => waterVisual != null || pixelWaterVisual != null;
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
            ColorAppearanceManager.Instance.RegisterWater(waterVisual);
        }

        protected override void OnDetach()
        {
            StopSteamReaction();
            ColorAppearanceManager.Existing?.UnregisterWater(waterVisual);
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

            return TriggerSteamReaction(actor != null ? actor.transform.position : transform.position);
        }

        public bool ApplyColorAt(string colorId, GameObject actor, Vector3 hitWorldPoint)
        {
            return CanApplyColor(colorId, actor) &&
                string.Equals(colorId, "red", System.StringComparison.OrdinalIgnoreCase) &&
                TriggerSteamReaction(hitWorldPoint);
        }

        protected override void OnResetForRoom()
        {
            StopSteamReaction();
            // Authored water returns to the same volume and keeps the same color
            // unlock state. Only transient steam/contact gameplay resets; retain
            // the static water meshes and bubbles. A real disable/detach still
            // owns full cleanup through OnDetach.
            DisconnectAllPlayers();
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
            WaterPresentationSettings settings = WaterPresentationSettings.Load();
            if (settings != null && settings.IsValid)
            {
                spawnedWaterVisual = new GameObject("Pixel Water Visual");
                spawnedWaterVisual.transform.SetParent(transform, false);
                pixelWaterVisual = spawnedWaterVisual.AddComponent<GameplayWaterPresentation>();
                pixelWaterVisual.Initialize(this, settings);
                return;
            }
            waterVisual =
                GetComponentInChildren<InteractiveWater.InteractiveWater>(
                    true);
            if (waterVisual != null)
            {
                ConfigureWaterLayer();
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
            ConfigureWaterLayer();
        }

        private void ConfigureWaterLayer()
        {
            ColorTypeDefinition definition = ColorRuntimeService.Instance.Catalog?.Find("blue");
            if (waterVisual != null && definition != null && definition.unityLayer >= 0 && definition.unityLayer < 32)
                waterVisual.SetPresentationLayer(definition.unityLayer);
        }

        private void DestroySpawnedWaterVisual()
        {
            if (spawnedWaterVisual != null)
            {
                // Destroy is deferred. A same-frame reattach must not discover this retired instance.
                spawnedWaterVisual.SetActive(false);
                // Keep the inactive child attached until deferred destruction; changing parents
                // during a hierarchy disable is rejected by Unity.
                Destroy(spawnedWaterVisual);
                spawnedWaterVisual = null;
            }

            waterVisual = null;
            pixelWaterVisual = null;
        }

        private bool TriggerSteamReaction(Vector3 reference)
        {
            if (!WaterSteamPlacement.TryFind(this, reference, GridCellWorldSize,
                    out Vector3 surface, out Vector2 halfWidthDepth)) return false;
            StopSteamReaction();
            steamOrigin = surface;
            steamHalfWidthDepth = halfWidthDepth;
            steamContinuous =
                steamMode == SteamReactionMode.Continuous;
            steamRemainingSeconds = steamContinuous
                ? Mathf.Max(.05f, steamDuration)
                : Mathf.Max(.05f, steamVfxLifetime);
            AudioCuePlayer.Play(AudioCueIds.SteamBurst, .9f);
            if (steamContinuous)
            {
                AudioCuePlayer.PlayLoop(
                    AudioCueIds.SteamLoop,
                    this,
                    .85f);
            }
            SpawnSteamVfx();
            ApplySteamPush();
            return true;
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
                StopSteamAudio();
                DestroySteamVfx();
            }
        }

        private void StopSteamReaction()
        {
            steamRemainingSeconds = 0f;
            steamContinuous = false;
            StopSteamAudio();
            DestroySteamVfx();
        }

        private void StopSteamAudio()
        {
            AudioCuePlayer.StopLoop(this);
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
            steamVfxInstance.transform.position = steamOrigin;
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
            Vector3 center = steamOrigin + Vector3.up * (height * .5f);
            Vector3 halfExtents = new Vector3(
                steamHalfWidthDepth.x,
                height * .5f,
                steamHalfWidthDepth.y);
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
