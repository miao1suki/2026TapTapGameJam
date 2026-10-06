using System;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.Interactions;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>
    /// 颜色状态与视觉支持服务。它只维护解锁、颜色属性和水体视觉；交互关系由
    /// Project.Interactions.InteractionManager 负责。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ColorRuntimeService : MonoBehaviour
    {
        private static ColorRuntimeService instance;
        private readonly HashSet<string> unlocked = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<ColorBlock> blocks = new HashSet<ColorBlock>();
        private readonly List<GameObject> waterVisuals = new List<GameObject>();
        private GameObject waterVisualRoot;
        private bool waterDirty = true;
        private float waterReveal = -1f;
        private ColorCatalog catalog;

        public static ColorRuntimeService Instance
        {
            get
            {
                if (instance != null) return instance;
                var existing =
                    ProjectDiscovery.FindFirst<ColorRuntimeService>();
                if (existing != null) return existing;
                var root = new GameObject("Color Runtime Service");
                return root.AddComponent<ColorRuntimeService>();
            }
        }

        public ColorCatalog Catalog => catalog;
        public static ColorRuntimeService Existing => instance;
        public event Action<ColorTypeDefinition> ColorUnlocked;
        public event Action<ColorBlock, string, string> BlockColorChanged;

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
                Debug.LogError("[ColorBlocks] 缺少 Resources/ColorBlocks/ColorCatalog，请先初始化颜色目录资源。");
            HSVColorFadeManager fadeManager = HSVColorFadeManager.EnsureCreated();
            InitializeFadeStates(fadeManager);
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
                IBlockWaterSource waterSource =
                    block.GetComponent<IBlockWaterSource>();
                if (waterSource != null &&
                    waterSource.UsesOwnedVisual)
                {
                    continue;
                }

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

        public bool Unlock(string typeId)
        {
            var definition = catalog != null ? catalog.Find(typeId) : null;
            if (definition == null)
            {
                Debug.LogError(
                    $"[ColorBlocks] Unlock 失败：颜色目录中找不到 {typeId}。",
                    this);
                return false;
            }

            if (!unlocked.Add(typeId))
            {
                Debug.Log(
                    $"[ColorBlocks] Unlock 跳过：{typeId} 已经解锁。",
                    this);
                return false;
            }

            Debug.Log(
                $"[ColorBlocks] Unlock 成功：{typeId}，开始恢复同色方块与水体。",
                this);
            foreach (var block in new List<ColorBlock>(blocks))
                if (block != null) block.OnBaseColorUnlocked(typeId);
            ColorUnlocked?.Invoke(definition);
            EventMgr.RaiseColorUnlocked(definition);
            if (!string.IsNullOrEmpty(definition.unlockEventId))
                EventMgr.RaiseColorTypeEvent(definition.unlockEventId, typeId);

            // 交互图可以继续编排恢复时机和表现，但颜色解锁必须先有一个
            // 稳定的基础恢复状态。这样即使某个旧定义的 Restore 节点没有
            // 填写颜色值，钥匙也不会只播放镜头而留下褪色的材质/水体。
            HSVColorFadeManager.Instance.SetColorFaded(typeId, false);
            foreach (ColorBlock block in new List<ColorBlock>(blocks))
            {
                if (block == null || block.BaseColorTypeId != typeId)
                {
                    continue;
                }

                InteractionManager.Trigger(
                    block.gameObject,
                    InteractionNodeKind.Manual);
            }
            return true;
        }

        /// <summary>
        /// 只修改当前运行实例，不写入场景、目录或存档。
        /// </summary>
        public bool SetUnlockedForCurrentSession(
            string typeId,
            bool value)
        {
            return value
                ? Unlock(typeId)
                : Lock(typeId);
        }

        /// <summary>
        /// 只修改当前运行实例，不写入场景、目录或存档。
        /// </summary>
        public void SetAllUnlockedForCurrentSession(bool value)
        {
            if (catalog == null)
            {
                return;
            }

            for (int index = 0;
                 index < catalog.Colors.Count;
                 index++)
            {
                ColorTypeDefinition definition =
                    catalog.Colors[index];
                if (definition != null &&
                    !string.IsNullOrWhiteSpace(definition.id))
                {
                    SetUnlockedForCurrentSession(
                        definition.id,
                        value);
                }
            }
        }

        private bool Lock(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) ||
                !unlocked.Remove(typeId))
            {
                return false;
            }

            foreach (ColorBlock block in new List<ColorBlock>(blocks))
            {
                if (block != null &&
                    block.BaseColorTypeId == typeId)
                {
                    block.ResetToNeutral();
                }
            }

            HSVColorFadeManager.Instance.SetColorFaded(
                typeId,
                true);
            return true;
        }

        public void ResetProgress()
        {
            unlocked.Clear();
            foreach (var block in new List<ColorBlock>(blocks))
                if (block != null) block.ResetToNeutral();
            HSVColorFadeManager.Instance.ResetAll();
        }

        internal void Register(ColorBlock block)
        {
            blocks.Add(block);
            waterDirty = true;
            bool isUnlocked = IsUnlocked(block.BaseColorTypeId);
            HSVColorFadeManager.Instance.SetColorFaded(
                block.BaseColorTypeId,
                !isUnlocked,
                0f);
            if (isUnlocked) block.OnBaseColorUnlocked(block.BaseColorTypeId);
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

        private void InitializeFadeStates(HSVColorFadeManager fadeManager)
        {
            if (fadeManager == null || catalog == null)
            {
                return;
            }

            for (int index = 0; index < catalog.Colors.Count; index++)
            {
                ColorTypeDefinition definition = catalog.Colors[index];
                if (definition != null && !string.IsNullOrWhiteSpace(definition.id))
                {
                    fadeManager.SetColorFaded(definition.id, true, 0f);
                }
            }
        }
    }
}

public partial class EventMgr
{
    public static event Action<Project.ColorBlocks.ColorTypeDefinition> OnColorUnlocked;
    public static event Action<Project.ColorBlocks.ColorBlock, string, string> OnColorBlockChanged;
    public static event Action<string, string> OnColorTypeEvent;

    public static void RaiseColorUnlocked(Project.ColorBlocks.ColorTypeDefinition color) =>
        OnColorUnlocked?.Invoke(color);

    public static void RaiseColorBlockChanged(Project.ColorBlocks.ColorBlock block, string previous, string current) =>
        OnColorBlockChanged?.Invoke(block, previous, current);

    public static void RaiseColorTypeEvent(string eventId, string typeId) =>
        OnColorTypeEvent?.Invoke(eventId, typeId);

}
