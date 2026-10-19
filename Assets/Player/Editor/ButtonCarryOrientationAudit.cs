using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Player.Editor
{
    /// <summary>Real carry commands, pressure physics and four-way native artwork; never saves author scenes.</summary>
    public static class ButtonCarryOrientationAudit
    {
        private const string Flag = "ButtonCarryOrientationAudit.Running";
        private const string GamingFlag = "ButtonCarryOrientationAudit.Gaming";
        private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        private static PlayerController player;
        private static CarryableFeature carried;
        private static ContactCube cube;
        private static DoorButton button;
        private static int phase, checks;
        private static double started;
        private static float ready;
        private static Vector3 destination;
        private static bool initialized;

        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                !Array.Exists(Environment.GetCommandLineArgs(), value => value == "-noaudio"))
                throw new InvalidOperationException("Use the silent isolated verification project only.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            SessionState.SetBool(GamingFlag, false);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }

        public static void RunGamingProbe()
        {
            if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
                !Array.Exists(Environment.GetCommandLineArgs(), value => value == "-noaudio"))
                throw new InvalidOperationException("Use the silent isolated verification project only.");
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Playing/Gaming.unity");
            foreach (var key in Object.FindObjectsByType<ColorKeyPickup>(FindObjectsSortMode.None)) key.gameObject.SetActive(false);
            SessionState.SetBool(GamingFlag, true);
            SessionState.SetBool(Flag, true); Resume(); EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod] private static void Resume()
        {
            if (!SessionState.GetBool(Flag, false)) return;
            phase = checks = 0; initialized = false; started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("BUTTON CARRY: " + description);
            checks++; Debug.Log("BUTTON CARRY PASS: " + description);
        }

        private static GameObject Spawn(string path, Vector3 position) => Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/" + path), position, Quaternion.identity);
        private static void Move(Transform target, Vector3 position)
        {
            target.position = position;
            if (target.TryGetComponent<Rigidbody>(out var body))
            {
                body.position = position;
                if (!body.isKinematic) body.linearVelocity = Vector3.zero;
            }
            Physics.SyncTransforms();
        }
        private static object Call(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, Hidden).Invoke(target, args);
        private static bool CellFree(Vector3Int cell) => (bool)Call(player, "IsCarryDropCellFree", cell);

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup - started > 80) { Finish(false, "Timed out"); return; }
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || Time.time < .2f) return;
            if (initialized && Time.time < ready) return;
            try
            {
                if (phase == 0)
                {
                    if (SessionState.GetBool(GamingFlag, false))
                    {
                        var flow = Project.GameFlow.GameFlowController.Instance;
                        if (flow == null || !flow.IsInitialized || flow.IsTransitioning) return;
                        flow.EnterGameplayImmediatelyForDevelopment();
                    }
                    initialized = true;
                    ColorRuntimeService.Instance.SetAllUnlockedForCurrentSession(true);
                    if (SessionState.GetBool(GamingFlag, false))
                    {
                        player = Object.FindFirstObjectByType<PlayerController>();
                        player.GetComponent<PlayerInputDriver>().enabled = false;
                        ready = Time.time + .8f; phase = 10; return;
                    }
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.name = "Audit floor"; floor.transform.position = new Vector3(10, 9.5f, 0);
                    floor.transform.localScale = new Vector3(20, 1, 3);
                    player = Spawn("Player/Player.prefab", new Vector3(10.5f, 10.5f, 0)).GetComponent<PlayerController>();
                    player.GetComponent<PlayerInputDriver>().enabled = false;
                    player.Motor.interpolation = RigidbodyInterpolation.None;
                    cube = Spawn("ContactCubes/Red_ContactCube.prefab", new Vector3(9.5f, 10.5f, 0)).GetComponent<ContactCube>();
                    carried = cube.GetComponent<CarryableFeature>();
                    destination = new Vector3(11.5f, 10.5f, 0);
                    button = Spawn("Mechanisms/DoorButton.prefab", destination).GetComponent<DoorButton>();
                    var door = Spawn("Mechanisms/Door.prefab", new Vector3(16.5f, 10.5f, 0)).GetComponent<DoorController>();
                    door.SetButtons(new[] { button }); button.SetOwner(door);
                    var placed = button.gameObject.AddComponent<LevelEditorPlacedBlock>();
                    placed.Configure(new Vector2Int(11, 10), "audit button", Color.white, false, true);
                    Check(button.Facing == ButtonFacing.Up, "old/new button default remains up without scene migration");
                    ready = Time.time + .5f; phase++;
                }
                else if (phase == 1)
                {
                    Move(player.transform, new Vector3(10.5f, 10.5f, 0));
                    Move(cube.transform, new Vector3(9.5f, 10.5f, 0));
                    player.SetMoveInput(Vector2.left); player.SetMoveInput(Vector2.zero);
                    player.UpdateCarryInput(true, false, false, false);
                    ready = Time.time + .85f; phase++;
                }
                else if (phase == 2)
                {
                    Check(player.IsCarrying && carried.IsCarryActive, "real F picks up the gravity-enabled red ContactCube");
                    player.SetMoveInput(Vector2.right); player.SetMoveInput(Vector2.zero);
                    Check(CellFree(new Vector3Int(11, 10, 0)), "button author record and pressure trigger permit sharing its cell");
                    Check(carried.CanPlaceCarryAt(player, destination), "full cube bounds / motion participant accept the same button cell");
                    // Simulate an old collider override being restored before
                    // the sensor's next physics step. Only this exact volume
                    // is non-solid; real descendants are not exempted.
                    button.GetComponent<BoxCollider>().isTrigger = false;
                    Physics.SyncTransforms();
                    Check(CellFree(new Vector3Int(11, 10, 0)) && carried.CanPlaceCarryAt(player, destination),
                        "stale sensor collider flags cannot reject an otherwise valid weighted drop");
                    Call(button, "FixedUpdate");
                    Check(button.GetComponent<BoxCollider>().isTrigger, "button restores its non-solid pressure invariant");
                    Check(player.TryGetCarryPlacementPreview(out var bounds, out bool valid) && valid &&
                        Vector3.Distance(bounds.center, destination) < .03f, "preview is valid at the identical real destination");
                    player.UpdateCarryInput(false, false, true, false);
                    Check(carried.IsCarryDropAnimating, "real E starts drop onto the button");
                    ready = Time.time + .7f; phase++;
                }
                else if (phase == 3)
                {
                    player.UpdateCarryInput(false, false, false, false);
                    Check(!player.IsCarrying && !carried.IsCarryActive, "drop completes rather than returning to the head");
                    Check(Vector3.Distance(cube.transform.position, destination) < .08f && cube.GetComponent<Rigidbody>().useGravity,
                        "placed cube remains gravity-enabled in the button cell");
                    Check(button.IsPressed, "the dropped cube actually presses the existing button");
                    VerifyDirections(); VerifyBlockedPlacement(); VerifyColorButtonResetPolicy();
                    Finish(true, "BUTTON CARRY ORIENTATION AUDIT PASSED " + checks);
                }
                else if (phase == 10)
                {
                    VerifyGamingButtons();
                    Finish(true, "GAMING BUTTON PLACEMENT PROBE COMPLETED " + checks);
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        private static void VerifyDirections()
        {
            Move(player.transform, new Vector3(30.5f, 10.5f, 0));
            cube.GetComponent<Rigidbody>().isKinematic = true;
            foreach (ButtonFacing facing in Enum.GetValues(typeof(ButtonFacing)))
            {
                using var serialized = new SerializedObject(button);
                var property = serialized.FindProperty("facing");
                property.intValue = (int)facing;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ButtonPressureGeometry.Configure(button.GetComponent<BoxCollider>(), facing);
                button.ResetButton(); Move(cube.transform, destination);
                Call(button, "FixedUpdate");
                Check(button.IsPressed, facing + " accepts the cube face directed toward its edge");
                button.ResetButton();
                Move(cube.transform, destination - ButtonPressureGeometry.Normal(facing) * .7f);
                Call(button, "FixedUpdate");
                Check(!button.IsPressed, facing + " rejects the opposite cube face merely crossing its sensor");
                Move(cube.transform, destination);

                Check(typeof(DoorButton).GetField("facing", Hidden).GetCustomAttribute<BlockParameterAttribute>() != null,
                    facing + " is exposed as an existing per-instance parameter");
                Check(LevelEditorComponentOverrideUtility.TryCapture(button, property, out var captured),
                    facing + " captures through the existing planning override utility");
                var restored = Spawn("Mechanisms/DoorButton.prefab", new Vector3(40, 10, 0));
                var copy = JsonUtility.FromJson<LevelEditorComponentValueOverride>(JsonUtility.ToJson(captured));
                LevelEditorComponentOverrideUtility.ApplyOverrides(restored, new[] { copy }, false);
                Check(restored.GetComponent<DoorButton>().Facing == facing, facing + " survives planning JSON round-trip");
                Check(ObjectArtworkPlanningPreview.TryGet(restored, Vector2.one, null, out var art) &&
                    Mathf.Abs(Mathf.DeltaAngle(art.RotationDegrees, ButtonPressureGeometry.RotationDegrees(facing))) < .01f &&
                    Vector2.Distance(art.Cells, Vector2.one) < .001f, facing + " preview rotates one full 16x16 native cell");
                var presentation = MechanismArtworkPresentation.Ensure(restored);
                presentation.Refresh(0);
                var sprite = presentation.Visual;
                Check(sprite != null && Vector2.Distance(new Vector2(sprite.bounds.size.x, sprite.bounds.size.y), Vector2.one) < .001f &&
                    Vector3.Distance(sprite.bounds.center, restored.transform.position + Vector3.forward * sprite.transform.localPosition.z) < .005f,
                    facing + " runtime sprite remains centred with square native pixels");
                Object.Destroy(restored);
            }
            using var defaults = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/ColorBlocks/Prefabs/Red_Button.prefab").GetComponent<ButtonLockFeature>());
            Check(defaults.FindProperty("facing").intValue == 0 &&
                typeof(ButtonLockFeature).GetField("facing", Hidden).GetCustomAttribute<BlockParameterAttribute>() != null,
                "color button shares the same four-way parameter and compatible default");
        }

        private static void VerifyBlockedPlacement()
        {
            Move(cube.transform, new Vector3(50.5f, 10.5f, 0));
            Move(player.transform, new Vector3(10.5f, 10.5f, 0));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = destination + Vector3.right * .45f;
            wall.transform.localScale = new Vector3(.02f, 1, 1);
            wall.transform.SetParent(button.transform, true);
            Physics.SyncTransforms();
            Check(!CellFree(new Vector3Int(11, 10, 0)), "a thin actual wall still rejects the button cell");
            Object.DestroyImmediate(wall);
            Physics.SyncTransforms();
            Check(CellFree(new Vector3Int(11, 10, 0)), "removing only the wall immediately restores valid button occupancy");
        }

        private static void VerifyColorButtonResetPolicy()
        {
            Move(player.transform, new Vector3(30.5f, 10.5f, 0));
            cube.GetComponent<Rigidbody>().isKinematic = true;
            foreach (ButtonFacing facing in Enum.GetValues(typeof(ButtonFacing)))
            foreach (DoorButtonMode mode in Enum.GetValues(typeof(DoorButtonMode)))
            {
                var instance = Spawn("ColorBlocks/Prefabs/Red_Button.prefab", new Vector3(60.5f, 10.5f, 0));
                try
                {
                    var feature = instance.GetComponent<ButtonLockFeature>();
                    var host = instance.GetComponent<BlockAbilityHost>();
                    using var serialized = new SerializedObject(feature);
                    serialized.FindProperty("mode").intValue = (int)mode;
                    serialized.FindProperty("facing").intValue = (int)facing;
                    serialized.FindProperty("startsOn").boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    host.RefreshActivation(true);
                    Call(feature, "FixedUpdate");
                    Check(feature.IsAttached, mode + " " + facing + " uses the real active color feature host");
                    var member = instance.AddComponent<Project.Rooms.RoomResetObject>();
                    member.Bind(null, "button-reset-fixture");
                    Check(member.Persistent == (mode == DoorButtonMode.Latched) && feature.Persistent == member.Persistent,
                        mode + " " + facing + " room persistence respects press mode");
                    Move(cube.transform, instance.transform.position);
                    Call(feature, "FixedUpdate");
                    Check(feature.IsSignalActive, mode + " " + facing + " real pressure cube activates color button");
                    Move(cube.transform, new Vector3(65.5f, 10.5f, 0));
                    host.ResetForRoom();
                    Check(feature.IsSignalActive == (mode == DoorButtonMode.Latched),
                        mode + " " + facing + " direct host reset clears held pressure and preserves latched progress");
                    Move(cube.transform, instance.transform.position);
                    Call(feature, "FixedUpdate");
                    Check(feature.IsSignalActive, mode + " " + facing + " pressure can reactivate before real room reset");
                    Move(cube.transform, new Vector3(65.5f, 10.5f, 0));
                    member.RestorePose(); member.RestoreState();
                    Check(feature.IsSignalActive == (mode == DoorButtonMode.Latched),
                        mode + " " + facing + " room R reset dispatch releases Held and retains Latched");
                    Check(feature.Facing == facing && instance.GetComponent<BoxCollider>().isTrigger,
                        mode + " " + facing + " room reset retains pressure orientation and non-solid sensor");
                }
                finally { Object.DestroyImmediate(instance); }
            }
        }

        private static void VerifyGamingButtons()
        {
            Physics.simulationMode = SimulationMode.Script;
            var hits = new Collider[128];
            var buttons = Object.FindObjectsByType<DoorButton>(FindObjectsSortMode.None);
            Check(buttons.Length > 0, "formal Gaming contains authored door buttons");
            foreach (var candidate in buttons)
            {
                Vector3 target = candidate.transform.position;
                var cell = new Vector3Int(Mathf.FloorToInt(target.x), Mathf.FloorToInt(target.y), 0);
                var placed = candidate.GetComponent<LevelEditorPlacedBlock>();
                var member = candidate.GetComponent<Project.Rooms.RoomResetObject>();
                Debug.Log("GAMING BUTTON: " + candidate.name + " id=" + (placed != null ? placed.PlanningBoxId : "manual") +
                    " position=" + target.ToString("F3") + " cell=" + cell + " region=" + (member != null ? member.RegionId : "none") +
                    " pressure=" + candidate.GetComponent<BoxCollider>().isTrigger);
                foreach (int side in new[] { -1, 1 })
                {
                    Move(player.transform, new Vector3(cell.x + .5f + side, cell.y + .5f, target.z));
                    bool gridBlocked = (bool)Call(player, "IsCarryCellOccupiedByPlacedBlock", cell);
                    bool rayBlocked = (bool)Call(player, "HasCarryBlockerInDirection", side < 0 ? Vector3Int.right : Vector3Int.left);
                    bool free = CellFree(cell);
                    Debug.Log("GAMING BUTTON QUERY: " + (side < 0 ? "from left" : "from right") + " gridBlocked=" + gridBlocked +
                        " rayBlocked=" + rayBlocked + " cellFree=" + free);
                    int count = Physics.OverlapBoxNonAlloc(new Vector3(cell.x + .5f, cell.y + .5f, target.z), Vector3.one * .49f,
                        hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < count; i++)
                    {
                        var hit = hits[i];
                        if (hit.transform.IsChildOf(player.transform)) continue;
                        var author = hit.GetComponentInParent<LevelEditorPlacedBlock>();
                        Debug.Log("GAMING BUTTON SOLID: " + hit.name + " type=" + hit.GetType().Name +
                            " bounds=" + hit.bounds + " author=" + (author != null ? author.PlanningBoxId : "none") +
                            " parent=" + (hit.transform.parent != null ? hit.transform.parent.name : "none"));
                    }
                }
                checks++;
            }
        }

        private static void Finish(bool success, string message)
        {
            EditorApplication.update -= Tick; SessionState.SetBool(Flag, false);
            File.WriteAllText(Path.Combine(Application.dataPath, "../Library/ButtonCarryOrientationAudit.txt"), message);
            if (success) Debug.Log(message); else Debug.LogError(message);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
