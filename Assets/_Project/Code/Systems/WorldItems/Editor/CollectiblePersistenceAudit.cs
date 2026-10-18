using System;
using System.Collections.Generic;
using System.IO;
using DG.Tweening;
using Project.Items;
using Project.LevelEditor;
using Project.Pickups;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.WorldItems.Editor
{
    /// <summary>Real trigger and flight completion through room/respawn reset boundaries.</summary>
    public static class CollectiblePersistenceAudit
    {
        private const string Flag = "CollectiblePersistenceAudit.Running";
        private const string FixturePath = "CollectiblePersistenceAudit.FixturePath";
        private const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
        private const string ItemPath = "Assets/_Project/Content/WorldItems/Collectible.prefab";
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static double started;
        private static int checks;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Use only the silent isolated probe project.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // A saved fixture gives identity tests a real Scene.path. Only a
            // newly named isolated fixture is written, never an authored map.
            string path = "Assets/__CollectiblePersistenceAudit_" + Guid.NewGuid().ToString("N") + ".unity";
            if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Cannot save isolated fixture.");
            SessionState.SetString(FixturePath, path);
            SessionState.SetBool(Flag, true);
            Resume();
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .3f) return;
            EditorApplication.update -= Tick;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            string report;
            try
            {
                checks = 0;
                AudioListener.volume = 0f;
                AudioListener.pause = true;
                Physics.simulationMode = SimulationMode.Script;
                VerifyCollectionThroughRoomResets(); Cleanup();
                VerifyUncollectedReset(); Cleanup();
                VerifyStableIdentityAndRestore(); Cleanup();
                passed = true;
                report = "PASS " + checks + " collectible persistence/identity checks";
            }
            catch (Exception error) { report = "FAIL " + checks + "\n" + error; Debug.LogException(error); }
            finally { Cleanup(); Physics.simulationMode = previous; }
            Finish(passed, report);
        }

        private static void Check(bool condition, string label)
        {
            checks++;
            if (!condition) throw new InvalidOperationException("COLLECTIBLE PERSISTENCE: " + label);
        }

        private static GameObject Spawn(string path, Vector3 position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing fixture: " + path);
            GameObject result = Object.Instantiate(prefab, position, Quaternion.identity);
            objects.Add(result);
            return result;
        }

        private static PlayerController Player(Vector3 position)
        {
            PlayerController player = Spawn(PlayerPath, position).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            player.Motor.useGravity = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
            return player;
        }

        private static CollectiblePickup Item(Vector3 position, string planningId = null, int localX = 0)
        {
            CollectiblePickup item = Spawn(ItemPath, position).GetComponent<CollectiblePickup>();
            item.GetComponent<Rigidbody>().interpolation = RigidbodyInterpolation.None;
            if (planningId != null)
            {
                var placed = item.GetComponent<LevelEditorPlacedBlock>() ?? item.gameObject.AddComponent<LevelEditorPlacedBlock>();
                placed.SetPlanningSource(planningId, "audit", new Vector2Int(localX, 0));
            }
            return item;
        }

        private static RoomLifecycleManager Manager(PlayerController player)
        {
            var root = new GameObject("Persistence audit rooms"); objects.Add(root);
            var manager = root.AddComponent<RoomLifecycleManager>();
            var first = new RoomRegion { id = "audit_a" };
            var second = new RoomRegion { id = "audit_b" };
            for (int x = 100; x < 110; x++) first.cells.Add(new Vector2Int(x, -21));
            for (int x = 110; x < 120; x++) second.cells.Add(new Vector2Int(x, -21));
            manager.Configure(new List<RoomRegion> { first, second }, 1f, string.Empty);
            var serialized = new SerializedObject(manager);
            serialized.FindProperty("player").objectReferenceValue = player;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return manager;
        }

        private static void Move(PlayerController player, Vector3 position)
        {
            player.transform.position = position;
            player.Motor.position = position;
            player.Motor.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static void Step(int count = 1)
        {
            Physics.SyncTransforms();
            for (int step = 0; step < count; step++) Physics.Simulate(.02f);
        }

        private static void VerifyCollectionThroughRoomResets()
        {
            PlayerController player = Player(new Vector3(100.5f, 20.5f, 0f));
            CollectiblePickup item = Item(new Vector3(102.5f, 20.5f, 0f), "audit_reward");
            RoomLifecycleManager manager = Manager(player);
            var member = item.GetComponent<RoomResetObject>() ?? item.gameObject.AddComponent<RoomResetObject>();
            manager.Register(member);
            RoomLifecycleManager.BeforeRespawn(player, player.transform.position);
            int rewards = 0;
            Action<CollectiblePickup, GameObject> listener = (collected, actor) =>
            {
                if (collected != item) return;
                Check(collected.IsCollected && actor == player.gameObject, "reward observes settled progress and original collector");
                rewards++;
                // Reentrant listeners may ask to reset the level immediately.
                manager.ResetRegion("audit_a");
            };
            CollectiblePickup.Collected += listener;
            try
            {
                Move(player, item.transform.position); Step(3);
                var flight = item.GetComponent<PickupFlightAnimator>();
                Check(item.CollectionRequested && !item.IsCollected && flight.IsPlaying,
                    "real player Trigger starts actual shared pickup flight before reward");
                Check(member.Persistent && rewards == 0, "accepted flight is protected before settlement");
                manager.ResetRegion("audit_a"); item.ResetForRoom();
                Check(flight.IsPlaying && item.CollectionRequested && rewards == 0,
                    "direct and manager room reset do not cancel a pending reward");
                player.RequestRespawn();
                Check(flight.IsPlaying && rewards == 0, "live R respawn preserves the accepted flight");
                player.EnterDeath();
                Check(player.IsDead, "fixture enters real death state before respawn");
                player.RequestRespawn();
                Check(!player.IsDead && flight.IsPlaying && rewards == 0,
                    "death and actual respawn path preserve the pending reward");
                Move(player, new Vector3(111.5f, 20.5f, 0f)); manager.UpdatePlayerRegion(player);
                Check(flight.IsPlaying && rewards == 0, "leaving the owning room does not cancel collection flight");
                DOTween.CompleteAll(true);
                Check(item.IsCollected && rewards == 1 && !item.gameObject.activeSelf,
                    "actual flight completion settles exactly once and recycles author object");
                Check(member.Persistent && !item.CanCollect && !item.CanBePushed,
                    "settled pickup is not a resettable physical or collection target");
                for (int repeat = 0; repeat < 4; repeat++)
                {
                    manager.ResetRegion("audit_a"); item.ResetForRoom();
                    member.RestorePose(); member.RestoreState();
                    item.ResetForReuse();
                    item.gameObject.SetActive(true);
                    DOTween.CompleteAll(true);
                    Check(item.IsCollected && !item.gameObject.activeSelf && rewards == 1,
                        "reset/reuse/parent reenable cannot create duplicate settled reward " + repeat);
                }
            }
            finally { CollectiblePickup.Collected -= listener; }
        }

        private static void VerifyUncollectedReset()
        {
            PlayerController player = Player(new Vector3(100.5f, 20.5f, 0f));
            Vector3 authored = new Vector3(104.5f, 20.5f, 0f);
            CollectiblePickup item = Item(authored);
            RoomLifecycleManager manager = Manager(player);
            var member = item.GetComponent<RoomResetObject>() ?? item.gameObject.AddComponent<RoomResetObject>();
            manager.Register(member);
            Check(!item.IsCollected && !member.Persistent, "uncollected pickup remains resettable");
            item.transform.position = authored + Vector3.right * 2f;
            item.GetComponent<Rigidbody>().position = item.transform.position;
            item.GetComponent<Rigidbody>().useGravity = true;
            manager.ResetRegion("audit_a");
            Check((item.transform.position - authored).sqrMagnitude < .00001f,
                "uncollected pushed item still returns to its authored position");
            Check(item.CanCollect && item.CanBePushed && !item.GetComponent<Rigidbody>().useGravity,
                "uncollected reset restores dynamic no-gravity body and collection eligibility");
            foreach (Collider shape in item.GetComponentsInChildren<Collider>(true))
                Check(shape.enabled, "uncollected reset restores authored solid/Trigger colliders");
            Check(Physics.GetIgnoreCollision(item.GetComponent<Collider>(), player.GetComponent<Collider>()),
                "uncollected reset preserves player solid pass-through");
        }

        private static void VerifyStableIdentityAndRestore()
        {
            var first = Item(new Vector3(200f, 20f, 0f), "batch:reward", 1);
            var second = Item(new Vector3(202f, 20f, 0f), "batch:reward", 2);
            var noAuthor = Item(new Vector3(204f, 20f, 0f));
            Check(first.TryGetStableInstanceId(out string id) && second.TryGetStableInstanceId(out string other) && id != other,
                "same-category same-planning-batch objects use distinct stable local-cell identities");
            first.transform.position += Vector3.right * 8f;
            Check(first.TryGetStableInstanceId(out string moved) && moved == id,
                "physics displacement never changes stable instance identity");
            var reconstructed = Item(new Vector3(220f, 20f, 0f), "batch:reward", 1);
            Check(reconstructed.TryGetStableInstanceId(out string restoredId) && restoredId == id &&
                reconstructed.GetInstanceID() != first.GetInstanceID(),
                "same authored identity survives object reconstruction without runtime instance ID");
            Check(!noAuthor.TryGetStableInstanceId(out string unavailable) && unavailable == null,
                "missing planning identity is reported rather than inventing unstable save keys");
            int rewards = 0;
            Action<CollectiblePickup, GameObject> listener = (item, actor) => { if (item == reconstructed) rewards++; };
            CollectiblePickup.Collected += listener;
            try
            {
                reconstructed.RestoreCollectedState(); reconstructed.RestoreCollectedState();
                reconstructed.ResetForRoom(); reconstructed.gameObject.SetActive(true);
                DOTween.CompleteAll(true);
                Check(reconstructed.IsCollected && !reconstructed.gameObject.activeSelf && rewards == 0,
                    "restoring an already-settled record is silent, idempotent, and stays hidden");
                foreach (Collider shape in reconstructed.GetComponentsInChildren<Collider>(true))
                    Check(!shape.enabled, "restored collected record has no remaining physical/pickup collision");
            }
            finally { CollectiblePickup.Collected -= listener; }
        }

        private static void Cleanup()
        {
            foreach (GameObject obj in objects) if (obj != null) Object.DestroyImmediate(obj);
            objects.Clear();
        }

        private static void Finish(bool passed, string report)
        {
            EditorApplication.update -= Tick;
            SessionState.EraseBool(Flag);
            string fixture = SessionState.GetString(FixturePath, string.Empty);
            SessionState.EraseString(FixturePath);
            if (fixture.StartsWith("Assets/__CollectiblePersistenceAudit_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(fixture);
            File.WriteAllText("Library/CollectiblePersistenceAudit.txt", report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
