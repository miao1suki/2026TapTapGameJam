using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.Mechanisms;
using Project.LevelEditor.Editor;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.Rooms.Editor
{
    public static class RoomIntegrationVerification
    {
        private const string Folder = "Assets/_Project/Development/RoomLifecycleTests";
        private const string ScenePath = Folder + "/RoomRespawnTest.unity";
        private const string Prefabs = "Assets/_Project/Content/ColorBlocks/Prefabs/";
        private static int checks;
        private static int phase;
        private static double started;
        private static int nextFrame;
        private static readonly List<string> errors = new List<string>();

        private static void Check(bool valid, string message)
        {
            checks++;
            if (!valid) throw new InvalidOperationException(message);
        }

        // Intended for a disposable verification project, not the user's open scene.
        public static void BuildAndVerify()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe"))
                throw new InvalidOperationException("验证入口仅用于隔离项目，不覆盖当前工作场景。");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            string source = File.ReadAllText(Folder + "/MVMapSource.mvmap.json");
            var imported = PlanningMVMapImporter.Read(source);
            Check(imported.rooms.Count > 0, "MVMap room count");
            Check(imported.rooms.Sum(r => r.cells.Count) > 0, "MVMap cell count");
            var roundTrip = PlanningDocument.FromPersistentJson(imported.ToPersistentJson());
            foreach (var room in imported.rooms)
            {
                var copy = roundTrip.FindRoom(room.id);
                Check(copy != null && copy.name == room.name && copy.sourceColorHex == room.sourceColorHex, "room identity");
                var cells = new HashSet<Vector2Int>(copy.cells.Select(c => new Vector2Int(c.x, c.y)));
                foreach (var cell in room.cells) Check(cells.Contains(new Vector2Int(cell.x, cell.y)), "round-trip geometry");
            }
            BuildMap(imported);
            BuildHarness();
            VerifySceneBuilder();
            File.WriteAllText("Library/RoomImportVerification.txt", $"PASS {checks} assertions; imported {imported.rooms.Count} regions / {imported.rooms.Sum(r => r.cells.Count)} cells; two scenes saved.");
            AssetDatabase.SaveAssets();
        }

        private static void VerifySceneBuilder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var doc = new PlanningDocument { name = "应用验证" };
            AddRegion(doc, "builder_room", "验证房间", new RectInt(0, 0, 8, 4), false);
            var entry = LevelEditorPaletteService.GetOrCreate().PropEntries.First(e => e.Prefab != null && e.Prefab.GetComponent<RespawnAnchorFeature>() != null);
            var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(2, 1, 1, 1))
                { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
            doc.rooms[0].boxes.Add(box);
            PlanningSceneBuilder.SuppressSceneUndo = true;
            try
            {
                Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), "scene apply: " + message);
                var anchor = Object.FindFirstObjectByType<RespawnAnchorFeature>();
                Check(anchor != null && anchor.RegionId == "builder_room" && !string.IsNullOrEmpty(anchor.AnchorId), "builder binds stable checkpoint identity");
                var member = anchor.GetComponent<RoomResetObject>();
                Check(member != null && member.RegionId == "builder_room", "builder reset membership");
                var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
                Check(manager.Regions.Count == 1 && manager.FindRegion(anchor.transform.position) == "builder_room" && !string.IsNullOrEmpty(manager.PlanningJson), "builder publishes layout and planning source");
                string id = anchor.AnchorId;
                Check(PlanningSceneBuilder.TryApplyDocument(doc, out message), "second incremental apply");
                anchor = Object.FindFirstObjectByType<RespawnAnchorFeature>();
                Check(anchor.AnchorId == id && Object.FindObjectsByType<RespawnAnchorFeature>(FindObjectsSortMode.None).Length == 1, "incremental preserves checkpoint identity");
            }
            finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        }

        private static RoomLifecycleManager CreateManager(PlanningDocument document)
        {
            var regions = new List<RoomRegion>();
            foreach (var room in document.rooms)
                regions.Add(new RoomRegion { id = room.id, label = room.name, corridor = room.isConnector,
                    cells = room.cells.Select(c => new Vector2Int(c.x, c.y)).ToList() });
            var manager = new GameObject("__PlanningMapGenerated").AddComponent<RoomLifecycleManager>();
            manager.Configure(regions, 1, document.ToPersistentJson());
            foreach (var region in regions)
                foreach (var cell in region.cells)
                    Check(manager.FindRegion(new Vector3(cell.x + .5f, -cell.y - .5f, 0)) == region.id, "runtime world ownership");
            return manager;
        }

        private static void BuildMap(PlanningDocument document)
        {
            var converted = PlanningMVMapObjectImporter.Read(File.ReadAllText(Folder + "/MVMapSource.mvmap.json"),
                LevelEditorPaletteService.GetOrCreate(), out _);
            MVMapObjectImportVerification.BuildScene(converted);
        }

        private static void BuildHarness()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var document = new PlanningDocument { name = "区域重生测试" };
            AddRegion(document, "test_room_a", "测试房间 A", new RectInt(0, 0, 18, 8), false);
            AddRegion(document, "test_corridor", "测试通道", new RectInt(18, 5, 6, 3), true);
            AddRegion(document, "test_room_b", "测试房间 B", new RectInt(24, 0, 18, 8), false);
            CreateManager(document);
            MakeSolid("地面 A", new Vector3(9, -7.5f, 0), new Vector3(18, 1, 1));
            MakeSolid("通道地面", new Vector3(21, -7.5f, 0), new Vector3(6, 1, 1));
            MakeSolid("地面 B", new Vector3(33, -7.5f, 0), new Vector3(18, 1, 1));
            var player = MakePlayer(new Vector3(3.5f, -6, 0));
            MakeAnchor("重生点 A1", new Vector3(3.5f, -6.5f, .2f), "test_room_a");
            MakeAnchor("重生点 A2", new Vector3(15.5f, -6.5f, .2f), "test_room_a");
            MakeAnchor("通道重生点", new Vector3(21.5f, -6.5f, .2f), "test_corridor");
            MakeAnchor("重生点 B", new Vector3(37.5f, -6.5f, .2f), "test_room_b");
            MakeItem("Universal_Block", "可搬运测试块 A", new Vector3(6.5f, -6.5f, 0));
            MakeItem("Universal_Block", "可搬运测试块 B", new Vector3(30.5f, -6.5f, 0));
            MakeItem("Red_Platform", "未解锁机关测试", new Vector3(34.5f, -6.5f, 0));
            MakeItem("Blue_WaterSource", "水体测试", new Vector3(9.5f, -6.5f, 0));
            var vine = MakeItem("Green_Ladder", "藤蔓母根", new Vector3(12.5f, -6.5f, 0)).GetComponent<ClimbableVineFeature>();
            var segments = new List<GameObject>();
            for (int i = 1; i <= 2; i++)
            {
                var segment = MakeItem("Green_LadderSon", "藤蔓节段 " + i, vine.transform.position + Vector3.up * i);
                segment.transform.SetParent(vine.transform, true);
                segments.Add(segment);
            }
            vine.ConfigureInitialGrowth(segments);
            PrefabUtility.RecordPrefabInstancePropertyModifications(vine);
            var obstacle = MakeItem("Green_PlantObstacle", "植物障碍", new Vector3(28.5f, -6.5f, 0));
            obstacle.GetComponent<RoomResetObject>().SetRegion("test_room_b");
            MakeItem("ColorKey_blue", "蓝色钥匙", new Vector3(8.5f, -4.5f, 0));
            MakeItem("ColorKey_green", "绿色钥匙", new Vector3(13.5f, -4.5f, 0));
            var door = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/Door.prefab"));
            door.name = "测试门"; door.transform.position = new Vector3(26.5f, -7, 0);
            door.AddComponent<RoomResetObject>();
            var buttons = new List<DoorButton>();
            for (int i = 0; i < 2; i++)
            {
                var button = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/DoorButton.prefab"));
                button.name = "测试按钮 " + (i + 1); button.transform.position = new Vector3(19.5f + i * 3, -7, 0);
                button.AddComponent<RoomResetObject>(); buttons.Add(button.GetComponent<DoorButton>());
            }
            door.GetComponent<DoorController>().SetButtons(buttons);
            PrefabUtility.RecordPrefabInstancePropertyModifications(door.GetComponent<DoorController>());
            foreach (var button in buttons) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            MakeCamera(player.transform);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void AddRegion(PlanningDocument doc, string id, string name, RectInt bounds, bool corridor)
        {
            var region = new PlanningRoom(name) { id = id, isConnector = corridor, independentCells = corridor };
            for (int x = bounds.x; x < bounds.xMax; x++) for (int y = bounds.y; y < bounds.yMax; y++) region.cells.Add(new PlanningCell(x, y));
            doc.rooms.Add(region);
        }
        private static GameObject MakeItem(string prefab, string name, Vector3 point)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + prefab + ".prefab");
            Check(source != null, "prefab exists: " + prefab);
            var item = (GameObject)PrefabUtility.InstantiatePrefab(source);
            item.name = name; item.transform.position = point;
            item.AddComponent<RoomResetObject>(); return item;
        }
        private static void MakeAnchor(string name, Vector3 point, string owner)
        {
            var item = MakeItem("Respawn_Block", name, point);
            var anchor = item.GetComponent<RespawnAnchorFeature>();
            anchor.EnsureRuntimeAnchorId(); anchor.SetRegion(owner);
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("activationRadiusBlocks").floatValue = 1.5f;
            serialized.FindProperty("respawnHeightBlocks").floatValue = .5f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(anchor);
        }
        private static GameObject MakeSolid(string name, Vector3 point, Vector3 scale)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = name; floor.transform.position = point; floor.transform.localScale = scale;
            floor.AddComponent<RoomResetObject>(); return floor;
        }
        private static PlayerController MakePlayer(Vector3 point)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/RuntimePlayer.prefab");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab); item.name = "TestPlayer"; item.transform.position = point;
            return item.GetComponent<PlayerController>();
        }
        private static void MakeCamera(Transform player)
        {
            var item = new GameObject("Main Camera"); item.tag = "MainCamera";
            var camera = item.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .15f, .19f); item.AddComponent<AudioListener>();
            var manager = item.AddComponent<CameraControlManager>(); manager.ConfigureOutput(camera);
            var mode = item.AddComponent<CameraModeController>(); mode.Configure(camera, player);
            var follow = item.AddComponent<CameraFollowController>(); follow.Configure(manager, mode, player);
            follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
        }

        public static void RunPlayVerification()
        {
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("RoomVerification.Running", true);
            checks = 0; phase = 0; errors.Clear();
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod]
        private static void ResumeVerification()
        {
            if (!SessionState.GetBool("RoomVerification.Running", false)) return;
            checks = 0; phase = 0; nextFrame = 0; errors.Clear(); started = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick; EditorApplication.update += Tick;
            Application.logMessageReceived -= OnLog; Application.logMessageReceived += OnLog;
        }
        private static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) &&
                !stack.Contains("UnityEditor.Search.SearchDatabase")) errors.Add(message + "\n" + stack);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - started > 120) throw new TimeoutException("play verification timeout");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (Time.frameCount < nextFrame) return;
                var player = Object.FindFirstObjectByType<PlayerController>();
                var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
                if (player == null || manager == null || Time.time < .5f) return;
                player.GetComponent<PlayerInputDriver>().enabled = false;
                player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                player.Motor.interpolation = RigidbodyInterpolation.None;
                if (phase == 0)
                {
                    Check(manager.CurrentRegionId == "test_room_a", "initial owner");
                    Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out var anchor) &&
                        ((Component)anchor).name == "重生点 A1", "initial checkpoint activation");
                    player.transform.position = player.Motor.position = new Vector3(15.5f, -6, 0); phase++; nextFrame = Time.frameCount + 5; return;
                }
                if (phase == 1)
                {
                    var current = GameObject.Find("重生点 A2").GetComponent<RespawnAnchorFeature>();
                    var old = GameObject.Find("重生点 A1").GetComponent<RespawnAnchorFeature>();
                    Check(current.IsCurrent && !old.IsCurrent && old.IsActivated, "current checkpoint separate from visited history");
                    Check(current.TargetRenderer.sharedMaterial == current.ActivatedMaterial &&
                        old.TargetRenderer.sharedMaterial == old.InactiveMaterial, "only current checkpoint uses current material");
                    Check(Mathf.Approximately(current.ActivationRadiusBlocks, 1.5f), "expanded checkpoint radius");
                    Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out var anchor) &&
                        ((Component)anchor).name == "重生点 A2", "latest checkpoint activation: player=" + player.transform.position + "; point=" + (anchor as Component)?.name);
                    var a = GameObject.Find("可搬运测试块 A"); var b = GameObject.Find("可搬运测试块 B");
                    a.transform.position += Vector3.up; b.transform.position += Vector3.up;
                    ColorRuntimeService.Instance.Unlock("blue"); ColorRuntimeService.Instance.Unlock("green");
                    manager.ResetRegion("test_room_a");
                    Check(a.transform.position == new Vector3(6.5f, -6.5f, 0), "authored position restored");
                    Check(b.transform.position == new Vector3(30.5f, -5.5f, 0), "other room left untouched");
                    Check(RoomResetObject.HideForReaction(a, new GameObject("临时替代物")) && !a.activeSelf, "authored object hidden for reaction");
                    manager.ResetRegion("test_room_a");
                    Check(a.activeSelf && a.transform.position == new Vector3(6.5f, -6.5f, 0), "hidden authored object restored");
                    Check(ColorRuntimeService.Instance.IsUnlocked("blue") && ColorRuntimeService.Instance.IsUnlocked("green"), "unlock preserved");
                    var water = GameObject.Find("水体测试").GetComponent<ColorObject>();
                    Check(water.IsActive, "water stays unlocked");
                    var vine = GameObject.Find("藤蔓母根").GetComponent<ClimbableVineFeature>();
                    Check(vine.CurrentGrowthBlocks == 2, "authored vine links survive reset");
                    player.transform.position = player.Motor.position = new Vector3(21.5f, -6, 0); manager.UpdatePlayerRegion(player);
                    Check(manager.CurrentRegionId == "test_corridor", "corridor recognized");
                    phase++; nextFrame = Time.frameCount + 3; return;
                }
                if (phase == 2)
                {
                    Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out var anchor) &&
                        ((Component)anchor).name == "通道重生点", "corridor checkpoint");
                    foreach (var button in Object.FindObjectsByType<DoorButton>(FindObjectsSortMode.None)) button.TryPress(player.gameObject);
                    manager.ResetRegion("test_corridor");
                    Check(Object.FindObjectsByType<DoorButton>(FindObjectsSortMode.None).All(b => b.IsPressed), "buttons persistent");
                    Check(Object.FindFirstObjectByType<DoorController>().State == DoorState.Unlocked, "door persistent");
                    player.transform.position = player.Motor.position = new Vector3(30.5f, -6, 0); manager.UpdatePlayerRegion(player);
                    player.RequestRespawn();
                    Check(Mathf.Abs(player.Motor.position.x - 21.5f) < .01f, "alive R pathway respawns to latest checkpoint: " + player.Motor.position);
                    Check(manager.CurrentRegionId == "test_corridor", "respawn owner updated");
                    Check(GameObject.Find("未解锁机关测试").transform.position == new Vector3(34.5f, -6.5f, 0), "inactive motion feature does not overwrite authored pose");
                    phase++; nextFrame = Time.frameCount + 3; return;
                }
                if (phase == 3)
                {
                    Check(Mathf.Abs(player.transform.position.x - 21.5f) < .01f, "respawn pose visible after physics sync: " + player.transform.position + "; body=" + player.Motor.position);
                    Check(GameObject.Find("临时替代物") == null, "temporary replacement cleaned up");
                    var key = GameObject.Find("蓝色钥匙").GetComponent<ColorKeyPickup>();
                    typeof(Project.Items.DropItemBase).GetField("collectionRequested", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(key, true);
                    key.transform.position += Vector3.up;
                    Vector3 collectedPose = key.transform.position;
                    manager.ResetRegion("test_room_a");
                    Check(key.CollectionRequested && key.transform.position == collectedPose, "collected key state is not reset");
                    player.EnterDeath(); phase++; nextFrame = Time.frameCount + 4; return;
                }
                Check(!player.IsDead && Mathf.Abs(player.Motor.position.x - 21.5f) < .01f, "death auto respawn");
                Check(errors.Count == 0, "runtime errors: " + string.Join("\n", errors));
                File.WriteAllText("Library/RoomPlayVerification.txt", $"PASS {checks} runtime assertions; no runtime errors.");
                Stop(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Library/RoomPlayVerification.txt", "FAIL " + exception + "\n" + string.Join("\n", errors)); Stop(1);
            }
        }
        private static void Stop(int code)
        {
            SessionState.SetBool("RoomVerification.Running", false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
