using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.GameFlow;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// Reproducible, isolated authoring fixture for the named m_room_2 source map.
public static class MRoom2MapImport
{
    public const string ScenePath = "Assets/Scenes/MRoom2.unity";
    public const string SavePath = "Assets/Scenes/MRoom2.planning.json";
    public const string SourcePath = "Assets/Scenes/MRoom2.mvmap.json";
    private static int checks;
    private static readonly Dictionary<string, string> Prefabs = new Dictionary<string, string>
    {
        { "水", "Blue_WaterSource" }, { "墙", null },
        { "植物障碍", "Green_PlantObstacle" }, { "藤蔓", "Green_Ladder" },
        { "收集品", "Collectible" }, { "梯子", "LadderSide" },
        { "弹性植物", "Green_JumpPlant" }, { "移动平台", "Red_Platform" },
        { "曲柄", "Red_Crank" }, { "岩浆块", "Red_Lava" },
        { "重生点", "Respawn_Block" }, { "红色接触方块", "Red_ContactCube" },
        { "基座", "Red_Foundation" }, { "绿色接触方块", "Green_ContactCube" }
    };

    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException("MRoom2: " + message);
    }

    public static void Build()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("仅可在隔离验证工程执行此导入，以免覆盖正在编辑的场景。");
        checks = 0;
        File.WriteAllText("Library/MRoom2ImportVerification.txt", "RUNNING");
        var source = PlanningMVMapImporter.Read(File.ReadAllText(SourcePath));
        Check(source.name == "m_room_2" && source.rooms.Count == 15, "source map identity");
        var palette = LevelEditorPaletteService.GetOrCreate();
        var doc = new PlanningDocument { name = "m_room_2", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("m_room_2") { id = "mroom2_play_area", sourceColorHex = "#4c8dd8" };
        // Source 'rooms' are object categories, not gameplay region boundaries.
        var bounds = new RectInt(-123, -32, 49, 37);
        foreach (var cell in bounds.allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        doc.rooms.Add(room);
        var expected = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (var item in source.rooms)
        {
            if (item.name == "移动平台路线") continue;
            Check(Prefabs.TryGetValue(item.name, out string prefab), "known named object: " + item.name);
            expected.Add(item.name, new HashSet<Vector2Int>(item.cells.Select(c => new Vector2Int(c.x, c.y))));
            var rectangles = item.name == "藤蔓"
                ? item.cells.Select(c => new RectInt(c.x, c.y, 1, 1)).ToList()
                : PlanningMVMapObjectImporter.Partition(item.cells);
            int part = 0;
            foreach (var rect in rectangles)
                AddBox(palette, room, "mroom2_" + item.id + "_" + part++, item.name, prefab, rect, bounds.position);
        }
        PlanningBox At(string label, int x, int y) => room.boxes.Single(b => b.label == label && b.x + bounds.x == x && b.y + bounds.y == y);
        var longPlatform = At("移动平台", -84, -14);
        var upperPlatform = At("移动平台", -94, -19);
        var lowerPlatform = At("移动平台", -110, 0);
        var verticalPlatform = At("移动平台", -112, -24);
        longPlatform.mechanismBaseId = At("基座", -95, -13).id;
        verticalPlatform.mechanismBaseId = At("基座", -108, -23).id;
        upperPlatform.crankOwnerId = At("曲柄", -92, -13).id;
        lowerPlatform.crankOwnerId = At("曲柄", -110, 2).id;
        foreach (var crank in room.boxes.Where(b => b.label == "曲柄")) crank.initialCrankPlatformCreated = true;
        ConfigureRoute(longPlatform, false, 35, true);
        ConfigureRoute(upperPlatform, false, 7, false);
        ConfigureRoute(lowerPlatform, false, 3, false);
        ConfigureRoute(verticalPlatform, true, 5, false);
        foreach (var basis in room.boxes.Where(b => b.label == "基座"))
            SetParameter(basis, "useGravity", "Boolean", "0", typeof(MechanismBaseFeature).FullName);
        foreach (var box in room.boxes.Where(b => b.isMerged && b.width * b.height > 1))
            foreach (var cell in new RectInt(box.x, box.y, box.width, box.height).allPositionsWithin)
            {
                var unit = box.Clone(); unit.mergeParts.Clear();
                unit.id = box.id + "_cell_" + cell.x + "_" + cell.y;
                unit.x = cell.x; unit.y = cell.y; unit.width = unit.height = 1;
                unit.isMerged = false;
                box.mergeParts.Add(JsonUtility.ToJson(unit));
            }
        // Route cells describe empty swept space, never a solid wall or a water object.
        var routeCells = source.rooms.Single(r => r.name == "移动平台路线").cells.Select(c => new Vector2Int(c.x, c.y)).ToHashSet();
        Check(routeCells.Count == 49 && !routeCells.Contains(new Vector2Int(-94, -14)), "source route gap");
        routeCells.Add(new Vector2Int(-94, -14));
        foreach (var rect in PlanningMVMapObjectImporter.Partition(routeCells.Select(c => new PlanningCell(c.x, c.y)).ToList()))
            doc.regions.Add(new PlanningRegion { id = "mroom2_route_" + doc.regions.Count,
                annotation = "平台路线", x = rect.x, y = rect.y, width = rect.width, height = rect.height, colorHex = "#4c8dd8" });
        Check(Enumerable.Range(-119, 35).All(x => routeCells.Contains(new Vector2Int(x, -14))), "continuous longest route through plant obstacle");
        doc.playerStartRoomId = room.id;
        doc.playerStartLocal = new Vector2(-109.5f - bounds.x, -7.5f - bounds.y);
        doc.Normalize();
        VerifyCoverage(doc, expected, bounds.position);
        VerifyReverseRouteDefaults(palette);
        VerifyLadderDefaults();
        VerifyExistingLadderScenes();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(doc.TryGetPlayerStartWorldPosition(out Vector3 spawn) && spawn == new Vector3(-109.5f, 7.5f, 0), "authored spawn coordinate");
        var standard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Player/Player.prefab");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(standard);
        player.name = "Player"; player.transform.position = spawn;
        var cameraRoot = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraRoot.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .19f);
        cameraRoot.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var cameraManager = cameraRoot.AddComponent<CameraControlManager>(); cameraManager.ConfigureOutput(camera);
        var mode = cameraRoot.AddComponent<CameraModeController>(); mode.Configure(camera, player.transform);
        var follow = cameraRoot.AddComponent<CameraFollowController>(); follow.Configure(cameraManager, mode, player.transform);
        follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
        // No formal flow context: this is an independently playable development scene.
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.white;
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = .8f; light.transform.rotation = Quaternion.Euler(50, -30, 0);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "scene saved");
        PlanningSaveStore.Save(SavePath, doc);
        AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc, expected, bounds.position);
        VerifyWindow(doc);
        VerifyIncremental(doc);
        VerifyLadderEditing(doc, palette, expected, bounds.position);
        // Apply is also a shared-map edit. Explicitly restore both authored outputs after
        // the destructive edit tests, rather than leaving their companion save behind.
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath), "restore authored scene after edit tests");
        PlanningSaveStore.Save(SavePath, doc);
        // Reopen the on-disk scene, not the unsaved verification edits.
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc, expected, bounds.position);
        File.WriteAllText("Library/MRoom2ImportVerification.txt", $"PASS {checks} assertions; {expected.Sum(p => p.Value.Count)} gameplay cells; " +
            $"{room.boxes.Count} editor objects; 4 platforms; 50 route cells; one editable reset area; default locked colours.");
        Debug.Log(File.ReadAllText("Library/MRoom2ImportVerification.txt"));
    }

    private static PlanningBox AddBox(LevelEditorPalette palette, PlanningRoom room, string id, string label, string prefab, RectInt rect, Vector2Int origin)
    {
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop,
            new RectInt(rect.position - origin, rect.size))
        { id = id, label = label, isMerged = rect.width * rect.height > 1,
            singleInstance = prefab == "Red_Crank" || prefab == "Respawn_Block" || prefab == "Collectible" ||
                prefab == "Red_ContactCube" || prefab == "Green_ContactCube" };
        if (prefab == null)
        {
            var entry = palette.Entries.First(e => e?.DisplayName == "白方块");
            box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName;
        }
        else
        {
            var entries = palette.PropEntries.Where(e => e?.Prefab?.name == prefab).ToArray();
            Check(entries.Select(e => AssetDatabase.GetAssetPath(e.Prefab)).Distinct().Count() == 1, "unambiguous prefab path: " + prefab);
            var entry = entries.FirstOrDefault();
            Check(entry?.Prefab != null, "palette prefab: " + prefab);
            box.propEntryId = entry.EntryId; box.propEntryName = entry.DisplayName;
        }
        room.boxes.Add(box); return box;
    }

    private static void ConfigureRoute(PlanningBox box, bool vertical, int distance, bool reverse)
    {
        SetParameter(box, "route", "Enum", vertical ? "1" : "0");
        SetParameter(box, "travelBlocks", "Integer", distance.ToString());
        SetParameter(box, "reverseRoute", "Boolean", reverse ? "1" : "0");
        // Keep the same approximate 2 cells/sec on short and long authored routes.
        SetParameter(box, "travelDuration", "Float", (distance * .5f).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void SetParameter(PlanningBox box, string path, string type, string value, string componentType = null)
    {
        var parameter = new LevelEditorComponentValueOverride();
        parameter.Configure("", componentType ?? typeof(MovingPlatformFeature).FullName, 0, path, type, value, null);
        box.hasComponentOverrides = true; box.componentOverrides.Add(parameter);
    }

    private static void VerifyCoverage(PlanningDocument doc, Dictionary<string, HashSet<Vector2Int>> expected, Vector2Int origin)
    {
        foreach (var category in expected)
        {
            var actual = new HashSet<Vector2Int>();
            foreach (var b in doc.rooms.Single().boxes.Where(b => b.label == category.Key))
                foreach (var cell in new RectInt(b.x + origin.x, b.y + origin.y, b.width, b.height).allPositionsWithin)
                    Check(actual.Add(cell), "no duplicate " + category.Key + " cell");
            Check(actual.SetEquals(category.Value), "exact source geometry: " + category.Key);
        }
    }

    private static void VerifyReverseRouteDefaults(LevelEditorPalette palette)
    {
        var go = Object.Instantiate(palette.PropEntries.First(e => e?.Prefab?.name == "Red_Platform").Prefab);
        try
        {
            var feature = go.GetComponent<MovingPlatformFeature>();
            var serialized = new SerializedObject(feature);
            Check(!serialized.FindProperty("reverseRoute").boolValue, "old prefabs retain positive route");
            serialized.FindProperty("travelBlocks").intValue = 5;
            for (int direction = 0; direction < 2; direction++) for (int reverse = 0; reverse < 2; reverse++)
            {
                serialized.FindProperty("route").intValue = direction;
                serialized.FindProperty("reverseRoute").boolValue = reverse == 1;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var wanted = (direction == 0 ? Vector3.right : Vector3.up) * (reverse == 0 ? 5 : -5);
                Check(Vector3.Distance(feature.PreviewEnd - feature.PreviewStart, wanted) < .001f, "four route directions");
            }
        }
        finally { Object.DestroyImmediate(go); }
    }

    private static void VerifyScene(PlanningDocument doc, Dictionary<string, HashSet<Vector2Int>> expected, Vector2Int origin)
    {
        var saved = PlanningSaveStore.Read(SavePath);
        var lifecycle = Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(lifecycle != null && saved.ToPersistentJson() == PlanningDocument.FromPersistentJson(lifecycle.PlanningJson).ToPersistentJson(), "scene and shared map agree");
        Check(saved.ToPersistentJson() == doc.ToPersistentJson(), "map roundtrip");
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length == doc.rooms.Single().boxes.Count, "one instance per editor object");
        foreach (var p in placed)
        {
            var box = doc.rooms.Single().boxes.Single(b => b.id == p.PlanningBoxId);
            Check(p.SizeCells == new Vector2Int(box.width, box.height), "scene size " + box.id);
            var world = new Vector2Int(box.x + origin.x, box.y + origin.y);
            Check(p.Cell == new Vector2Int(world.x, -world.y - box.height), "scene cell " + box.id);
            Check(p.GetComponent<RoomResetObject>() != null, "room reset membership " + box.id);
            if (box.isMerged && box.propEntryId == PlanningDoorUtility.SideLadderEntryId)
            {
                var trigger = p.GetComponent<BoxCollider>();
                Check(trigger != null && trigger.isTrigger && Mathf.Abs(trigger.bounds.size.y - box.height) < .001f &&
                    Mathf.Abs(trigger.bounds.center.y - (-world.y - box.height * .5f)) < .001f, "ladder fits authored cells " + box.id);
            }
            var platform = p.GetComponent<MovingPlatformFeature>();
            if (platform == null) continue;
            var expectedDelta = box.componentOverrides.First(v => v.PropertyPath == "route").Value == "1" ? Vector3.up : Vector3.right;
            expectedDelta *= float.Parse(box.componentOverrides.First(v => v.PropertyPath == "travelBlocks").Value);
            if (box.componentOverrides.First(v => v.PropertyPath == "reverseRoute").Value == "1") expectedDelta *= -1;
            Check(Vector3.Distance(platform.PreviewEnd - platform.PreviewStart, expectedDelta) < .001f, "persisted route " + box.id);
            if (!string.IsNullOrEmpty(box.crankOwnerId))
                Check(platform.IsCrankControlled && platform.CrankController != null && platform.CrankController.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == box.crankOwnerId, "persistent crank binding");
            else
                Check(platform.LinkedControls.Count == 1 && platform.LinkedControls[0].GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == box.mechanismBaseId, "persistent base binding");
        }
        Check(placed.Count(p => p.GetComponent<MovingPlatformFeature>() != null) == 4, "four platforms, no extra auto-created lift");
        Check(placed.Count(p => p.GetComponent<CrankPlatformFeature>() != null) == 2, "two cranks");
        var roots = placed.Where(p => doc.rooms.Single().boxes.Single(b => b.id == p.PlanningBoxId).label == "藤蔓").ToArray();
        Check(roots.Length == 4, "four vine cells");
        var linked = roots.Select(p => p.GetComponent<ClimbableVineFeature>()).ToArray();
        Check(linked.Count(v => new SerializedObject(v).FindProperty("vineRoot").objectReferenceValue != null) == 1, "stacked vine remains one linked root and child");
        Check(GameObject.FindGameObjectsWithTag("Player").Length == 1, "one tagged player");
        Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(GameObject.FindGameObjectWithTag("Player")) == "Assets/_Project/Content/Player/Player.prefab", "standard player");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.isActiveAndEnabled) == 1, "one camera");
        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "one event system");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "no missing scripts");
                Check(!PrefabUtility.IsPrefabAssetMissing(t.gameObject), "no missing prefab");
            }
        VerifyCoverage(saved, expected, origin);
    }

    private static void VerifyIncremental(PlanningDocument doc)
    {
        var before = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Check(before.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i)), "unchanged apply preserves all scene instances");
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
            Check(restored.ToPersistentJson() == doc.ToPersistentJson(), "editor restores correct scene-specific map");
            Check(PlanningSaveStore.LoadAll().Any(s => Path.GetFullPath(s.Path) == Path.GetFullPath(SavePath)), "paired map in save list");
            string before = restored.rooms[0].boxes[0].label;
            restored.rooms[0].boxes[0].label = "往返验证";
            PlanningSaveStore.Save("Library/MRoom2EditRoundtrip.planning.json", restored);
            Check(PlanningSaveStore.Read("Library/MRoom2EditRoundtrip.planning.json").rooms[0].boxes[0].label == "往返验证", "editable save roundtrip");
            restored.rooms[0].boxes[0].label = before;
        }
        finally { Object.DestroyImmediate(window); }
        Check(PlanningSaveStore.Read(SavePath).ToPersistentJson() == doc.ToPersistentJson(), "verification does not leak edits");
    }

    private static void VerifyLadderEditing(PlanningDocument original, LevelEditorPalette palette, Dictionary<string, HashSet<Vector2Int>> expected, Vector2Int origin)
    {
        var doc = PlanningDocument.FromPersistentJson(original.ToPersistentJson());
        var room = doc.rooms.Single();
        var ladder = room.boxes.First(b => b.propEntryId == PlanningDoorUtility.SideLadderEntryId && b.height == 6);
        var canvas = new PlanningCanvas(); canvas.SetDocument(doc); canvas.SelectRoom(room.id); canvas.SetMode(PlanningCanvasMode.Detail);
        var selection = (HashSet<string>)typeof(PlanningCanvas).GetField("mergeSelection", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(canvas);
        selection.Add(ladder.id); canvas.UnmergeSelectedItems();
        Check(room.boxes.Count(b => b.propEntryId == PlanningDoorUtility.SideLadderEntryId) == 8, "six-cell ladder unmerges into six units plus two other runs");
        VerifyCoverage(doc, expected, origin);
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Physics.SyncTransforms();
        foreach (var unit in room.boxes.Where(b => b.propEntryId == PlanningDoorUtility.SideLadderEntryId && b.width == 1 && b.height == 1))
        {
            var instance = placed.Single(p => p.PlanningBoxId == unit.id);
            Check(instance.SizeCells == Vector2Int.one && Mathf.Abs(instance.GetComponent<BoxCollider>().bounds.size.y - 1) < .001f, "unmerged imported ladder stays one cell");
        }
        canvas.MergeSelectedItems();
        Check(room.boxes.Any(b => b.propEntryId == PlanningDoorUtility.SideLadderEntryId && b.height == 6 && b.isMerged), "ladder units merge back to original length");
        VerifyCoverage(doc, expected, origin);
        var legacy = AddBox(palette, room, "mroom2_legacy_ladder_verification", "旧规格梯子验证", "LadderSide", new RectInt(-77, -30, 1, 2), origin);
        legacy.isMerged = false; legacy.singleInstance = true;
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out string message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        var old = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Single(p => p.PlanningBoxId == legacy.id);
        Physics.SyncTransforms();
        Check(old.SizeCells == new Vector2Int(1, 2) && Mathf.Abs(old.GetComponent<BoxCollider>().bounds.size.y - 2) < .001f,
            "legacy authored ladder retains two-cell trigger; metadata=" + old.SizeCells + " scale=" + old.transform.lossyScale +
            " collider=" + old.GetComponent<BoxCollider>().size + " bounds=" + old.GetComponent<BoxCollider>().bounds.size);
    }

    private static void VerifyLadderDefaults()
    {
        foreach (var id in new[] { PlanningDoorUtility.FrontLadderEntryId, PlanningDoorUtility.SideLadderEntryId })
            Check(PlanningDoorUtility.Footprint(id) == Vector2Int.one, "new ladder editor footprint is one cell: " + id);
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.DoorEntryId) == new Vector2Int(1, 2), "vertical door footprint unchanged");
        Check(PlanningDoorUtility.Footprint(PlanningDoorUtility.HorizontalDoorEntryId) == new Vector2Int(2, 1), "horizontal door footprint unchanged");
        foreach (string name in new[] { "LadderFront", "LadderSide" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/Mechanisms/" + name + ".prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var collider = go.GetComponent<BoxCollider>();
                Check(collider.size.y == 1f && collider.center.y == 0f && collider.isTrigger, "default prefab centered unit trigger: " + name);
                var placed = go.AddComponent<LevelEditorPlacedBlock>();
                placed.Configure(Vector2Int.zero, name, Color.white, false, true);
                placed.SetSizeCells(new Vector2Int(1, 2));
                go.GetComponent<MechanismArtworkPresentation>().Refresh(0f);
                Check(Mathf.Approximately(collider.size.y, 2f) && Mathf.Approximately(collider.center.y, .5f), "old scene metadata preserves original two-cell pivot: " + name);
                collider.size = new Vector3(collider.size.x, 1f, collider.size.z); collider.center = Vector3.zero;
                go.transform.localScale = new Vector3(1, 5, 1); placed.SetSizeCells(new Vector2Int(1, 5));
                go.GetComponent<MechanismArtworkPresentation>().Refresh(0f); Physics.SyncTransforms();
                Check(Mathf.Abs(collider.bounds.size.y - 5f) < .001f && collider.center.y == 0f, "scaled long ladder is not expanded twice: " + name);
            }
            finally { Object.DestroyImmediate(go); }
        }
        var document = new PlanningDocument { worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("梯子放置验证"); document.rooms.Add(room);
        foreach (var cell in new RectInt(0, 0, 2, 2).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        var canvas = new PlanningCanvas(); canvas.SetDocument(document); canvas.SelectRoom(room.id); canvas.SetMode(PlanningCanvasMode.Detail);
        var prepare = typeof(PlanningCanvas).GetMethod("PrepareDoorPlacement", BindingFlags.NonPublic | BindingFlags.Instance);
        foreach (var id in new[] { PlanningDoorUtility.FrontLadderEntryId, PlanningDoorUtility.SideLadderEntryId })
        {
            var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 1, 2)) { propEntryId = id };
            Check((bool)prepare.Invoke(canvas, new object[] { room, box, new RectInt(0, 0, 2, 2) }) && box.width == 1 && box.height == 1 && box.singleInstance,
                "real placement prepares one-cell ladder: " + id);
        }
    }

    private static void VerifyExistingLadderScenes()
    {
        foreach (string path in new[] { "Assets/_Project/Scenes/Playing/Gaming.unity", "Assets/Scenes/Pick.unity", "Assets/Scenes/Test.unity" })
        {
            EditorSceneManager.OpenScene(path);
            var ladders = Object.FindObjectsByType<Project.Mechanisms.NeutralLadder>(FindObjectsSortMode.None);
            foreach (var ladder in ladders)
            {
                var placed = ladder.GetComponent<LevelEditorPlacedBlock>();
                if (placed == null) continue;
                MechanismArtworkPresentation.Ensure(ladder.gameObject); Physics.SyncTransforms();
                Check(Mathf.Abs(ladder.GetComponent<BoxCollider>().bounds.size.y - placed.SizeCells.y * placed.CellWorldSize) < .001f,
                    "existing ladder authored height preserved without rebuilding scene: " + path + "/" + ladder.name);
            }
        }
    }
}
