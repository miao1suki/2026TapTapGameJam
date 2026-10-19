using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

/// <summary>Disposable edit-mode planner → JSON → real body → Scene/canvas native-pixel checks.</summary>
public static class PlatformShapePlacementAudit
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool result, string description)
    {
        if (!result) throw new InvalidOperationException("PLATFORM SHAPE: " + description);
        checks++; Debug.Log("PLATFORM SHAPE PASS: " + description);
    }
    private static object Call(PlanningCanvas canvas, string method, params object[] args) =>
        typeof(PlanningCanvas).GetMethod(method, Hidden).Invoke(canvas, args);

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            Array.IndexOf(Environment.GetCommandLineArgs(), "-noaudio") < 0)
            throw new InvalidOperationException("Use the silent isolated verification project only.");
        int exit = 0;
        try
        {
            checks = 0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
            var library = ObjectArtworkLibrary.Load();
            Check(palette != null && library != null, "formal palette and artwork loaded read-only");
            foreach (string entryId in new[] { PlanningDoorUtility.MovingPlatformEntryId, PlanningMechanismUtility.LiftEntryId })
            foreach (bool vertical in new[] { false, true })
            foreach (int length in new[] { 1, 3, 5 })
                Verify(palette, entryId, vertical, length);
            foreach (string entryId in new[] { PlanningDoorUtility.MovingPlatformEntryId, PlanningMechanismUtility.LiftEntryId })
            foreach (bool vertical in new[] { false, true })
            foreach (int length in new[] { 2, 3 })
                VerifyMerge(palette, entryId, vertical, length);
            VerifyRejectedMerges(palette);
            VerifyWaterPalette(palette);
            VerifyLegacyRecords(palette);
            VerifyShapeUI();
            File.WriteAllText("Library/PlatformShapePlacementAudit.txt", "PASS " + checks);
            Debug.Log("PLATFORM SHAPE FINAL PASS " + checks);
        }
        catch (Exception error)
        {
            exit = 1; File.WriteAllText("Library/PlatformShapePlacementAudit.txt", error.ToString()); Debug.LogException(error);
        }
        EditorApplication.Exit(exit);
    }

    private static void Verify(LevelEditorPalette palette, string entryId, bool vertical, int length)
    {
        string label = entryId + " " + (vertical ? "vertical" : "horizontal") + length;
        var room = new PlanningRoom("Shape fixture");
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) room.cells.Add(new PlanningCell(x, y));
        var document = new PlanningDocument(); document.rooms.Add(room);
        var canvas = new PlanningCanvas(); canvas.SetDocument(document); canvas.SetPalette(palette);
        var entry = palette.PropEntries.First(item => item.EntryId == entryId);
        canvas.SetDetailProp(entryId, entry.DisplayName); canvas.SetPlatformPlacementShape(vertical, length);
        var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(2, 2, 1, 1));
        Call(canvas, "ApplyDetailSelection", box);
        Check((bool)Call(canvas, "PrepareDoorPlacement", room, box, new RectInt(0, 0, 100, 100)), label + " normal author placement accepted");
        Vector2Int size = PlanningPlatformShapeUtility.Size(vertical, length);
        Check(box.width == size.x && box.height == size.y && box.singleInstance, label + " actual author footprint is exact, including single cell");
        // A different placement brush must never mutate the existing author box.
        box.mechanismBaseId = "preserve-base"; box.mechanismButtonIds.Add("preserve-button"); box.crankOwnerId = "preserve-crank";
        int sourceX = 20;
        foreach (string id in new[] { "preserve-base", "preserve-button", "preserve-crank" })
            room.boxes.Add(new PlanningBox(PlanningDetailType.Prop, new RectInt(sourceX++, 20, 1, 1)) { id = id, propEntryId = "audit-source" });
        string original = JsonUtility.ToJson(box);
        room.boxes.Add(box);
        canvas.SetPlatformPlacementShape(!vertical, 7);
        Check(JsonUtility.ToJson(box) == original, label + " changing brush leaves old footprint/parameters/bindings untouched");
        typeof(PlanningCanvas).GetField("selectedBoxId", Hidden).SetValue(canvas, box.id);
        Check(canvas.SetSelectedPlatformShape(vertical, length), label + " selected body has an explicit resize path");
        Check(box.mechanismBaseId == "preserve-base" && box.mechanismButtonIds.SequenceEqual(new[] { "preserve-button" }) &&
            box.crankOwnerId == "preserve-crank", label + " resize preserves all mechanism binding identities");
        // The runtime fixture does not bind to nonexistent source objects.
        var saved = JsonUtility.FromJson<PlanningBox>(JsonUtility.ToJson(box));
        Check(PlanningPlatformShapeUtility.IsVertical(entry.Prefab.GetComponent<MovingPlatformFeature>(), saved.componentOverrides, size) == vertical,
            label + " JSON retains explicit orientation even for1x1");

        var parent = new GameObject("Platform shape fixture root");
        var method = typeof(PlanningSceneBuilder).GetMethod("PlacePlanningBox", BindingFlags.Static | BindingFlags.NonPublic);
        var placed = (LevelEditorPlacedBlock)method.Invoke(null, new object[] { palette, saved, new Vector2Int(2, 2), parent.transform, new Vector2Int(2, 2) });
        try
        {
            Check(placed != null && placed.SizeCells == size, label + " planner builds exactly one correctly sized scene body");
            var feature = placed.GetComponent<MovingPlatformFeature>();
            var collider = placed.GetComponent<BoxCollider>(); Physics.SyncTransforms();
            Check(feature.BodyOrientation == (vertical ? MovingPlatformBodyOrientation.Vertical : MovingPlatformBodyOrientation.Horizontal), label + " serialized body orientation applied to real instance");
            float physicalLength = vertical ? collider.bounds.size.y : collider.bounds.size.x;
            Check(Mathf.Abs(physicalLength - length) < .001f, label + " collider long edge spans exact grid length, including thin crank platform");
            var artwork = MechanismArtworkPresentation.Ensure(placed.gameObject); artwork.Refresh(0);
            var visual = artwork.Visual;
            Check(Mathf.Abs(visual.transform.TransformVector(Vector3.right / 16).magnitude - 1f / 16) < .0001f &&
                Mathf.Abs(visual.transform.TransformVector(Vector3.up / 16).magnitude - 1f / 16) < .0001f,
                label + " runtime keeps square native16px/cell pixels");
            Check(visual.sprite.rect.width == Mathf.Min(3, length) * 16 && visual.sprite.rect.height == 16,
                label + " short body crops full source frame; never squeezes48px into16px");
            Check(Mathf.Abs(Mathf.DeltaAngle(visual.transform.localEulerAngles.z, vertical ? 90 : 0)) < .01f,
                label + " runtime visual uses correct rotation");
            ArtworkScenePreview.RefreshObject(placed.gameObject);
            Check(ArtworkScenePreview.TryGetFirstMesh(placed.gameObject, out var mesh, out _) && mesh.vertexCount >= 4,
                label + " editor Scene resolves the same 2D platform art");
            var active = new HashSet<string>();
            Check((bool)Call(canvas, "TryAddPlatformPreview", box, new RectInt(2, 2, size.x, size.y), active), label + " planning canvas uses platform native tile preview");
            var images = (Dictionary<string, Image>)typeof(PlanningCanvas).GetField("detailPreviewImages", Hidden).GetValue(canvas);
            var tiles = active.Select(key => images[key]).ToArray();
            Check(tiles.Length == Mathf.CeilToInt(length / 3f) && tiles.All(image =>
                Mathf.Abs(Mathf.DeltaAngle(image.style.rotate.value.angle.value, vertical ? -90 : 0)) < .01f), label + " canvas rotates then tiles/crops length correctly");
            float pixelCount = tiles.Sum(image => image.uv.width * ((Texture2D)image.image).width);
            Check(Mathf.Abs(pixelCount - length * 16) < .001f, label + " canvas displays exact authored pixels for footprint");
            float grid = (float)typeof(PlanningCanvas).GetProperty("GridSize", Hidden).GetValue(canvas);
            Rect target = (Rect)Call(canvas, "CellRect", 2, 2, size.x, size.y);
            var first = tiles[0];
            float shownInset = vertical ? (first.style.left.value.value + first.style.width.value.value * .5f - .375f * grid - target.xMin) / grid
                : (first.style.top.value.value + .125f * grid - target.yMin) / grid;
            float physicalInset = vertical ? collider.bounds.min.x - (placed.transform.position.x - size.x * .5f)
                : placed.transform.position.y + size.y * .5f - collider.bounds.max.y;
            Check(Mathf.Abs(shownInset - physicalInset) < .001f, label + " canvas support edge agrees with real body, including rotated thin strip inset");
        }
        finally { Object.DestroyImmediate(parent); }
    }

    private static PlanningCanvas Fixture(LevelEditorPalette palette, out PlanningDocument document, out PlanningRoom room)
    {
        document = new PlanningDocument(); room = new PlanningRoom("Platform merge fixture");
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) room.cells.Add(new PlanningCell(x, y));
        document.rooms.Add(room);
        var canvas = new PlanningCanvas(); canvas.SetDocument(document); canvas.SetPalette(palette);
        return canvas;
    }

    private static PlanningBox Place(PlanningCanvas canvas, PlanningRoom room, LevelEditorPropEntry entry, bool vertical, Vector2Int cell)
    {
        canvas.SetDetailProp(entry.EntryId, entry.DisplayName); canvas.SetPlatformPlacementShape(vertical, 1);
        var box = new PlanningBox(PlanningDetailType.Prop, new RectInt(cell, Vector2Int.one));
        Call(canvas, "ApplyDetailSelection", box);
        Check((bool)Call(canvas, "PrepareDoorPlacement", room, box, new RectInt(0, 0, 8, 8)), "ordinary single platform placement " + cell);
        room.boxes.Add(box);
        return box;
    }

    private static void SelectForMerge(PlanningCanvas canvas, IEnumerable<PlanningBox> boxes)
    {
        var selection = (HashSet<string>)typeof(PlanningCanvas).GetField("mergeSelection", Hidden).GetValue(canvas);
        selection.Clear(); foreach (var box in boxes) selection.Add(box.id);
    }

    private static void VerifyMerge(LevelEditorPalette palette, string entryId, bool vertical, int length)
    {
        string label = "merge " + entryId + " " + (vertical ? "vertical" : "horizontal") + length;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = Fixture(palette, out var document, out var room);
        var entry = palette.PropEntries.First(item => item.EntryId == entryId);
        var originals = new List<PlanningBox>();
        for (int i = 0; i < length; i++) originals.Add(Place(canvas, room, entry, vertical, new Vector2Int(2 + (vertical ? 0 : i), 2 + (vertical ? i : 0))));
        var old = originals.Select(box => JsonUtility.ToJson(box)).ToArray();
        string rejection = null;
        canvas.ValidationFailed += value => rejection = value;
        canvas.StatusChanged += value => { if (rejection == null) rejection = value; };
        SelectForMerge(canvas, originals); canvas.MergeSelectedItems();
        Check(room.boxes.Count == 1, label + " real author merge produced one record; count=" + room.boxes.Count + "; status=" + rejection +
            "; records=" + string.Join(" / ", room.boxes.Select(box => JsonUtility.ToJson(box))));
        var merged = room.boxes.Single();
        Check(merged.isMerged && merged.singleInstance && merged.width == (vertical ? 1 : length) && merged.height == (vertical ? length : 1),
            label + " singleInstance authors merge into one continuous body");
        Check(merged.mergeParts.Count == length && old.SequenceEqual(merged.mergeParts), label + " source records retain exact old footprint/parameters for unmerge");
        var feature = entry.Prefab.GetComponent<MovingPlatformFeature>();
        Check(PlanningPlatformShapeUtility.IsVertical(feature, merged.componentOverrides, new Vector2(merged.width, merged.height)) == vertical,
            label + " explicit body direction remains correct");
        // Exercise the normal multi-cell enumeration, not just one private spawn.
        var previous = LevelEditorState.Palette; LevelEditorState.Palette = palette;
        try
        {
            Check(PlanningSceneBuilder.TryApplyDocument(document, out string reason), label + " real scene apply: " + reason);
            var generated = GameObject.Find("__PlanningMapGenerated");
            Check(generated != null && generated.GetComponentsInChildren<MovingPlatformFeature>(true).Length == 1,
                label + " real builder creates exactly one motion consumer, never one per occupied cell");
        }
        finally { LevelEditorState.Palette = previous; }
        canvas.UnmergeSelectedItems();
        Check(room.boxes.Count == length && room.boxes.All(box => box.width == 1 && box.height == 1 && box.singleInstance),
            label + " unmerge restores independent single cells without footprint inflation");
    }

    private static void Override(PlanningBox box, string property, string type, string value)
    {
        box.hasComponentOverrides = true;
        var next = new LevelEditorComponentValueOverride();
        next.Configure("", typeof(MovingPlatformFeature).FullName, 0, property, type, value, null);
        box.componentOverrides.RemoveAll(old => old.PropertyPath == property && old.ComponentTypeName == next.ComponentTypeName);
        box.componentOverrides.Add(next);
    }

    private static void VerifyRejectedMerges(LevelEditorPalette palette)
    {
        var entry = palette.PropEntries.First(item => item.EntryId == PlanningDoorUtility.MovingPlatformEntryId);
        foreach (string conflict in new[] { "base", "buttons", "crank", "base-cell", "travel", "drive", "timeline", "orientation", "gap", "corner" })
        {
            var canvas = Fixture(palette, out _, out var room);
            var a = Place(canvas, room, entry, false, new Vector2Int(2, 2));
            var b = Place(canvas, room, entry, false, new Vector2Int(3, 2));
            switch (conflict)
            {
                case "base": a.mechanismBaseId = "a"; b.mechanismBaseId = "b"; break;
                case "buttons": a.mechanismButtonIds.Add("a"); b.mechanismButtonIds.Add("b"); break;
                case "crank": a.crankOwnerId = "a"; b.crankOwnerId = "b"; break;
                case "base-cell": a.mechanismBaseId = b.mechanismBaseId = "a"; b.mechanismBaseCellOffset = Vector2Int.up; break;
                case "travel": Override(b, "travelBlocks", "Integer", "17"); break;
                case "drive": Override(a, "driveMode", "Enum", "1"); Override(b, "driveMode", "Enum", "2"); break;
                case "timeline": Override(b, "timelineOrder", "Integer", "7"); break;
                case "orientation": PlanningPlatformShapeUtility.SetOrientation(b, true); break;
                case "gap": b.x++; break;
                case "corner": b.x = a.x; b.y++; break;
            }
            string before = JsonUtility.ToJson(room);
            SelectForMerge(canvas, new[] { a, b }); canvas.MergeSelectedItems();
            Check(room.boxes.Count == 2 && JsonUtility.ToJson(room) == before, conflict + " incompatible merge is rejected atomically");
        }
        // Saved explicit defaults and omitted defaults are still compatible.
        var equalCanvas = Fixture(palette, out _, out var equalRoom);
        var first = Place(equalCanvas, equalRoom, entry, false, new Vector2Int(2, 2));
        var second = Place(equalCanvas, equalRoom, entry, false, new Vector2Int(3, 2));
        using var serialized = new SerializedObject(entry.Prefab.GetComponent<MovingPlatformFeature>());
        Check(LevelEditorComponentOverrideUtility.TryCapture(entry.Prefab.GetComponent<MovingPlatformFeature>(), serialized.FindProperty("travelBlocks"), out var captured),
            "effective parameter default captured read-only");
        first.componentOverrides.RemoveAll(value => value.PropertyPath == "travelBlocks");
        second.componentOverrides.RemoveAll(value => value.PropertyPath == "travelBlocks");
        second.componentOverrides.Add(captured);
        SelectForMerge(equalCanvas, new[] { first, second }); equalCanvas.MergeSelectedItems();
        Check(equalRoom.boxes.Count == 1 && equalRoom.boxes[0].isMerged, "explicit default equals inherited default during merge comparison");
        var doorCanvas = Fixture(palette, out _, out var doorRoom);
        var door = new PlanningBox(PlanningDetailType.Prop, new RectInt(2, 2, 1, 2)) { singleInstance = true, propEntryId = PlanningDoorUtility.DoorEntryId };
        var button = new PlanningBox(PlanningDetailType.Prop, new RectInt(3, 2, 1, 1)) { singleInstance = true, propEntryId = PlanningDoorUtility.ButtonEntryId, doorOwnerId = door.id };
        doorRoom.boxes.Add(door); doorRoom.boxes.Add(button); SelectForMerge(doorCanvas, new[] { door, button }); doorCanvas.MergeSelectedItems();
        Check(doorRoom.boxes.Count == 2 && doorRoom.boxes.All(box => !box.isMerged), "doors and owned pressure buttons remain non-mergeable");
    }

    private static void VerifyWaterPalette(LevelEditorPalette palette)
    {
        var water = palette.PropEntries.First(entry => entry.Prefab != null && entry.Prefab.GetComponent<WaterVisualFeature>() != null);
        var platform = palette.PropEntries.First(entry => entry.EntryId == PlanningDoorUtility.MovingPlatformEntryId);
        var canvas = Fixture(palette, out _, out var room);
        var waterBox = new PlanningBox(PlanningDetailType.Prop, new RectInt(2, 2, 1, 1)) { propEntryId = water.EntryId, propEntryName = water.DisplayName };
        room.boxes.Add(waterBox);
        var previous = LevelEditorState.Palette;
        var foreign = ScriptableObject.CreateInstance<LevelEditorPalette>();
        LevelEditorState.Palette = foreign;
        try
        {
            Check(!PlanningWaterLayerUtility.IsWater(waterBox, foreign) && PlanningWaterLayerUtility.IsWater(waterBox, palette),
                "fixture deliberately uses a different global palette");
            var placed = Place(canvas, room, platform, true, new Vector2Int(2, 2));
            Check(room.boxes.Count == 2 && PlanningShapeUtility.Contains(placed, new Vector2Int(2, 2)),
                "new placement classifies water using this canvas palette, not unrelated global selection");
            typeof(PlanningCanvas).GetField("selectedBoxId", Hidden).SetValue(canvas, placed.id);
            Check(canvas.SetSelectedPlatformShape(true, 3), "explicit resize also permits water below the same platform");
        }
        finally { LevelEditorState.Palette = previous; Object.DestroyImmediate(foreign); }
    }

    private static void VerifyShapeUI()
    {
        var field = typeof(MovingPlatformFeature).GetField("bodyOrientation", Hidden);
        Check(field.GetCustomAttribute<BlockParameterAttribute>() == null && field.GetCustomAttribute<HideInInspector>() != null &&
            field.GetCustomAttribute<SerializeField>() != null, "body direction is serialized but only the shape entry edits it with its footprint");
        var verticalSingle = new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 1, 1)) { propEntryId = PlanningDoorUtility.MovingPlatformEntryId };
        PlanningPlatformShapeUtility.SetOrientation(verticalSingle, true);
        var resetParameters = new List<LevelEditorComponentValueOverride>();
        PlanningPlatformShapeUtility.PreserveOrientation(verticalSingle, resetParameters, null);
        Check(PlanningPlatformShapeUtility.Orientation(null, resetParameters) == MovingPlatformBodyOrientation.Vertical,
            "resetting ordinary route parameters cannot silently turn a vertical single-cell body horizontal");
        var window = ScriptableObject.CreateInstance<PlanningEditorWindow>();
        try
        {
            var tools = (VisualElement)typeof(PlanningEditorWindow).GetMethod("BuildPlatformShapeTools", Hidden).Invoke(window, null);
            Check(tools is Foldout group && group.value && tools.style.display.value != DisplayStyle.None &&
                tools.Q<DropdownField>("PlanningPlatformBodyOrientation") != null && tools.Q<IntegerField>("PlanningPlatformBodyLength") != null &&
                tools.Q<Button>("PlanningApplySelectedPlatformShape") != null,
                "visible expanded platform tools expose direction, single-cell length and selected-platform action");
        }
        finally { Object.DestroyImmediate(window); }
    }

    private static void VerifyLegacyRecords(LevelEditorPalette palette)
    {
        foreach (string entryId in new[] { PlanningDoorUtility.MovingPlatformEntryId, PlanningMechanismUtility.LiftEntryId })
        foreach (Vector2Int size in new[] { Vector2Int.one, new Vector2Int(1, 5), new Vector2Int(5, 1) })
        {
            var canvas = Fixture(palette, out var document, out var room);
            var legacy = new PlanningBox(PlanningDetailType.Prop, new RectInt(new Vector2Int(1, 1), size))
            { propEntryId = entryId, propEntryName = "legacy platform", singleInstance = true };
            room.boxes.Add(legacy);
            canvas.SetDocument(document);
            string before = JsonUtility.ToJson(legacy);
            canvas.SetPalette(palette); canvas.SetPlatformPlacementShape(true, 3); canvas.SetPlatformPlacementShape(false, 1);
            Check(legacy.width == size.x && legacy.height == size.y && !legacy.hasComponentOverrides && JsonUtility.ToJson(legacy) == before,
                entryId + " legacy " + size + " remains Auto with exact occupied cells after opening and brush changes");
        }
    }
}
