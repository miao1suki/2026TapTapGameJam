using System;
using System.IO;
using DG.Tweening;
using Project.ColorBlocks;
using Project.Items;
using Project.GameFlow;
using Project.Player;
using Project.Pickups;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.WorldItems.Editor
{
    /// <summary>Real trigger/solid callbacks; never invoke pickup handlers reflectively.</summary>
    public static class CollectibleContactAudit
    {
        private const string Flag = "CollectibleContactAudit.Running";
        private const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
        private const string ItemPath = "Assets/_Project/Content/WorldItems/Collectible.prefab";
        private static double started;
        private static int checks;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                !Array.Exists(Environment.GetCommandLineArgs(), argument => argument == "-noaudio"))
                throw new InvalidOperationException("Collectible audit requires the silent isolated probe project.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
            if (EditorApplication.timeSinceStartup - started > 120)
            { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
            EditorApplication.update -= Tick;
            var simulation = Physics.simulationMode;
            try
            {
                AudioListener.volume = 0f;
                AudioListener.pause = true;
                Physics.simulationMode = SimulationMode.Script;
                checks = 0;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                VerifyTriggerPassThrough();
                VerifyLatePlayerAndReuse();
                VerifyMenuPreviewHandoff();
                VerifyForeignIgnoreOwnership();
                VerifyCarriedBodyIsolation();
                foreach (string color in new[] { "Red", "Green", "Blue" })
                    VerifyPush("Assets/_Project/Content/ContactCubes/" + color + "_ContactCube.prefab", color);
                VerifyPush("Assets/_Project/Content/ColorBlocks/Prefabs/Red_Foundation.prefab", "base");
                Finish(true, "PASS " + checks + " collectible physics/trigger/lifecycle checks");
            }
            catch (Exception error) { Debug.LogException(error); Finish(false, error.ToString()); }
            finally { Physics.simulationMode = simulation; }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("COLLECTIBLE CONTACT: " + message);
            checks++;
            Debug.Log("COLLECTIBLE PASS: " + message);
        }

        private static GameObject Spawn(string path, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing fixture: " + path);
            return Object.Instantiate(prefab, position, Quaternion.identity);
        }

        private static PlayerController Player(Vector3 position)
        {
            var player = Spawn(PlayerPath, position).GetComponent<PlayerController>();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.useGravity = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeRotation |
                RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
            return player;
        }

        private static CollectiblePickup Item(Vector3 position) =>
            Spawn(ItemPath, position).GetComponent<CollectiblePickup>();

        private static Collider Trigger(CollectiblePickup item)
        {
            foreach (var shape in item.GetComponentsInChildren<Collider>(true))
                if (shape.isTrigger) return shape;
            throw new InvalidOperationException("Fixture has no pickup trigger.");
        }

        private static void Step(int count = 1)
        {
            Physics.SyncTransforms();
            for (int i = 0; i < count; i++) Physics.Simulate(.02f);
        }

        private static void VerifyTriggerPassThrough()
        {
            var player = Player(new Vector3(98, 20, 0));
            var item = Item(new Vector3(100, 20, 0));
            try
            {
                var shape = item.GetComponent<Collider>();
                var playerShape = player.GetComponent<Collider>();
                Check(!shape.isTrigger && Physics.GetIgnoreCollision(shape, playerShape),
                    "existing player ignores collectible push solid without converting it to trigger");
                Check(!Physics.GetIgnoreCollision(Trigger(item), playerShape),
                    "separate pickup trigger still interacts with player");
                Check(!item.GetComponent<Rigidbody>().useGravity && item.CanBePushed,
                    "pickup is dynamic and floats");
                int lockDepth = player.ControlLockDepth;
                player.Motor.linearVelocity = Vector3.right * 5;
                Step(50);
                Check(item.CollectionRequested && !item.CollectionFinished,
                    "real child trigger starts shared collection lifecycle");
                Check(item.GetComponent<PickupFlightAnimator>().IsPlaying && item.gameObject.activeSelf,
                    "collection keeps author object and flight visible until completion");
                Check(player.Motor.position.x > 102.5f && player.Motor.linearVelocity.x > 4.9f,
                    "player continues through item without wall or collision slowdown");
                Check(player.ControlLockDepth == lockDepth && !player.IsControlLocked,
                    "ordinary collectible never freezes player controls");
                Check(!shape.enabled && !Trigger(item).enabled,
                    "collection disables both push body shape and pickup trigger");
                player.Motor.position = new Vector3(95, 20, 0);
                player.Motor.linearVelocity = Vector3.zero;
                item.ResetForRoom();
                Check(item.CollectionRequested && !item.IsCollected && !item.CanCollect &&
                    item.GetComponent<PickupFlightAnimator>().IsPlaying && !shape.enabled && !Trigger(item).enabled,
                    "room reset preserves an accepted collection flight instead of respawning a second reward");
                DOTween.CompleteAll(true);
                item.ResetForRoom();
                Check(item.IsCollected && !item.gameObject.activeSelf && !item.CanCollect && !item.CanBePushed,
                    "finished collection remains hidden and ineligible across subsequent room reset");
                Check(!item.GetComponent<Rigidbody>().useGravity && player.ControlLockDepth == lockDepth,
                    "reset keeps floating pickup and leaves player locks untouched");
            }
            finally { Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(player.gameObject); }
        }

        private static void VerifyLatePlayerAndReuse()
        {
            var item = Item(new Vector3(200, 20, 0));
            var player = Player(new Vector3(195, 20, 0));
            try
            {
                var shape = item.GetComponent<Collider>();
                var playerShape = player.GetComponent<Collider>();
                Check(Physics.GetIgnoreCollision(shape, playerShape),
                    "player instantiated after collectible is filtered immediately by activation event");
                player.gameObject.SetActive(false);
                player.gameObject.SetActive(true);
                Check(Physics.GetIgnoreCollision(shape, playerShape),
                    "player GameObject reenable reapplies solid filtering");
                item.enabled = false;
                Check(!Physics.GetIgnoreCollision(shape, playerShape),
                    "disabling pickup component restores only owned ignore pairs");
                item.enabled = true;
                Check(Physics.GetIgnoreCollision(shape, playerShape),
                    "reenabling pickup component immediately restores player pass-through");
                item.gameObject.SetActive(false);
                item.gameObject.SetActive(true);
                item.GetComponent<Rigidbody>().useGravity = true;
                item.ResetForReuse();
                Check(!item.GetComponent<Rigidbody>().useGravity && item.CanBePushed &&
                    Physics.GetIgnoreCollision(shape, playerShape),
                    "pooled reenable and explicit reuse repair old gravity flag and collision state");
                Check(!Physics.GetIgnoreCollision(Trigger(item), playerShape),
                    "all reuse paths preserve trigger interaction");
            }
            finally { Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(player.gameObject); }
        }

        private static void VerifyMenuPreviewHandoff()
        {
            var root = new GameObject("menu physical handoff fixture");
            var presentation = root.AddComponent<WorldMenuPresentation>();
            presentation.BeginPreview();
            var item = Item(new Vector3(600, 20, 0));
            var player = Player(new Vector3(598, 20, 0));
            try
            {
                var shape = item.GetComponent<Collider>();
                var playerShape = player.GetComponent<Collider>();
                Check(!item.CanCollect && Physics.GetIgnoreCollision(shape, playerShape),
                    "preview blocks collection but not a late player's physical pass-through setup");
                presentation.StageWorld(player.gameObject.scene);
                Check(!playerShape.enabled && player.Motor.isKinematic,
                    "real world-menu stage temporarily disables player's physical participant");
                presentation.EndPreview();
                Check(playerShape.enabled && !player.Motor.isKinematic &&
                    Physics.GetIgnoreCollision(shape, playerShape),
                    "real menu handoff reapplies pass-through after restoring player collider");
                Check(!Physics.GetIgnoreCollision(Trigger(item), playerShape),
                    "menu handoff keeps collection trigger live");
                player.Motor.useGravity = false;
                player.Motor.linearVelocity = Vector3.right * 5;
                Step(50);
                Check(player.Motor.position.x > 602.5f && player.Motor.linearVelocity.x > 4.9f,
                    "after menu handoff real physics crosses pickup without a wall or slowdown");
                Check(item.CollectionRequested,
                    "after menu handoff real trigger collects while player crosses the pickup");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(item.gameObject);
                Object.DestroyImmediate(player.gameObject);
            }
        }

        private static void VerifyForeignIgnoreOwnership()
        {
            var player = Player(new Vector3(295, 20, 0));
            player.enabled = false; // Keep the collider enabled, but remove player from active registry.
            var item = Item(new Vector3(300, 20, 0));
            try
            {
                var shape = item.GetComponent<Collider>();
                var playerShape = player.GetComponent<Collider>();
                Physics.IgnoreCollision(shape, playerShape, true);
                player.enabled = true;
                item.enabled = false;
                Check(Physics.GetIgnoreCollision(shape, playerShape),
                    "preexisting ignore pair is not owned or restored by collectible");
                item.enabled = true;
                item.ResetForRoom();
                item.enabled = false;
                Check(Physics.GetIgnoreCollision(shape, playerShape),
                    "repeated enable/reset does not steal preexisting ignore ownership");
                Physics.IgnoreCollision(shape, playerShape, false);
            }
            finally { Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(player.gameObject); }
        }

        private static void VerifyCarriedBodyIsolation()
        {
            var player = Player(new Vector3(490, 20, 0));
            var cube = Spawn("Assets/_Project/Content/ContactCubes/Red_ContactCube.prefab", new Vector3(500, 20, 0));
            cube.transform.SetParent(player.transform, true);
            var cubeBody = cube.GetComponent<Rigidbody>();
            cubeBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
            cubeBody.isKinematic = true;
            cubeBody.useGravity = false;
            var item = Item(new Vector3(500, 20, 0));
            try
            {
                var shape = item.GetComponent<Collider>();
                var playerShape = player.GetComponent<Collider>();
                var cubeShape = cube.GetComponentInChildren<Collider>();
                Check(cubeShape.attachedRigidbody == cubeBody && cubeBody != player.Motor,
                    "carried fixture is a player descendant with an independent real cube body");
                Check(Physics.GetIgnoreCollision(shape, playerShape) && !Physics.GetIgnoreCollision(shape, cubeShape),
                    "player pass-through does not include independently simulated carried descendants");
                Step(3);
                Check(item.CanCollect && !item.CollectionRequested,
                    "carried cube inside pickup trigger is not mistaken for player collection");
                item.ResetForRoom();
                player.gameObject.SetActive(false);
                player.gameObject.SetActive(true);
                Check(Physics.GetIgnoreCollision(shape, playerShape) && !Physics.GetIgnoreCollision(shape, cubeShape),
                    "room reset and player reenable preserve carried-body collision separation");
                item.enabled = false;
                item.enabled = true;
                Check(!Physics.GetIgnoreCollision(shape, cubeShape),
                    "pickup component reuse never owns ignore pairs for carried cube");
                cube.transform.SetParent(null, true);
                Check(!Physics.GetIgnoreCollision(shape, cubeShape) && item.CanBePushed,
                    "putting cube down leaves collectible push collision enabled");
            }
            finally
            {
                Object.DestroyImmediate(item.gameObject);
                Object.DestroyImmediate(cube);
                Object.DestroyImmediate(player.gameObject);
            }
        }

        private static void VerifyPush(string path, string label)
        {
            var item = Item(new Vector3(400, 20, 0));
            var pusher = Spawn(path, new Vector3(398.5f, 20, 0));
            try
            {
                var body = pusher.GetComponent<Rigidbody>();
                Check(body != null, label + " has actual push body");
                var originalConstraints = body.constraints;
                // This case drives an authored solid as a kinematic physical
                // pusher, not through its gameplay controller. A resting base
                // intentionally freezes X; the real hook motion releases that
                // axis. Release it here too instead of testing a frozen fixture.
                body.constraints = RigidbodyConstraints.FreezeRotation |
                    RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                body.isKinematic = true;
                body.useGravity = false;
                Vector3 start = body.position;
                var itemBody = item.GetComponent<Rigidbody>();
                var itemShape = item.GetComponent<Collider>();
                foreach (var shape in pusher.GetComponentsInChildren<Collider>(true))
                    if (shape.enabled && !shape.isTrigger)
                        Check(!Physics.GetIgnoreCollision(itemShape, shape),
                            label + " still collides with collectible solid");
                for (int i = 0; i < 50; i++)
                {
                    body.MovePosition(body.position + Vector3.right * .04f);
                    Step();
                }
                Debug.Log($"COLLECTIBLE PUSH {label}: pusher={start:F3}->{body.position:F3}, " +
                    $"constraints={originalConstraints}->{body.constraints}, item={itemBody.position:F3}, " +
                    $"velocity={itemBody.linearVelocity:F3}, collected={item.CollectionRequested}, " +
                    $"dynamic={!itemBody.isKinematic}, gravity={itemBody.useGravity}, solid={itemShape.bounds}");
                Check(body.position.x > start.x + 1.9f,
                    label + " fixture actually moves its solid through pickup position");
                Check(itemBody.position.x > 400.5f && item.CanCollect && !item.CollectionRequested,
                    label + " physically pushes pickup without collecting it");
                Check(!itemBody.useGravity && !itemBody.isKinematic,
                    label + " push preserves dynamic no-gravity pickup");
            }
            finally { Object.DestroyImmediate(pusher); Object.DestroyImmediate(item.gameObject); }
        }

        private static void Finish(bool passed, string result)
        {
            SessionState.SetBool(Flag, false);
            EditorApplication.update -= Tick;
            File.WriteAllText(Path.Combine("Library", "CollectibleContactAudit.txt"), result);
            if (passed) Debug.Log(result); else Debug.LogError(result);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
