using System;
using System.Collections.Generic;
using Project.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Items
{
    public readonly struct CollectibleProgressSnapshot
    {
        public int Collected { get; }
        public int Total { get; }
        public int Remaining => Mathf.Max(0, Total - Collected);
        public float Ratio => Total > 0 ? Collected / (float)Total : 0f;
        public bool IsComplete => Total > 0 && Remaining == 0;
        /// <summary>
        /// Compare integer rewards against a configured decimal ratio, not a
        /// rounded float division. The float-to-decimal conversion intentionally
        /// retains float's seven useful decimal digits: .6f means 60%, rather
        /// than its binary expansion .6000000238418579 requiring a seventh item.
        /// </summary>
        public bool MeetsRatio(float threshold)
        {
            if (Total <= 0 || !float.IsFinite(threshold)) return false;
            decimal ratio = (decimal)Mathf.Clamp01(threshold);
            decimal required = decimal.Ceiling(Total * ratio);
            return Collected >= required;
        }
        public CollectibleProgressSnapshot(int collected, int total)
        {
            Total = Mathf.Max(0, total);
            Collected = Mathf.Clamp(collected, 0, Total);
        }
    }

    /// <summary>
    /// Session progress, not a disk save owner. Authored inactive objects are
    /// catalogued once per scene/revision; physics queries never scan a scene.
    /// Only stable author IDs can be exported for a future save service.
    /// </summary>
    public static class CollectibleProgressService
    {
        private sealed class Entry
        {
            public string Key, StableId, Region;
            public int Amount;
            public bool Collected;
            public CollectiblePickup Source;
        }
        private sealed class Catalog
        {
            public readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
            public readonly Dictionary<int, string> Instances = new Dictionary<int, string>();
            public readonly HashSet<string> CollectedIds = new HashSet<string>(StringComparer.Ordinal);
            public int SceneHandle, RoomRevision = -1;
            public bool Scanned;
        }
        private static readonly Dictionary<string, Catalog> Catalogs = new Dictionary<string, Catalog>(StringComparer.Ordinal);
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Catalogs.Clear();
            Changed = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshSceneCatalog(scene);
        private static string SceneKey(Scene scene) => string.IsNullOrEmpty(scene.path)
            ? "session-scene:" + scene.handle : scene.path.Replace('\\', '/');
        private static Catalog CatalogFor(Scene scene)
        {
            string key = SceneKey(scene);
            if (!Catalogs.TryGetValue(key, out Catalog catalog)) Catalogs[key] = catalog = new Catalog();
            return catalog;
        }

        /// <summary>Explicitly refresh after an author/editor adds or removes world items.</summary>
        public static void RefreshSceneCatalog(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            Catalog catalog = CatalogFor(scene);
            bool newLoad = catalog.SceneHandle != scene.handle;
            if (newLoad)
            {
                catalog.Instances.Clear();
                catalog.Entries.Clear();
            }
            catalog.SceneHandle = scene.handle;
            var manager = RoomLifecycleManager.ForScene(scene);
            catalog.RoomRevision = manager != null ? manager.Revision : -1;
            catalog.Scanned = true;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (CollectiblePickup item in root.GetComponentsInChildren<CollectiblePickup>(true))
                {
                    // Scripted ending props opt out before their inactive
                    // parent is revealed, so they never alter the denominator.
                    if (!item.ExcludedFromProgress && Register(item) && !item.IsCollected)
                        item.RestoreCollectedState();
                }
        }

        private static void EnsureScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            Catalog catalog = CatalogFor(scene);
            var manager = RoomLifecycleManager.ForScene(scene);
            int revision = manager != null ? manager.Revision : -1;
            if (!catalog.Scanned || catalog.SceneHandle != scene.handle || catalog.RoomRevision != revision)
                RefreshSceneCatalog(scene);
        }

        /// <returns>Whether this identity was already collected in this session.</returns>
        public static bool Register(CollectiblePickup item)
        {
            if (item == null || item.ExcludedFromProgress || !item.gameObject.scene.IsValid()) return false;
            Scene scene = item.gameObject.scene;
            Catalog catalog = CatalogFor(scene);
            string stableId = item.TryGetStableInstanceId(out string value) ? value : null;
            int instance = item.GetInstanceID();
            string key = stableId != null ? "author:" + stableId : "session-item:" + instance;
            bool previouslyCollected = false;
            if (catalog.Instances.TryGetValue(instance, out string oldKey) && oldKey != key &&
                catalog.Entries.TryGetValue(oldKey, out Entry previous) && previous.Source == item)
            {
                // Runtime fixtures and authoring can assign planning identity
                // after OnEnable. Migrate instead of counting both identities.
                previouslyCollected = previous.Collected;
                catalog.Entries.Remove(oldKey);
            }
            catalog.Instances[instance] = key;
            bool added = !catalog.Entries.TryGetValue(key, out Entry entry);
            if (added) catalog.Entries[key] = entry = new Entry { Key = key, StableId = stableId };
            var member = item.GetComponent<RoomResetObject>();
            string region = member != null ? member.RegionId : null;
            if (string.IsNullOrEmpty(region))
            {
                var manager = RoomLifecycleManager.ForScene(scene);
                region = manager != null ? manager.FindRegion(member != null ? member.AuthoredWorldPosition : item.transform.position) : null;
            }
            bool changed = added || entry.Amount != item.Amount || entry.Region != region;
            entry.Amount = Mathf.Max(1, item.Amount);
            entry.Region = region;
            entry.Source = item;
            bool collected = item.IsCollected || previouslyCollected || stableId != null && catalog.CollectedIds.Contains(stableId);
            changed |= collected && !entry.Collected;
            entry.Collected |= collected;
            if (entry.Collected && stableId != null) catalog.CollectedIds.Add(stableId);
            if (changed) Changed?.Invoke();
            return entry.Collected;
        }

        internal static void RecordCollected(CollectiblePickup item)
        {
            Register(item);
        }

        internal static void Exclude(CollectiblePickup item)
        {
            if (item == null || !Catalogs.TryGetValue(SceneKey(item.gameObject.scene), out Catalog catalog)) return;
            int instance = item.GetInstanceID();
            if (!catalog.Instances.TryGetValue(instance, out string key)) return;
            catalog.Instances.Remove(instance);
            if (catalog.Entries.TryGetValue(key, out Entry entry) && entry.Source == item)
            {
                catalog.Entries.Remove(key);
                Changed?.Invoke();
            }
        }

        public static CollectibleProgressSnapshot GetSceneProgress(Scene scene) => GetProgress(scene, null, false);
        public static CollectibleProgressSnapshot GetRegionProgress(Scene scene, string regionId) => GetProgress(scene, regionId, true);
        /// <summary>All visited world scenes in this session; Boss scenes inherit Gaming's real progress.</summary>
        public static CollectibleProgressSnapshot GetWorldProgress()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++) EnsureScene(SceneManager.GetSceneAt(index));
            int total = 0, collected = 0;
            foreach (Catalog catalog in Catalogs.Values)
                foreach (Entry entry in catalog.Entries.Values)
                {
                    total += entry.Amount;
                    if (entry.Collected) collected += entry.Amount;
                }
            return new CollectibleProgressSnapshot(collected, total);
        }
        private static CollectibleProgressSnapshot GetProgress(Scene scene, string regionId, bool filterRegion)
        {
            if (!scene.IsValid()) return default;
            EnsureScene(scene);
            int total = 0, collected = 0;
            foreach (Entry entry in CatalogFor(scene).Entries.Values)
            {
                if (filterRegion && !string.Equals(entry.Region, regionId, StringComparison.Ordinal)) continue;
                total += entry.Amount;
                if (entry.Collected) collected += entry.Amount;
            }
            return new CollectibleProgressSnapshot(collected, total);
        }

        /// <summary>Only stable IDs; runtime-only identities are never save keys.</summary>
        public static void CopyCollectedStableIds(Scene scene, ICollection<string> destination)
        {
            if (destination == null || !scene.IsValid()) return;
            EnsureScene(scene);
            foreach (Entry entry in CatalogFor(scene).Entries.Values)
                if (entry.Collected && !string.IsNullOrEmpty(entry.StableId)) destination.Add(entry.StableId);
        }

        /// <summary>Silent, idempotent restore hook; caller owns file I/O and schema.</summary>
        public static void RestoreCollectedStableIds(Scene scene, IEnumerable<string> collectedIds)
        {
            if (collectedIds == null || !scene.IsValid()) return;
            EnsureScene(scene);
            Catalog catalog = CatalogFor(scene);
            bool changed = false;
            foreach (string id in collectedIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                catalog.CollectedIds.Add(id);
                if (catalog.Entries.TryGetValue("author:" + id, out Entry entry) && !entry.Collected)
                {
                    entry.Collected = true;
                    changed = true;
                    if (entry.Source != null) entry.Source.RestoreCollectedState();
                }
            }
            if (changed) Changed?.Invoke();
        }
    }
}
