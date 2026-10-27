using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.GridArt.Editor;
using Project.LevelEditor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanningEditorPrototype
{
    /// <summary>Read-only source projection / retained-reference regression; root runs in the isolated probe.</summary>
    public static class PlanningArtReferenceAudit
    {
        private static readonly List<string> report = new();
        private static int checks;
        [MenuItem("Tools/2026TapTap/Legacy/验证旧美术画布关卡参照")]
        public static void Run()
        {
            if (!Application.dataPath.Replace('\\', '/').EndsWith("/Library/FlowRetirementProbe/Assets", StringComparison.OrdinalIgnoreCase) ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Reference audit runs only in stopped FlowRetirementProbe.");
            report.Clear(); checks = 0;
            try
            {
                var palette = AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");
                Check(palette != null, "shared level palette loads read-only");
                var water = Entry<WaterVisualFeature>(palette);
                var vine = Entry<ClimbableVineFeature>(palette, e => e.Prefab.GetComponent<LadderSonFeature>() == null &&
                    e.Prefab.GetComponent<ClimbableVineFeature>().GrowthSegmentPrefab != null &&
                    e.Prefab.GetComponent<ClimbableVineFeature>().GrowthSegmentPrefab.GetComponent<LadderSonFeature>() != null);
                var platform = Entry<MovingPlatformFeature>(palette, e => !e.Prefab.GetComponent<MovingPlatformFeature>().IsCrankControlled);
                var spike = Entry<Project.WorldItems.NeutralSpike>(palette);
                var collectible = Entry<Project.Items.CollectiblePickup>(palette);
                var document = new PlanningDocument();
                var room = new PlanningRoom("远离原点的关卡") { id = "reference-room" };
                for (int y = 20; y < 32; y++) for (int x = 100; x < 120; x++) room.cells.Add(new PlanningCell(x, y));
                document.rooms.Add(room);
                room.boxes.Add(new PlanningBox { id = "wall", type = PlanningDetailType.Solid, x = 0, y = 0, width = 10, height = 1 });
                room.boxes.Add(Box(water, "water-a", 1, 1, 2, 3));
                room.boxes.Add(Box(water, "water-l-arm", 3, 3, 1, 1));
                room.boxes.Add(Box(water, "water-duplicate-force", 1, 1, 1, 1));
                room.boxes.Add(Box(vine, "vine-chain", 5, 1, 1, 3, false));
                room.boxes.Add(Box(platform, "old-one-cell-platform", 8, 2, 1, 1));
                var vertical = Box(platform, "vertical-five-platform", 10, 2, 1, 5);
                PlanningPlatformShapeUtility.SetOrientation(vertical, true); room.boxes.Add(vertical);
                var pointed = Box(spike, "right-spike", 12, 3, 1, 1);
                pointed.hasComponentOverrides = true;
                var facing = new LevelEditorComponentValueOverride();
                facing.Configure("", typeof(Project.WorldItems.NeutralSpike).FullName, 0, "facing", "Enum", "4", null);
                pointed.componentOverrides.Add(facing); room.boxes.Add(pointed);
                room.boxes.Add(Box(collectible, "two-cell-collectible", 14, 3, 1, 1));
                string before = JsonUtility.ToJson(document), paletteBefore = EditorJsonUtility.ToJson(palette);
                int sceneObjects = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                var snapshot = new GridArtLevelSnapshot(); PlanningArtAdapter.ReadDocument(document, palette, snapshot);
                Check(JsonUtility.ToJson(document) == before && EditorJsonUtility.ToJson(palette) == paletteBefore, "reference does not rewrite document, defaults, palette or layout");
                Check(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == sceneObjects,
                    "2D reference projection never instantiates scene objects / 3D prefabs");
                Check(snapshot.rooms.Count == 1 && snapshot.rooms[0].boundsWorld == new Rect(100, -32, 20, 12) &&
                    snapshot.hasWorldBounds && snapshot.worldBounds.Contains(new Vector2(100.5f, -20.5f)), "room extents and reference stay at actual far-away world position");
                Check(snapshot.groups[0].bounds.Single() == new Rect(100, -21, 10, 1), "wall conversion remains planning-down to world-up");
                var waterItems = snapshot.referenceItems.Where(i => i.layer == GridArtReferenceLayer.Water).ToList();
                Check(waterItems.Count == 7 && waterItems.Select(i => i.quad.bounds.position).Distinct().Count() == 7,
                    "non-rectangular water union keeps all seven cells and deduplicates overlapping water force art");
                ObjectArtworkPlanningPreview.TryGetWaterTile(0, out var surface); ObjectArtworkPlanningPreview.TryGetWaterTile(2, out var bottom);
                Check(waterItems.Any(i => i.quad.bounds.position == new Vector2(101, -22) && i.quad.uv == surface.UV) &&
                    waterItems.Any(i => i.quad.bounds.position == new Vector2(101, -24) && i.quad.uv == bottom.UV),
                    "water surface and bottom follow world topology, not merged bounding-box center");
                var vines = Items("vine-chain");
                Check(vines.Count == 3 && vines.Select(i => i.quad.texture).Distinct().Count() == 1 &&
                    vines.Select(i => i.quad.uv.y).Distinct().Count() == 3, "adjacent vine mothers share one sampled chain with continuous per-cell UV slices");
                var shortPlatform = Items("old-one-cell-platform").Single().quad;
                Check(Mathf.Approximately(shortPlatform.bounds.width, 1) && Mathf.Approximately(shortPlatform.uv.width * shortPlatform.texture.width, 16),
                    "legacy single-cell platform crops exactly sixteen authored pixels without becoming three cells");
                var longPlatform = Items("vertical-five-platform");
                Check(longPlatform.Count == 2 && longPlatform.All(i => i.quad.quarterTurns == 1) &&
                    Mathf.Approximately(longPlatform.Sum(i => i.quad.bounds.height), 5), "five-cell vertical platform rotates native three-plus-two-pixel-cropped segments");
                Check(Items("right-spike").Single().quad.quarterTurns == 3, "instance directional artwork override reaches reference geometry");
                var pickup = Items("two-cell-collectible").Single().quad;
                Check(Mathf.Approximately(pickup.bounds.width, 2) && Mathf.Approximately(pickup.bounds.height, 2) &&
                    Mathf.Approximately(pickup.uv.width * pickup.texture.width, 32), "32px collectible stays two cells rather than squeezing into logical one-cell pickup");
                TestRetained();
                File.WriteAllText("Library/PlanningArtReferenceAudit.txt", "PASS " + checks + "\n" + string.Join("\n", report));
                Debug.Log("PlanningArtReferenceAudit PASS " + checks);
                List<GridArtLevelReferenceItem> Items(string id) => snapshot.referenceItems.Where(i => i.id == id).ToList();
            }
            catch (Exception error)
            {
                File.WriteAllText("Library/PlanningArtReferenceAudit.txt", "FAIL " + checks + "\n" + string.Join("\n", report) + "\n" + error);
                throw;
            }
        }
        private static void TestRetained()
        {
            var snapshot = new GridArtLevelSnapshot();
            for (int y = 0; y < 100; y++) for (int x = 0; x < 100; x++) snapshot.AddReference("wall", new GridArtQuad(
                new Rect(x, y, 1, 1), new Rect(0, 0, 1, 1), Texture2D.whiteTexture), Color.white, GridArtReferenceLayer.Wall);
            var reference = new GridArtLevelReference(); reference.Configure(snapshot);
            Check(reference.QuadCount == 10000 && reference.BatchCount < 100, "ten-thousand reference cells use spatial mesh batches, not ten-thousand elements or Painter2D strokes");
            int revision = reference.GeometryRevision;
            for (int i = 0; i < 100; i++) reference.SetView(new Vector2(20 + i, 40 - i), 16 + i, new Rect(0, 0, 800, 600));
            Check(reference.GeometryRevision == revision && reference.VisibleBatchCount < reference.BatchCount,
                "one hundred pan/zoom updates retain geometry and cull distant batches without texture regeneration");
            reference.SetReferenceVisibility(false, .35f);
            Check(reference.VisibleBatchCount == 0 && reference.QuadCount == 10000, "visibility toggles do not destroy cached reference geometry");
            reference.SetReferenceVisibility(true, .15f); reference.SetView(new Vector2(0, 600), 20, new Rect(0, 0, 800, 600));
            Check(reference.GeometryRevision == revision && reference.VisibleBatchCount > 0, "opacity/show restore uses the same geometry");
            var overlap = new GridArtLevelSnapshot();
            foreach (var texture in new[] { Texture2D.whiteTexture, Texture2D.grayTexture, Texture2D.whiteTexture })
                overlap.AddReference("overlap", new GridArtQuad(new Rect(0, 0, 2, 2), new Rect(0, 0, 1, 1), texture),
                    Color.white, GridArtReferenceLayer.Object);
            reference.Configure(overlap);
            Check(reference.BatchCount == 3, "overlapping transparent foreground preserves source order rather than sorting by texture");
            reference.Configure(null); Check(reference.QuadCount == 0 && reference.BatchCount == 0, "scene/source clear drops old reference geometry without leaking visual elements");
        }
        private static PlanningBox Box(LevelEditorPropEntry entry, string id, int x, int y, int width, int height, bool merged = true) =>
            new() { id = id, type = PlanningDetailType.Prop, propEntryId = entry.EntryId, propEntryName = entry.DisplayName,
                x = x, y = y, width = width, height = height, isMerged = merged, singleInstance = merged };
        private static LevelEditorPropEntry Entry<T>(LevelEditorPalette palette, Func<LevelEditorPropEntry, bool> filter = null) where T : Component
        {
            var entry = palette.PropEntries.FirstOrDefault(e => e?.Prefab != null && e.Prefab.GetComponent<T>() != null && (filter == null || filter(e)));
            Check(entry != null, "official " + typeof(T).Name + " palette source exists"); return entry;
        }
        private static void Check(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException(message); report.Add(message); }
    }
}
