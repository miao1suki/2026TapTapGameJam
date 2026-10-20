using System;
using System.IO;
using System.Linq;
using DG.Tweening;
using Project.GameFlow;
using Project.Items;
using Project.Player;
using Project.Rooms;
using Project.Saving;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Project.WorldItems.Editor
{
    // Actual Gaming startup/restore, actual Trigger, autosave and room reset.
    // Never run in the authoring project or use its persistent save directory.
    public static class CollectibleFormalSaveAudit
    {
        private const string Flag = "CollectibleFormalSaveAudit.Running";
        private const string Seed = Flag + ".Seed";
        private const string DirectoryKey = Flag + ".Directory";
        private static double started;
        private static bool entered;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use only the silent isolated probe project.");
            var scene = EditorSceneManager.OpenScene(GameFlowController.GameplayScenePath);
            var items = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CollectiblePickup>(true))
                .Where(item => !item.ExcludedFromProgress).ToArray();
            if (items.Length < 2 || !items[0].TryGetStableInstanceId(out string id))
                throw new InvalidOperationException("Gaming requires two authored collectibles.");
            var data = RunSaveService.CreateNew(scene.path);
            var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).First();
            data.initialPlayerPosition = player.transform.position;
            data.fallbackPlayerPosition = player.transform.position;
            data.hasFallbackPlayerPosition = true;
            data.collectedCollectibleIds.Add(id);
            SessionState.SetString(Seed, JsonUtility.ToJson(data));
            SessionState.SetString(DirectoryKey, Path.GetFullPath("Library/CollectibleFormalSaveAudit-" + Guid.NewGuid().ToString("N")));
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareSave()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            RunSaveService.SaveDirectoryOverrideForTests = SessionState.GetString(DirectoryKey, string.Empty);
            RunSaveService.Save(JsonUtility.FromJson<RunSaveData>(SessionState.GetString(Seed, string.Empty)));
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup;
            entered = false;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 90) { Finish(false, "startup timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .5f) return;
            if (!entered)
            {
                if (GameFlowController.Instance == null || !GameFlowController.Instance.EnterGameplayImmediatelyForDevelopment()) return;
                entered = true;
                return;
            }
            if (Time.time < 3f || GameplayPresentationGate.IsSuppressed || RunSessionCoordinator.Instance == null ||
                !RunSessionCoordinator.Instance.HasActiveRun) return;
            EditorApplication.update -= Tick;
            SimulationMode previous = Physics.simulationMode;
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CollectiblePickup>(true))
                    .Where(item => !item.ExcludedFromProgress).ToArray();
                var seed = JsonUtility.FromJson<RunSaveData>(SessionState.GetString(Seed, string.Empty));
                var restored = all.Single(item => item.TryGetStableInstanceId(out string id) && id == seed.collectedCollectibleIds[0]);
                Check(restored.IsCollected && !restored.gameObject.activeSelf, "loaded collected item must be hidden");
                var item = all.FirstOrDefault(candidate => candidate != restored && candidate.CanCollect);
                if (item == null)
                    throw new InvalidOperationException("No eligible uncollected item: " + string.Join("; ", all.Select(candidate =>
                        candidate.name + " collected=" + candidate.IsCollected + " requested=" + candidate.CollectionRequested +
                        " active=" + candidate.gameObject.activeInHierarchy + " enabled=" + candidate.enabled +
                        " self=" + candidate.gameObject.activeSelf + " parents=" + Parents(candidate.transform.parent))));
                Check(item.TryGetStableInstanceId(out string collectedId), "uncollected item needs stable identity");
                var player = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).First();
                player.GetComponent<PlayerInputDriver>().enabled = false;
                Physics.simulationMode = SimulationMode.Script;
                player.transform.position = item.transform.position;
                player.Motor.position = item.transform.position;
                player.Motor.linearVelocity = Vector3.zero;
                Physics.SyncTransforms();
                for (int step = 0; step < 3; step++) Physics.Simulate(.02f);
                Check(item.CollectionRequested, "actual player Trigger must accept pickup after loaded-run startup");
                DOTween.CompleteAll(true);
                Check(item.IsCollected && !item.gameObject.activeSelf, "actual flight must settle pickup");
                Check(RunSaveService.TryLoad(out var saved, out var error) && saved.collectedCollectibleIds.Contains(collectedId),
                    "autosave must record pickup: " + error);
                var member = item.GetComponent<RoomResetObject>();
                member.Manager?.ResetRegion(member.RegionId);
                CollectibleProgressService.RefreshSceneCatalog(scene);
                Check(item.IsCollected && !item.gameObject.activeSelf && CollectibleProgressService.GetWorldProgress().Collected >= 2,
                    "room reset must preserve both loaded and freshly collected progress");
                var remaining = all.First(candidate => candidate != item && candidate != restored);
                var remainingMember = remaining.GetComponent<RoomResetObject>();
                remainingMember.Manager?.ResetRegion(remainingMember.RegionId);
                Check(remaining.CanCollect && remaining.GetComponentsInChildren<Collider>(true).All(shape => shape.enabled),
                    "room reset must not restore stale inactive/disabled flags on an uncollected item");
                Finish(true, "loaded Gaming hides collected item; real Trigger/flight/autosave/room persistence passed");
            }
            catch (Exception error) { Finish(false, error.ToString()); }
            finally { Physics.simulationMode = previous; }
        }

        private static string Parents(Transform target)
        {
            string value = string.Empty;
            while (target != null) { value += target.name + ":" + target.gameObject.activeSelf + "/"; target = target.parent; }
            return value;
        }

        private static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(Flag);
            File.WriteAllText("Library/CollectibleFormalSaveAudit.txt", (passed ? "PASS " : "FAIL ") + report);
            if (passed) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
