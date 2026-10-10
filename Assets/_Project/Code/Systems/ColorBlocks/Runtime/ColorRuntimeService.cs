using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>
    /// Owns room-scoped color unlock state and fixed color object activation.
    /// Gameplay visuals and reactions belong to object feature components.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ColorRuntimeService : MonoBehaviour
    {
        private static ColorRuntimeService instance;
        private readonly HashSet<string> unlocked =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<IColorObject> colorObjects =
            new HashSet<IColorObject>();
        private ColorCatalog catalog;

        public static ColorRuntimeService Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                ColorRuntimeService existing =
                    ProjectDiscovery.FindFirst<ColorRuntimeService>();
                if (existing != null)
                {
                    return existing;
                }

                GameObject root =
                    new GameObject("Color Runtime Service");
                return root.AddComponent<ColorRuntimeService>();
            }
        }

        public static ColorRuntimeService Existing => instance;
        public ColorCatalog Catalog => catalog;
        public event Action<ColorTypeDefinition> ColorUnlocked;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            catalog = Resources.Load<ColorCatalog>(
                "ColorBlocks/ColorCatalog");
            if (catalog == null)
            {
                Debug.LogError(
                    "[ColorBlocks] 缺少 Resources/ColorBlocks/ColorCatalog。",
                    this);
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
                    ColorAppearanceManager.Instance.Play(
                        definition.id,
                        false,
                        0f);
                }
            }
        }

        public bool IsUnlocked(string typeId)
        {
            return !string.IsNullOrEmpty(typeId) &&
                   unlocked.Contains(typeId);
        }

        public bool Unlock(string typeId)
        {
            ColorTypeDefinition definition =
                catalog != null ? catalog.Find(typeId) : null;
            if (definition == null)
            {
                Debug.LogError(
                    $"[ColorBlocks] Unlock 失败：找不到颜色 {typeId}。",
                    this);
                return false;
            }

            if (!unlocked.Add(typeId))
            {
                return false;
            }

            foreach (IColorObject colorObject in
                     new List<IColorObject>(colorObjects))
            {
                if (colorObject != null &&
                    string.Equals(
                        colorObject.BaseColorTypeId,
                        typeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    colorObject.Activate();
                }
            }

            ColorAppearanceManager.Instance.Play(
                typeId,
                true);
            ColorUnlocked?.Invoke(definition);
            EventMgr.RaiseColorUnlocked(definition);
            if (!string.IsNullOrEmpty(definition.unlockEventId))
            {
                EventMgr.RaiseColorTypeEvent(
                    definition.unlockEventId,
                    typeId);
            }

            return true;
        }

        public bool SetUnlockedForCurrentSession(
            string typeId,
            bool value)
        {
            return value
                ? Unlock(typeId)
                : Lock(typeId);
        }

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

        public void ResetForRoom()
        {
            unlocked.Clear();
            foreach (IColorObject colorObject in
                     new List<IColorObject>(colorObjects))
            {
                colorObject?.Deactivate();
            }

            ColorAppearanceManager.Instance.ResetAll();
            EventMgr.RaiseRoomColorReset();
        }

        public void ResetProgress()
        {
            ResetForRoom();
        }

        internal void Register(IColorObject colorObject)
        {
            if (colorObject == null)
            {
                return;
            }

            colorObjects.Add(colorObject);
            if (IsUnlocked(colorObject.BaseColorTypeId))
            {
                colorObject.Activate();
            }
            else
            {
                colorObject.Deactivate();
            }
        }

        internal void Unregister(IColorObject colorObject)
        {
            if (colorObject != null)
            {
                colorObjects.Remove(colorObject);
            }
        }

        private bool Lock(string typeId)
        {
            if (string.IsNullOrWhiteSpace(typeId) ||
                !unlocked.Remove(typeId))
            {
                return false;
            }

            foreach (IColorObject colorObject in
                     new List<IColorObject>(colorObjects))
            {
                if (colorObject != null &&
                    string.Equals(
                        colorObject.BaseColorTypeId,
                        typeId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    colorObject.Deactivate();
                }
            }

            ColorAppearanceManager.Instance.Play(
                typeId,
                false);
            return true;
        }
    }
}

public partial class EventMgr
{
    public static event Action<Project.ColorBlocks.ColorTypeDefinition>
        OnColorUnlocked;
    public static event Action<string, string> OnColorTypeEvent;
    public static event Action OnRoomColorReset;

    public static void RaiseColorUnlocked(
        Project.ColorBlocks.ColorTypeDefinition color)
    {
        OnColorUnlocked?.Invoke(color);
    }

    public static void RaiseColorTypeEvent(
        string eventId,
        string typeId)
    {
        OnColorTypeEvent?.Invoke(eventId, typeId);
    }

    public static void RaiseRoomColorReset()
    {
        OnRoomColorReset?.Invoke();
    }
}
