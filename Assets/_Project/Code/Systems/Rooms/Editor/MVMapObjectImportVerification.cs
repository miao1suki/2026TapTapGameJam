using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Project.Rooms.Editor
{
    /// <summary>Deterministic import fixture. Runs only in the disposable verification project.</summary>
    public static class MVMapObjectImportVerification
    {
        private const string Folder = "Assets/_Project/Development/RoomLifecycleTests";
        public const string ScenePath = Folder + "/MVMapImportTest.unity";
        private const string SavePath = Folder + "/MVMapObjects.planning.json";
        private static int checks;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new InvalidOperationException(message);
        }

        public static void BuildAndVerify()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe"))
                throw new InvalidOperationException("试导入生成只允许在隔离项目执行。");
            checks = 0;
            var palette = LevelEditorPaletteService.GetOrCreate();
            Check(!palette.PropEntries.Any(e => e?.Prefab != null && e.Prefab.name == "AnchorPoint"), "legacy anchor removed");
            Check(palette.PropEntries.Any(e => e?.Prefab != null && e.Prefab.name == "Green_PlantObstacle"), "plant obstacle retained");
            Check(palette.PropEntries.Any(e => e?.Prefab != null && e.Prefab.name == "Respawn_Block"), "checkpoint retained");
            string source = File.ReadAllText(Folder + "/MVMapSource.mvmap.json");
            var raw = PlanningMVMapImporter.Read(source);
            Check(raw.rooms.Count == 73 && raw.rooms.Sum(r => r.cells.Count) == 2450, "new source 73 / 2450");
            var doc = PlanningMVMapObjectImporter.Read(source, palette, out string report);
            Check(doc.rooms.Count == 1 && doc.worldBlockCellWidth == 1, "object areas are not reset rooms");
            var room = doc.rooms[0];
            int minX = room.cells.Min(c => c.x), minY = room.cells.Min(c => c.y);
            var expected = new HashSet<Vector2Int>(raw.rooms.SelectMany(r => r.cells).Select(c => new Vector2Int(c.x, c.y)));
            var actual = new HashSet<Vector2Int>();
            foreach (var box in room.boxes.Where(b => b.id.StartsWith("mvmap_") && !b.id.EndsWith("_test_button")))
                foreach (var cell in new RectInt(box.x + minX, box.y + minY, box.width, box.height).allPositionsWithin)
                    Check(actual.Add(cell), "no overlapping imported objects");
            Check(actual.SetEquals(expected), "exact geometry; no holes filled or cells dropped");
            var copy = PlanningDocument.FromPersistentJson(doc.ToPersistentJson());
            Check(copy.rooms[0].boxes.Count == room.boxes.Count && copy.rooms[0].boxes.Select(b => b.id).SequenceEqual(room.boxes.Select(b => b.id)), "stable IDs roundtrip");
            var second = PlanningMVMapObjectImporter.Read(source, palette, out _);
            Check(second.rooms[0].boxes.Select(b => b.id).SequenceEqual(room.boxes.Select(b => b.id)), "repeat conversion stable");
            var concave = new List<PlanningCell> { new PlanningCell(0, 0), new PlanningCell(1, 0), new PlanningCell(0, 1), new PlanningCell(4, 4) };
            Check(PlanningMVMapObjectImporter.Partition(concave).Sum(r => r.width * r.height) == 4, "concave and disconnected exact partition");
            var overrideDoc = PlanningMVMapObjectImporter.Read(source, palette, out _, new Dictionary<string, string> { [raw.rooms[0].id] = "Green_PlantObstacle" });
            Check(overrideDoc.rooms[0].boxes[0].propEntryName == palette.PropEntries.First(e => e?.Prefab != null && e.Prefab.name == "Green_PlantObstacle").DisplayName, "explicit ID overrides inference");
            BuildScene(doc);
            var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
            Check(placed.Length == room.boxes.Count, "all rectangles become scene objects");
            Check(Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None).Length == 1, "one guessed door");
            var door = Object.FindFirstObjectByType<DoorController>();
            Check(door.Buttons.Count == 2 && Object.FindObjectsByType<DoorButton>(FindObjectsSortMode.None).All(b => b.Owner == door), "two guessed buttons bound to door");
            var vines = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).Where(v => !(v is LadderSonFeature)).ToArray();
            Check(vines.Length == 1 && vines[0].CurrentGrowthBlocks == 22, "23 cells produce one mother and 22 linked segments");
            var oldIds = placed.Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
            PlanningSceneBuilder.SuppressSceneUndo = true;
            try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), "incremental reapply: " + message); }
            finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
            Check(oldIds.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i)), "incremental preserves authored instances");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "no missing scripts");
            Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.CompareTag("MainCamera")) == 1, "one main camera");
            Check(EditorSceneManager.SaveScene(scene, ScenePath), "scene saved");
            File.WriteAllText(SavePath, doc.ToPersistentJson());
            File.WriteAllText("Docs/Developer/MVMAP_OBJECT_MAPPING.md", report);
            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);
            var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
            Check(PlanningDocument.FromPersistentJson(manager.PlanningJson).rooms[0].boxes.Count == room.boxes.Count, "scene embeds shareable save");
            Check(Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).Count(v => !(v is LadderSonFeature)) == 1, "vine ownership survives saved scene reload");
            door = Object.FindFirstObjectByType<DoorController>();
            Check(door.Buttons.Count == 2 && door.Buttons.All(b => b.Owner == door), "door references survive saved scene reload");
            PublishLocalWorkspace();
            File.WriteAllText("Library/MVMapObjectImportVerification.txt", $"PASS {checks} assertions; 73 source objects / 2450 cells / {room.boxes.Count} placed instances.");
            AssetDatabase.SaveAssets();
        }

        internal static void BuildScene(PlanningDocument doc)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlanningSceneBuilder.SuppressSceneUndo = true;
            try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), "scene build: " + message); }
            finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
            var room = doc.rooms[0];
            Check(doc.TryGetPlayerStartWorldPosition(out Vector3 spawn), "planning spawn available");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.name = "TestPlayer"; player.transform.position = spawn;
            var checkpoint = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Content/ColorBlocks/Prefabs/Respawn_Block.prefab"));
            checkpoint.name = "测试起点重生点";
            checkpoint.transform.position = spawn;
            checkpoint.GetComponent<RespawnAnchorFeature>().EnsureRuntimeAnchorId();
            checkpoint.GetComponent<RespawnAnchorFeature>().SetRegion(room.id);
            PrefabUtility.RecordPrefabInstancePropertyModifications(checkpoint.GetComponent<RespawnAnchorFeature>());
            var cameraRoot = new GameObject("Main Camera"); cameraRoot.tag = "MainCamera";
            var camera = cameraRoot.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .19f);
            cameraRoot.AddComponent<AudioListener>();
            var manager = cameraRoot.AddComponent<CameraControlManager>(); manager.ConfigureOutput(camera);
            var mode = cameraRoot.AddComponent<CameraModeController>(); mode.Configure(camera, player.transform);
            var follow = cameraRoot.AddComponent<CameraFollowController>(); follow.Configure(manager, mode, player.transform);
            follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
            Check(Object.FindFirstObjectByType<RoomLifecycleManager>().FindRegion(spawn) == room.id, "spawn inside test region");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static int phase;
        private static double started;
        private static float unlockTime;
        private static readonly List<string> errors = new List<string>();
        public static void RunPlayVerification()
        {
            if (!Application.dataPath.Contains("FlowRetirementProbe")) throw new InvalidOperationException("仅限隔离项目。");
            EditorSceneManager.OpenScene(ScenePath);
            SessionState.SetBool("MVMap.Objects.Play", true);
            BeginPlayChecks();
            EditorApplication.EnterPlaymode();
        }
        [InitializeOnLoadMethod] private static void ResumePlayChecks()
        {
            if (SessionState.GetBool("MVMap.Objects.Play", false)) BeginPlayChecks();
        }
        private static void BeginPlayChecks()
        {
            phase = checks = 0; errors.Clear(); started = EditorApplication.timeSinceStartup;
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
                if (EditorApplication.timeSinceStartup - started > 100) throw new TimeoutException("MVMap Play Mode timeout");
                if (!EditorApplication.isPlaying || Time.time < .5f) return;
                var player = Object.FindFirstObjectByType<PlayerController>();
                var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
                if (player == null || manager == null) return;
                player.GetComponent<PlayerInputDriver>().enabled = false;
                player.Motor.constraints = RigidbodyConstraints.FreezeAll;
                if (phase == 0)
                {
                    Check(manager.CurrentRegionId == "mvmap_objects_test_area", "player starts in test region");
                    Check(Object.FindObjectsByType<ColorObject>(FindObjectsSortMode.None).All(c => !c.IsActive), "colors initially locked");
                    Check(PlayerRespawnPointRegistry.TryFindPreferred(player, player.transform.position, out _), "checkpoint active near spawn");
                    foreach (var id in new[] { "red", "green", "blue" }) ColorRuntimeService.Instance.Unlock(id);
                    unlockTime = Time.time; phase = 1; return;
                }
                if (phase == 1)
                {
                    if (Time.time - unlockTime < 18) return;
                    Check(Object.FindObjectsByType<ColorObject>(FindObjectsSortMode.None).All(c => c.IsActive), "all mapped color props unlock");
                    Check(Object.FindObjectsByType<WaterSourceFeature>(FindObjectsSortMode.None).Length > 0, "mapped water runtime available");
                    var vine = Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).First(v => !(v is LadderSonFeature));
                    Check(vine.CurrentGrowthBlocks == 22, "runtime vine chain preserved");
                    player.RequestRespawn(); phase = 2; unlockTime = Time.time; return;
                }
                if (Time.time - unlockTime < 1) return;
                Check(new[] { "red", "green", "blue" }.All(ColorRuntimeService.Instance.IsUnlocked), "R preserves unlock progress");
                Check(Object.FindObjectsByType<ClimbableVineFeature>(FindObjectsSortMode.None).First(v => !(v is LadderSonFeature)).CurrentGrowthBlocks == 22, "R preserves authored vine chain");
                var door = Object.FindFirstObjectByType<DoorController>();
                Check(door.Buttons.Count == 2 && door.Buttons.All(b => b.Owner == door), "runtime door references intact");
                Check(errors.Count == 0, "runtime errors: " + string.Join("\n", errors));
                File.WriteAllText("Library/MVMapObjectPlayVerification.txt", $"PASS {checks} runtime assertions; no gameplay errors.");
                StopPlay(0);
            }
            catch (Exception exception)
            {
                File.WriteAllText("Library/MVMapObjectPlayVerification.txt", "FAIL " + exception + "\n" + string.Join("\n", errors));
                StopPlay(1);
            }
        }
        private static void StopPlay(int code)
        {
            SessionState.SetBool("MVMap.Objects.Play", false);
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            EditorApplication.ExitPlaymode(); EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }

        [Serializable] private sealed class PreferenceBackup { public string scope, document, savePath; }
        private static void PublishLocalWorkspace()
        {
            // Optional local cache update. Never modify another scene's preferences or unrelated saves.
            string dataPath = Environment.GetEnvironmentVariable("TAPTAP_WORKSPACE_DATA_PATH");
            if (string.IsNullOrWhiteSpace(dataPath)) return;
            dataPath = dataPath.Replace('\\', '/').TrimEnd('/');
            string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(dataPath) + "." + AssetDatabase.AssetPathToGUID(ScenePath);
            var old = new PreferenceBackup { scope = scope, document = EditorPrefs.GetString(scope + ".Document", ""), savePath = EditorPrefs.GetString(scope + ".SavePath", "") };
            string backupFolder = Path.Combine(Path.GetDirectoryName(dataPath), "Library/MVMapReplacement20261013");
            Directory.CreateDirectory(backupFolder);
            string backupPath = Path.Combine(backupFolder, "workspace-preferences.json");
            if (!File.Exists(backupPath)) File.WriteAllText(backupPath, JsonUtility.ToJson(old, true));
            // Back up a linked local save only when it identifies this retired import, not by filename guess.
            if (!string.IsNullOrWhiteSpace(old.savePath) && File.Exists(old.savePath) &&
                Path.GetFullPath(old.savePath).StartsWith(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dataPath), "Library/PlanningEditorSaves")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                string contents = File.ReadAllText(old.savePath);
                string retiredSource = Path.Combine(backupFolder, "MVMapSource.mvmap.json");
                if (File.Exists(retiredSource))
                {
                    var retired = PlanningMVMapImporter.Read(File.ReadAllText(retiredSource));
                    if (retired.rooms.Any(r => contents.Contains(r.id)))
                    { File.Copy(old.savePath, Path.Combine(backupFolder, Path.GetFileName(old.savePath)), true); File.Delete(old.savePath); }
                }
            }
            EditorPrefs.DeleteKey(scope + ".Document");
            EditorPrefs.SetString(scope + ".SavePath", Path.Combine(Path.GetDirectoryName(dataPath), SavePath).Replace('\\', '/'));
        }
    }
}
