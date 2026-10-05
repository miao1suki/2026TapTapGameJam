using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>Gameplay color authority. Material transitions belong to HSVColorFadeManager.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ColorWorldManager : MonoBehaviour
    {
        private static ColorWorldManager instance;
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<ColorBlock> blocks = new HashSet<ColorBlock>();
        private readonly List<GameObject> waterVisuals = new List<GameObject>();
        private GameObject waterVisualRoot;
        private bool waterDirty = true;
        private float waterReveal = -1f;
        private ColorCatalog catalog;
        private bool hasRecolorAbility;

        public static ColorWorldManager Instance
        {
            get
            {
                if (instance != null) return instance;
                var existing = FindFirstObjectByType<ColorWorldManager>();
                if (existing != null) return existing;
                var root = new GameObject("Color World Manager");
                return root.AddComponent<ColorWorldManager>();
            }
        }

        public ColorCatalog Catalog => catalog;
        public static ColorWorldManager Existing => instance;
        public bool HasRecolorAbility => hasRecolorAbility;
        public event Action<ColorTypeDefinition> ColorUnlocked;
        public event Action<ColorBlock, string, string> BlockColorChanged;
        public event Action RecolorAbilityGranted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            catalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
            if (catalog == null)
                Debug.LogError("[ColorBlocks] 缺少 Resources/ColorBlocks/ColorCatalog，请用颜色工作台初始化。");
            HSVColorFadeManager.EnsureCreated();
        }

        private void LateUpdate()
        {
            if (waterDirty)
            {
                waterDirty = false;
                RebuildWaterVisuals();
                waterReveal = -1f;
            }
            float nextReveal = HSVColorFadeManager.Instance.GetSaturation("blue");
            if (!Mathf.Approximately(waterReveal, nextReveal))
            {
                waterReveal = nextReveal;
                ApplyWaterReveal(nextReveal);
            }
        }

        private void RebuildWaterVisuals()
        {
            if (waterVisualRoot != null) Destroy(waterVisualRoot);
            waterVisualRoot = null;
            waterVisuals.Clear();
            if (catalog == null || catalog.BlueWaterPrefab == null) return;

            var zones = new List<Bounds>();
            foreach (ColorBlock block in blocks)
            {
                if (block == null || !block.IsActiveWater) continue;
                BoxCollider collider = block.GetComponent<BoxCollider>();
                if (collider != null) zones.Add(collider.bounds);
            }
            bool merged;
            do
            {
                merged = false;
                for (int i = 0; i < zones.Count && !merged; i++)
                for (int j = i + 1; j < zones.Count; j++)
                {
                    Bounds a = zones[i], b = zones[j];
                    bool sameDepth = Close(a.min.z, b.min.z) && Close(a.max.z, b.max.z);
                    bool sameHeight = Close(a.min.y, b.min.y) && Close(a.max.y, b.max.y);
                    bool sameWidth = Close(a.min.x, b.min.x) && Close(a.max.x, b.max.x);
                    bool horizontal = sameDepth && sameHeight &&
                        a.max.x >= b.min.x - .002f && b.max.x >= a.min.x - .002f;
                    bool vertical = sameDepth && sameWidth &&
                        a.max.y >= b.min.y - .002f && b.max.y >= a.min.y - .002f;
                    if (!horizontal && !vertical) continue;
                    a.Encapsulate(b);
                    zones[i] = a;
                    zones.RemoveAt(j);
                    merged = true;
                    break;
                }
            } while (merged);

            if (zones.Count == 0) return;
            waterVisualRoot = new GameObject("Water visuals");
            waterVisualRoot.transform.SetParent(transform, false);
            waterVisualRoot.SetActive(false);
            foreach (Bounds zone in zones)
            {
                GameObject visual = Instantiate(catalog.BlueWaterPrefab,
                    new Vector3(zone.min.x, zone.max.y + .01f, zone.min.z - .02f),
                    Quaternion.identity, waterVisualRoot.transform);
                visual.name = "Water";
                InteractiveWater.InteractiveWater water = visual.GetComponent<InteractiveWater.InteractiveWater>();
                if (water != null) water.ConfigureBlockVolume(
                    new Vector2(zone.size.x, zone.size.z + .04f), zone.size.y + .01f);
                waterVisuals.Add(visual);
            }
            ApplyWaterReveal(HSVColorFadeManager.Instance.GetSaturation("blue"));
        }

        private void ApplyWaterReveal(float saturation)
        {
            if (waterVisualRoot == null) return;
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(saturation));
            bool visible = reveal > .001f;
            if (waterVisualRoot.activeSelf != visible) waterVisualRoot.SetActive(visible);
            foreach (GameObject visual in waterVisuals)
            {
                if (visual == null) continue;
                InteractiveWater.InteractiveWater water =
                    visual.GetComponent<InteractiveWater.InteractiveWater>();
                if (water != null) water.SetReveal(reveal);
            }
        }

        private static bool Close(float a, float b) => Mathf.Abs(a - b) < .002f;

        public bool IsUnlocked(string typeId) => !string.IsNullOrEmpty(typeId) && unlocked.Contains(typeId);

        public void GrantRecolorAbility()
        {
            if (hasRecolorAbility) return;
            hasRecolorAbility = true;
            RecolorAbilityGranted?.Invoke();
            EventMgr.RaiseRecolorAbilityGranted();
        }

        public bool Unlock(string typeId)
        {
            var definition = catalog != null ? catalog.Find(typeId) : null;
            if (definition == null || !unlocked.Add(typeId)) return false;
            foreach (var block in new List<ColorBlock>(blocks))
                if (block != null) block.OnBaseColorUnlocked(typeId);
            ColorUnlocked?.Invoke(definition);
            EventMgr.RaiseColorUnlocked(definition);
            if (!string.IsNullOrEmpty(definition.unlockEventId))
                EventMgr.RaiseColorTypeEvent(definition.unlockEventId, typeId);
            HSVColorFadeManager.Instance.SetColorFaded(typeId, false);
            ColorInteractionRunner.Run(definition, ColorGraphNodeKind.ColorUnlocked);
            return true;
        }

        public bool TryRecolor(ColorBlock block, string targetTypeId)
        {
            if (!hasRecolorAbility || block == null || !blocks.Contains(block) || !IsUnlocked(targetTypeId)) return false;
            if (catalog == null || catalog.Find(targetTypeId) == null) return false;
            return block.ApplyCurrentColor(targetTypeId);
        }

        public void ResetProgress()
        {
            unlocked.Clear();
            hasRecolorAbility = false;
            foreach (var block in new List<ColorBlock>(blocks))
                if (block != null) block.ResetToNeutral();
            HSVColorFadeManager.Instance.ResetAll();
        }

        internal void Register(ColorBlock block)
        {
            blocks.Add(block);
            waterDirty = true;
            if (IsUnlocked(block.BaseColorTypeId)) block.OnBaseColorUnlocked(block.BaseColorTypeId);
            else block.ResetToNeutral();
        }

        internal void Unregister(ColorBlock block)
        {
            blocks.Remove(block);
            waterDirty = true;
        }

        internal void NotifyBlockChanged(ColorBlock block, string previous, string current)
        {
            if (previous == "blue" || current == "blue") waterDirty = true;
            BlockColorChanged?.Invoke(block, previous, current);
            EventMgr.RaiseColorBlockChanged(block, previous, current);
        }
    }
}

public partial class EventMgr
{
    public static event Action<Project.ColorBlocks.ColorTypeDefinition> OnColorUnlocked;
    public static event Action<Project.ColorBlocks.ColorBlock, string, string> OnColorBlockChanged;
    public static event Action<string, string> OnColorTypeEvent;
    public static event Action OnRecolorAbilityGranted;

    public static void RaiseColorUnlocked(Project.ColorBlocks.ColorTypeDefinition color) =>
        OnColorUnlocked?.Invoke(color);

    public static void RaiseColorBlockChanged(Project.ColorBlocks.ColorBlock block, string previous, string current) =>
        OnColorBlockChanged?.Invoke(block, previous, current);

    public static void RaiseColorTypeEvent(string eventId, string typeId) =>
        OnColorTypeEvent?.Invoke(eventId, typeId);

    public static void RaiseRecolorAbilityGranted() => OnRecolorAbilityGranted?.Invoke();
}
