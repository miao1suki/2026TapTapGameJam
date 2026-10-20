using System;
using System.Collections.Generic;
using Project.Rooms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.BossLogic
{
    public readonly struct BossTravelState
    {
        public bool HasEnteredBoss { get; }
        public bool IsBossFightActive { get; }
        public bool HasDefeatedBoss { get; }
        public string BossRegionId { get; }
        public BossTravelState(bool entered, bool active, bool defeated, string region)
        {
            HasEnteredBoss = entered; IsBossFightActive = active;
            HasDefeatedBoss = defeated; BossRegionId = region;
        }
    }

    /// <summary>Session encounter contract shared with the checkpoint map.</summary>
    public static class BossTravelProgress
    {
        private sealed class Progress
        {
            public bool Entered, Defeated, DedicatedBossScene;
            public string Region;
            public readonly HashSet<int> Fighting = new HashSet<int>();
        }
        private readonly struct BossOrigin
        {
            public readonly Scene Scene;
            public readonly Vector3 Position;
            public BossOrigin(Scene scene, Vector3 position) { Scene = scene; Position = position; }
        }
        private static readonly Dictionary<string, Progress> Scenes = new Dictionary<string, Progress>(StringComparer.Ordinal);
        private static readonly Dictionary<int, HashSet<int>> LivingBosses = new Dictionary<int, HashSet<int>>();
        private static readonly Dictionary<int, BossOrigin> Origins = new Dictionary<int, BossOrigin>();
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            Scenes.Clear();
            LivingBosses.Clear();
            Origins.Clear();
            Changed = null;
        }

        public static void ResetRuntimeProgress()
        {
            Scenes.Clear();
            Origins.Clear();
            Changed?.Invoke();
        }

        public static void RestoreRuntimeProgress(
            Scene scene,
            bool entered,
            bool defeated)
        {
            if (!scene.IsValid())
            {
                return;
            }

            Progress progress = For(scene);
            progress.Entered = entered;
            progress.Defeated = defeated;
            progress.Fighting.Clear();
            Changed?.Invoke();
        }
        private static string Key(Scene scene) => string.IsNullOrEmpty(scene.path) ? "session:" + scene.handle : scene.path;
        private static Progress For(Scene scene)
        {
            string key = Key(scene);
            if (!Scenes.TryGetValue(key, out Progress progress)) Scenes[key] = progress = new Progress();
            return progress;
        }

        public static bool HasLivingBoss(Scene scene)
        {
            return scene.IsValid() &&
                   LivingBosses.TryGetValue(scene.handle, out HashSet<int> living) &&
                   living.Count > 0;
        }

        public static BossTravelState GetState(Scene scene)
        {
            if (!scene.IsValid()) return default;
            Progress value = For(scene);
            bool entered = false, fighting = false, defeated = false;
            foreach (Progress progress in Scenes.Values)
            {
                entered |= progress.Entered;
                fighting |= progress.Fighting.Count > 0;
                defeated |= progress.Defeated;
            }
            return new BossTravelState(entered, fighting, defeated, value.Region);
        }

        public static bool IsBossScene(Scene scene)
        {
            if (!scene.IsValid()) return false;
            // The development-only separate Boss scene is a dedicated arena, whereas a
            // Boss placed in one room of Gaming must not forbid the whole map.
            if (string.Equals(scene.name, "Boss", StringComparison.OrdinalIgnoreCase)) return true;
            if (!Scenes.TryGetValue(Key(scene), out Progress progress)) return false;
            if (progress.DedicatedBossScene) return true;
            if (string.IsNullOrEmpty(progress.Region)) return false;
            RoomLifecycleManager manager = RoomLifecycleManager.ForScene(scene);
            if (manager == null) return false;
            bool hasValidRoom = false;
            foreach (RoomRegion region in manager.Regions)
            {
                if (region == null || string.IsNullOrEmpty(region.id) || region.cells == null || region.cells.Count == 0) continue;
                hasValidRoom = true;
                if (!string.Equals(region.id, progress.Region, StringComparison.Ordinal)) return false;
            }
            return hasValidRoom;
        }

        /// <summary>Explicitly mark a separate arena; never inferred just from a Boss existing in a mixed world.</summary>
        public static void SetDedicatedBossScene(Scene scene, bool value)
        {
            if (!scene.IsValid()) return;
            Progress progress = For(scene);
            if (progress.DedicatedBossScene == value) return;
            progress.DedicatedBossScene = value;
            Changed?.Invoke();
        }

        /// <summary>Explicit author override; ordinary Boss movement never changes room identity.</summary>
        public static void SetBossRegion(Scene scene, string regionId)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(regionId)) return;
            Progress value = For(scene);
            if (value.Region == regionId) return;
            value.Region = regionId;
            Changed?.Invoke();
        }

        internal static void Register(BossController boss)
        {
            if (boss == null || !boss.gameObject.scene.IsValid()) return;
            if (!boss.IsDead && TrackLivingBoss(boss)) Changed?.Invoke();
            int id = boss.GetInstanceID();
            if (!Origins.TryGetValue(id, out BossOrigin origin))
                Origins[id] = origin = new BossOrigin(boss.gameObject.scene, boss.transform.position);
            Progress value = For(origin.Scene);
            if (!string.IsNullOrEmpty(value.Region)) return;
            var manager = RoomLifecycleManager.ForScene(origin.Scene);
            string region = manager != null ? manager.FindRegion(origin.Position) : null;
            if (!string.IsNullOrEmpty(region)) SetBossRegion(origin.Scene, region);
        }

        internal static void BeginFight(BossController boss)
        {
            if (boss == null) return;
            Register(boss);
            Progress value = For(boss.gameObject.scene);
            bool changed = !value.Entered;
            value.Entered = true;
            changed |= TrackLivingBoss(boss);
            changed |= value.Fighting.Add(boss.GetInstanceID());
            if (changed) Changed?.Invoke();
        }

        internal static void Defeat(BossController boss)
        {
            if (boss == null) return;
            Register(boss);
            Progress value = For(boss.gameObject.scene);
            bool changed = !value.Defeated || !value.Entered;
            value.Defeated = value.Entered = true;
            changed |= UntrackLivingBoss(boss);
            changed |= value.Fighting.Remove(boss.GetInstanceID());
            if (changed) Changed?.Invoke();
        }

        internal static void Unregister(BossController boss)
        {
            if (boss == null) return;
            bool changed = UntrackLivingBoss(boss);
            changed |= For(boss.gameObject.scene).Fighting.Remove(boss.GetInstanceID());
            if (changed) Changed?.Invoke();
            // Keep the original room/position for a disabled and re-enabled
            // instance, rather than accidentally recording its chase location.
        }

        private static bool TrackLivingBoss(BossController boss)
        {
            if (boss == null || boss.IsDead || !boss.gameObject.scene.IsValid()) return false;
            int handle = boss.gameObject.scene.handle;
            if (!LivingBosses.TryGetValue(handle, out HashSet<int> living))
                LivingBosses[handle] = living = new HashSet<int>();
            return living.Add(boss.GetInstanceID());
        }

        private static bool UntrackLivingBoss(BossController boss)
        {
            if (boss == null || !boss.gameObject.scene.IsValid()) return false;
            int handle = boss.gameObject.scene.handle;
            if (!LivingBosses.TryGetValue(handle, out HashSet<int> living) ||
                !living.Remove(boss.GetInstanceID())) return false;
            if (living.Count == 0) LivingBosses.Remove(handle);
            return true;
        }
    }
}
