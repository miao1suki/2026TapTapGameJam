using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PlanningEditorPrototype;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.Player;
using Project.Rooms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Explicit, guarded migration of the saved R1 scene; never rebuilds source art.</summary>
public static class R1HorizontalMirror
{
    private const int Width = 58;
    private const string Backup = "Library/R1BeforeHorizontalMirror";
    private const string OriginalSha = "6545C090A3EEF604E7D904054B8D94299D94C6C7C8220DA1C0CB51D13BAF8CDD";
    private static int checks;
    [Serializable] private sealed class Signature { public List<LevelEditorComponentValueOverride> values; }
    private sealed class Pose
    {
        public Vector3 position, scale;
        public Dictionary<Collider, Bounds> collisions;
    }
    private static void Check(bool value, string text)
    {
        checks++;
        if (!value) throw new InvalidOperationException("R1 mirror: " + text);
    }
    private static Vector3 Reflect(Vector3 p) => new Vector3(Width - p.x, p.y, p.z);
    private static void Dirty(Object value)
    {
        EditorUtility.SetDirty(value);
        if (PrefabUtility.IsPartOfPrefabInstance(value)) PrefabUtility.RecordPrefabInstancePropertyModifications(value);
    }
    private static LevelEditorPlacedBlock[] Blocks() => Object.FindObjectsByType<LevelEditorPlacedBlock>(FindObjectsSortMode.None)
        .Where(p => !string.IsNullOrEmpty(p.PlanningBoxId)).ToArray();
    public static void ApplyAndVerify()
    {
        Check(Application.dataPath.Replace('\\', '/').EndsWith("/2026TapTapGameJam-R1/Assets"), "dedicated R1 directory only");
        Check(!EditorApplication.isPlaying, "edit mode only");
        using (var hash = SHA256.Create())
            Check(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(R1LevelBuilder.ScenePath))).Replace("-", "") == OriginalSha,
                "saved designer baseline must match; refuse repeat mirror or unseen edits");
        Directory.CreateDirectory(Backup);
        foreach (var path in new[] { R1LevelBuilder.ScenePath, R1LevelBuilder.SavePath })
        {
            var target = Path.Combine(Backup, Path.GetFileName(path));
            if (!File.Exists(target)) File.Copy(path, target);
            else Check(File.ReadAllBytes(path).SequenceEqual(File.ReadAllBytes(target)), "existing backup matches unchanged input");
        }
        var scene = EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        Physics.SyncTransforms();
        var doc = PlanningSaveStore.Read(R1LevelBuilder.SavePath);
        var originalDoc = PlanningDocument.FromPersistentJson(doc.ToPersistentJson());
        var blocks = Blocks();
        Check(blocks.Length == 76 && doc.rooms.Count == 1, "one room and all 76 saved roots");
        var before = blocks.ToDictionary(p => p.PlanningBoxId, p => new Pose
        {
            position = p.transform.position, scale = p.transform.localScale,
            collisions = p.GetComponentsInChildren<Collider>(true).ToDictionary(c => c, c => c.bounds)
        });
        var player = Object.FindFirstObjectByType<PlayerController>();
        Vector3 playerBefore = player.transform.position;
        foreach (var room in doc.rooms)
        {
            // The one rectangular R1 room stays in place; its detail layout is reflected.
            foreach (var box in room.boxes) MirrorBox(box);
        }
        foreach (var note in doc.regions) note.x = Width - note.x - note.width;
        doc.playerStartLocal.x = Width - doc.playerStartLocal.x;
        if (doc.hasUnassignedPlayerStart) doc.playerStartWorld = Reflect(doc.playerStartWorld);
        foreach (var placed in blocks.OrderBy(p => Depth(p.transform)))
        {
            var t = placed.transform;
            t.position = Reflect(before[placed.PlanningBoxId].position);
            bool bar = placed.PlanningBoxId == "r1_green_horizontal";
            // Rz(180) + local Y reflection is the same XY mirror, with positive physics scale.
            // It also maps ContactCube's reset-to-local-right initial length to world-left.
            if (bar) t.rotation = Quaternion.AngleAxis(180, Vector3.forward) * t.rotation;
            MirrorChildren(t, bar);
            var size = placed.SizeCells;
            var cell = placed.Cell;
            var so = new SerializedObject(placed);
            so.FindProperty("cell").vector2IntValue = new Vector2Int(Width - cell.x - size.x, cell.y);
            so.ApplyModifiedPropertiesWithoutUndo();
            var box = doc.rooms[0].boxes.Single(b => b.id == placed.PlanningBoxId);
            var values = box.componentOverrides;
            Check(box.hasComponentOverrides || values.Count == 0, "unmodified defaults remain empty");
            if (!box.hasComponentOverrides)
            {
                var palette = LevelEditorPaletteService.GetOrCreate();
                values = box.type == PlanningDetailType.Solid
                    ? palette.Entries.First(e => e.EntryId == box.paletteEntryId).ComponentValueOverrides.ToList()
                    : palette.PropEntries.First(e => e.EntryId == box.propEntryId).ComponentValueOverrides.ToList();
            }
            placed.SetPlanningSource(box.id, JsonUtility.ToJson(box.PersistentClone()) +
                JsonUtility.ToJson(new Signature { values = LevelEditorComponentOverrideUtility.PersistentValues(values) }),
                new Vector2Int(box.x, box.y));
            Dirty(t); Dirty(placed);
        }
        player.transform.position = Reflect(playerBefore); Dirty(player.transform);
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            camera.transform.position = Reflect(camera.transform.position); Dirty(camera.transform);
        }
        var manager = Object.FindFirstObjectByType<RoomLifecycleManager>();
        manager.Configure(new List<RoomRegion>(manager.Regions), manager.CellSize, doc.ToPersistentJson(true));
        Dirty(manager);
        Physics.SyncTransforms();
        foreach (var placed in blocks)
        {
            var pose = before[placed.PlanningBoxId];
            Check(Vector3.Distance(placed.transform.position, Reflect(pose.position)) < .0001f, "exact root reflection " + placed.PlanningBoxId);
            Check(placed.transform.localScale == pose.scale, "preserved scale " + placed.PlanningBoxId);
            foreach (var pair in pose.collisions)
            {
                if (pair.Value.size == Vector3.zero) continue;
                var now = pair.Key.bounds;
                Check(Vector3.Distance(now.center, Reflect(pair.Value.center)) < .002f, "collider center " + placed.PlanningBoxId);
                Check(Vector3.Distance(now.size, pair.Value.size) < .002f, "collider size " + placed.PlanningBoxId);
            }
        }
        foreach (var box in doc.rooms[0].boxes)
        {
            var old = originalDoc.rooms[0].boxes.Single(b => b.id == box.id);
            Check(box.x == Width - old.x - old.width && box.y == old.y, "planning rectangle " + box.id);
            Check(box.crankOwnerId == old.crankOwnerId && box.doorOwnerId == old.doorOwnerId, "stable wiring " + box.id);
        }
        PlanningSaveStore.Save(R1LevelBuilder.SavePath, doc);
        Check(EditorSceneManager.SaveScene(scene), "save reflected scene");
        EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        Check(PlanningDocument.FromPersistentJson(Object.FindFirstObjectByType<RoomLifecycleManager>().PlanningJson).ToPersistentJson() ==
            PlanningSaveStore.Read(R1LevelBuilder.SavePath).ToPersistentJson(), "embedded and paired planning match after reload");
        foreach (var placed in Blocks())
            Check(Vector3.Distance(placed.transform.position, Reflect(before[placed.PlanningBoxId].position)) < .0001f, "saved exact pose " + placed.PlanningBoxId);
        Check(Vector3.Distance(Object.FindFirstObjectByType<PlayerController>().transform.position, Reflect(playerBefore)) < .0001f, "saved mirrored player");
        File.WriteAllText("Library/R1HorizontalMirror.txt", $"PASS {checks} assertions: exact reflection around X=29; 76 root poses, every saved collider, positive scales, manual tweaks, planning and annotations, stable links, saved/reloaded scene and paired JSON. Backup: {Backup}. Scene-only manual transforms still must not be regenerated from planning.");
        Debug.Log(File.ReadAllText("Library/R1HorizontalMirror.txt"));
    }
    private static void MirrorBox(PlanningBox box)
    {
        box.x = Width - box.x - box.width;
        box.mechanismBaseCellOffset = new Vector2Int(-box.mechanismBaseCellOffset.x, box.mechanismBaseCellOffset.y);
        for (int i = 0; i < box.mergeParts.Count; i++)
        {
            var part = JsonUtility.FromJson<PlanningBox>(box.mergeParts[i]);
            MirrorBox(part); box.mergeParts[i] = JsonUtility.ToJson(part);
        }
    }
    public static void VerifySaved()
    {
        Check(!EditorApplication.isPlaying, "verification is edit mode only");
        EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        var doc = PlanningSaveStore.Read(R1LevelBuilder.SavePath);
        var before = Blocks().ToDictionary(p => p.PlanningBoxId, p =>
            (instance: p.GetInstanceID(), position: p.transform.position, rotation: p.transform.rotation, scale: p.transform.localScale));
        Check(before.Count == 76, "all authored objects present");
        Check(Vector3.Distance(before["r1_green_horizontal"].position, new Vector3(39.5f, -24.5f, 0)) < .0001f,
            "green bar root mirrors the source endpoint");
        Check(Vector3.Dot(before["r1_green_horizontal"].rotation * Vector3.right, Vector3.left) > .99f,
            "green bar serialized orientation survives reload");
        try
        {
            PlanningSceneBuilder.SuppressSceneUndo = true;
            Check(PlanningSceneBuilder.TryApplyDocument(doc, out var message), message);
            foreach (var block in Blocks())
            {
                var old = before[block.PlanningBoxId];
                Check(block.GetInstanceID() == old.instance, "no-op apply retains instance " + block.PlanningBoxId);
                Check(Vector3.Distance(block.transform.position, old.position) < .0001f &&
                    Quaternion.Angle(block.transform.rotation, old.rotation) < .001f && block.transform.localScale == old.scale,
                    "no-op apply retains exact mirrored manual pose " + block.PlanningBoxId);
            }
            Check(Blocks().Length == before.Count, "no-op apply does not add or remove roots");
            File.WriteAllText("Library/R1MirrorNoOpVerification.txt", $"PASS {checks} assertions; paired mirror planning applies without rebuilding any of the 76 saved roots or losing any manual pose/rotation/scale. Verification discards its unsaved scene.");
            Debug.Log(File.ReadAllText("Library/R1MirrorNoOpVerification.txt"));
        }
        finally
        {
            PlanningSceneBuilder.SuppressSceneUndo = false;
            // Test only in memory; keep the verified saved author scene authoritative.
            EditorSceneManager.OpenScene(R1LevelBuilder.ScenePath);
        }
    }
    private static void MirrorChildren(Transform parent, bool reflectY)
    {
        foreach (var collider in parent.GetComponents<Collider>())
        {
            if (collider is BoxCollider box)
            {
                var center = box.center;
                if (reflectY) center.y = -center.y; else center.x = -center.x;
                box.center = center; Dirty(box);
            }
            else if (collider is CapsuleCollider capsule)
            {
                var center = capsule.center;
                if (reflectY) center.y = -center.y; else center.x = -center.x;
                capsule.center = center; Dirty(capsule);
            }
            else throw new InvalidOperationException("Unreviewed collider type: " + collider.GetType());
        }
        foreach (Transform child in parent)
        {
            if ((child.hideFlags & HideFlags.DontSaveInEditor) != 0) continue;
            // Preset vine segments are themselves planned roots, handled once by saved world pose.
            if (child.GetComponent<LevelEditorPlacedBlock>() != null) continue;
            var p = child.localPosition; var q = child.localRotation;
            if (reflectY) { p.y = -p.y; q = new Quaternion(-q.x, q.y, -q.z, q.w); }
            else { p.x = -p.x; q = new Quaternion(q.x, -q.y, -q.z, q.w); }
            child.localPosition = p; child.localRotation = q; Dirty(child);
            MirrorChildren(child, reflectY);
        }
    }
    private static int Depth(Transform value)
    {
        int result = 0;
        for (var p = value.parent; p != null; p = p.parent) result++;
        return result;
    }
}
