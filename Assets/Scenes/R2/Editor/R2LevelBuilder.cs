using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.CameraModes;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Levels.ILorR;
using Project.Mechanisms;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Explicit source import. Never executes on import or overwrites an existing level.</summary>
public static class R2LevelBuilder
{
    public const string Folder = "Assets/Scenes/R2";
    public const string ScenePath = Folder + "/R2.unity";
    public const string SavePath = Folder + "/R2.planning.json";
    public const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
    private static int checks;
    [Serializable] private sealed class WallRun { public int row, firstColumn, lastColumn; }
    [Serializable] private sealed class Cell { public string column; public int row; }
    [Serializable] private sealed class GrowingCube
    {
        public string id, column, targetColumn;
        public int row, targetRow;
        public bool pinned;
    }
    [Serializable] private sealed class Source
    {
        public bool designConfirmed;
        public List<WallRun> wallRuns;
        public List<GrowingCube> growingCubes;
        public List<Cell> obstacleCells, blueCubes;
    }
    private static Source ReadSource() => JsonUtility.FromJson<Source>(File.ReadAllText(Folder + "/R2.design.json"));
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("R2: " + message);
    }
    private static List<PlanningCell> Terrain(Source source)
    {
        var cells = new List<PlanningCell>();
        foreach (var run in source.wallRuns)
            for (int col = run.firstColumn; col <= run.lastColumn; col++)
                cells.Add(new PlanningCell(col - 6, run.row - 96));
        Check(cells.Count == 393 && cells.Select(c => new Vector2Int(c.x,c.y)).Distinct().Count() == 393, "393 exact original black cells, no removals");
        return cells;
    }
    public static void Build()
    {
        Check(Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R2/Assets"), "isolated R2 worktree only");
        Check(ReadSource().designConfirmed, "wait for designer confirmation; do not publish an incomplete scene");
        Check(!File.Exists(ScenePath) && !File.Exists(SavePath), "new scene only; never overwrite designer changes");
        var doc = CreateDocument();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Apply(doc);
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn), "spawn exists");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        player.name = "Player"; player.transform.position = spawn;
        var cameraRoot = new GameObject("Main Camera") { tag = "MainCamera" };
        var camera = cameraRoot.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .15f, .19f);
        cameraRoot.AddComponent<AudioListener>();
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var manager = cameraRoot.AddComponent<CameraControlManager>(); manager.ConfigureOutput(camera);
        var mode = cameraRoot.AddComponent<CameraModeController>(); mode.Configure(camera, player.transform);
        var follow = cameraRoot.AddComponent<CameraFollowController>(); follow.Configure(manager, mode, player.transform);
        follow.SetRequestedMode(CameraViewMode.Side2D, 0, true);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/UI/SharedGameUI.prefab"));
        new GameObject("R2 Starting Progress").AddComponent<R2SceneSetup>();
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = Color.white;
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional; light.intensity = .8f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "scene saved");
        PlanningSaveStore.Save(SavePath, doc);
        AssetDatabase.Refresh(); AssetDatabase.SaveAssets();
        Verify();
    }

    public static PlanningDocument CreateDocument()
    {
        var source = ReadSource();
        var doc = new PlanningDocument { name = "R2", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("R2") { id = "ilorr_r2", sourceAreaId = "excel_r2_F97_BK129",
            sourceNotes = "关卡设计.xlsx / Sheet1 / F97:BK129；原点F96，向上扩一行容纳Y96:Y97门；不移除黑色地形。" };
        foreach (var cell in new RectInt(0, 0, 58, 34).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        doc.rooms.Add(room);
        var palette = LevelEditorPaletteService.GetOrCreate();
        int part = 0;
        foreach (var rect in PlanningMVMapObjectImporter.Partition(Terrain(source)))
            Add(palette, room, "wall_" + part++, "实体地形", null, rect, false);
        PlanningBox Box(string id, string label, string prefab, string column, int row, int width = 1, int height = 1, bool single = true)
            => Add(palette, room, id, label, prefab, ExcelRect(column, row, width, height), single);

        var crank = Box("crank", "曲柄 AN112", "Red_Crank", "AN", 112);
        crank.initialCrankPlatformCreated = true;
        var lift = Box("lift", "移动平台 AF118:AI118（升至第108行）", "Red_CrankLift", "AF", 118, 4, 1, false);
        lift.crankOwnerId = crank.id;
        Parameter(lift, typeof(MovingPlatformFeature), "pathMode", "Enum", "3");
        Parameter(lift, typeof(MovingPlatformFeature), "upDistance", "Float", "10");
        Box("cube_red_middle", "红方块 AQ115", "Red_ContactCube", "AQ", 115);
        Box("cube_red_entrance", "红方块 BI115", "Red_ContactCube", "BI", 115);
        foreach (var cell in source.blueCubes)
            Box("cube_blue_" + cell.column + cell.row, "补充蓝方块 " + cell.column + cell.row, "Blue_ContactCube", cell.column, cell.row);
        foreach (var spec in source.growingCubes)
        {
            var root = ExcelRect(spec.column, spec.row, 1, 1);
            var target = ExcelRect(spec.targetColumn, spec.targetRow, 1, 1);
            Check(root.x == target.x || root.y == target.y, "growth endpoint must be cardinal: " + spec.id);
            int length = Math.Abs(target.x - root.x) + Math.Abs(target.y - root.y) + 1;
            var cube = Box(spec.id, "绿方块 " + spec.column + spec.row + "（极限 " + spec.targetColumn + spec.targetRow + "）", "Green_ContactCube", spec.column, spec.row);
            Parameter(cube, typeof(ContactCube), "maximumLength", "Integer", length.ToString(CultureInfo.InvariantCulture));
            if (spec.pinned)
            {
                Parameter(cube, typeof(Rigidbody), "m_Constraints", "Integer", ((int)RigidbodyConstraints.FreezeAll).ToString());
                Parameter(cube, typeof(CarryableFeature), "m_Enabled", "Boolean", "0");
            }
        }
        foreach (var cell in source.obstacleCells)
            Box("plant_" + cell.column + cell.row, "植物障碍 " + cell.column + cell.row, "Green_PlantObstacle", cell.column, cell.row);
        for (int row = 98; row <= 104; row++)
        {
            var vine = Box("vine_" + row, "开局完整藤蔓 AX" + row, "Green_Ladder", "AX", row);
            Parameter(vine, typeof(ClimbableVineFeature), "maxGrowthBlocks", "Integer", "0");
        }
        Box("spring_upper", "弹性植物 O113:P113", "Green_JumpPlant", "O", 113, 2, 1, false);
        Box("spring_middle", "弹性植物 K120:L120", "Green_JumpPlant", "K", 120, 2, 1, false);
        Box("spring_lower", "弹性植物 O127:P127", "Green_JumpPlant", "O", 127, 2, 1, false);
        Box("water", "普通水域 U98:Z105", "Blue_WaterSource", "U", 98, 6, 8, false);
        Box("lava", "岩浆 T127:AJ128", "Red_Lava", "T", 127, 17, 2, false);
        Box("spikes_upper", "朝上尖刺 AF119:AM119", "Spike_上", "AF", 119, 8, 1, false);
        Box("spikes_lower", "朝上尖刺 AS125:BF125", "Spike_上", "AS", 125, 14, 1, false);
        Box("collectible_upper", "收集品 AX105", "Collectible", "AX", 105);
        Box("collectible_inner", "收集品 AP118", "Collectible", "AP", 118);
        var door = Box("exit", "出口竖门 Y96:Y97", "Door", "Y", 96, 1, 2);
        int order = 0;
        foreach (var cell in new[] { new Cell {column = "AC", row = 104}, new Cell {column = "AB", row = 112}, new Cell {column = "AL", row = 125} })
        {
            var button = Box("button_" + cell.column + cell.row, "门按钮 " + cell.column + cell.row, "DoorButton", cell.column, cell.row);
            button.doorOwnerId = door.id; button.doorButtonOrder = ++order;
            Parameter(button, typeof(DoorButton), "mode", "Enum", "0");
        }
        foreach (var box in room.boxes.Where(b => b.isMerged))
            foreach (var cell in new RectInt(box.x, box.y, box.width, box.height).allPositionsWithin)
            {
                var unit = box.Clone(); unit.mergeParts.Clear(); unit.id += "_cell_" + cell.x + "_" + cell.y;
                unit.x = cell.x; unit.y = cell.y; unit.width = unit.height = 1; unit.isMerged = false;
                box.mergeParts.Add(JsonUtility.ToJson(unit));
            }
        doc.regions.Add(new PlanningRegion { id = "r2_note_route", annotation = "平台轨迹（非实体）", x = 27, y = 12, width = 1, height = 11, colorHex = "#999999" });
        foreach (var spec in source.growingCubes)
        {
            var target = ExcelRect(spec.targetColumn, spec.targetRow, 1, 1);
            doc.regions.Add(new PlanningRegion { id = "r2_note_" + spec.id, annotation = spec.column + spec.row + " 生长极限", x = target.x, y = target.y, width = 1, height = 1, colorHex = "#A8D08D" });
        }
        doc.playerStartRoomId = room.id; doc.playerStartLocal = new Vector2(57.5f, 19.5f); // BK115.
        doc.Normalize(); return doc;
    }

    private static RectInt ExcelRect(string column, int row, int width, int height)
    {
        int index = 0; foreach (char letter in column) index = index * 26 + letter - 'A' + 1;
        return new RectInt(index - 6, row - 96, width, height);
    }
    private static PlanningBox Add(LevelEditorPalette palette, PlanningRoom room, string id, string label, string prefab, RectInt rect, bool single)
    {
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop, rect)
        { id = "r2_" + id, label = label, singleInstance = single, isMerged = !single && rect.width * rect.height > 1 };
        if (prefab == null)
        {
            var entry = palette.Entries.Single(e => e?.DisplayName == "白方块");
            box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName;
        }
        else if (prefab == "DoorButton") { box.propEntryId = PlanningDoorUtility.ButtonEntryId; box.propEntryName = "门按钮"; }
        else
        {
            var entries = palette.PropEntries.Where(e => e?.Prefab?.name == prefab).ToArray();
            Check(entries.Length > 0 && entries.Select(e => AssetDatabase.GetAssetPath(e.Prefab)).Distinct().Count() == 1, "unambiguous existing prefab " + prefab);
            box.propEntryId = entries[0].EntryId; box.propEntryName = entries[0].DisplayName;
        }
        room.boxes.Add(box); return box;
    }
    private static void Parameter(PlanningBox box, Type type, string path, string valueType, string value)
    {
        var parameter = new LevelEditorComponentValueOverride(); parameter.Configure("", type.FullName, 0, path, valueType, value, null);
        box.hasComponentOverrides = true; box.componentOverrides.Add(parameter);
    }
    private static void Apply(PlanningDocument doc)
    {
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out var message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
    }
    public static void Preflight()
    {
        checks = 0;
        var doc = CreateDocument();
        Check(PlanningDocument.FromPersistentJson(doc.ToPersistentJson()).ToPersistentJson() == doc.ToPersistentJson(), "planning roundtrip");
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn) && spawn == new Vector3(57.5f,-19.5f,0), "BK115 spawn");
        var walls = new HashSet<Vector2Int>();
        foreach (var box in doc.rooms.Single().boxes.Where(b => b.type == PlanningDetailType.Solid))
            foreach (var cell in new RectInt(box.x,box.y,box.width,box.height).allPositionsWithin)
                Check(walls.Add(cell), "no duplicated terrain cell");
        Check(walls.SetEquals(Terrain(ReadSource()).Select(c => new Vector2Int(c.x,c.y))), "partition preserves every Excel black cell");
        Check(!walls.Contains(new Vector2Int(19,0)) && !walls.Contains(new Vector2Int(19,1)), "Y96:Y97 doorway contains no original wall");
        var door = doc.rooms[0].boxes.Single(b => b.id == "r2_exit");
        Check(door.x == 19 && door.y == 0 && door.width == 1 && door.height == 2, "approved vertical door footprint");
        var buttons = doc.rooms[0].boxes.Where(b => b.doorOwnerId == door.id).ToArray();
        Check(buttons.Length == 3 && buttons.Select(b => b.doorButtonOrder).SequenceEqual(new[]{1,2,3}), "all three buttons bind to the exit with stable order");
        Check(ReadSource().designConfirmed && ReadSource().blueCubes.Count == 0, "confirmed layout; no added blue cube");
        Check(ReadSource().growingCubes.Count == 2 && ReadSource().obstacleCells.Count == 8, "two growing roots and eight plant cells");
        File.WriteAllText("Library/R2Preflight.txt", $"PASS {checks} source checks; {doc.rooms[0].boxes.Count} author objects. No blue cube; growth trigger awaits programmer confirmation.");
        Debug.Log(File.ReadAllText("Library/R2Preflight.txt"));
    }
    public static void Verify()
    {
        checks = 0;
        Check(ReadSource().designConfirmed, "complete designer confirmation required");
        var doc = PlanningSaveStore.Read(SavePath);
        Check(doc.ToPersistentJson() == CreateDocument().ToPersistentJson(), "paired save equals source");
        Check(PlanningDocument.FromPersistentJson(doc.ToPersistentJson()).ToPersistentJson() == doc.ToPersistentJson(), "editable save roundtrip");
        var imported = PlanningFragmentUtility.Import(new PlanningDocument(), doc, new Vector2Int(100, 100), true, out var detached);
        Check(detached == 0, "full-room fragment has no detached links");
        var importedBoxes = imported.rooms.Single().boxes;
        var importedDoor = importedBoxes.Single(b => b.label == "出口竖门 Y96:Y97");
        Check(importedDoor.id != "r2_exit" && importedBoxes.Count(b => b.doorOwnerId == importedDoor.id) == 3, "fragment remaps door and all button IDs");
        var importedCrank = importedBoxes.Single(b => b.label == "曲柄 AN112");
        Check(importedCrank.id != "r2_crank" && importedBoxes.Single(b => b.label.StartsWith("移动平台", StringComparison.Ordinal)).crankOwnerId == importedCrank.id, "fragment remaps crank binding");
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        var before = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
        Apply(doc);
        Check(before.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i)), "no-op apply preserves every instance");
        VerifyScene(doc);
        PlanningSaveStore.Save("Library/R2Roundtrip.planning.json", doc);
        Check(PlanningSaveStore.Read("Library/R2Roundtrip.planning.json").ToPersistentJson() == doc.ToPersistentJson(), "disk save roundtrip");
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        File.WriteAllText("Library/R2Verification.txt", $"PASS {checks} assertions; {doc.rooms[0].boxes.Count} objects, 393 exact terrain cells, paired/embedded/roundtrip/fragment/no-op/reload, complete dependencies, unique standard player, door/3 latched buttons, crank/10-cell lift, 2 green roots, 8 plant cells, no blue cube. Play audit is separate; not a full route completion.");
        Debug.Log(File.ReadAllText("Library/R2Verification.txt"));
    }
    private static void VerifyScene(PlanningDocument doc)
    {
        var lifecycle = Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(lifecycle != null && PlanningDocument.FromPersistentJson(lifecycle.PlanningJson).ToPersistentJson() == doc.ToPersistentJson(), "embedded planning equals paired save");
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length == doc.rooms[0].boxes.Count, "one instance per author object");
        foreach (var p in placed)
        {
            var box = doc.rooms[0].boxes.Single(b => b.id == p.PlanningBoxId);
            Check(p.SizeCells == new Vector2Int(box.width, box.height), "footprint " + box.id);
            Check(p.Cell == new Vector2Int(box.x, -box.y - box.height), "cell " + box.id);
            Check(p.GetComponent<RoomResetObject>() != null, "room membership " + box.id);
            if (box.type == PlanningDetailType.Solid)
            {
                var collider = p.GetComponent<BoxCollider>();
                Check(collider != null && Vector3.Distance(collider.bounds.center,new Vector3(box.x+box.width*.5f,-box.y-box.height*.5f,0)) < .001f &&
                    Vector3.Distance(collider.bounds.size,new Vector3(box.width,box.height,1)) < .001f, "physical terrain exactly covers source cells " + box.id);
                Check(p.GetComponent<Renderer>().sharedMaterial != null, "terrain material dependency " + box.id);
            }
        }
        Check(Object.FindFirstObjectByType<RoomLifecycleManager>() != null, "room lifecycle");
        var door = Object.FindFirstObjectByType<DoorController>();
        Check(door != null && door.Buttons.Count == 3 && door.Buttons.All(b => b.Owner == door && b.Persistent), "one door, all three latched buttons");
        var lift = Object.FindObjectsByType<MovingPlatformFeature>(FindObjectsSortMode.None).Single();
        Check(lift.CrankController != null && lift.IsCrankControlled && lift.Path.FirstEnd == 10f, "bound crank, ten-cell upward route");
        Check(placed.Count(p => p.GetComponent<CrankPlatformFeature>() != null) == 1, "one crank");
        var cubes = placed.Select(p => p.GetComponent<ContactCube>()).Where(c => c != null).ToArray();
        Check(cubes.Length == 4 && cubes.Count(c => c.Color == CubeColor.Red) == 2 && cubes.All(c => c.Color != CubeColor.Blue), "two red/two green/no blue cubes");
        var upper = placed.Single(p => p.PlanningBoxId == "r2_cube_green_upper");
        Check(upper.GetComponent<ContactCube>().MaximumLength == 11 && upper.transform.lossyScale == Vector3.one, "AK114 maximum eleven without root scaling");
        Check(upper.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll && !upper.GetComponent<CarryableFeature>().enabled, "AK114 fixed and not carryable");
        var entrance = placed.Single(p => p.PlanningBoxId == "r2_cube_green_entrance");
        Check(entrance.GetComponent<ContactCube>().MaximumLength == 9 && entrance.GetComponent<CarryableFeature>().enabled && entrance.GetComponent<Rigidbody>().constraints != RigidbodyConstraints.FreezeAll, "BG115 maximum nine and ordinary physics");
        var plants = placed.Where(p => p.GetComponent<PlantObstacleFeature>() != null).ToArray();
        var expectedPlants = ReadSource().obstacleCells.Select(c => { var r = ExcelRect(c.column,c.row,1,1); return new Vector2Int(r.x,-r.y-1); }).ToHashSet();
        Check(plants.Length == 8 && plants.Select(p => p.Cell).ToHashSet().SetEquals(expectedPlants), "all eight exact plant cells, collectible hole preserved");
        Check(placed.Count(p => p.GetComponent<ClimbableVineFeature>() != null) == 7, "seven initial vine cells");
        var players = GameObject.FindGameObjectsWithTag("Player");
        Check(players.Length == 1 && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0]) == PlayerPath, "unique standard player prefab");
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn) && spawn == new Vector3(57.5f,-19.5f,0) && Vector3.Distance(players[0].transform.position,spawn) < .001f, "BK115 spawn");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "one gameplay camera");
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "one listener");
        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "one event system");
        Check(Object.FindFirstObjectByType<R2SceneSetup>() != null, "blue-only standalone progress setup");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0 && !PrefabUtility.IsPrefabAssetMissing(t.gameObject), "no missing scripts/prefabs");
    }

    public static void FinalAudit() { Preflight(); Verify(); }
}
