using System;
using System.Collections.Generic;
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

/// <summary>Explicit R1 source import. Does not run at import or rebuild an existing scene.</summary>
public static class R1LevelBuilder
{
    public const string Folder = "Assets/Scenes/R1";
    public const string ScenePath = Folder + "/R1.unity";
    public const string SavePath = Folder + "/R1.planning.json";
    public const string PlayerPath = "Assets/_Project/Content/Player/Player.prefab";
    private static int checks;
    [Serializable] private sealed class Source { public List<PlanningCell> cells; }
    [Serializable] private sealed class OverrideSignature { public List<LevelEditorComponentValueOverride> values; }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("R1: " + message);
    }

    public static void Build()
    {
        Check(Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R1/Assets"), "R1 worktree only");
        Check(!File.Exists(ScenePath) && !File.Exists(SavePath), "new scene only; never overwrite designer edits");
        var doc = CreateDocument();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        Apply(doc);
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn), "spawn exists");
        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
        player.name = "Player";
        player.transform.position = spawn;
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
        new GameObject("R1 Starting Progress").AddComponent<R1SceneSetup>();
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
        var source = JsonUtility.FromJson<Source>(File.ReadAllText(Folder + "/R1.design.json"));
        Check(source.cells.Count == 446, "446 original black cells");
        var walls = source.cells.Where(c => !(c.x == 57 && c.y == 4)).ToList(); // Approved BK10 doorway clearance.
        Check(walls.Count == 445, "only BK10 removed");
        var doc = new PlanningDocument { name = "R1", worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("R1") { id = "ilorr_r1", sourceAreaId = "excel_r1_F6_BK46",
            sourceNotes = "关卡设计.xlsx / Sheet1 / F6:BK46；一格=游戏一格。BK10让位出口门；平台从轨迹下端开始。" };
        foreach (var cell in new RectInt(0, 0, 58, 41).allPositionsWithin)
            room.cells.Add(new PlanningCell(cell.x, cell.y));
        doc.rooms.Add(room);
        var palette = LevelEditorPaletteService.GetOrCreate();
        int part = 0;
        foreach (var rect in PlanningMVMapObjectImporter.Partition(walls))
            Add(palette, room, "wall_" + part++, "实体地形", null, rect, false);
        PlanningBox Box(string id, string label, string prefab, string column, int row, int width = 1, int height = 1, bool single = true)
            => Add(palette, room, id, label, prefab, ExcelRect(column, row, width, height), single);

        var left = Box("crank_left", "左上曲柄 M10", "Red_Crank", "M", 10);
        var right = Box("crank_right", "右侧曲柄 BH27", "Red_Crank", "BH", 27);
        var lower = Box("crank_lower", "下方曲柄 AT33（共享右侧进度）", "Red_Crank", "AT", 33);
        foreach (var crank in new[] { left, right, lower }) crank.initialCrankPlatformCreated = true;
        lower.crankOwnerId = right.id;
        var liftLeft = Box("lift_left", "左平台 P18:S18", "Red_CrankLift", "P", 18, 4, 1, false);
        var liftRight = Box("lift_right", "右平台 AX36:BA36", "Red_CrankLift", "AX", 36, 4, 1, false);
        ConfigureLift(liftLeft, left, 10); ConfigureLift(liftRight, right, 14);
        Box("cube_red", "红方块 Z14", "Red_ContactCube", "Z", 14);
        Box("cube_blue_upper", "蓝方块 AB14", "Blue_ContactCube", "AB", 14);
        Box("cube_blue_middle", "蓝方块 T26", "Blue_ContactCube", "T", 26);
        Box("cube_blue_left", "蓝方块 G32", "Blue_ContactCube", "G", 32);
        var green1 = Box("cube_green_upper", "绿方块 AD14（生长极限 AD7）", "Green_ContactCube", "AD", 14);
        var green2 = Box("cube_green_left", "绿方块 K31（生长极限 K23）", "Green_ContactCube", "K", 31);
        var green3 = Box("cube_green_lower", "绿方块 N39（生长极限 N31）", "Green_ContactCube", "N", 39);
        Parameter(green1, typeof(ContactCube), "maximumLength", "Integer", "8");
        Parameter(green2, typeof(ContactCube), "maximumLength", "Integer", "9");
        Parameter(green3, typeof(ContactCube), "maximumLength", "Integer", "9");
        foreach (var pinned in new[] { green2, green3 })
        {
            Parameter(pinned, typeof(Rigidbody), "m_Constraints", "Integer", ((int)RigidbodyConstraints.FreezeAll).ToString());
            Parameter(pinned, typeof(CarryableFeature), "m_Enabled", "Boolean", "0");
        }
        for (int row = 11; row <= 22; row++) Box("ladder_" + row, "中性梯子 K" + row, "LadderFront", "K", row);
        for (int row = 28; row <= 32; row++) Box("vine_left_" + row, "左藤蔓 I" + row, "Green_Ladder", "I", row);
        for (int row = 17; row <= 25; row++) Box("vine_middle_" + row, "中间藤蔓 Z" + row, "Green_Ladder", "Z", row);
        var fixedGreen = Box("green_horizontal", "固定横向绿方块 X30:AJ30（原横藤蔓）", "Green_ContactCube", "X", 30, 13, 1);
        Parameter(fixedGreen, typeof(ContactCube), "maximumLength", "Integer", "13");
        Parameter(fixedGreen, typeof(ContactCube), "initialLength", "Integer", "13");
        Parameter(fixedGreen, typeof(Rigidbody), "m_Constraints", "Integer", ((int)RigidbodyConstraints.FreezeAll).ToString());
        Parameter(fixedGreen, typeof(CarryableFeature), "m_Enabled", "Boolean", "0");
        foreach (var vine in room.boxes.Where(b => b.id.StartsWith("r1_vine_", StringComparison.Ordinal)))
            Parameter(vine, typeof(ClimbableVineFeature), "maxGrowthBlocks", "Integer", "0");
        Box("spring_upper", "上弹性植物 AN14:AP14", "Green_JumpPlant", "AN", 14, 3, 1, false);
        Box("spring_lower", "下弹性植物 Q41:S41", "Green_JumpPlant", "Q", 41, 3, 1, false);
        var current = Box("current_up", "向上水流 W32:W40", "Blue_WaterWay", "W", 32, 1, 9, false);
        Parameter(current, typeof(DirectionalCurrentFeature), "flowDirection", "Enum", "2");
        Box("lava_upper", "上岩浆 AQ15:AY15", "Red_Lava", "AQ", 15, 9, 1, false);
        Box("lava_lower", "下岩浆 X41:BJ45", "Red_Lava", "X", 41, 39, 5, false);
        var door = Box("exit", "出口门 BK10:BK11", "Door", "BK", 10, 1, 2);
        var button1 = Box("button_left", "门按钮 H23", "DoorButton", "H", 23);
        var button2 = Box("button_middle", "门按钮 U26", "DoorButton", "U", 26);
        int order = 0;
        foreach (var button in new[] { button1, button2 })
        {
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
        void Note(string id, string text, string column, int row, int height, string color)
        {
            var rect = ExcelRect(column, row, 1, height);
            doc.regions.Add(new PlanningRegion { id = "r1_note_" + id, annotation = text,
                x = rect.x, y = rect.y, width = 1, height = height, colorHex = color });
        }
        Note("route_left", "左平台轨迹（非实体）", "Q", 8, 11, "#999999");
        Note("route_right", "右平台轨迹（非实体）", "AY", 22, 15, "#999999");
        Note("growth1", "AD14 方块生长极限", "AD", 7, 1, "#A8D08D");
        Note("growth2", "K31 方块生长极限", "K", 23, 1, "#A8D08D");
        Note("growth3", "N39 方块生长极限", "N", 31, 1, "#A8D08D");
        Note("player_max", "玩家最高处（设计标注）", "W", 29, 1, "#FFD966");
        doc.playerStartRoomId = room.id; doc.playerStartLocal = new Vector2(5.5f, 34.5f); // K40.
        doc.Normalize();
        return doc;
    }

    private static RectInt ExcelRect(string column, int row, int width, int height)
    {
        int index = 0; foreach (char letter in column) index = index * 26 + letter - 'A' + 1;
        return new RectInt(index - 6, row - 6, width, height);
    }
    private static PlanningBox Add(LevelEditorPalette palette, PlanningRoom room, string id, string label, string prefab, RectInt rect, bool single)
    {
        var box = new PlanningBox(prefab == null ? PlanningDetailType.Solid : PlanningDetailType.Prop, rect)
        { id = "r1_" + id, label = label, singleInstance = single, isMerged = !single && rect.width * rect.height > 1 };
        if (prefab == null)
        {
            var entry = palette.Entries.Single(e => e?.DisplayName == "白方块");
            box.paletteEntryId = entry.EntryId; box.paletteEntryName = entry.DisplayName;
        }
        else if (prefab == "DoorButton") { box.propEntryId = PlanningDoorUtility.ButtonEntryId; box.propEntryName = "门按钮"; }
        else
        {
            var entries = palette.PropEntries.Where(e => e?.Prefab?.name == prefab).ToArray();
            Check(entries.Length > 0 && entries.Select(e => AssetDatabase.GetAssetPath(e.Prefab)).Distinct().Count() == 1, "unambiguous prefab " + prefab);
            box.propEntryId = entries[0].EntryId; box.propEntryName = entries[0].DisplayName;
        }
        room.boxes.Add(box); return box;
    }
    private static void Parameter(PlanningBox box, Type type, string path, string valueType, string value)
    {
        var parameter = new LevelEditorComponentValueOverride(); parameter.Configure("", type.FullName, 0, path, valueType, value, null);
        box.hasComponentOverrides = true; box.componentOverrides.Add(parameter);
    }
    private static void ConfigureLift(PlanningBox box, PlanningBox crank, int distance)
    {
        box.crankOwnerId = crank.id;
        Parameter(box, typeof(MovingPlatformFeature), "pathMode", "Enum", "3");
        Parameter(box, typeof(MovingPlatformFeature), "upDistance", "Float", distance.ToString());
    }
    private static void Apply(PlanningDocument doc)
    {
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try { Check(PlanningSceneBuilder.TryApplyDocument(doc, out var message), message); }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
    }

    /// <summary>Explicit migration of two saved instances, without applying/rebuilding the map.</summary>
    public static void PinGreenCubes()
    {
        checks = 0;
        Check(Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R1/Assets"), "dedicated R1 worktree only");
        Check(Application.isBatchMode && !EditorApplication.isPlaying, "closed author editor; batch edit only");
        const string backup = "Library/R1BeforeFixedGreenCubes-20261019";
        Directory.CreateDirectory(backup);
        foreach (var path in new[] { ScenePath, SavePath })
        {
            var destination = Path.Combine(backup, Path.GetFileName(path));
            Check(!File.Exists(destination), "refuse to overwrite existing backup " + destination);
            File.Copy(path, destination);
        }
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var doc = PlanningSaveStore.Read(SavePath);
        var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(manager != null && PlanningDocument.FromPersistentJson(manager.PlanningJson).ToPersistentJson() == doc.ToPersistentJson(),
            "embedded and paired document agree before editing");
        var blocks = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(blocks.Length == 76 && doc.rooms.Count == 1 && doc.rooms[0].boxes.Count == 76, "all 76 authored roots retained");
        var poses = blocks.ToDictionary(p => p.PlanningBoxId,
            p => (position: p.transform.position, rotation: p.transform.rotation, scale: p.transform.localScale));
        var signatures = blocks.ToDictionary(p => p.PlanningBoxId, p => p.PlanningSignature);
        var targets = new[] { "r1_cube_green_left", "r1_cube_green_lower" };
        foreach (var id in targets)
        {
            var placed = blocks.Single(p => p.PlanningBoxId == id);
            var box = doc.rooms[0].boxes.Single(b => b.id == id);
            var body = placed.GetComponent<Rigidbody>();
            var carry = placed.GetComponent<CarryableFeature>();
            Check(placed.GetComponent<ContactCube>().Color == CubeColor.Green && body != null && carry != null, "expected green cube " + id);
            Check(!box.componentOverrides.Any(v => v.ComponentTypeName == typeof(Rigidbody).FullName && v.PropertyPath == "m_Constraints") &&
                !box.componentOverrides.Any(v => v.ComponentTypeName == typeof(CarryableFeature).FullName && v.PropertyPath == "m_Enabled"),
                "pin migration has not already been applied " + id);
            Parameter(box, typeof(Rigidbody), "m_Constraints", "Integer", ((int)RigidbodyConstraints.FreezeAll).ToString());
            Parameter(box, typeof(CarryableFeature), "m_Enabled", "Boolean", "0");
            body.constraints = RigidbodyConstraints.FreezeAll;
            carry.enabled = false;
            placed.SetPlanningSource(box.id, JsonUtility.ToJson(box.PersistentClone()) + JsonUtility.ToJson(new OverrideSignature
            { values = LevelEditorComponentOverrideUtility.PersistentValues(box.componentOverrides) }), new Vector2Int(box.x, box.y));
            foreach (var component in new Component[] { body, carry, placed })
            {
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        manager.Configure(new List<RoomRegion>(manager.Regions), manager.CellSize, doc.ToPersistentJson(true));
        EditorUtility.SetDirty(manager);
        PlanningSaveStore.Save(SavePath, doc);
        Check(EditorSceneManager.SaveScene(scene), "save targeted scene edit");
        EditorSceneManager.OpenScene(ScenePath);
        var after = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(after.Length == poses.Count, "no author root added or removed");
        foreach (var placed in after)
        {
            var before = poses[placed.PlanningBoxId];
            Check(Vector3.Distance(placed.transform.position, before.position) < .00001f &&
                Quaternion.Angle(placed.transform.rotation, before.rotation) < .001f && placed.transform.localScale == before.scale,
                "exact saved pose unchanged " + placed.PlanningBoxId);
            if (targets.Contains(placed.PlanningBoxId))
                Check(placed.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll &&
                    !placed.GetComponent<CarryableFeature>().enabled, "saved fixed and noncarryable " + placed.PlanningBoxId);
            else Check(placed.PlanningSignature == signatures[placed.PlanningBoxId], "unrelated signature unchanged " + placed.PlanningBoxId);
        }
        Check(PlanningDocument.FromPersistentJson(Object.FindFirstObjectByType<RoomLifecycleManager>().PlanningJson).ToPersistentJson() ==
            PlanningSaveStore.Read(SavePath).ToPersistentJson(), "embedded and paired planning agree after reload");
        File.WriteAllText("Library/R1PinnedGreenCubesMigration.txt", $"PASS {checks} assertions; only K31/N39 constraints and carry enabled state changed, all 76 exact author poses preserved. Backup: {backup}.");
        Debug.Log(File.ReadAllText("Library/R1PinnedGreenCubesMigration.txt"));
    }
    public static void Verify()
    {
        checks = 0;
        var doc = PlanningSaveStore.Read(SavePath);
        Check(doc.ToPersistentJson() == CreateDocument().ToPersistentJson(), "paired save equals confirmed source");
        var imported = PlanningFragmentUtility.Import(new PlanningDocument(), doc, new Vector2Int(100, 100), true, out var detached);
        Check(detached == 0, "full fragment has no detached links");
        var importedCrank = imported.rooms.Single().boxes.Single(b => b.label.StartsWith("下方曲柄", StringComparison.Ordinal));
        var importedOwner = imported.rooms.Single().boxes.Single(b => b.label.StartsWith("右侧曲柄", StringComparison.Ordinal));
        Check(importedCrank.crankOwnerId == importedOwner.id && importedOwner.id != "r1_crank_right", "shared handle stable IDs remap on fragment import");
        var removedOwner = PlanningDocument.FromPersistentJson(doc.ToPersistentJson());
        removedOwner.rooms[0].boxes.RemoveAll(b => b.id == "r1_crank_right");
        PlanningMechanismUtility.RemoveMissingLinks(removedOwner.rooms[0].boxes);
        Check(removedOwner.rooms[0].boxes.Single(b => b.id == "r1_crank_lower").crankOwnerId == null &&
            removedOwner.rooms[0].boxes.Single(b => b.id == "r1_lift_right").crankOwnerId == null, "deleting owner clears both handle and platform links");
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        var before = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i).ToArray();
        Apply(doc);
        Check(before.SequenceEqual(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Select(p => p.GetInstanceID()).OrderBy(i => i)), "no-op incremental apply retains every object");
        VerifyScene(doc);
        PlanningSaveStore.Save("Library/R1Roundtrip.planning.json", doc);
        Check(PlanningSaveStore.Read("Library/R1Roundtrip.planning.json").ToPersistentJson() == doc.ToPersistentJson(), "editable save roundtrip");
        EditorSceneManager.OpenScene(ScenePath);
        VerifyScene(doc);
        File.WriteAllText("Library/R1Verification.txt", $"PASS {checks} assertions; {doc.rooms[0].boxes.Count} objects; 445 terrain cells; 2 platforms / 3 handles / 2 shared controllers; 1 door / 2 latched buttons; 14 vine cells; one fixed 13-cell green bar; 12 ladder cells; standard player K40.");
        Debug.Log(File.ReadAllText("Library/R1Verification.txt"));
    }
    private static void VerifyScene(PlanningDocument doc)
    {
        var lifecycle = Object.FindFirstObjectByType<RoomLifecycleManager>();
        Check(lifecycle != null && PlanningDocument.FromPersistentJson(lifecycle.PlanningJson).ToPersistentJson() == doc.ToPersistentJson(), "embedded planning matches paired save");
        var placed = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
        Check(placed.Length == doc.rooms[0].boxes.Count, "one generated object per author box");
        foreach (var p in placed)
        {
            var box = doc.rooms[0].boxes.Single(b => b.id == p.PlanningBoxId);
            Check(p.SizeCells == new Vector2Int(box.width, box.height), "size " + box.id);
            Check(p.Cell == new Vector2Int(box.x, -box.y - box.height), "position " + box.id);
            Check(p.GetComponent<RoomResetObject>() != null, "room membership " + box.id);
        }
        var expectedWalls = JsonUtility.FromJson<Source>(File.ReadAllText(Folder + "/R1.design.json")).cells
            .Where(c => !(c.x == 57 && c.y == 4)).Select(c => new Vector2Int(c.x, c.y)).ToHashSet();
        var actualWalls = new HashSet<Vector2Int>();
        foreach (var box in doc.rooms[0].boxes.Where(b => b.type == PlanningDetailType.Solid))
            foreach (var cell in new RectInt(box.x, box.y, box.width, box.height).allPositionsWithin)
                Check(actualWalls.Add(cell), "no duplicate terrain");
        Check(actualWalls.SetEquals(expectedWalls), "exact Excel terrain minus approved BK10");
        var platforms = placed.Select(p => p.GetComponent<MovingPlatformFeature>()).Where(p => p != null).ToArray();
        Check(platforms.Length == 2 && platforms.All(p => p.CrankController != null && p.IsCrankControlled), "two bound crank platforms");
        Check(platforms.Single(p => p.CrankController.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_crank_left").Path.FirstEnd == 10f, "left route 10 cells upward");
        Check(platforms.Single(p => p.CrankController.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_crank_right").Path.FirstEnd == 14f, "right route 14 cells upward");
        var cranks = placed.Select(p => p.GetComponent<CrankPlatformFeature>()).Where(c => c != null).ToArray();
        Check(cranks.Length == 3 && cranks.Count(c => c.SharedController != null) == 1, "three handles, one shared secondary");
        var lower = cranks.Single(c => c.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_crank_lower");
        Check(lower.SharedController.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == "r1_crank_right", "lower shares right crank progress");
        var fixedGreen = placed.Single(p => p.PlanningBoxId == "r1_green_horizontal");
        Check(fixedGreen.SizeCells == new Vector2Int(13, 1) && fixedGreen.transform.lossyScale == Vector3.one, "13-cell single cube footprint without root scaling");
        Check(!fixedGreen.GetComponent<CarryableFeature>().enabled && fixedGreen.GetComponent<Rigidbody>().constraints == RigidbodyConstraints.FreezeAll, "green bar pinned and not carryable");
        var door = Object.FindFirstObjectByType<DoorController>();
        Check(door != null && door.Buttons.Count == 2 && door.Buttons.All(b => b.Owner == door && b.Persistent), "one door with both latched buttons");
        Check(Object.FindObjectsByType<DirectionalCurrentFeature>(FindObjectsSortMode.None).Single().VisualFlowDirection == Vector2.up, "upward water");
        var players = GameObject.FindGameObjectsWithTag("Player");
        Check(players.Length == 1 && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(players[0]) == PlayerPath, "unique standard player");
        Check(doc.TryGetPlayerStartWorldPosition(out var spawn) && Vector3.Distance(players[0].transform.position, spawn) < .001f && spawn == new Vector3(5.5f, -34.5f, 0), "K40 spawn");
        Check(Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Count(c => c.enabled) == 1, "one camera");
        Check(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 1, "one audio listener");
        Check(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "one event system");
        Check(Object.FindFirstObjectByType<R1SceneSetup>() != null, "scene-specific starting progress");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0 && !PrefabUtility.IsPrefabAssetMissing(t.gameObject), "no missing dependency");
    }
    // Explicit incremental update; never runs as an import callback.
    public static void UpdateConfirmed()
    {
        Check(Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R1/Assets"), "R1 worktree only");
        var doc = CreateDocument();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        Apply(doc);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "updated scene saved");
        PlanningSaveStore.Save(SavePath, doc); AssetDatabase.Refresh(); AssetDatabase.SaveAssets(); Verify();
    }
}
