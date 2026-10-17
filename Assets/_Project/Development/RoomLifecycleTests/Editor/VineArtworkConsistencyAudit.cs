using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using PlanningEditorPrototype;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.LevelEditor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// Read-only source assets; all temporary objects and reports stay in the probe.
public static class VineArtworkConsistencyAudit
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Check(bool pass, string label)
    {
        if (!pass) throw new InvalidOperationException("VINE ART: " + label);
        checks++; Debug.Log("VINE ART PASS: " + label);
    }
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < .002f;
    private static object Call(object owner, string name, params object[] args) =>
        owner.GetType().GetMethod(name, Hidden).Invoke(owner, args);
    private static PlanningArtworkPreview Resolve(PlanningCanvas canvas, PlanningBox box)
    {
        object[] args = { box, default(PlanningArtworkPreview) };
        Check((bool)Call(canvas, "ResolveArtworkPreview", args), "canvas resolves vine " + box.id);
        return (PlanningArtworkPreview)args[1];
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/Library/FlowRetirementProbe/Assets"))
            throw new InvalidOperationException("此验证仅可在 Library/FlowRetirementProbe 隔离工程执行。");
        if (!Environment.GetCommandLineArgs().Any(arg => string.Equals(arg, "-noaudio", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("隔离验证必须使用 -noaudio，避免触发游戏音效。");
        int code = 0; checks = 0;
        try
        {
            var library = ObjectArtworkLibrary.Load();
            Check(library != null && library.vineGrowth.Length == 192, "original 192 TA frames remain intact");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Content/ColorBlocks/Prefabs/Green_Ladder.prefab");
            Check(prefab != null, "canonical vine source exists");
            PlanningColumns(prefab);
            PlanningBrushBatches(prefab);
            PureAuthorReads(prefab);
            ScaleAndAnchor(library);
            string report = "PASS " + checks + " vine artwork grouping, cache, author-read and world-scale assertions";
            File.WriteAllText("Library/VineArtworkConsistency.txt", report); Debug.Log(report);
        }
        catch (Exception error)
        {
            code = 1; File.WriteAllText("Library/VineArtworkConsistency.txt", error.ToString()); Debug.LogException(error);
        }
        EditorApplication.Exit(code);
    }
    private static void PlanningColumns(GameObject prefab)
    {
        var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(
            "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        var entry = palette.PropEntries.First(e => e?.Prefab == prefab);
        var doc = new PlanningDocument { name = "藤蔓表现验证" };
        var room = new PlanningRoom("主体");
        var other = new PlanningRoom("隔离房间");
        doc.rooms.Add(room); doc.rooms.Add(other);
        foreach (var cell in new RectInt(0, 0, 12, 12).allPositionsWithin)
        { room.cells.Add(new PlanningCell(cell.x, cell.y)); other.cells.Add(new PlanningCell(cell.x + 20, cell.y)); }
        PlanningBox Box(int x, int y) => new PlanningBox(PlanningDetailType.Prop, new RectInt(x, y, 1, 1))
            { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
        var bottom = Box(2, 5); var middle = Box(2, 4); var top = Box(2, 3);
        var gap = Box(2, 1); var diagonal = Box(3, 2); var separate = Box(4, 5);
        room.boxes.AddRange(new[] { bottom, middle, top, gap, diagonal, separate });
        var otherRoom = Box(2, 4); other.boxes.Add(otherRoom);
        var patch = Box(2, 2); doc.assemblyPatches.Add(patch);
        var canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(doc);
        Check(ObjectArtworkPlanningPreview.TryGetVine(prefab.GetComponent<ClimbableVineFeature>(), Vector2.one,
            entry.ComponentValueOverrides, 0f, out var authoredSingle), "palette-authored vine width and sequence resolve");
        string before = JsonUtility.ToJson(doc);
        var parts = new[] { Resolve(canvas, bottom), Resolve(canvas, middle), Resolve(canvas, top) };
        Check(parts.All(p => p.Texture == parts[0].Texture), "three-cell column samples one shared whole-root texture");
        Check(parts[0].Texture.height == 48 && parts.All(p => Near(p.Cells.y, 1f)), "whole-root 48 pixels partition into16 pixels per gameplay cell");
        for (int row = 0; row < parts.Length; row++)
        {
            Check(Near(parts[row].UV.y, row / 3f) && Near(parts[row].UV.height, 1f / 3), "continuous root UV slice bottom-to-top " + row);
            Check(Near(parts[row].Cells.x, authoredSingle.Cells.x), "shared width retains actual palette override for row " + row);
        }
        Check(parts[0].Texture.GetPixels32().Any(p => p.a > 0), "whole-root preview contains actual TA artwork");
        foreach (var single in new[] { gap, diagonal, separate, otherRoom, patch })
            Check(Resolve(canvas, single).Texture.height == 16, "gaps, diagonals, other rooms and assembly scope cannot bridge roots: " + single.id);
        Check(before == JsonUtility.ToJson(doc), "resolving previews never changes authored document or placement");
        for (int i = 0; i < 100; i++)
            Check(ResolveCached(canvas, bottom).Texture == parts[0].Texture, "repeated pan redraw reuses root texture " + i);

        // A deliberate document edit splits the column. The same invalidation path
        // as paint/erase must discard all sibling root/slice caches together.
        room.boxes.Remove(middle); Call(canvas, "InvalidatePreviewData");
        Check(Resolve(canvas, bottom).Texture.height == 16 && Resolve(canvas, top).Texture.height == 16,
            "erasing the middle immediately restores two independent tips");
        room.boxes.Add(middle); Call(canvas, "InvalidatePreviewData");
        Check(Resolve(canvas, middle).Texture.height == 48, "drawing middle back rejoins shared root without reopening window");

        bottom.hasComponentOverrides = true;
        var widthOverride = new LevelEditorComponentValueOverride();
        widthOverride.Configure("", typeof(ClimbableVineFeature).AssemblyQualifiedName, 0, "vineWidth", "Float", "1.6", null);
        bottom.componentOverrides.Add(widthOverride); Call(canvas, "InvalidatePreviewData");
        Check(Near(Resolve(canvas, top).Cells.x, 1.6f), "mother width override applies to the whole prebuilt root");
        var root = prefab.GetComponent<ClimbableVineFeature>();
        Check(ObjectArtworkPlanningPreview.TryGetVine(root, new Vector2(1, 3), null, 1, out var whole), "shared root preview entry resolves grown sequence");
        var screenshot = new Texture2D(whole.Texture.width, whole.Texture.height, TextureFormat.RGBA32, false);
        try
        {
            screenshot.SetPixels32(whole.Texture.GetPixels32()); screenshot.Apply();
            File.WriteAllBytes("Library/VineArtwork-ContinuousColumn.png", screenshot.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(screenshot); }
    }
    private static PlanningArtworkPreview ResolveCached(PlanningCanvas canvas, PlanningBox box)
    {
        object[] args = { box, default(PlanningArtworkPreview) };
        if (!(bool)Call(canvas, "ResolveArtworkPreview", args)) throw new Exception("cache resolution failed");
        return (PlanningArtworkPreview)args[1];
    }
    private static void PlanningBrushBatches(GameObject prefab)
    {
        var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>(
            "Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
        var entry = palette.PropEntries.First(e => e?.Prefab == prefab);
        var doc = new PlanningDocument { name = "正式关卡多格笔刷藤蔓验证" };
        var room = new PlanningRoom("批次"); doc.rooms.Add(room);
        foreach (var cell in new RectInt(0, 0, 40, 40).allPositionsWithin)
            room.cells.Add(new PlanningCell(cell.x, cell.y));
        PlanningBox Batch(RectInt rect) => new(PlanningDetailType.Prop, rect)
            { propEntryId = entry.EntryId, propEntryName = entry.DisplayName };
        var upper = Batch(new RectInt(2, 3, 1, 5));
        var lower = Batch(new RectInt(2, 8, 1, 5));
        var horizontal = Batch(new RectInt(5, 4, 12, 1));
        var rectangle = Batch(new RectInt(20, 3, 2, 3));
        room.boxes.AddRange(new[] { upper, lower, horizontal, rectangle });
        var canvas = new PlanningCanvas(); canvas.SetPalette(palette); canvas.SetDocument(doc);
        string before = JsonUtility.ToJson(doc);
        var a = Resolve(canvas, upper); var b = Resolve(canvas, lower);
        Check(a.Texture == b.Texture && a.Texture.height == 160,
            "adjacent 1x5 brush batches become one ten-cell runtime-compatible mother root");
        var groups = (Dictionary<string, Dictionary<Vector2Int, PlanningArtworkPreview>>)canvas.GetType()
            .GetField("vinePreviewCellSlices", Hidden).GetValue(canvas);
        for (int row = 0; row < 10; row++)
        {
            var cell = new Vector2Int(2, 12 - row);
            var slice = groups[row < 5 ? lower.id : upper.id][cell];
            Check(slice.Texture == a.Texture && Near(slice.UV.y, row / 10f) && Near(slice.UV.height, .1f),
                "cross-batch ten-cell root preserves one continuous bottom-to-top image: " + row);
        }
        Check(Resolve(canvas, horizontal).Texture.height == 16 && groups[horizontal.id].Count == 12 &&
            groups[horizontal.id].Values.All(v => v.Texture.height == 16 && Near(v.UV.height, 1)),
            "12x1 brush rectangle remains twelve separately rooted vines, not one stretched image");
        Check(Resolve(canvas, rectangle).Texture.height == 48 && groups[rectangle.id].Count == 6,
            "2x3 brush rectangle creates two three-cell roots without repeating tips in every cell");
        foreach (var batch in room.boxes)
        {
            var active = new HashSet<string>();
            Check((bool)Call(canvas, "TryAddArtworkPreview", batch,
                new RectInt(batch.x, batch.y, batch.width, batch.height), active, Vector2Int.zero),
                "multi-cell vine brush uses dedicated cell-slice draw path");
            Check(active.Count == batch.width * batch.height, "every brush cell draws exactly one native-size UV slice");
            var images = (Dictionary<string, Image>)canvas.GetType().GetField("detailPreviewImages", Hidden).GetValue(canvas);
            string key = active.First();
            var first = images[key];
            for (int repaint = 0; repaint < 20; repaint++)
                Call(canvas, "TryAddArtworkPreview", batch,
                    new RectInt(batch.x, batch.y, batch.width, batch.height), active, Vector2Int.zero);
            Check(ReferenceEquals(first, images[key]), "pan/repaint reuses existing vine cell UI images");
        }
        Check(before == JsonUtility.ToJson(doc), "multi-cell root joining and painting never change author rectangles or placement");
    }
    private static void PureAuthorReads(GameObject prefab)
    {
        var mother = Object.Instantiate(prefab);
        try
        {
            var root = mother.GetComponent<ClimbableVineFeature>();
            var rootBox = mother.GetComponent<BoxCollider>();
            for (int i = 1; i <= 3; i++)
            {
                var segment = Object.Instantiate(root.GrowthSegmentPrefab, mother.transform);
                segment.transform.localPosition = Vector3.up * i;
                segment.transform.localScale = Vector3.one;
            }
            string before = EditorJsonUtility.ToJson(root);
            Check(root.ArtworkRoot == root && root.AuthoredArtworkCells == new Vector2(root.ArtworkWidthCells, 4), "pure hierarchy read identifies four-cell author root");
            foreach (var segment in mother.GetComponentsInChildren<LadderSonFeature>(true))
                Check(segment.ArtworkRoot == root, "child preview resolves its real direct mother without rebinding");
            Check(ObjectArtworkPlanningPreview.TryGetVine(root, root.AuthoredArtworkCells, out var preview) && preview.Texture.height == 64,
                "Scene author root matches the same four-cell planning image");
            Check(before == EditorJsonUtility.ToJson(root), "edit-time preview does not mutate ownership, segments or max-growth fields");
            Bounds enabled = root.AuthoredArtworkRootBounds; rootBox.enabled = false;
            Check(root.AuthoredArtworkRootBounds == enabled, "locked/disabled collider does not collapse edit-time root bounds");
        }
        finally { Object.DestroyImmediate(mother); }
    }
    private static void ScaleAndAnchor(ObjectArtworkLibrary library)
    {
        Sprite frame = library.vineGrowth[library.vineGrownFrame];
        foreach (float cell in new[] { .5f, 1f, 2f })
        foreach (Vector3 authorScale in new[] { Vector3.one, new Vector3(2, 4, 1), new Vector3(.5f, 2, 3) })
        {
            var owner = new GameObject("Vine scale check");
            try
            {
                owner.transform.position = new Vector3(3.5f, -9.25f, 2);
                owner.transform.localScale = authorScale;
                var box = owner.AddComponent<BoxCollider>(); box.center = new Vector3(.1f, .25f, 0);
                var visual = new GameObject("Vine image"); visual.transform.SetParent(owner.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>(); renderer.sprite = frame;
                Bounds rootBounds = VineArtworkLayout.RootBounds(owner.transform, box, cell);
                VineArtworkLayout.Apply(visual.transform, owner.transform, frame, 1.2f, 7, cell, rootBounds);
                Check(Near(renderer.bounds.size.x, 1.2f * cell) && Near(renderer.bounds.size.y, 7 * cell),
                    "world art dimensions ignore nonuniform author scale " + authorScale + "/cell" + cell);
                Check(Near(renderer.bounds.min.y, rootBounds.min.y) && Near(renderer.bounds.center.x, rootBounds.center.x),
                    "image starts at the actual author bottom and center " + authorScale + "/cell" + cell);
                Check(Near(visual.transform.position.z, owner.transform.position.z - .51f * cell),
                    "visual depth remains in world cell units " + authorScale + "/cell" + cell);
            }
            finally { Object.DestroyImmediate(owner); }
        }
    }
}
