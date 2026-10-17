using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.GameFlow;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Explicit named-map authoring fixture; never runs against the main project.</summary>
public static class MRoom3MapImport
{
    public const string ScenePath = "Assets/Scenes/MRoom3.unity";
    public const string SavePath = "Assets/Scenes/MRoom3.planning.json";
    public const string SourcePath = "Assets/Scenes/MRoom3.mvmap.json";
    public const string RegionId = "mroom3_play_area";
    private const string NewAreaId = "area_1ge22nuuqv";
    private const string MisfiledPlatformId = "room_34jiraqppa";
    private static int checks;

    private static void Check(bool success, string message)
    {
        checks++;
        if (!success) throw new InvalidOperationException("MRoom3: " + message);
    }
    public static void Build()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new Exception("Isolated authoring project only");
        checks = 0;
        var source = PlanningMVMapImporter.Read(File.ReadAllText(SourcePath));
        var items = source.rooms.Where(r => r.sourceAreaId == NewAreaId || r.id == MisfiledPlatformId).ToArray();
        Check(items.Length == 32 && items.Single(i => i.id == MisfiledPlatformId).name == "移动平台2", "exact new-area whitelist plus confirmed misfiled platform");
        Check(items.Single(r => r.name == "墙").cells.Count == 696, "source wall count");
        // Confirmed correction: move the far vertical platform into its blue route
        // column; preserve all three wall cells which previously blocked its path.
        var platform3 = items.Single(r => r.name == "移动平台3");
        foreach (var cell in platform3.cells.Where(c => c.x == 61)) cell.x = 60;
        var allCells = items.SelectMany(i => i.cells).ToArray();
        var bounds = new RectInt(allCells.Min(c => c.x) - 1, allCells.Min(c => c.y) - 1,
            allCells.Max(c => c.x) - allCells.Min(c => c.x) + 3, allCells.Max(c => c.y) - allCells.Min(c => c.y) + 3);
        var doc = new PlanningDocument { name = "m_room_3 绿色能力进阶关", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("绿色能力进阶关") { id = RegionId, sourceAreaId = NewAreaId, sourceColorHex = "#4cc9d8" };
        foreach (var cell in bounds.allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        doc.rooms.Add(room);
        var palette = LevelEditorPaletteService.GetOrCreate();
        var walls = items.Single(r => r.name == "墙").cells.Select(c => new Vector2Int(c.x, c.y)).ToHashSet();
        var expected = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (var item in items)
        {
            if (item.name.EndsWith("路线", StringComparison.Ordinal))
            {
                foreach (var rect in PlanningMVMapObjectImporter.Partition(item.cells))
                    doc.regions.Add(new PlanningRegion { id = "mroom3_route_" + doc.regions.Count, annotation = item.name,
                        x = rect.x, y = rect.y, width = rect.width, height = rect.height, colorHex = "#4c8dd8" });
                continue;
            }
            string prefab = ResolvePrefab(item.name);
            expected.Add(item.name, item.cells.Select(c => new Vector2Int(c.x, c.y)).ToHashSet());
            bool single = prefab == "Green_Ladder" || prefab == "Respawn_Block" || prefab == "Collectible" ||
                prefab == "Red_Foundation" || prefab?.EndsWith("ContactCube", StringComparison.Ordinal) == true || prefab == "DoorButton";
            // null is the built-in solid palette entry, not an unknown prefab.
            var rects = single || item.name == "尖刺" ? item.cells.Select(c => new RectInt(c.x, c.y, 1, 1)).ToList()
                : PlanningMVMapObjectImporter.Partition(item.cells);
            int part = 0;
            foreach (var rect in rects)
                AddBox(palette, room, "mroom3_" + item.id + "_" + part++, item.name,
                    item.name == "尖刺" ? SpikePrefab(rect.position, walls) : prefab, rect, bounds.position, single || item.name == "尖刺");
        }
        PlanningBox At(string label, int x, int y) => room.boxes.Single(b => b.label == label && b.x + bounds.x == x && b.y + bounds.y == y);
        ConfigurePlatform(At("移动平台1", -43, -15), At("基座1", -42, -9), true, 6, false);
        ConfigurePlatform(At("移动平台2", 55, -13), At("基座2", 55, -8), false, 3, false);
        ConfigurePlatform(At("移动平台3", 24, 8), At("基座3", 22, 6), false, 2, true);
        ConfigurePlatform(At("移动平台3", 60, 20), At("基座3", 22, 6), true, 3, false);
        foreach (var box in room.boxes.Where(b => b.label.StartsWith("基座", StringComparison.Ordinal)))
            Parameter(box, typeof(MechanismBaseFeature), "useGravity", "Boolean", "0");
        for (int number = 1; number <= 6; number++)
        {
            var door = room.boxes.Single(b => b.label == "门" + number);
            int order = 0;
            foreach (var button in room.boxes.Where(b => b.label == "按一次就行的按钮" + number))
            {
                button.doorOwnerId = door.id; button.doorButtonOrder = ++order;
                Parameter(button, typeof(DoorButton), "mode", "Enum", "0");
            }
            Check(order == new[] { 1, 2, 3, 3, 3, 1 }[number - 1], "button count/order for door " + number);
        }
        foreach (var box in room.boxes.Where(b => b.isMerged))
            foreach (var cell in new RectInt(box.x, box.y, box.width, box.height).allPositionsWithin)
            {
                var unit = box.Clone(); unit.mergeParts.Clear(); unit.id += "_cell_" + cell.x + "_" + cell.y;
                unit.x = cell.x; unit.y = cell.y; unit.width = unit.height = 1; unit.isMerged = false;
                box.mergeParts.Add(JsonUtility.ToJson(unit));
            }
        doc.playerStartRoomId = room.id;
        // Confirmed spawn next to the leftmost checkpoint, in the corridor above its floor.
        doc.playerStartLocal = new Vector2(-46.5f - bounds.x, -9.5f - bounds.y);
        doc.Normalize();
        VerifyCoverage(doc, expected, bounds.position);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Apply(doc);
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn) && spawn == new Vector3(-46.5f, 9.5f, 0), "spawn coordinate");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab"));
        player.name = "Player"; player.transform.position = spawn;
        var cameraRoot = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraRoot.AddComponent<Camera>(); camera.backgroundColor = new Color(.12f, .15f, .19f);
        camera.clearFlags = CameraClearFlags.SolidColor; cameraRoot.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var manager = cameraRoot.AddComponent<CameraControlManager>(); manager.ConfigureOutput(camera);
        var mode = cameraRoot.AddComponent<CameraModeController>(); mode.Configure(camera, player.transform);
        var follow = cameraRoot.AddComponent<CameraFollowController>(); follow.Configure(manager, mode, player.transform);
        follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.white;
        var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = .8f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "saved scene"); PlanningSaveStore.Save(SavePath, doc);
        AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        var before = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
        Apply(doc);
        Check(before.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i)), "no-op apply retains every object");
        VerifyWindow(doc);
        Check(PlanningSaveStore.Read(SavePath).ToPersistentJson() == doc.ToPersistentJson(), "shared map unchanged by window roundtrip");
        VerifyCoverage(PlanningSaveStore.Read(SavePath), expected, bounds.position);
        EditorSceneManager.OpenScene(ScenePath); VerifyScene(doc);
        File.WriteAllText("Library/MRoom3Import.txt", $"PASS {checks} assertions; {room.boxes.Count} objects; {expected.Sum(p => p.Value.Count)} gameplay cells; " +
            "696 walls; 7 water; 4 platforms/3 bases; 6 doors/13 latched buttons; 40 vines; 10 checkpoints; 68 oriented spikes; source new-area only.");
        Debug.Log(File.ReadAllText("Library/MRoom3Import.txt"));
    }
    private static string ResolvePrefab(string name)
    {
        if (name == "墙") return null;
        if (name == "水") return "Blue_WaterSource";
        if (name == "藤蔓") return "Green_Ladder";
        if (name == "重生点") return "Respawn_Block";
        if (name == "收集品") return "Collectible";
        if (name == "岩浆") return "Red_Lava";
        if (name == "尖刺") return "Spike_上";
        if (name.StartsWith("基座", StringComparison.Ordinal)) return "Red_Foundation";
        if (name.StartsWith("移动平台", StringComparison.Ordinal)) return "Red_Platform";
        if (name.StartsWith("红色", StringComparison.Ordinal)) return "Red_ContactCube";
        if (name.StartsWith("蓝色", StringComparison.Ordinal)) return "Blue_ContactCube";
        if (name.StartsWith("门", StringComparison.Ordinal)) return "Door";
        if (name.StartsWith("按一次就行的按钮", StringComparison.Ordinal)) return "DoorButton";
        throw new Exception("Unmapped named item " + name);
    }
    private static string SpikePrefab(Vector2Int cell, HashSet<Vector2Int> walls)
    {
        // MVMap Y grows down. Prefer an adjacent wall, with vertical priority at
        // corners. Never treat neighbouring spikes as solid support.
        var directions = new[] { Vector2Int.down, Vector2Int.up, Vector2Int.left, Vector2Int.right };
        var names = new[] { "Spike_下", "Spike_上", "Spike_右", "Spike_左" };
        for (int distance = 1; distance <= 4; distance++)
            for (int index = 0; index < directions.Length; index++)
                if (walls.Contains(cell + directions[index] * distance)) return names[index];
        throw new Exception("Spike has no wall support near " + cell);
    }
    private static void AddBox(LevelEditorPalette palette, PlanningRoom room, string id, string label, string prefab, RectInt rect, Vector2Int origin, bool single)
    {
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop, new RectInt(rect.position - origin, rect.size))
        { id = id, label = label, singleInstance = single || prefab == "Door", isMerged = !single && prefab != "Door" && rect.width * rect.height > 1 };
        if (prefab == null)
        {
            var entry = palette.Entries.Single(e => e?.DisplayName == "白方块"); box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName;
        }
        else if (prefab == "DoorButton") { box.propEntryId = PlanningDoorUtility.ButtonEntryId; box.propEntryName = "门按钮"; }
        else
        {
            var entries = palette.PropEntries.Where(e => e?.Prefab?.name == prefab).ToArray();
            Check(entries.Length > 0 && entries.Select(e => AssetDatabase.GetAssetPath(e.Prefab)).Distinct().Count() == 1, "unambiguous prefab " + prefab);
            box.propEntryId = entries[0].EntryId; box.propEntryName = entries[0].DisplayName;
        }
        room.boxes.Add(box);
    }
    private static void Parameter(PlanningBox box, Type type, string path, string valueType, string value)
    {
        var parameter = new LevelEditorComponentValueOverride(); parameter.Configure("", type.FullName, 0, path, valueType, value, null);
        box.hasComponentOverrides = true; box.componentOverrides.Add(parameter);
    }
    private static void ConfigurePlatform(PlanningBox box, PlanningBox basis, bool vertical, int distance, bool reverse)
    {
        box.mechanismBaseId = basis.id;
        Parameter(box, typeof(MovingPlatformFeature), "route", "Enum", vertical ? "1" : "0");
        Parameter(box, typeof(MovingPlatformFeature), "travelBlocks", "Integer", distance.ToString());
        Parameter(box, typeof(MovingPlatformFeature), "reverseRoute", "Boolean", reverse ? "1" : "0");
        Parameter(box, typeof(MovingPlatformFeature), "travelDuration", "Float", (distance * .5f).ToString(CultureInfo.InvariantCulture));
        Parameter(box, typeof(MovingPlatformFeature), "signalMotion", "Enum", "1");
    }
    private static void Apply(PlanningDocument doc)
    {
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out var message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
    }
    private static void VerifyCoverage(PlanningDocument doc, Dictionary<string, HashSet<Vector2Int>> expected, Vector2Int origin)
    {
        foreach (var category in expected)
        {
            var actual = new HashSet<Vector2Int>();
            foreach (var box in doc.rooms.Single().boxes.Where(b => b.label == category.Key))
                foreach (var cell in new RectInt(box.x + origin.x, box.y + origin.y, box.width, box.height).allPositionsWithin)
                    Check(actual.Add(cell), "no duplicate category cell");
            Check(actual.SetEquals(category.Value), "exact coverage " + category.Key);
        }
    }
    public static void VerifyScene(PlanningDocument doc)
    {
        var lifecycle = Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(lifecycle != null && PlanningDocument.FromPersistentJson(lifecycle.PlanningJson).ToPersistentJson() == doc.ToPersistentJson(), "embedded planning matches paired save");
        var boxes = doc.rooms.Single().boxes;
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length == boxes.Count, "one generated object per authoring box");
        var origin = new Vector2Int(doc.rooms[0].cells.Min(c => c.x), doc.rooms[0].cells.Min(c => c.y));
        foreach (var p in placed)
        {
            var box = boxes.Single(b => b.id == p.PlanningBoxId);
            Check(p.SizeCells == new Vector2Int(box.width, box.height), "size " + box.label);
            Check(p.Cell == new Vector2Int(box.x + origin.x, -box.y - origin.y - box.height), "position " + box.label);
            Check(p.GetComponent<RoomResetObject>() != null, "region reset membership " + box.label);
            if (p.TryGetComponent<MovingPlatformFeature>(out var platform))
            {
                Check(platform.LinkedControls.Count == 1 && platform.LinkedControls[0].GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == box.mechanismBaseId, "stable base binding");
                Check(platform.SignalMotion == MovingPlatformSignalMotion.PoweredEndpoint, "powered endpoint mode");
                string Value(string path) => box.componentOverrides.Single(v => v.PropertyPath == path).Value;
                Vector3 delta = (Value("route") == "1" ? Vector3.up : Vector3.right) * int.Parse(Value("travelBlocks")) * (Value("reverseRoute") == "1" ? -1 : 1);
                Check(Vector3.Distance(platform.PreviewEnd - platform.PreviewStart, delta) < .001f, "persisted movement geometry");
            }
        }
        Check(placed.Count(p => p.GetComponent<MovingPlatformFeature>() != null) == 4, "four platforms");
        var bases = placed.Select(p => p.GetComponent<MechanismBaseFeature>()).Where(b => b != null).ToArray();
        Check(bases.Length == 3 && bases.All(b => !b.UsesGravity), "three fixed bases");
        var doors = placed.Select(p => p.GetComponent<DoorController>()).Where(d => d != null).ToArray();
        Check(doors.Length == 6 && doors.Sum(d => d.Buttons.Count) == 13, "six doors and thirteen buttons");
        foreach (var door in doors)
            foreach (var button in door.Buttons) Check(button.Owner == door && button.Persistent, "latched pressure button binding");
        var players = GameObject.FindGameObjectsWithTag("Player");
        Check(players.Length == 1 && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0]) == "Assets/_Project/Content/Player/Player.prefab", "unique standard player");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "one camera");
        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "one event system");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0 && !PrefabUtility.IsPrefabAssetMissing(t.gameObject), "no missing scripts/prefabs");
    }
    private static void VerifyWindow(PlanningDocument doc)
    {
        string scope = "2026TapTap.Planning.Scene." + Hash128.Compute(Application.dataPath) + "." + AssetDatabase.AssetPathToGUID(ScenePath);
        EditorPrefs.DeleteKey(scope + ".Document"); EditorPrefs.DeleteKey(scope + ".SavePath");
        var window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
        try
        {
            window.CreateGUI();
            var restored = (PlanningDocument)typeof(PlanningEditorWindow).GetField("document", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(window);
            Check(restored.ToPersistentJson() == doc.ToPersistentJson(), "editor picks new scene's shared planning");
            Check(PlanningSaveStore.LoadAll().Any(s => Path.GetFullPath(s.Path) == Path.GetFullPath(SavePath)), "paired save discoverable");
            var copy = PlanningDocument.FromPersistentJson(restored.ToPersistentJson()); copy.rooms[0].boxes[0].label += "编辑验证";
            PlanningSaveStore.Save("Library/MRoom3Roundtrip.planning.json", copy);
            Check(PlanningSaveStore.Read("Library/MRoom3Roundtrip.planning.json").ToPersistentJson() == copy.ToPersistentJson(), "editable save roundtrip");
        }
        finally { Object.DestroyImmediate(window); }
    }
}
