using System;
using System.Collections.Generic;
using Project.InputAbstraction;
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
        /// retains float's seven useful decimal digits: .1f means 10%, rather
        /// than relying on a rounded binary approximation.
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
        private static int notificationSuppression;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Catalogs.Clear();
            notificationSuppression = 0;
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
            if (changed) NotifyChanged();
            return entry.Collected;
        }

        internal static void RecordCollected(CollectiblePickup item)
        {
            Register(item);
            EmitProgressAchievementSignals(item);
        }

        private static void EmitProgressAchievementSignals(
            CollectiblePickup item)
        {
            if (item == null)
            {
                return;
            }

            CollectibleProgressSnapshot progress =
                GetWorldProgress();
            if (progress.MeetsRatio(.3f))
            {
                GameplaySignalHub.Emit(
                    AchievementSignalIds
                        .CollectibleProgress5,
                    item.gameObject,
                    progress.Collected,
                    progress.Ratio * 100f);
            }
            if (progress.MeetsRatio(.6f))
            {
                GameplaySignalHub.Emit(
                    AchievementSignalIds
                        .CollectibleProgress10,
                    item.gameObject,
                    progress.Collected,
                    progress.Ratio * 100f);
            }
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
                NotifyChanged();
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

        /// <summary>
        /// Debug-only exact mutation. Returns false without changing anything
        /// when the requested amount cannot be represented by authored amounts.
        /// </summary>
        public static bool TrySetWorldCollectedCountForDebug(
            int desired,
            out int applied,
            out string error)
        {
            applied = 0;
            error = string.Empty;
            for (int index = 0;
                 index < SceneManager.sceneCount;
                 index++)
            {
                EnsureScene(SceneManager.GetSceneAt(index));
            }

            var entries = new List<Entry>();
            foreach (Catalog catalog in Catalogs.Values)
            {
                entries.AddRange(
                    catalog.Entries.Values);
            }
            entries.Sort(
                (left, right) =>
                    string.CompareOrdinal(
                        left.Key,
                        right.Key));

            int total = 0;
            for (int index = 0;
                 index < entries.Count;
                 index++)
            {
                total += entries[index].Amount;
            }
            CollectibleProgressSnapshot current =
                GetWorldProgress();
            applied = current.Collected;
            if (desired < 0 || desired > total)
            {
                error =
                    $"目标数量必须在 0 到 {total} 之间。";
                return false;
            }

            bool[,] reachable =
                new bool[entries.Count + 1, total + 1];
            reachable[0, 0] = true;
            for (int index = 0;
                 index < entries.Count;
                 index++)
            {
                int amount =
                    Mathf.Max(1, entries[index].Amount);
                for (int sum = 0;
                     sum <= total;
                     sum++)
                {
                    if (!reachable[index, sum])
                    {
                        continue;
                    }

                    reachable[index + 1, sum] = true;
                    if (sum + amount <= total)
                    {
                        reachable[
                            index + 1,
                            sum + amount] = true;
                    }
                }
            }
            if (!reachable[entries.Count, desired])
            {
                error =
                    "当前收集品数量组合无法精确得到 " +
                    desired +
                    "。";
                return false;
            }

            var selected =
                new HashSet<Entry>();
            int remaining = desired;
            for (int index = entries.Count;
                 index > 0;
                 index--)
            {
                Entry entry = entries[index - 1];
                int amount =
                    Mathf.Max(1, entry.Amount);
                if (remaining >= amount &&
                    reachable[
                        index - 1,
                        remaining - amount])
                {
                    selected.Add(entry);
                    remaining -= amount;
                }
            }
            if (remaining != 0)
            {
                error =
                    "收集品数量组合重建失败。";
                return false;
            }

            var visualChanges =
                new List<(CollectiblePickup source,
                    bool collected)>();
            notificationSuppression++;
            try
            {
                foreach (Catalog catalog in
                         Catalogs.Values)
                {
                    catalog.CollectedIds.Clear();
                    foreach (Entry entry in
                             catalog.Entries.Values)
                    {
                        bool collected =
                            selected.Contains(entry);
                        if (entry.Collected != collected)
                        {
                            entry.Collected = collected;
                            if (entry.Source != null)
                            {
                                visualChanges.Add(
                                    (entry.Source,
                                        collected));
                            }
                        }
                        if (collected &&
                            !string.IsNullOrEmpty(
                                entry.StableId))
                        {
                            catalog.CollectedIds.Add(
                                entry.StableId);
                        }
                    }
                }

                for (int index = 0;
                     index < visualChanges.Count;
                     index++)
                {
                    CollectiblePickup source =
                        visualChanges[index].source;
                    if (visualChanges[index].collected)
                    {
                        source.RestoreCollectedState();
                    }
                    else
                    {
                        source
                            .ResetPersistentCollectionForNewRun();
                    }
                }
            }
            finally
            {
                notificationSuppression--;
            }

            applied = desired;
            NotifyChanged();
            return true;
        }

        private static void NotifyChanged()
        {
            if (notificationSuppression == 0)
            {
                Changed?.Invoke();
            }
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
            // Keep restored identities even if this scene revision does not
            // currently instantiate them. Saving must not silently erase them.
            foreach (string id in CatalogFor(scene).CollectedIds) destination.Add(id);
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
            if (changed) NotifyChanged();
        }

        public static void ResetSessionProgress(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            ResetProgress(new List<Scene> { scene }, false);
        }

        public static void ResetAllSessionProgress()
        {
            var scenes = new List<Scene>();
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene =
                    SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                scenes.Add(scene);
            }
            ResetProgress(scenes, true);
        }

        private static void ResetProgress(List<Scene> scenes, bool allScenes)
        {
            var items = new List<CollectiblePickup>();
            notificationSuppression++;
            try
            {
                foreach (Scene scene in scenes)
                    foreach (GameObject root in scene.GetRootGameObjects())
                        foreach (var item in root.GetComponentsInChildren<CollectiblePickup>(true))
                            if (!item.ExcludedFromProgress) items.Add(item);

                // Clear object state before clearing catalogs or activating any
                // root. Otherwise the first OnEnable/UI query scans other items
                // still marked collected and resurrects the previous run's IDs.
                foreach (var item in items) item.PreparePersistentCollectionForNewRun();
                if (allScenes) Catalogs.Clear();
                else foreach (Scene scene in scenes) Catalogs.Remove(SceneKey(scene));
                foreach (var item in items)
                    if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                foreach (Scene scene in scenes) RefreshSceneCatalog(scene);
            }
            finally { notificationSuppression--; }
            NotifyChanged();
        }
    }
}
