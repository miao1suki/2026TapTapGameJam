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

// Authoring policy regression; only runs in the silent disposable verification project.
public static class WaterAllItemsAudit
{
    private static int checks;
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("WATER ALL ITEMS: " + label);
        checks++;
        Debug.Log("WATER ALL ITEMS PASS: " + label);
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets") ||
            !Environment.GetCommandLineArgs().Contains("-noaudio"))
            throw new InvalidOperationException("Run only in silent FlowRetirementProbe.");
        try { Execute(); File.WriteAllText("Library/WaterAllItemsResult.txt", "PASS " + checks); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static void Execute()
    {
        checks = 0;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var palette = LevelEditorPaletteService.GetOrCreate();
        var waterEntry = palette.PropEntries.First(e => e?.Prefab?.GetComponent<WaterVisualFeature>() != null);
        var lavaEntry = palette.PropEntries.First(e => e?.Prefab?.GetComponent<LavaHazardFeature>() != null);
        PlanningBox Prop(LevelEditorPropEntry entry, int x = 0, int y = 0) => new PlanningBox(PlanningDetailType.Prop, new RectInt(x, y, 1, 1))
            { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
        var document = new PlanningDocument { worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var room = new PlanningRoom("Water overlap") { independentCells = true };
        foreach (var cell in new RectInt(0, 0, 24, 12).allPositionsWithin) room.cells.Add(new PlanningCell(cell.x, cell.y));
        document.rooms.Add(room);
        var water = Prop(waterEntry);
        var lava = Prop(lavaEntry);
        foreach (var entry in palette.PropEntries.Where(e => e?.Prefab != null))
        {
            var item = Prop(entry);
            bool allowed = entry.Prefab.GetComponent<LavaHazardFeature>() == null;
            Check(PlanningWaterLayerUtility.Excludes(water, item, palette) == !allowed, "water foreground classification " + entry.DisplayName);
            Check(PlanningWaterLayerUtility.Excludes(item, water, palette) == !allowed, "symmetric foreground classification " + entry.DisplayName);
            room.boxes.Clear(); room.boxes.Add(item);
            Check(PlanningWaterLayerUtility.CanPlace(document, room, water, Vector2Int.zero, palette) == allowed,
                "water after foreground " + entry.DisplayName);
            room.boxes.Clear(); room.boxes.Add(water);
            Check(PlanningWaterLayerUtility.CanPlace(document, room, item, Vector2Int.zero, palette) == allowed,
                "foreground after water " + entry.DisplayName);
        }
        room.boxes.Clear(); room.boxes.Add(water);
        Check(!PlanningWaterLayerUtility.CanPlace(document, room, lava, Vector2Int.zero, palette), "lava paint rejects water");
        Check(PlanningWaterLayerUtility.CanPlace(document, room, lava, Vector2Int.right, palette), "adjacent lava remains legal");
        room.boxes.Add(lava);
        Check(!PlanningWaterLayerUtility.TryValidate(PlanningWaterLayerUtility.WorldBoxes(document), palette, out _), "invalid full document rejected");
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try
        {
            Check(!PlanningSceneBuilder.TryApplyDocument(document, out var message) && message.Contains("水与岩浆"), "Scene apply fails with actionable error");
            Check(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).Length == 0 &&
                GameObject.Find("__PlanningMapStaging") == null, "failed apply does not generate or remove author content");
        }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }

        // Real shape, not bounding rectangle: lava in a water hole must remain legal.
        room.boxes.Clear();
        var irregular = Prop(waterEntry); irregular.width = 3; irregular.height = 2; irregular.isMerged = true;
        irregular.mergeParts.Add(JsonUtility.ToJson(Prop(waterEntry, 0, 0)));
        irregular.mergeParts.Add(JsonUtility.ToJson(Prop(waterEntry, 2, 0)));
        irregular.mergeParts.Add(JsonUtility.ToJson(Prop(waterEntry, 0, 1)));
        room.boxes.Add(irregular); lava.x = 1; lava.y = 0; room.boxes.Add(lava);
        Check(PlanningWaterLayerUtility.TryValidate(PlanningWaterLayerUtility.WorldBoxes(document), palette, out _), "lava in irregular water hole allowed");
        Check(!PlanningWaterLayerUtility.CanPlace(document, room, lava, Vector2Int.zero, palette), "drag into actual water rejected");
        Check(PlanningWaterLayerUtility.CanPlace(document, room, lava, new Vector2Int(2, 1), palette), "drag into another hole allowed");

        // Canvas entry used by detail/assembly placement, movement and replacement.
        var canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(document); canvas.SetMode(PlanningCanvasMode.Detail);
        var placement = typeof(PlanningCanvas).GetMethod("CanPlaceWaterLayer", BindingFlags.NonPublic | BindingFlags.Instance);
        Check(!(bool)placement.Invoke(canvas, new object[] { room, lava, Vector2Int.zero, null }), "canvas shares exclusion rule");
        Check((bool)placement.Invoke(canvas, new object[] { room, lava, new Vector2Int(1, 0), null }), "canvas preserves holes");
        VerifyCanvasWaterOwners(palette, waterEntry);

        // Cross-document import must also reject water vs an existing global lava patch.
        var source = new PlanningDocument { worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var sourceRoom = new PlanningRoom("Source") { independentCells = true };
        sourceRoom.cells.Add(new PlanningCell(0, 0)); sourceRoom.boxes.Add(Prop(waterEntry)); source.rooms.Add(sourceRoom);
        var target = new PlanningDocument { worldBlockCellWidth = 1, worldBlockCellHeight = 1 };
        var targetRoom = new PlanningRoom("Target") { independentCells = true };
        targetRoom.cells.Add(new PlanningCell(20, 0)); target.rooms.Add(targetRoom);
        target.assemblyPatches.Add(Prop(lavaEntry, 5, 0));
        bool rejected = false;
        try { PlanningFragmentUtility.Import(target, source, new Vector2Int(5, 0), false, out _); }
        catch (InvalidDataException error) { rejected = error.Message.Contains("水与岩浆"); }
        Check(rejected && target.rooms.Count == 1, "fragment conflict rejected atomically");
        var wall = new PlanningBox(PlanningDetailType.Solid, new RectInt(5, 0, 1, 1));
        target.assemblyPatches.Clear(); target.assemblyPatches.Add(wall);
        var imported = PlanningFragmentUtility.Import(target, source, new Vector2Int(5, 0), false, out _);
        Check(imported.rooms.Count == 2 && imported.assemblyPatches.Count == 1, "water may import behind a global wall");
        room.boxes.Clear();
        string[] ids = { "overlap_water", "overlap_current", "overlap_buoyancy" };
        var families = new[] { typeof(WaterSourceFeature), typeof(DirectionalCurrentFeature), typeof(BuoyancyColumnFeature) };
        for (int index = 0; index < families.Length; index++)
        {
            var entry = palette.PropEntries.First(e => e?.Prefab != null && e.Prefab.GetComponent(families[index]) != null);
            var item = Prop(entry, 4, 4); item.id = ids[index]; room.boxes.Add(item);
        }
        room.boxes.Add(Prop(waterEntry, 4, 4));
        PlanningSceneBuilder.SuppressSceneUndo = true;
        try
        {
            Check(PlanningSceneBuilder.TryApplyDocument(document, out var message), "different water families generate together: " + message);
            var generated = Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None);
            Check(generated.Count(b => b.GetComponent<WaterVisualFeature>() != null) == 3, "three families survive, same-kind duplicate emitted only once");
            foreach (string id in ids) Check(generated.Any(b => b.PlanningBoxId == id), "retained overlap source " + id);
            ArtworkScenePreview.RefreshOpenScenes();
            var waterObjects = generated.Where(b => b.GetComponent<WaterVisualFeature>() != null).ToArray();
            Check(waterObjects.Sum(b => ArtworkScenePreview.PartCount(b.gameObject)) == 1,
                "edit-time overlapping families share one water mesh");
            Check(waterObjects.SelectMany(b => b.GetComponentsInChildren<Renderer>()).All(r => r.forceRenderingOff),
                "overlapped editor water never falls back to old cube renderers");
            var instances = generated.ToDictionary(b => b.PlanningBoxId, b => b.GetInstanceID());
            Check(PlanningSceneBuilder.TryApplyDocument(document, out _), "incremental overlap application");
            Check(Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None).All(b => instances[b.PlanningBoxId] == b.GetInstanceID()),
                "all overlapped water instances persist across reapply");
        }
        finally { PlanningSceneBuilder.SuppressSceneUndo = false; }
        Debug.Log("WATER ALL ITEMS COMPLETE: " + checks);
    }

    private static void VerifyCanvasWaterOwners(LevelEditorPalette palette, LevelEditorPropEntry entry)
    {
        PlanningBox Water(string id, int x, int y) => new PlanningBox(PlanningDetailType.Prop,new RectInt(x,y,1,1))
            { id=id, propEntryId=entry.EntryId, propEntryName=entry.DisplayName };
        var document=new PlanningDocument { worldBlockCellWidth=4,worldBlockCellHeight=3 };
        var room=new PlanningRoom("Room at nonzero world origin") { independentCells=true };
        room.cells.Add(new PlanningCell(2,-1)); document.rooms.Add(room);
        var aboveRoom=new PlanningRoom("Adjacent room above") { independentCells=true };
        aboveRoom.cells.Add(new PlanningCell(2,-2)); document.rooms.Add(aboveRoom);
        var local=Water("z_local_water",3,0); room.boxes.Add(local); // world (11,-3)
        var above=Water("b_above_water",3,2); aboveRoom.boxes.Add(above); // world (11,-4)
        var overlap=Water("a_global_overlap",11,-3); document.assemblyPatches.Add(overlap);
        var below=Water("c_global_below",11,-2); document.assemblyPatches.Add(below);
        var canvas=new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(document);
        const BindingFlags hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        var waterCells=typeof(PlanningCanvas).GetMethod("WaterCellsFor",hidden);
        var previewField=typeof(PlanningCanvas).GetField("detailPreviewImages",hidden);
        Dictionary<string,Image> Images() => (Dictionary<string,Image>)previewField.GetValue(canvas);
        Image[] For(PlanningBox box) => Images().Where(p=>p.Key.StartsWith(box.id+":art:",StringComparison.Ordinal))
            .Select(p=>p.Value).Where(i=>i.style.display.value==DisplayStyle.Flex).ToArray();
        bool Uses(Image image,int tile)
        {
            Check(ObjectArtworkPlanningPreview.TryGetWaterTile(tile,out var expected),"formal water tile available: "+tile);
            return image.image==expected.Texture && image.uv==expected.UV;
        }

        canvas.SetMode(PlanningCanvasMode.Assembly);
        Check(For(local).Length==0 && For(overlap).Length==1 && For(above).Length==1 && For(below).Length==1 && Images().Count==3,
            "assembly draws overlapping room/global water once per world cell, not dark duplicate underlays");
        Check(Uses(For(overlap)[0],1),"assembly cross-owner neighbours select water-middle, not a false surface or bottom");
        Check(Uses(For(above)[0],0) && Uses(For(below)[0],2),"assembly top/bottom tiles respect world Y across room and patch origins");
        var cached=(HashSet<Vector2Int>)waterCells.Invoke(canvas,new object[]{local});
        Check(cached.SetEquals(new[]{new Vector2Int(11,-4),new Vector2Int(11,-3),new Vector2Int(11,-2)}),
            "assembly water cache converts local coordinates using true non-unit world block strides");
        Check(ReferenceEquals(cached,waterCells.Invoke(canvas,new object[]{above})) &&
            ReferenceEquals(cached,waterCells.Invoke(canvas,new object[]{overlap})),
            "all assembly owners reuse one cached union instead of scanning on each tile or repaint");

        canvas.SetMode(PlanningCanvasMode.Detail);
        Check(For(local).Length==1 && Images().Count==1,"detail retains its room water even when a lower-id global patch overlaps it");
        Check(Uses(For(local)[0],0),"detail only considers visible owner-local neighbours");
        var detail=(HashSet<Vector2Int>)waterCells.Invoke(canvas,new object[]{local});
        Check(!ReferenceEquals(detail,cached) && detail.SetEquals(new[]{new Vector2Int(3,0)}),
            "mode switch invalidates global world-cell cache before reading detail-local cells");
        canvas.SelectRoom(aboveRoom.id);
        Check(For(above).Length==1 && Images().Count==1,"switching detail rooms preserves that owner's independent water preview");
        canvas.SetMode(PlanningCanvasMode.Assembly);
        Check(Images().Count==3 && For(overlap).Length==1 && Uses(For(overlap)[0],1),
            "switching back rebuilds world deduplication and adjacency, without stale detail ownership");
    }
}
