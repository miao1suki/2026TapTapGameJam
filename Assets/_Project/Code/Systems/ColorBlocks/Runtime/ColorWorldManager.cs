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
            if (IsUnlocked(block.BaseColorTypeId)) block.OnBaseColorUnlocked(block.BaseColorTypeId);
            else block.ResetToNeutral();
        }

        internal void Unregister(ColorBlock block) => blocks.Remove(block);

        internal void NotifyBlockChanged(ColorBlock block, string previous, string current)
        {
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
