using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.ContactCubes.Editor
{
    /// <summary>Real standard-player screen application, then deterministic PhysX growth/reset checks.</summary>
    public static class BlueContactCubeGrowthAudit
    {
        private const string Flag = "BlueContactCubeGrowthAudit.Running";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly List<GameObject> objects = new List<GameObject>();
        private static readonly List<ContactCube> cubes = new List<ContactCube>();
        private static readonly StringBuilder report = new StringBuilder();
        private static PlayerController player;
        private static PlayerColorWheel wheel;
        private static Camera camera;
        private static int checks, successes;
        private static double started;

        public static void Run()
        {
            string path = Application.dataPath.Replace('\\', '/');
            if ((!path.Contains("/Library/FlowRetirementProbe/Assets") &&
                !path.Contains("/Library/R1R2Integration-FlowRetirementProbe-")) ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
                throw new InvalidOperationException("Only run in the silent isolated FlowRetirementProbe project.");
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
            EditorApplication.update -= Wait;
            EditorApplication.update += Wait;
        }

        private static void Wait()
        {
            if (EditorApplication.timeSinceStartup - started > 120) { Finish(false, "Timeout"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .6f) return;
            EditorApplication.update -= Wait;
            SimulationMode previous = Physics.simulationMode;
            bool passed = false;
            try
            {
                checks = successes = 0; report.Clear();
                AudioListener.volume = 0f; AudioListener.pause = true;
                Physics.simulationMode = SimulationMode.Script;
                ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession();
                CreatePlayer();
                VerifyDirectionsAndGridSizes();
                VerifyGridRows();
                VerifyRepeatedAndDerivedHits();
                VerifyObstructions();
                VerifyGrowthPlayerEscape();
                VerifyContactCompetition();
                VerifyGatesAndCleanup();
                VerifyRoomReset();
                passed = true;
            }
            catch (Exception error) { report.AppendLine(error.ToString()); Debug.LogException(error); }
            finally { Cleanup(); Physics.simulationMode = previous; }
            Finish(passed, (passed ? "PASS " : "FAIL ") + checks + "\n" + report);
        }

        private static void Finish(bool passed, string text)
        {
            EditorApplication.update -= Wait;
            SessionState.EraseBool(Flag);
            File.WriteAllText("Library/BlueContactCubeGrowthAudit.txt", text);
            Debug.Log(text);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException("BLUE GREEN GROWTH: " + label);
        }

        private static void CreatePlayer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
            player = Object.Instantiate(prefab, new Vector3(1000, 10, 0), Quaternion.identity).GetComponent<PlayerController>();
            objects.Add(player.gameObject);
            player.GetComponent<PlayerInputDriver>().enabled = false;
            player.Motor.constraints = RigidbodyConstraints.FreezeAll;
            player.Motor.interpolation = RigidbodyInterpolation.None;
            wheel = player.GetComponent<PlayerColorWheel>();
            wheel.ColorApplicationSucceeded += () => successes++;
            Check(wheel.TrySelectColor("blue"), "standard player selects unlocked blue");
            var cameraObject = new GameObject("Growth audit camera"); objects.Add(cameraObject);
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 12;
        }

        private static ContactCube Spawn(CubeColor color, Vector3 position, float size = 1f)
        {
            var owner = new GameObject("Grid fixture " + size); objects.Add(owner);
            owner.AddComponent<BlueGrowthAuditGrid>().Size = size;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Content/ContactCubes/{color}_ContactCube.prefab");
            GameObject obj = Object.Instantiate(prefab, position, Quaternion.identity, owner.transform);
            ContactCube cube = obj.GetComponent<ContactCube>(); cubes.Add(cube);
            obj.GetComponent<ContactCubeMotion>().enabled = false;
            obj.GetComponent<Rigidbody>().isKinematic = true;
            obj.GetComponent<Rigidbody>().useGravity = false;
            if (obj.TryGetComponent(out HorizontalCubeVine climb)) climb.enabled = false;
            var settings = new SerializedObject(cube);
            settings.FindProperty("maximumLength").intValue = 4;
            settings.FindProperty("stepSeconds").floatValue = .1f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            cube.ResetForRoom(); Physics.SyncTransforms();
            return cube;
        }

        private static void MovePlayer(Vector3 position)
        {
            player.transform.position = player.Motor.position = position;
            player.Motor.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private static void Move(ContactCube cube, Vector3 position)
        {
            cube.transform.position = cube.GetComponent<Rigidbody>().position = position;
            Physics.SyncTransforms();
        }

        private static void Click(ContactCube cube, Vector3 point, bool accepted = true)
        {
            camera.transform.position = new Vector3(point.x, point.y, -20);
            camera.transform.rotation = Quaternion.identity;
            Physics.SyncTransforms();
            int before = successes;
            wheel.Tick(camera.WorldToScreenPoint(point));
            player.TryUseSelectedColorAbility();
            Check(successes == before + (accepted ? 1 : 0), "real standard-player screen hit " + (accepted ? "accepted" : "rejected"));
            Check(cube.Color == CubeColor.Green || !accepted, "successful blue application preserves green fixed identity");
        }

        private static void Step(int count)
        {
            for (int index = 0; index < count; index++)
            {
                Physics.SyncTransforms();
                foreach (ContactCube cube in cubes)
                    if (cube != null && cube.isActiveAndEnabled)
                        typeof(ContactCube).GetMethod("FixedUpdate", Hidden).Invoke(cube, null);
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        private static void VerifyDirectionsAndGridSizes()
        {
            Vector3[] offsets = { Vector3.down, Vector3.up, Vector3.left, Vector3.right };
            Vector3[] expected = { Vector3.down, Vector3.up, Vector3.right, Vector3.left };
            int fixture = 0;
            foreach (float size in new[] { .5f, 1f, 2.5f })
            for (int direction = 0; direction < offsets.Length; direction++)
            {
                Vector3 root = new Vector3((2000.5f + fixture++ * 20) * size, 10.5f * size, 0);
                ContactCube cube = Spawn(CubeColor.Green, root, size);
                Check(cube.GetComponent<BlueContactCubeGrowthTarget>() != null, "existing green prefab automatically gets only growth receiver");
                Check(Mathf.Abs(cube.CellWorldSize - size) < .0001f, "grid utility resolves parent provider " + size);
                MovePlayer(root + offsets[direction] * (size * 1.5f));
                Click(cube, root); MovePlayer(root + Vector3.forward * 10); Step(7);
                Check(cube.Length == 2, "uses existing step interval at size " + size);
                Check(Vector3.Dot(cube.GrowthDirection, expected[direction]) > .999f, "four-way player/root grid direction " + direction);
                MovePlayer(root + Vector3.forward * 10); Step(30);
                Check(cube.Length == 4 && !cube.IsPlayerGrowthRequested, "single click continues to existing maximum and clears request");
                Check(Mathf.Abs(cube.GetComponentInChildren<BoxCollider>().size.x - 4 * size) < .0001f,
                    "one solid body scales length by configured grid size");
                Check(cube.transform.position == root && cube.ColorId == "green", "growth preserves root anchor and color");
                cube.enabled = false;
            }
        }

        private static void VerifyGridRows()
        {
            Vector3 root = new Vector3(3100.5f, 10.01f, 0);
            ContactCube cube = Spawn(CubeColor.Green, root);
            MovePlayer(root + new Vector3(-1.5f, .95f, 0)); Click(cube, root); Step(7);
            Check(Vector3.Dot(cube.GrowthDirection, Vector3.right) > .999f, "same world grid row is horizontal even with unequal centre Y");
            cube.ResetForRoom(); Move(cube, new Vector3(3100.5f, 11.001f, 0));
            MovePlayer(new Vector3(3099, 10.999f, 0)); Click(cube, cube.transform.position); Step(7);
            Check(Vector3.Dot(cube.GrowthDirection, Vector3.right) > .999f, "one-percent-cell row-boundary tolerance handles solver jitter");
            cube.ResetForRoom();
            MovePlayer(cube.transform.position + new Vector3(-1.5f, -.03f, 0)); Click(cube, cube.transform.position); Step(7);
            Check(Vector3.Dot(cube.GrowthDirection, Vector3.down) > .999f, "a different row beyond tolerance takes vertical precedence");
            cube.enabled = false;
        }

        private static void VerifyRepeatedAndDerivedHits()
        {
            Vector3 root = new Vector3(3200.5f, 10.5f, 0);
            ContactCube cube = Spawn(CubeColor.Green, root);
            MovePlayer(root + Vector3.left * 1.5f); Click(cube, root); Step(3);
            Click(cube, root); Step(4);
            Check(cube.Length == 2, "repeated clicks do not reset the step timer");
            MovePlayer(root + new Vector3(1, 2, 0)); Click(cube, root + Vector3.right);
            Check(Vector3.Dot(cube.GrowthDirection, Vector3.right) > .999f, "hitting grown entity retains its existing axis");
            MovePlayer(root + Vector3.forward * 10); Step(30);
            Check(cube.Length == 4, "derived-body hit starts no duplicate controller and respects maximum");
            MovePlayer(root + Vector3.left * 1.5f); Click(cube, root, false); Step(10);
            Check(cube.Length == 4 && !cube.IsPlayerGrowthRequested, "maximum-length clicks cannot extend or queue a later request");
            ContactCube red = Spawn(CubeColor.Red, root + Vector3.down);
            Step(20); Check(cube.Length == 1, "ordinary red contact still fully retracts completed growth");
            Move(red, root + Vector3.down * 20);
            MovePlayer(root + Vector3.up * 1.5f); Click(cube, root);
            // Select the new axis from above, then leave its added cell clear.
            // Player-occupied-cell pausing is verified separately with real PhysX.
            MovePlayer(root + Vector3.forward * 10); Step(7);
            Check(cube.Length == 2 && Vector3.Dot(cube.GrowthDirection, Vector3.up) > .999f,
                "after fully retracting, next blue click can select a new axis");
            cube.enabled = red.enabled = false;
        }

        private static GameObject Wall(Vector3 center, Vector3 size, bool trigger = false)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(wall);
            wall.name = "Actual growth obstruction"; wall.transform.position = center; wall.transform.localScale = size;
            wall.GetComponent<Collider>().isTrigger = trigger;
            return wall;
        }

        private static void VerifyObstructions()
        {
            Vector3 root = new Vector3(3300.5f, 10.5f, 0);
            ContactCube cube = Spawn(CubeColor.Green, root);
            GameObject wall = Wall(root + Vector3.right * 2, new Vector3(.03f, 1, 1));
            GameObject floor = Wall(root + Vector3.down, new Vector3(10, 1, 2));
            GameObject trigger = Wall(root + Vector3.right, Vector3.one, true);
            MovePlayer(root + Vector3.left * 1.5f); Click(cube, root); Step(25);
            Check(cube.Length == 2 && cube.IsPlayerGrowthRequested, "thin real wall blocks added cell; floor contact and pure trigger do not");
            Check(cube.GetComponentInChildren<BoxCollider>().bounds.max.x < wall.GetComponent<Collider>().bounds.min.x,
                "growth never teleports its solid body across a wall");
            wall.SetActive(false); Step(20);
            Check(cube.Length == 4, "removing wall resumes pending single-click growth to maximum");
            cube.enabled = false; floor.SetActive(false); trigger.SetActive(false);
            foreach (Vector3 direction in new[] { Vector3.up, Vector3.down, Vector3.left })
            {
                root.x += 20;
                ContactCube blocked = Spawn(CubeColor.Green, root);
                GameObject obstruction = Wall(root + direction, Vector3.one);
                MovePlayer(root + (direction == Vector3.left ? Vector3.right : direction) * 2);
                Click(blocked, root); Step(20);
                Check(blocked.Length == 1, "real wall prevents first growth step " + direction);
                obstruction.SetActive(false); blocked.enabled = false;
            }
        }

        private static void VerifyContactCompetition()
        {
            Vector3 root = new Vector3(3500.5f, 10.5f, 0);
            ContactCube cube = Spawn(CubeColor.Green, root);
            ContactCube red = Spawn(CubeColor.Red, root + Vector3.down);
            MovePlayer(root + Vector3.left * 1.5f); Click(cube, root); Step(20);
            Check(cube.State == CubeReactionState.Locked && cube.Length == 1, "red contact locks a blue-click growth request");
            Move(red, root + Vector3.down * 20); Step(7);
            Check(cube.Length == 2, "removing red resumes requested growth without accumulating locked time");
            ContactCube blue = Spawn(CubeColor.Blue, root + Vector3.up);
            Move(red, root + Vector3.down); Step(20);
            Check(cube.State == CubeReactionState.Locked && cube.Length == 2, "real red-blue simultaneous contact still locks green");
            cube.ResetForRoom(); blue.enabled = false;
            Step(7); Check(cube.State == CubeReactionState.Shrinking && cube.Length == 1, "reset removes player growth and retains ordinary red-only shrinking");
            Move(red, root + Vector3.down * 20); blue.enabled = true; Step(7);
            Check(cube.State == CubeReactionState.Growing && cube.Length == 2 && Vector3.Dot(cube.GrowthDirection, Vector3.down) > .999f,
                "ordinary blue cube contact retains away-from-blue direction");
            cube.enabled = red.enabled = blue.enabled = false;
        }

        private static void VerifyGrowthPlayerEscape()
        {
            int index = 0;
            foreach (Vector3 direction in new[] { Vector3.up, Vector3.down })
            {
                Vector3 root = new Vector3(3420.5f + index++ * 30, 10.5f, 0);
                ContactCube cube = Spawn(CubeColor.Green, root);
                MovePlayer(root + direction * 1.5f);
                player.Motor.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
                player.Motor.useGravity = false;
                Click(cube, root); Step(7);
                Check(cube.Length == 1 && cube.IsPlayerGrowthRequested,
                    "player occupying the added cell safely pauses the pending growth step " + direction);
                Step(30);
                BoxCollider grown = cube.GetComponentInChildren<BoxCollider>();
                CapsuleCollider actor = player.GetComponent<CapsuleCollider>();
                bool overlap = Physics.ComputePenetration(grown, grown.transform.position, grown.transform.rotation,
                    actor, actor.transform.position, actor.transform.rotation, out _, out float penetration);
                Check(!overlap || penetration <= .04f,
                    "real physics leaves player outside the green solid while growth waits " + direction +
                    $" length={cube.Length} depth={penetration:F6} player={player.Motor.position} green={grown.bounds}" +
                    $" sleep={player.Motor.IsSleeping()} kinematic={player.Motor.isKinematic} constraints={player.Motor.constraints}" +
                    $" velocity={player.Motor.linearVelocity} collider={actor.enabled}/{actor.isTrigger} ignore={Physics.GetIgnoreCollision(grown,actor)}");
                Check(!Physics.GetIgnoreCollision(cube.GetComponentInChildren<BoxCollider>(), player.GetComponent<CapsuleCollider>()),
                    "player keeps real green collision throughout expansion");
                MovePlayer(root + direction * 5); Step(30);
                Check(cube.Length == cube.MaximumLength && !cube.IsPlayerGrowthRequested,
                    "player moving clear resumes the same click request to maximum " + direction);
                overlap = Physics.ComputePenetration(grown, grown.transform.position, grown.transform.rotation,
                    actor, actor.transform.position, actor.transform.rotation, out _, out penetration);
                Check(!overlap || penetration <= .04f, "resumed growth still leaves real player outside the solid " + direction);
                cube.enabled = false;
                cube.GetComponentInChildren<BoxCollider>().enabled = false;
                root.x += 10;
                ContactCube trapped = Spawn(CubeColor.Green, root);
                GameObject wall = Wall(root + direction * 2.5f, Vector3.one);
                MovePlayer(root + direction * 1.5f);
                Click(trapped, root); Step(20);
                Check(trapped.Length == 1 && trapped.IsPlayerGrowthRequested,
                    "wall behind player safely pauses growth instead of trapping or pushing player through it " + direction);
                Check(Vector3.Distance(player.Motor.position, root + direction * 1.5f) < .04f,
                    "blocked growth leaves real player outside both cube and wall " + direction);
                wall.SetActive(false); Step(20);
                Check(trapped.Length == 1, "clearing the wall alone cannot grow through an occupying player " + direction);
                MovePlayer(root + Vector3.forward * 5); Step(20);
                Check(trapped.Length > 1, "player moving clear resumes pending growth " + direction);
                trapped.enabled = false;
                trapped.GetComponentInChildren<BoxCollider>().enabled = false;
                player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                player.Motor.useGravity = true;
            }
        }

        private static void VerifyGatesAndCleanup()
        {
            Vector3 root = new Vector3(3600.5f, 10.5f, 0);
            ContactCube cube = Spawn(CubeColor.Green, root);
            var target = cube.GetComponent<BlueContactCubeGrowthTarget>();
            MovePlayer(root + Vector3.left * 1.5f);
            Check(!target.ApplyColor("blue", player.gameObject), "contact/universal broadcast entry cannot start click growth");
            Check(!target.CanApplyColor("red", player.gameObject) && !target.CanApplyColor("green", player.gameObject), "only blue ability is accepted");
            Check(!target.CanApplyColor("blue", cube.gameObject), "non-player actors cannot create click growth requests");
            wheel.ClearSelectedColor(); Click(cube, root, false);
            Check(!target.CanApplyColor("blue", player.gameObject), "no selected ability is rejected");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("green", false);
            wheel.TrySelectColor("blue"); Click(cube, root, false);
            Check(!cube.IsPlayerGrowthRequested, "locked green scene color cannot grow");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("green", true);
            Click(cube, root); Step(7); ColorRuntimeService.Instance.SetUnlockedForCurrentSession("green", false); Step(1);
            Check(!cube.IsPlayerGrowthRequested && cube.State == CubeReactionState.Idle, "locking scene color clears pending growth");
            ColorRuntimeService.Instance.SetUnlockedForCurrentSession("green", true);
            Click(cube, root); target.enabled = false;
            Check(!cube.IsPlayerGrowthRequested, "disabling receiver immediately cancels its request"); target.enabled = true;
            Click(cube, root); cube.SetCarrySuspended(true);
            Check(!cube.IsPlayerGrowthRequested && !target.CanApplyColor("blue", player.gameObject), "carry suspension cancels and gates growth");
            cube.SetCarrySuspended(false); Click(cube, root); cube.SetHookSuspended(true);
            Check(!cube.IsPlayerGrowthRequested, "hook suspension clears click request"); cube.SetHookSuspended(false);
            Click(cube, root); cube.enabled = false;
            Check(!cube.IsPlayerGrowthRequested && cube.State == CubeReactionState.Idle, "disabling cube immediately clears growth state");
            cube.enabled = true;
            using (player.AcquireControlLock(cube)) Check(!target.CanApplyColor("blue", player.gameObject), "player control lock gates direct entry");
            ContactCube red = Spawn(CubeColor.Red, root + Vector3.up * 3);
            ContactCube blue = Spawn(CubeColor.Blue, root + Vector3.down * 3);
            Check(red.GetComponent<BlueContactCubeGrowthTarget>() == null && blue.GetComponent<BlueContactCubeGrowthTarget>() == null,
                "red and blue cubes receive no player application receiver");
            cube.enabled = red.enabled = blue.enabled = false;
            ColorRuntimeService.Instance.ResetProgress();
            Check(!wheel.TrySelectColor("blue") && !target.CanApplyColor("blue", player.gameObject), "ability lock remains separate from scene unlock");
            ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
            ColorRuntimeService.Instance.UnlockAllAbilitiesForCurrentSession(); wheel.TrySelectColor("blue");
        }

        private static void VerifyRoomReset()
        {
            Vector3 root = new Vector3(3700.5f, 10.5f, 0);
            var go = new GameObject("Growth audit rooms"); objects.Add(go);
            RoomLifecycleManager manager = go.AddComponent<RoomLifecycleManager>();
            var first = new RoomRegion { id = "growth" }; var second = new RoomRegion { id = "next" };
            for (int x = 3690; x < 3730; x++) for (int y = 0; y < 30; y++)
                (x < 3720 ? first : second).cells.Add(new Vector2Int(x, -y - 1));
            manager.Configure(new List<RoomRegion> { first, second }, 1f, "");
            var managerSettings = new SerializedObject(manager);
            managerSettings.FindProperty("player").objectReferenceValue = player;
            managerSettings.ApplyModifiedPropertiesWithoutUndo();
            MovePlayer(root + Vector3.left * 1.5f); manager.UpdatePlayerRegion(player);
            ContactCube cube = Spawn(CubeColor.Green, root);
            cube.GetComponent<RoomResetObject>().Bind(manager, "growth"); manager.Register(cube.GetComponent<RoomResetObject>());
            Wall(new Vector3(3710, 8.5f, 0), new Vector3(40, 1, 3));
            Click(cube, root); Step(7); Check(cube.Length == 2, "growth exists before real respawn chain");
            player.RequestRespawn();
            Check(cube.Length == 1 && !cube.IsPlayerGrowthRequested && cube.State == CubeReactionState.Idle,
                "real R entry restores author length and clears click growth");
            cube.GetComponent<ContactCubeMotion>().enabled = false; cube.GetComponent<Rigidbody>().isKinematic = true;
            MovePlayer(root + Vector3.left * 1.5f); manager.UpdatePlayerRegion(player); Click(cube, root); Step(7);
            MovePlayer(new Vector3(3725.5f, 10.5f, 0)); manager.UpdatePlayerRegion(player);
            Check(cube.Length == 1 && !cube.IsPlayerGrowthRequested, "leaving region restores green author state");
            MovePlayer(root + Vector3.left * 1.5f); manager.UpdatePlayerRegion(player); Click(cube, root); Step(7);
            player.EnterDeath();
            Check(!cube.GetComponent<BlueContactCubeGrowthTarget>().CanApplyColor("blue", player.gameObject), "dead player cannot request growth");
            player.RequestRespawn();
            Check(!player.IsDead && cube.Length == 1 && !cube.IsPlayerGrowthRequested, "death and real respawn reset request and length");
            Check(ColorRuntimeService.Instance.IsUnlocked("green") && ColorRuntimeService.Instance.IsAbilityUnlocked("blue"),
                "R/death/region reset preserve both color progress stages");
            report.AppendLine("Directions: 4 x 3 grid sizes; real screen hit, grown hit, step timing, walls, contact competition, gates, R/death/region reset.");
        }

        private static void Cleanup()
        {
            for (int index = objects.Count - 1; index >= 0; index--)
                if (objects[index] != null) Object.DestroyImmediate(objects[index]);
            objects.Clear(); cubes.Clear(); player = null; wheel = null; camera = null;
        }
    }

    public sealed class BlueGrowthAuditGrid : MonoBehaviour, IGridCellSizeProvider
    {
        public float Size = 1f;
        public float CellWorldSize => Size;
    }
}
