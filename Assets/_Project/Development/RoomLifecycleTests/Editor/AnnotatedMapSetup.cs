using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// One-time authoring fixture for the annotated section; not a generic colour guessing importer.
public static class AnnotatedMapSetup
{
    private const string Folder = "Assets/_Project/Development/RoomLifecycleTests";
    public const string ScenePath = Folder + "/MVMapImportTest.unity";
    public const string SavePath = Folder + "/MVMapObjects.planning.json";
    private static int checks;
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new InvalidOperationException(message);
    }

    public static void Build()
    {
        if (!Application.dataPath.Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("地图替换仅在隔离验证项目执行。");
        checks = 0;
        var palette = LevelEditorPaletteService.GetOrCreate();
        var source = PlanningMVMapImporter.Read(File.ReadAllText(Folder + "/MVMapSource.mvmap.json"));
        Check(source.rooms.Any(r => r.id == "room_barp4u06n5"), "annotated source identity");
        var doc = new PlanningDocument { name = "标注关卡 · 门与水域", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        AddRoom(doc, "annotated_upper", "上层机关房", new RectInt(27, -52, 47, 30));
        AddRoom(doc, "annotated_lower", "下层水域房", new RectInt(27, -22, 36, 23));
        AddRoom(doc, "annotated_right", "右侧水流房", new RectInt(74, -52, 26, 45));
        // The screenshot also shows the unplanned area below the right room. Keep it editable,
        // without pretending that the designer supplied a fourth purple room boundary.
        AddRoom(doc, "annotated_unassigned", "未划分区", new RectInt(63, -22, 37, 23), excludeExisting: true);
        var bounds = new RectInt(27, -52, 73, 53);
        var waterIds = new HashSet<string> { "room_a0gcanoal5", "room_1z91em9jvf", "room_s1t0ee8lq0",
            "room_eqo9mlzvfp", "room_6ogwmzxzvm", "room_nu9vd9a33z", "room_61iw8to579",
            "room_a0jf4eq7eb", "room_omqug6pc8n", "room_wlqehrlgd6", "room_thx9uiym0p", "room_uxe4bh381u" };
        var markers = new HashSet<string> { "room_op8mytvr7l", "room_pn08djxyzj", "room_sffoqtn0x9",
            "room_yb6c59ouvs", "room_5p5k6o4zek", "room_cqetcbj5xn" };
        var expected = new HashSet<Vector2Int>();
        foreach (var item in source.rooms)
        {
            var cells = item.cells.Where(c => bounds.Contains(new Vector2Int(c.x, c.y))).ToList();
            if (cells.Count == 0 || item.sourceColorHex == "#a56fd0") continue;
            if (markers.Contains(item.id)) continue; // Direction markers describe mechanics, not solid blocks.
            string prefab = null;
            string label = "墙";
            CurrentFlowDirection? flow = null;
            if (waterIds.Contains(item.id)) { prefab = "Blue_WaterSource"; label = "水域"; }
            else if (item.id == "room_rfwrom30jy") { prefab = "Door"; label = "门"; }
            else if (item.id == "room_yup68gintw" || item.id == "room_5eic9h9651" || item.id == "room_lnhrgmpay6")
            { prefab = "DoorButton"; label = item.id == "room_lnhrgmpay6" ? "持续压住按钮" : "保持按钮"; }
            else if (item.id == "room_xipjpj86iu") { prefab = "OneWayPlatform"; label = "单向平台"; }
            else if (item.id == "room_smcflrqwv8") { prefab = "Blue_WaterWay"; label = "向左水流"; flow = CurrentFlowDirection.Left; }
            else if (item.id == "room_lsrckjto6f") { prefab = "Blue_WaterWay"; label = "向下水流"; flow = CurrentFlowDirection.Down; }
            else if (item.id == "room_w00jx93c1y") { prefab = "Blue_WaterWay"; label = "向下水流"; flow = CurrentFlowDirection.Down; }
            else if (item.sourceColorHex == "#4cc9d8") { prefab = "Blue_WaterBubble"; label = "向上气泡柱"; }
            else Check(string.IsNullOrEmpty(item.sourceColorHex) || item.sourceColorHex == "#8b93a6",
                "Unmapped object in annotated section: " + item.id + " / " + item.sourceColorHex);
            // Include the direction origin in its real current/column, not as a separate wall.
            if (item.id == "room_barp4u06n5") cells.Add(new PlanningCell(79, -37));
            if (item.id == "room_226dv2y0ne") cells.Add(new PlanningCell(79, -16));
            if (item.id == "room_w00jx93c1y") cells.Add(new PlanningCell(93, -47));
            if (item.id == "room_lsrckjto6f") cells.Add(new PlanningCell(89, -36));
            if (item.id == "room_smcflrqwv8") cells.Add(new PlanningCell(64, -38));
            foreach (var c in cells) Check(expected.Add(new Vector2Int(c.x, c.y)), "source objects overlap");
            foreach (var owner in doc.rooms)
            {
                var owned = new HashSet<Vector2Int>(owner.cells.Select(c => new Vector2Int(c.x, c.y)));
                var selected = cells.Where(c => owned.Contains(new Vector2Int(c.x, c.y))).ToList();
                int part = 0;
                foreach (var rect in PlanningMVMapObjectImporter.Partition(selected))
                {
                    var box = AddBox(palette, owner, "annotated_" + item.id + "_" + owner.id + "_" + part++, label, prefab, rect);
                    if (prefab == "DoorButton")
                    {
                        box.doorOwnerId = "annotated_room_rfwrom30jy_annotated_upper_0";
                        Override<DoorButton>(box, "mode", "Enum", item.id == "room_lnhrgmpay6" ? "1" : "0");
                    }
                    if (flow.HasValue)
                    {
                        var direction = item.id == "room_smcflrqwv8" && rect.height > 1
                            ? CurrentFlowDirection.Up : flow.Value;
                        if (direction == CurrentFlowDirection.Up) box.label = "转弯水流 · 向上";
                        Override<DirectionalCurrentFeature>(box, "flowDirection", "Enum", ((int)direction).ToString());
                    }
                }
            }
        }
        var upper = doc.rooms[0];
        AddBox(palette, upper, "annotated_spawn_checkpoint", "测试起点重生点", "Respawn_Block", new RectInt(30, -33, 1, 1));
        // The second upper-right blue singleton lacks a labelled mechanic. Retain it as an
        // editable planning annotation, not an invented wall, water block or colour key.
        doc.regions.Add(new PlanningRegion { id = "annotated_direction_note", annotation = "水流起点标记（原图）",
            x = 66, y = -39, width = 1, height = 1, colorHex = "#4c8dd8" });
        doc.playerStartRoomId = upper.id;
        doc.playerStartLocal = new Vector2(3.5f, 18.5f); // World (30.5, 33.5), above the existing ledge.
        doc.Normalize();
        CheckGeometry(doc, expected);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(doc.TryGetPlayerStartWorldPosition(out Vector3 spawn), "spawn");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Content/Player/RuntimePlayer.prefab"));
        player.name = "TestPlayer"; player.transform.position = spawn;
        var cameraRoot = new GameObject("Main Camera"); cameraRoot.tag = "MainCamera";
        var camera = cameraRoot.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .15f, .19f);
        cameraRoot.AddComponent<AudioListener>();
        var cameraManager = cameraRoot.AddComponent<CameraControlManager>(); cameraManager.ConfigureOutput(camera);
        var mode = cameraRoot.AddComponent<CameraModeController>(); mode.Configure(camera, player.transform);
        var follow = cameraRoot.AddComponent<CameraFollowController>(); follow.Configure(cameraManager, mode, player.transform);
        follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "saved target scene");
        PlanningSaveStore.Save(SavePath, doc);
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePath);
        var embedded = Object.FindFirstObjectByType<RoomLifecycleManager>();
        var saved = PlanningSaveStore.Read(SavePath);
        Check(saved.ToPersistentJson() == PlanningDocument.FromPersistentJson(embedded.PlanningJson).ToPersistentJson(), "embedded scene == shared file");
        CheckGeometry(saved, expected);
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length == doc.rooms.Sum(r => r.boxes.Count), "all editor boxes spawned once");
        var sceneCells = new HashSet<Vector2Int>();
        foreach (var p in placed.Where(p => p.PlanningBoxId != "annotated_spawn_checkpoint"))
            for (int x = 0; x < p.SizeCells.x; x++) for (int y = 0; y < p.SizeCells.y; y++)
                Check(sceneCells.Add(new Vector2Int(p.Cell.x + x, -p.Cell.y - y - 1)), "scene geometry not overlapping");
        Check(sceneCells.SetEquals(expected), "actual scene cells match editable plan without offsets");
        foreach (var p in placed)
        {
            var flow = p.GetComponent<DirectionalCurrentFeature>();
            if (flow == null) continue;
            var b = doc.rooms.SelectMany(r => r.boxes).First(b => b.id == p.PlanningBoxId);
            Check(new SerializedObject(flow).FindProperty("flowDirection").intValue ==
                int.Parse(b.componentOverrides.First(v => v.PropertyPath == "flowDirection").Value), "saved current direction applied to component");
        }
        var door = Object.FindFirstObjectByType<DoorController>();
        Check(door != null && door.Buttons.Count == 3 && door.Buttons.All(b => b.Owner == door), "persistent door/button binding");
        Check(door.Buttons.Count(b => b.Mode == DoorButtonMode.Held) == 1, "one held/two latched");
        Check(Object.FindObjectsByType<OneWayPlatform>(FindObjectsSortMode.None).Length == 1, "one one-way platform");
        Check(Object.FindObjectsByType<BuoyancyColumnFeature>(FindObjectsSortMode.None).Length == 2, "two upward bubble columns");
        var instanceIds = placed.Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(saved, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(instanceIds.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
            .Select(p => p.GetInstanceID()).OrderBy(i => i)), "unchanged incremental apply preserves instances");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "missing script");
        VerifyWindow(saved);
        Check(PlanningSaveStore.Read(SavePath).ToPersistentJson() == saved.ToPersistentJson(), "closing editor does not leak verification edits into shared save");
        PublishWorkspace(Environment.GetEnvironmentVariable("TAPTAP_WORKSPACE_DATA_PATH"), saved);
        File.WriteAllText("Library/AnnotatedMapVerification.txt", $"PASS {checks} assertions; {expected.Count} gameplay cells; {placed.Length} editable instances; 3 planned rooms + unassigned area.");
        AssetDatabase.SaveAssets();
    }

    private static void AddRoom(PlanningDocument doc, string id, string name, RectInt rect, bool excludeExisting = false)
    {
        var occupied = new HashSet<Vector2Int>(doc.rooms.SelectMany(r => r.cells).Select(c => new Vector2Int(c.x, c.y)));
        var room = new PlanningRoom(name) { id = id, sourceColorHex = excludeExisting ? "#545c6b" : "#a56fd0" };
        foreach (var c in rect.allPositionsWithin)
            if (!excludeExisting || !occupied.Contains(c)) room.cells.Add(new PlanningCell(c.x, c.y));
        doc.rooms.Add(room);
    }

    private static PlanningBox AddBox(LevelEditorPalette palette, PlanningRoom room, string id, string label, string prefab, RectInt world)
    {
        int x = room.cells.Min(c => c.x), y = room.cells.Min(c => c.y);
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop,
            new RectInt(world.x - x, world.y - y, world.width, world.height))
        { id = id, label = label, isMerged = world.width * world.height > 1 && prefab != "Door", singleInstance = true };
        if (prefab == null)
        {
            var entry = palette.Entries.First(e => e?.DisplayName == "白方块");
            box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName;
        }
        else
        {
            var entry = prefab == "DoorButton" ? PlanningDoorUtility.GetButtonEntry() :
                palette.PropEntries.FirstOrDefault(e => e?.Prefab?.name == prefab);
            Check(entry?.Prefab != null, "registered prop: " + prefab);
            box.propEntryId = prefab == "DoorButton" ? PlanningDoorUtility.ButtonEntryId : entry.EntryId;
            box.propEntryName = entry.DisplayName;
        }
        room.boxes.Add(box); return box;
    }

    private static void Override<T>(PlanningBox box, string path, string type, string value)
    {
        var parameter = new LevelEditorComponentValueOverride();
        parameter.Configure("", typeof(T).FullName, 0, path, type, value, null);
        box.hasComponentOverrides = true; box.componentOverrides.Add(parameter);
    }

    private static void CheckGeometry(PlanningDocument doc, HashSet<Vector2Int> expected)
    {
        var actual = new HashSet<Vector2Int>();
        foreach (var room in doc.rooms)
        {
            int x = room.cells.Min(c => c.x), y = room.cells.Min(c => c.y);
            var allowed = new HashSet<Vector2Int>(room.cells.Select(c => new Vector2Int(c.x, c.y)));
            foreach (var b in room.boxes.Where(b => b.id != "annotated_spawn_checkpoint"))
                foreach (var c in new RectInt(b.x + x, b.y + y, b.width, b.height).allPositionsWithin)
                { Check(allowed.Contains(c), "box inside room"); Check(actual.Add(c), "non-overlapping geometry"); }
        }
        Check(actual.SetEquals(expected), "exact annotated geometry / no purple walls / no old objects");
    }

    private static void VerifyWindow(PlanningDocument doc)
    {
        string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(Application.dataPath) + "." + AssetDatabase.AssetPathToGUID(ScenePath);
        EditorPrefs.DeleteKey(scope + ".Document"); EditorPrefs.DeleteKey(scope + ".SavePath");
        var window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
        try
        {
            window.CreateGUI();
            var field = typeof(PlanningEditorWindow).GetField("document", BindingFlags.Instance | BindingFlags.NonPublic);
            var restored = (PlanningDocument)field.GetValue(window);
            Check(restored.ToPersistentJson() == doc.ToPersistentJson(), "fresh editor window restores embedded editable map");
            Check(PlanningSaveStore.LoadAll().Any(s => Path.GetFullPath(s.Path) == Path.GetFullPath(SavePath)), "shared save visible in actual save panel");
            string originalLabel = restored.rooms[0].boxes[0].label;
            restored.rooms[0].boxes[0].label = "编辑往返验证";
            PlanningSaveStore.Save("Library/AnnotatedEditRoundtrip.planning.json", restored);
            Check(PlanningSaveStore.Read("Library/AnnotatedEditRoundtrip.planning.json").rooms[0].boxes[0].label == "编辑往返验证", "editable save roundtrip");
            restored.rooms[0].boxes[0].label = originalLabel;
        }
        finally { Object.DestroyImmediate(window); }
    }

    private static void PublishWorkspace(string dataPath, PlanningDocument doc)
    {
        if (string.IsNullOrEmpty(dataPath)) return;
        dataPath = dataPath.Replace('\\', '/').TrimEnd('/');
        string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(dataPath) + "." + AssetDatabase.AssetPathToGUID(ScenePath);
        string backup = Path.Combine(Path.GetDirectoryName(dataPath), "Library/AnnotatedMap20261014/workspace-before.json");
        Directory.CreateDirectory(Path.GetDirectoryName(backup));
        if (!File.Exists(backup)) File.WriteAllText(backup, JsonUtility.ToJson(new WorkspaceBackup
            { document = EditorPrefs.GetString(scope + ".Document", ""), savePath = EditorPrefs.GetString(scope + ".SavePath", "") }, true));
        EditorPrefs.SetString(scope + ".Document", doc.ToPersistentJson());
        EditorPrefs.SetString(scope + ".SavePath", Path.Combine(Path.GetDirectoryName(dataPath), SavePath).Replace('\\', '/'));
        EditorPrefs.SetString(scope + ".View.Room", doc.rooms[0].id);
        EditorPrefs.SetInt(scope + ".View.Mode", (int)PlanningCanvasMode.Assembly);
        foreach (string mode in new[] { "World.UnifiedGrid", "Room", "Assembly" }) EditorPrefs.DeleteKey(scope + ".View." + mode + ".Zoom");
    }
    [Serializable] private sealed class WorkspaceBackup { public string document, savePath; }

    [InitializeOnLoadMethod]
    private static void OpenAppliedMapRequest()
    {
        // Verified local handoff only. Never automatically replace a dirty or playing scene.
        string flag = Path.GetFullPath("Library/AnnotatedMap20261014/open-ready.flag");
        if (!File.Exists(flag) || Application.dataPath.Contains("FlowRetirementProbe")) return;
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(flag)) return;
            File.Delete(flag);
            string report = Path.ChangeExtension(flag, ".result.txt");
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                    .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
            { File.WriteAllText(report, "地图已应用；当前场景有未保存内容或正在运行，请手动打开 MVMapImportTest。"); return; }
            EditorSceneManager.OpenScene(ScenePath);
            var map = PlanningSaveStore.Read(SavePath);
            PublishWorkspace(Application.dataPath, map);
            var existing = Resources.FindObjectsOfTypeAll<PlanningEditorWindow>().FirstOrDefault();
            if (existing != null) { existing.rootVisualElement.Clear(); existing.CreateGUI(); }
            var window = existing ?? EditorWindow.GetWindow<PlanningEditorWindow>();
            window.titleContent = new GUIContent("关卡编辑器"); window.minSize = new Vector2(980, 620);
            window.Show(); window.Focus();
            File.WriteAllText(report, "已打开 MVMapImportTest 与可编辑规划存档。");
        };
    }
}
