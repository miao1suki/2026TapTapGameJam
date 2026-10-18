using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Conservative formal-level compatibility check. Never regenerates author geometry.</summary>
public static class FormalArtworkCompatibilityAudit
{
    private const string ScenePath = "Assets/_Project/Scenes/Playing/Gaming.unity";
    private const string PairedPath = "Assets/_Project/Scenes/Playing/Gaming.planning.json";
    private const string MapPath = "Assets/_Project/Content/LevelMaps/正式关卡.json";
    private const string DuplicateId = "box_356e9c89";
    private const string CoverId = "box_a778178f";
    private const string RightId = "box_a778178f_split51";
    private static readonly StringBuilder report = new();
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FORMAL ART: " + message);
        checks++; report.AppendLine("PASS " + message);
    }

    public static void Run()
    {
        // This intentionally cannot modify the working project or run with live game audio.
        if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase) ||
            !Environment.GetCommandLineArgs().Contains("-noaudio"))
            throw new InvalidOperationException("Formal audit requires Library/FlowRetirementProbe and -noaudio.");
        int exitCode = 0;
        try { Execute(); report.AppendLine("PASS " + checks + " formal-level compatibility assertions."); }
        catch (Exception error) { exitCode = 1; report.AppendLine(error.ToString()); Debug.LogException(error); }
        finally
        {
            ArtworkScenePreview.Clear();
            File.WriteAllText("Library/FormalArtworkCompatibilityResult.txt", report.ToString(), new UTF8Encoding(false));
        }
        Debug.Log(report.ToString());
        EditorApplication.Exit(exitCode);
    }

    private static void Execute()
    {
        checks = 0; report.Clear();
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "edit mode only");
        foreach (var window in Resources.FindObjectsOfTypeAll<PlanningEditorWindow>()) window.Close();
        byte[] pairedBytes = File.ReadAllBytes(PairedPath), mapBytes = File.ReadAllBytes(MapPath);
        string original = File.ReadAllText(PairedPath);
        Check(pairedBytes.SequenceEqual(mapBytes), "paired planning and named formal map have identical original bytes");
        var document = JsonUtility.FromJson<PlanningDocument>(original);
        Check(document != null && document.rooms.Count >= 11, "formal author document retains at least the original 11 rooms");
        var ownerRoom = document.rooms.Single(room => room.boxes.Any(box => box.id == CoverId));
        var cover = ownerRoom.boxes.Single(box => box.id == CoverId);
        var duplicate = ownerRoom.boxes.SingleOrDefault(box => box.id == DuplicateId);
        bool alreadyRepaired = cover.width == 2 && ownerRoom.boxes.Any(box => box.id == RightId);
        Check(ownerRoom.id == "room_96d8a099" && cover.x == 48 && cover.y == 9 && (cover.width == 4 || alreadyRepaired) && cover.height == 1,
            "the retained room-9 platform is the explicit author-approved record");
        Check(duplicate != null, "the separately authored 16-cell-travel platform must remain in the document");
        {
            Check(duplicate.x == 50 && duplicate.y == 9 && duplicate.width == 1 && duplicate.height == 1,
                "duplicate is exactly the approved single cell, wholly covered by the retained record");
            Check(SamePlacementSemantics(cover, duplicate), "overlapping records have matching prefab, type, merge semantics and mechanism bindings");
            Check(Regex.Matches(original, Regex.Escape("\"" + DuplicateId + "\"")).Count == 1,
                "the separately authored single-cell record appears exactly once");
        }

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ArtworkScenePreview.Clear();
        var managers = All<RoomLifecycleManager>(scene);
        Check(managers.Length == 1, "Scene has exactly one planning lifecycle source");
        var manager = managers[0];
        Check(Normalize(manager.PlanningJson) == Normalize(original), "Scene embedded planning matches both external formal saves");
        var placed = All<LevelEditorPlacedBlock>(scene);
        var duplicates = placed.Where(block => block.PlanningBoxId == DuplicateId).ToArray();
        var retained = placed.Where(block => block.PlanningBoxId == CoverId).ToArray();
        Check(retained.Length == (alreadyRepaired ? 2 : 4) && retained.All(block => block.SizeCells == Vector2Int.one),
            "author's four-cell non-singleInstance platform remains four one-cell Scene instances");
        Check(duplicates.Length == (alreadyRepaired ? 1 : 0), "overlap suppression matches expected Scene source before surgical repair");
        report.AppendLine("INFO duplicate Scene instances=" + duplicates.Length + "; document override count=" + (duplicate?.componentOverrides?.Count ?? 0));
        var platformOwners = placed.Where(block => block.GetComponent<MovingPlatformFeature>() != null ||
            block.GetComponent<CrankPlatformFeature>() != null).ToArray();
        Check(platformOwners.Length > 0 && platformOwners.Any(block => block.SizeCells == Vector2Int.one),
            "latest formal Scene exercises legacy one-cell platform occupancy");
        var authorBefore = Snapshot(scene);
        bool dirtyBefore = scene.isDirty;
        Check(!dirtyBefore, "opening the author Scene does not dirty it");
        ArtworkScenePreview.RefreshOpenScenes();
        Check(scene.isDirty == dirtyBefore, "refreshing 2D Scene artwork does not dirty the author Scene");
        Check(Snapshot(scene).OrderBy(pair => pair.Key).SequenceEqual(authorBefore.OrderBy(pair => pair.Key)),
            "artwork refresh installs no runtime component or Scene object and changes no serialized author field");
        ValidateIncrementalLevel(document, scene, placed);
        foreach (var block in platformOwners)
        {
            Check(block.SizeCells.x >= 1 && block.SizeCells.y >= 1, "platform retains valid logical occupancy " + block.PlanningBoxId);
            Check(ArtworkScenePreview.TryGetFirstMesh(block.gameObject, out var mesh, out _) && mesh != null,
                "formal platform receives 2D Scene artwork without entering Play " + block.PlanningBoxId);
            if (block.SizeCells == Vector2Int.one)
            {
                Vector3 artSize = Vector3.Scale(mesh.bounds.size, Abs(block.transform.lossyScale));
                Check(artSize.x <= block.CellWorldSize + .01f && artSize.y <= block.CellWorldSize + .01f,
                    "legacy one-cell platform artwork is cropped at native pixels, not expanded to three cells " + block.PlanningBoxId);
            }
        }
        Screenshot(retained, "Library/FormalArtworkCompatibility_Room9.png");
        ArtworkScenePreview.Clear();
        Check(!scene.isDirty && Snapshot(scene).OrderBy(pair => pair.Key).SequenceEqual(authorBefore.OrderBy(pair => pair.Key)),
            "preview teardown and GPU capture leave serialized author objects unchanged");

        // User explicitly chose to keep the separately authored 16-cell route, not
        // the covering batch's default four-cell route. Preserve both parameter sets.
        var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(
            "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        var entry = palette.PropEntries.Single(value => value.EntryId == cover.propEntryId);
        Check(entry.Prefab != null && entry.Prefab.GetComponent<MovingPlatformFeature>() != null,
            "both records resolve to the same moving-platform prefab");
        var conflicts = EffectiveConflicts(entry, cover, duplicate);
        foreach (string conflict in conflicts) report.AppendLine("PRESERVED AUTHOR DIFFERENCE " + conflict);
        Check(duplicate.componentOverrides.Any(value => value.ComponentTypeName == typeof(MovingPlatformFeature).FullName &&
            value.PropertyPath == "travelBlocks" && value.Value == "16"), "user-selected single-cell platform keeps its 16-cell travel override");
        if (alreadyRepaired)
        {
            Check(ownerRoom.boxes.Single(box => box.id == RightId).x == 51, "split batch retains right-hand cell 51");
            Check(new SerializedObject(duplicates[0].GetComponent<MovingPlatformFeature>()).FindProperty("travelBlocks").intValue == 16,
                "reloaded single-cell Scene instance keeps 16-cell travel");
            report.AppendLine("INFO cleanup already applied; no assets rewritten."); return;
        }
        string originalBatch = RecordText(original, CoverId);
        // Normal erase semantics: split the rectangular batch rather than introducing
        // irregular coverage semantics into legacy non-merged records.
        string leftBatch = ReplaceScalar(originalBatch, "width", "2");
        string rightBatch = ReplaceScalar(ReplaceScalar(ReplaceScalar(originalBatch, "id", "\"" + RightId + "\""), "x", "51"), "width", "1");
        string revised = ReplaceRecord(original, CoverId, leftBatch + ",\n" + rightBatch);
        var revisedDocument = JsonUtility.FromJson<PlanningDocument>(revised);
        var revisedRoom = revisedDocument.rooms.Single(room => room.id == ownerRoom.id);
        var left = revisedRoom.boxes.Single(box => box.id == CoverId);
        var right = revisedRoom.boxes.Single(box => box.id == RightId);
        Check(revisedDocument.rooms.Count == document.rooms.Count && revisedDocument.rooms.Sum(room => room.boxes.Count) == document.rooms.Sum(room => room.boxes.Count) + 1,
            "normal rectangular batch split retains all rooms and separately authored objects");
        Check(RecordText(revised, DuplicateId) == RecordText(original, DuplicateId), "all bytes and unknown fields of the 16-cell platform record remain unchanged");
        Check(!left.isMerged && !right.isMerged && !left.singleInstance && !right.singleInstance &&
            PlanningShapeUtility.Cells(left).Concat(PlanningShapeUtility.Cells(right)).OrderBy(cell => cell.x).SequenceEqual(
                new[] { new Vector2Int(48, 9), new Vector2Int(49, 9), new Vector2Int(51, 9) }),
            "batch coverage excludes only 50,9; non-merged one-cell instance semantics unchanged");
        Check(SamePlacementSemantics(left, cover) && SamePlacementSemantics(right, cover) &&
            OverrideJson(left.componentOverrides) == OverrideJson(cover.componentOverrides) &&
            OverrideJson(right.componentOverrides) == OverrideJson(cover.componentOverrides),
            "both batch fragments retain every mechanism binding and component override");
        var converted = retained.Single(block => block.PlanningLocalCell == new Vector2Int(50, 9));
        Check(retained.All(block => block.GetComponent<MovingPlatformFeature>().LinkedControls.Count == 0 &&
            block.GetComponent<MovingPlatformFeature>().CrankController == null),
            "actual four batch instances have no conflicting hidden signal/crank binding");
        var mutableIds = new HashSet<int>(retained.Select(block => block.GetInstanceID()));
        foreach (var component in converted.GetComponentsInChildren<Component>(true)) if (component != null) mutableIds.Add(component.GetInstanceID());
        foreach (var block in retained)
        {
            PlanningBox desired = block.PlanningLocalCell.x == 50 ? duplicate : block.PlanningLocalCell.x == 51 ? right : left;
            if (block == converted)
            {
                LevelEditorComponentOverrideUtility.ApplyOverrides(block.gameObject,
                    desired.hasComponentOverrides ? desired.componentOverrides : entry.ComponentValueOverrides, false);
                foreach (var component in block.GetComponentsInChildren<Component>(true))
                    if (component != null) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            block.SetPlanningSource(desired.id, Signature(desired, entry), block.PlanningLocalCell);
            EditorUtility.SetDirty(block);
        }
        Check(new SerializedObject(converted.GetComponent<MovingPlatformFeature>()).FindProperty("travelBlocks").intValue == 16,
            "existing overlap-cell Scene instance now uses the separately authored 16-cell route");
        Check(retained.All(block => block.SizeCells == Vector2Int.one), "repair preserves all four logical single-cell colliders and placements");
        Check(retained.All(block => EditorJsonUtility.ToJson(block.transform) == authorBefore[block.transform.GetInstanceID()]),
            "all four existing Scene transforms, parents and local sizes remain unchanged");
        var source = new SerializedObject(manager);
        source.FindProperty("planningJson").stringValue = revised; source.ApplyModifiedPropertiesWithoutUndo();
        // External JSON is edited as raw text so future/unknown fields cannot be lost by a typed round-trip.
        File.WriteAllText(PairedPath, revised, EncodingFor(pairedBytes));
        File.WriteAllText(MapPath, revised, EncodingFor(mapBytes));
        PlanningSharedSaveUtility.ObserveSceneCopy(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "save only the edited Scene; never regenerate its geometry");
        Check(File.ReadAllText(PairedPath) == revised && File.ReadAllText(MapPath) == revised && manager.PlanningJson == revised,
            "Scene source and both external saves contain exactly the same surgically patched JSON");
        var after = Snapshot(scene);
        foreach (var item in authorBefore)
        {
            if (item.Key == manager.GetInstanceID() || mutableIds.Contains(item.Key)) continue;
            if (!after.TryGetValue(item.Key, out string value) || value != item.Value)
                throw new InvalidOperationException("Unrelated author object changed: " + item.Key);
        }
        Check(true, "all unrelated author GameObjects and serialized components preserved (" + authorBefore.Count + " snapshots)");
        Check(after.Keys.All(id => authorBefore.ContainsKey(id)), "cleanup adds no generated Scene object/component");
        Check(All<LevelEditorPlacedBlock>(scene).Count(block => block.PlanningBoxId == DuplicateId) == 1 &&
            All<LevelEditorPlacedBlock>(scene).Count(block => block.PlanningBoxId == CoverId) == 2 &&
            All<LevelEditorPlacedBlock>(scene).Count(block => block.PlanningBoxId == RightId) == 1,
            "source metadata matches split batch plus retained independent platform");
        AssetDatabase.Refresh();
        ArtworkScenePreview.Clear();
        Scene reopened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var reopenedBlocks = All<LevelEditorPlacedBlock>(reopened);
        var reopenedSingle = reopenedBlocks.Single(block => block.PlanningBoxId == DuplicateId);
        Check(new SerializedObject(reopenedSingle.GetComponent<MovingPlatformFeature>()).FindProperty("travelBlocks").intValue == 16 &&
            reopenedSingle.SizeCells == Vector2Int.one && reopenedSingle.PlanningLocalCell == new Vector2Int(50, 9),
            "disk Scene reload preserves independent single-cell source, occupancy and 16-cell route");
        Check(All<RoomLifecycleManager>(reopened).Single().PlanningJson == File.ReadAllText(PairedPath) &&
            File.ReadAllBytes(PairedPath).SequenceEqual(File.ReadAllBytes(MapPath)), "disk reload keeps Scene and both formal saves consistent");
        foreach (var block in reopenedBlocks.Where(block => block.PlanningBoxId == CoverId || block.PlanningBoxId == RightId || block.PlanningBoxId == DuplicateId))
        {
            var targetBox = revisedRoom.boxes.Single(box => box.id == block.PlanningBoxId);
            Check(block.PlanningSignature == Signature(targetBox, entry), "persisted planning source signature matches next editor apply " + block.PlanningBoxId);
        }
        report.AppendLine("CLEANED batch overlap at 50,9; preserved " + DuplicateId + " with travelBlocks16; no Scene roots rebuilt, moved or removed.");
    }

    private static void ValidateIncrementalLevel(PlanningDocument document, Scene scene, LevelEditorPlacedBlock[] placed)
    {
        if (document.rooms.Count < 12) return;
        // Fragment import rejects prefab-local placeholders; this is a same-Scene
        // audit. Keep those placeholders in the real document and validate the
        // resolved components below, while still checking all external assets.
        var dependencies = JsonUtility.FromJson<PlanningDocument>(JsonUtility.ToJson(document));
        foreach (var box in dependencies.rooms.SelectMany(room => room.boxes).Concat(dependencies.assemblyPatches)) StripPrefabLocal(box);
        PlanningFragmentUtility.ValidateDependencies(dependencies);
        Check(true, "formal document has valid unique IDs, merge snapshots and resolvable prefab/asset dependencies");
        var boxes = document.rooms.SelectMany(room => room.boxes).Concat(document.assemblyPatches).ToDictionary(box => box.id);
        foreach (var box in boxes.Values)
        {
            foreach (string reference in new[] { box.doorOwnerId, box.mechanismBaseId, box.crankOwnerId })
                if (!string.IsNullOrEmpty(reference)) Check(boxes.ContainsKey(reference), "mechanism reference exists " + box.id + " -> " + reference);
        }
        Check(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .All(node => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) == 0), "formal Scene has no missing MonoBehaviour scripts");
        var players = All<PlayerController>(scene);
        Check(players.Length == 1 && players[0].CompareTag("Player"), "one tagged formal player retained");
        Check(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(players[0].gameObject)) ==
            "Assets/_Project/Content/Player/Player.prefab", "formal player still uses the standard prefab");
        Check(All<Camera>(scene).Count(camera => camera.enabled && camera.CompareTag("MainCamera")) == 1,
            "one enabled MainCamera retained");
        var rendered = placed.GroupBy(block => block.PlanningBoxId ?? "").ToDictionary(group => group.Key, group => group.ToArray());
        var changedBlocks = new List<LevelEditorPlacedBlock>();
        foreach (var changedRoom in document.rooms)
        foreach (var box in changedRoom.boxes)
        {
            rendered.TryGetValue(box.id, out var instances);
            if (instances == null || instances.Length == 0)
            {
                // Existing author records can cover the same normal paint cell.
                // SceneBuilder emits only the first same-layer instance, not a
                // duplicate. Require full coverage by present equivalent paint.
                var covering = changedRoom.boxes.Where(other => other.id != box.id && other.type == box.type &&
                    other.propEntryId == box.propEntryId && other.paletteEntryId == box.paletteEntryId &&
                    rendered.ContainsKey(other.id)).ToArray();
                Check(!box.isMerged && !box.singleInstance && string.IsNullOrEmpty(box.doorOwnerId) &&
                    string.IsNullOrEmpty(box.mechanismBaseId) && string.IsNullOrEmpty(box.crankOwnerId) &&
                    PlanningShapeUtility.Cells(box).All(cell => covering.Any(other => PlanningShapeUtility.Contains(other, cell))),
                    "suppressed duplicate paint is fully represented by equivalent existing Scene coverage " + box.id);
                report.AppendLine("INFO overlapping author paint retained without spawning duplicate: " + box.id);
                continue;
            }
            Check(true, "updated room has Scene instance " + box.id);
            string prefix = JsonUtility.ToJson(box.PersistentClone());
            // Older valid signatures predate the optional button-list fields.
            // Compare normalized persistent records, not schema-era JSON bytes.
            Check(instances.All(block => JsonUtility.ToJson(PlanningBox.FromPersistentJson(
                    RecordText(block.PlanningSignature, box.id)).PersistentClone()) == prefix),
                "updated room Scene source matches saved parameters " + box.id);
            Check(instances.All(block => PlanningShapeUtility.Contains(box, block.PlanningLocalCell)),
                "updated room Scene instance stays inside its saved coverage " + box.id);
            foreach (var block in instances)
            {
                changedBlocks.Add(block);
                if (!string.IsNullOrEmpty(box.doorOwnerId))
                {
                    var button = block.GetComponent<Project.Mechanisms.DoorButton>();
                    Check(button != null && button.Owner != null && button.Owner.GetComponent<LevelEditorPlacedBlock>().PlanningBoxId == box.doorOwnerId,
                        "updated button is connected to its saved door " + box.id);
                }
            }
        }
        Screenshot(changedBlocks.ToArray(), "Library/FormalArtworkCompatibility_CurrentLevel.png");
        var reservedRoom = document.rooms.Single(room => room.id == "room_5bd7a307");
        if (document.rooms.Count == 12)
            Check(reservedRoom.cells.Count == 6 && reservedRoom.boxes.Count == 0, "12-room baseline retains its empty reservation");
        else
            Check(document.rooms.Count == 17 && reservedRoom.boxes.Count > 0, "current 17-room author delivery includes the completed former reservation");
        Check(PlanningWaterLayerUtility.TryValidate(PlanningWaterLayerUtility.WorldBoxes(document), LevelEditorPaletteService.GetOrCreate(), out var waterError),
            "formal water/lava occupancy: " + waterError);
    }

    private static void StripPrefabLocal(PlanningBox box)
    {
        box.componentOverrides.RemoveAll(value => value.ValueType == "ObjectReference" && value.Value == "reference:local");
        for (int i = 0; i < box.mergeParts.Count; i++)
        {
            var part = PlanningBox.FromPersistentJson(box.mergeParts[i]);
            StripPrefabLocal(part); box.mergeParts[i] = JsonUtility.ToJson(part);
        }
    }

    private static bool SamePlacementSemantics(PlanningBox a, PlanningBox b) => a.type == b.type && a.propEntryId == b.propEntryId &&
        a.paletteEntryId == b.paletteEntryId && a.isMerged == b.isMerged && a.singleInstance == b.singleInstance &&
        a.doorOwnerId == b.doorOwnerId && a.doorButtonOrder == b.doorButtonOrder && a.mechanismBaseId == b.mechanismBaseId &&
        a.mechanismBaseCellOffset == b.mechanismBaseCellOffset && a.crankOwnerId == b.crankOwnerId;

    [Serializable] private sealed class OverrideSignature { public List<LevelEditorComponentValueOverride> values; }
    private static string OverrideJson(IReadOnlyList<LevelEditorComponentValueOverride> values) =>
        JsonUtility.ToJson(new OverrideSignature { values = LevelEditorComponentOverrideUtility.PersistentValues(values) });
    private static string Signature(PlanningBox box, LevelEditorPropEntry entry) =>
        JsonUtility.ToJson(box.PersistentClone()) + OverrideJson(box.hasComponentOverrides ? box.componentOverrides : entry.ComponentValueOverrides);

    private static List<string> EffectiveConflicts(LevelEditorPropEntry entry, PlanningBox a, PlanningBox b)
    {
        string path = AssetDatabase.GetAssetPath(entry.Prefab);
        GameObject left = null, right = null;
        try
        {
            left = PrefabUtility.LoadPrefabContents(path); right = PrefabUtility.LoadPrefabContents(path);
            LevelEditorComponentOverrideUtility.ApplyOverrides(left, entry.ComponentValueOverrides, false);
            LevelEditorComponentOverrideUtility.ApplyOverrides(right, entry.ComponentValueOverrides, false);
            LevelEditorComponentOverrideUtility.ApplyOverrides(left, a.componentOverrides, false);
            LevelEditorComponentOverrideUtility.ApplyOverrides(right, b.componentOverrides, false);
            var one = GameplayValues(left); var two = GameplayValues(right);
            return one.Keys.Union(two.Keys).Where(key => !one.TryGetValue(key, out string x) || !two.TryGetValue(key, out string y) || x != y)
                .Select(key => key + ": retained=" + one.GetValueOrDefault(key, "<missing>") + "; duplicate=" + two.GetValueOrDefault(key, "<missing>"))
                .ToList();
        }
        finally
        {
            if (left != null) PrefabUtility.UnloadPrefabContents(left);
            if (right != null) PrefabUtility.UnloadPrefabContents(right);
        }
    }

    private static Dictionary<string, string> GameplayValues(GameObject root)
    {
        var result = new Dictionary<string, string>();
        foreach (var component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component is Transform || component is Renderer || component is MeshFilter ||
                component is MechanismArtworkPresentation || component is LevelEditorPlacedBlock) continue;
            using var serialized = new SerializedObject(component);
            var property = serialized.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyPath == "m_Script" || !LevelEditorComponentOverrideUtility.TryCapture(component, property, out var captured)) continue;
                string key = AnimationUtility.CalculateTransformPath(component.transform, root.transform) + "/" + component.GetType().FullName +
                    "[" + LevelEditorComponentOverrideUtility.GetComponentIndex(component) + "]." + property.propertyPath;
                string value = captured.Value;
                if (property.propertyType == SerializedPropertyType.ObjectReference)
                    value = ReferenceIdentity(property.objectReferenceValue, root.transform);
                if (property.propertyType == SerializedPropertyType.AnimationCurve)
                {
                    var curve = property.animationCurveValue;
                    value = curve.preWrapMode + "/" + curve.postWrapMode + "/" + string.Join(";", curve.keys.Select(keyframe =>
                        keyframe.time.ToString("R", CultureInfo.InvariantCulture) + "," + keyframe.value.ToString("R", CultureInfo.InvariantCulture) +
                        "," + keyframe.inTangent.ToString("R", CultureInfo.InvariantCulture) + "," + keyframe.outTangent.ToString("R", CultureInfo.InvariantCulture)));
                }
                result.Add(key, value);
            }
        }
        return result;
    }

    private static string ReferenceIdentity(Object item, Transform root)
    {
        if (item == null) return "null";
        Transform target = item is Component component ? component.transform : item is GameObject go ? go.transform : null;
        if (target != null && (target == root || target.IsChildOf(root)))
            return "local:" + AnimationUtility.CalculateTransformPath(target, root) + "/" + item.GetType().FullName;
        return GlobalObjectId.GetGlobalObjectIdSlow(item).ToString();
    }

    // Locate an exact record and edit only the specified fields; unknown save fields survive.
    private static (int start, int end) RecordBounds(string json, string id)
    {
        var stack = new Stack<int>(); bool quoted = false, escape = false; int start = -1, end = -1;
        var match = Regex.Match(json, "\\\"id\\\"\\s*:\\s*\\\"" + Regex.Escape(id) + "\\\"");
        if (!match.Success) throw new InvalidOperationException("Record not found: " + id);
        for (int index = 0; index < json.Length; index++)
        {
            char token = json[index];
            if (quoted) { if (escape) escape = false; else if (token == '\\') escape = true; else if (token == '"') quoted = false; continue; }
            if (token == '"') { if (index == match.Index) start = stack.Peek(); quoted = true; continue; }
            if (token == '{') stack.Push(index);
            else if (token == '}') { int opening = stack.Pop(); if (opening == start) { end = index + 1; break; } }
        }
        if (start < 0 || end < 0) throw new InvalidOperationException("Unbalanced duplicate JSON record.");
        return (start, end);
    }
    private static string RecordText(string json, string id) { var span = RecordBounds(json, id); return json.Substring(span.start, span.end - span.start); }
    private static string ReplaceRecord(string json, string id, string replacement) { var span = RecordBounds(json, id); return json.Remove(span.start, span.end - span.start).Insert(span.start, replacement); }
    private static string ReplaceScalar(string json, string key, string replacement)
    {
        var expression = new Regex("(\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*)(\\\"(?:\\\\.|[^\\\"])*\\\"|-?[0-9]+)");
        if (!expression.IsMatch(json)) throw new InvalidOperationException("Missing scalar: " + key);
        return expression.Replace(json, match => match.Groups[1].Value + replacement, 1);
    }

    private static Dictionary<int, string> Snapshot(Scene scene)
    {
        var result = new Dictionary<int, string>();
        foreach (var root in scene.GetRootGameObjects()) foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            result.Add(transform.gameObject.GetInstanceID(), EditorJsonUtility.ToJson(transform.gameObject));
            foreach (var component in transform.GetComponents<Component>())
                if (component != null) result.Add(component.GetInstanceID(), EditorJsonUtility.ToJson(component));
        }
        return result;
    }
    private static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    private static string Normalize(string text) => text?.Replace("\r\n", "\n").TrimStart('\uFEFF');
    private static Vector3 Abs(Vector3 value) => new(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    private static Encoding EncodingFor(byte[] bytes) => new UTF8Encoding(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);

    private static void Screenshot(LevelEditorPlacedBlock[] owners, string output)
    {
        if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) { report.AppendLine("INFO GPU unavailable; no screenshot, all serialized assertions still run."); return; }
        var center = owners.Aggregate(Vector3.zero, (sum, block) => sum + block.transform.position) / owners.Length;
        var cameraObject = new GameObject("Formal audit camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point, antiAliasing = 1 };
        var image = new Texture2D(1280, 720, TextureFormat.RGBA32, false); RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 6;
            camera.transform.position = new Vector3(center.x, center.y + 1, center.z - 25);
            camera.nearClipPlane = .1f; camera.farClipPlane = 100; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.055f, .075f, .11f); camera.targetTexture = target;
            target.Create(); ArtworkScenePreview.DrawForCamera(camera); camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(output, image.EncodeToPNG()); report.AppendLine("GPU " + SystemInfo.graphicsDeviceType + " local author Scene capture: " + output);
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            Object.DestroyImmediate(target); Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject);
        }
    }
}
