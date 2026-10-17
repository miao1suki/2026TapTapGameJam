using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using UnityEditor;
using UnityEngine;

internal static class FragmentSafetyAudit
{
    private static int checks;
    private static void Check(bool value, string name)
    { checks++; if (!value) throw new InvalidOperationException(name); }

    private static PlanningDocument Map(int x, int y, int width = 1, int height = 1)
    {
        var result = new PlanningDocument { name = "Fragment safety" };
        var room = new PlanningRoom("Room");
        for (int row = 0; row < height; row++) for (int col = 0; col < width; col++)
            room.cells.Add(new PlanningCell(x + col, y + row));
        result.rooms.Add(room);
        return result;
    }

    private static void Rejected(PlanningDocument target, PlanningDocument source, string reason)
    {
        string before = target.ToPersistentJson();
        bool rejected = false;
        try { PlanningFragmentUtility.Import(target, source, new Vector2Int(100, 100), false, out _); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected && target.ToPersistentJson() == before, reason);
    }

    public static void Run()
    {
        if (!Application.dataPath.Contains("FlowRetirementProbe"))
            throw new InvalidOperationException("Only run in isolated project.");
        try
        {
            var target = Map(5, 5);
            var legacy = Map(1, 1);
            legacy.worldBlockCellWidth = legacy.worldBlockCellHeight = 16;
            bool overlap = false;
            try { PlanningFragmentUtility.Import(target, legacy, Vector2Int.zero, false, out _); }
            catch (InvalidDataException) { overlap = true; }
            Check(overlap, "legacy grid migrates before room overlap validation");
            Check(legacy.rooms[0].cells.Count == 1 && legacy.worldBlockCellWidth == 16, "legacy source not mutated");

            var palette = LevelEditorPaletteService.GetOrCreate();
            var water = palette.PropEntries.First(p => p?.Prefab != null && p.Prefab.GetComponent<WaterVisualFeature>() != null);
            var source = Map(0, 0, 3, 3);
            var room = source.rooms[0];
            var waterBox = new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 2, 1))
            { propEntryId = "colleague-old-water-id", propEntryName = water.DisplayName, singleInstance = true };
            room.boxes.Add(waterBox);
            string sourceBefore = source.ToPersistentJson();
            var result = PlanningFragmentUtility.Import(target, source, new Vector2Int(20, 20), true, out _);
            Check(result.rooms[1].boxes[0].propEntryId == water.EntryId, "same-name column maps to current ID");
            Check(source.ToPersistentJson() == sourceBefore, "column remap does not alter colleague source");
            var repeated = PlanningFragmentUtility.Import(result, source, new Vector2Int(30, 30), false, out _);
            Check(repeated.rooms.Select(r => r.id).Distinct().Count() == 3 &&
                  repeated.rooms.Skip(1).SelectMany(r => r.boxes).Select(b => b.id).Distinct().Count() == 2,
                "repeated module imports receive independent IDs");

            source = Map(0, 0);
            source.rooms[0].boxes.Add(new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 1, 1))
            { propEntryId = "missing-column", propEntryName = "Missing colleague asset" });
            Rejected(target, source, "missing prefab rejected atomically");

            source = Map(0, 0);
            var merged = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1)) { isMerged = true };
            merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox(PlanningDetailType.Prop, new RectInt(0, 0, 1, 1))
            { propEntryId = "missing-hidden-part", propEntryName = "Missing unmerge asset" }));
            source.rooms[0].boxes.Add(merged);
            Rejected(target, source, "unmerge snapshots also validate missing assets");

            source = Map(0, 0);
            var duplicate = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1));
            source.rooms[0].boxes.Add(duplicate);
            source.rooms[0].boxes.Add(duplicate.Clone());
            Rejected(target, source, "duplicate authored IDs rejected atomically");

            source = Map(0, 0);
            var referenced = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1));
            var localReference = new LevelEditorComponentValueOverride();
            localReference.Configure("", "Example", 0, "target", "ObjectReference", "reference:local", null);
            referenced.componentOverrides.Add(localReference);
            source.rooms[0].boxes.Add(referenced);
            Rejected(target, source, "session/scene references cannot silently cross maps");
            localReference.Configure("", "Example", 0, "target", "ObjectReference", "reference:null", null);
            result = PlanningFragmentUtility.Import(target, source, new Vector2Int(100, 100), false, out _);
            Check(result.rooms.Count == 2, "explicit null reference remains valid");

            source = Map(0, 0, 2, 1);
            merged = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 2, 1)) { isMerged = true };
            var part = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1))
            { mechanismBaseId = "external-base", mechanismBaseCellOffset = new Vector2Int(3, 7), crankOwnerId = "external-crank" };
            merged.mergeParts.Add(JsonUtility.ToJson(part));
            merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox(PlanningDetailType.Solid, new RectInt(1, 0, 1, 1))));
            source.rooms[0].boxes.Add(merged);
            result = PlanningFragmentUtility.Import(target, source, new Vector2Int(100, 100), false, out _);
            var importedPart = PlanningBox.FromPersistentJson(result.rooms[1].boxes[0].mergeParts[0]);
            Check(string.IsNullOrEmpty(importedPart.mechanismBaseId) && string.IsNullOrEmpty(importedPart.crankOwnerId) && importedPart.mechanismBaseCellOffset == Vector2Int.zero,
                "unmerge cannot restore stale external bindings or offsets");

            source = Map(0, 0, 3, 3);
            merged = new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 3, 3)) { isMerged = true };
            merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 3, 1))));
            merged.mergeParts.Add(JsonUtility.ToJson(new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 1, 1, 2))));
            source.rooms[0].boxes.Add(merged);
            var fragment = PlanningFragmentUtility.Extract(source, new[] { source.rooms[0].id }, null, out _);
            result = PlanningFragmentUtility.Import(target, fragment, new Vector2Int(20, 20), false, out _);
            var importedShape = result.rooms[1].boxes[0];
            Check(PlanningShapeUtility.Cells(importedShape).Count() == 5 && !PlanningShapeUtility.Contains(importedShape, new Vector2Int(2, 2)),
                "irregular module import preserves holes");
            bool split = false;
            try { PlanningFragmentUtility.Extract(source, null, new RectInt(0, 0, 2, 2), out _); }
            catch (InvalidDataException) { split = true; }
            Check(split, "partial export refuses to split a merged logical object");
            Check(target.rooms.Count == 1, "all fragment operations preserve original target");

            var occupiedTarget = Map(5, 5);
            occupiedTarget.rooms[0].boxes.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(0, 0, 1, 1)));
            var patchSource = Map(0, 0);
            patchSource.assemblyPatches.Add(new PlanningBox(PlanningDetailType.Solid, new RectInt(-95, -95, 1, 1)));
            Rejected(occupiedTarget, patchSource, "out-of-room assembly patch cannot overwrite target object");
            patchSource.assemblyPatches.Clear();
            patchSource.assemblyPatches.Add(new PlanningBox(PlanningDetailType.Prop, new RectInt(-95, -95, 1, 1))
            { propEntryId = water.EntryId, propEntryName = water.DisplayName });
            result = PlanningFragmentUtility.Import(occupiedTarget, patchSource, new Vector2Int(100, 100), false, out _);
            Check(result.assemblyPatches.Count == 1 && result.rooms[0].boxes.Count == 1,
                "water-layer assembly patch may overlap foreground without deleting either");

            File.WriteAllText("Library/FragmentSafetyAudit.txt", "PASS " + checks);
            Debug.Log("FRAGMENT_SAFETY PASS " + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            File.WriteAllText("Library/FragmentSafetyAudit.txt", "FAIL " + error);
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }
}
