using System.IO;
using System.Collections.Generic;
using Project.BlockFeatures;
using Project.BlockFeatures.Editor;
using Project.GridArt;
using Project.GridArt.Editor;
using Project.LevelEditor;
using Project.LevelEditor.Editor;
using Project.ObjectArtwork;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PlanningEditorPrototype
{
    [InitializeOnLoad]
    internal static class PlanningArtAdapter
    {
        static PlanningArtAdapter() { GridArtLevelSource.Read = Read; GridArtColorSource.Read = ReadColors; }
        private static System.Collections.Generic.IReadOnlyList<GridArtColorDefinition> ReadColors()
        {
            var result = new System.Collections.Generic.List<GridArtColorDefinition>();
            var catalog = Resources.Load<Project.ColorBlocks.ColorCatalog>("ColorBlocks/ColorCatalog");
            if (catalog != null) foreach (var color in catalog.Colors)
                if (color != null) result.Add(new GridArtColorDefinition(color.id,color.displayName,color.unityLayer));
            return result;
        }
        internal static GridArtLevelSnapshot Read(Scene scene)
        {
            var snapshot = new GridArtLevelSnapshot();
            if (!scene.IsValid() || !scene.isLoaded) return snapshot;
            var document = PlanningEditorWindow.DocumentForScene(scene.handle);
            if (document != null) snapshot.label = "关卡编辑器 · 当前草稿";
            else
            {
                string path = PlanningSharedSaveUtility.SceneSavePath(scene.path);
                if (path != null && File.Exists(path))
                { document = PlanningSaveStore.Read(path); snapshot.label = "本关规划存档"; }
            }
            if (document == null) foreach (var root in scene.GetRootGameObjects())
            {
                var manager = root.GetComponentInChildren<Project.Rooms.RoomLifecycleManager>(true);
                if (manager == null || string.IsNullOrEmpty(manager.PlanningJson)) continue;
                document = JsonUtility.FromJson<PlanningDocument>(manager.PlanningJson);
                snapshot.label = "场景内关卡规划"; break;
            }
            if (document != null)
            {
                ReadDocument(document, Palette(), snapshot);
                foreach (var window in Resources.FindObjectsOfTypeAll<PlanningEditorWindow>())
                {
                    var canvas = window.rootVisualElement.Q<PlanningCanvas>();
                    if (canvas?.Document == document && !string.IsNullOrEmpty(canvas.SelectedRoomId))
                    { snapshot.currentRoomId = canvas.SelectedRoomId; break; }
                }
                ResolveCurrentRoom(scene, snapshot);
                return snapshot;
            }
            var sceneGroup = new GridArtLevelGroup { id = "scene", label = "场景墙地块" };
            var scenePalette = Palette();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var block in root.GetComponentsInChildren<LevelEditorPlacedBlock>(true))
                {
                    float size = block.CellWorldSize; snapshot.cellSize = size;
                    if (block.IsProp)
                    {
                        var target = new Rect((Vector2)block.Cell * size, (Vector2)block.SizeCells * size);
                        if (ObjectArtworkPlanningPreview.TryGet(block.gameObject, block.SizeCells, null, out var artwork))
                            AddArtwork(snapshot, block.PlanningBoxId, target, artwork, size);
                        else AddPlaceholder(snapshot, block.PlanningBoxId, block.EntryName, target);
                        continue;
                    }
                    Color tint = block.HasColorData ? block.EntryColor : Color.white;
                    if (!block.HasColorData && scenePalette != null) foreach (var entry in scenePalette.Entries)
                        if (entry != null && entry.DisplayName == block.EntryName) { tint = entry.Color; break; }
                    var shape = block.GetComponent<LevelEditorMergedShape>();
                    if (shape == null) AddWall(new Rect((Vector2)block.Cell*size, (Vector2)block.SizeCells*size));
                    else foreach (RectInt part in shape.Rectangles)
                        AddWall(new Rect(new Vector2(block.Cell.x+part.x,block.Cell.y+block.SizeCells.y-part.yMax)*size,(Vector2)part.size*size));
                    void AddWall(Rect rect)
                    {
                        sceneGroup.bounds.Add(rect);
                        snapshot.AddReference(block.PlanningBoxId, new GridArtQuad(rect, new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, GridArtReferenceLayer.Wall);
                    }
                }
            snapshot.groups.Add(sceneGroup);
            snapshot.RecalculateBounds(); return snapshot;
        }

        private static LevelEditorPalette Palette() => LevelEditorState.Palette ??
            AssetDatabase.LoadAssetAtPath<LevelEditorPalette>("Assets/_Project/Development/LevelEditor/Orpheus0829/LevelEditorPalette.asset");

        private sealed class Source
        {
            internal PlanningBox box;
            internal Vector2Int offset;
            internal LevelEditorPropEntry entry;
            internal IList<PlanningBox> siblings;
        }

        // Pure document projection: no normalization, asset creation, scene writes or prefab instantiation.
        internal static void ReadDocument(PlanningDocument document, LevelEditorPalette palette, GridArtLevelSnapshot snapshot)
        {
            snapshot.cellSize = 1;
            var sources = new List<Source>();
            foreach (var room in document.rooms)
            {
                var group = new GridArtLevelGroup { id = room.id, label = (room.isConnector ? "通道 · " : "房间 · ") + room.name };
                RectInt area = PlanningWorldUtility.Bounds(room);
                var bounds = new Rect(area.x * document.worldBlockCellWidth, -area.yMax * document.worldBlockCellHeight,
                    area.width * document.worldBlockCellWidth, area.height * document.worldBlockCellHeight);
                snapshot.rooms.Add(new GridArtLevelRoom { id = room.id, label = group.label, boundsWorld = bounds });
                foreach (var box in room.boxes)
                {
                    AddSource(box, PlanningWaterLayerUtility.Origin(document, room), room.boxes, group);
                }
                snapshot.groups.Add(group);
            }
            var patches = new GridArtLevelGroup { id = "assembly", label = "装配补丁" };
            foreach (var box in document.assemblyPatches) AddSource(box, Vector2Int.zero, document.assemblyPatches, patches);
            if (patches.bounds.Count > 0) snapshot.groups.Add(patches);

            // One union/owner set per source refresh, not per water tile or repaint.
            var water = new HashSet<Vector2Int>();
            var waterOwners = new Dictionary<Vector2Int, string>();
            foreach (var source in sources)
                if (PlanningWaterLayerUtility.IsWater(source.box, palette))
                    foreach (var cell in PlanningShapeUtility.Cells(source.box))
                    {
                        var world = cell + source.offset; water.Add(world);
                        if (!waterOwners.TryGetValue(world, out string owner) || string.CompareOrdinal(source.box.id, owner) < 0)
                            waterOwners[world] = source.box.id;
                    }
            foreach (var pair in waterOwners)
            {
                bool above = water.Contains(pair.Key + Vector2Int.down), below = water.Contains(pair.Key + Vector2Int.up);
                if (ObjectArtworkPlanningPreview.TryGetWaterTile(!above ? 0 : !below ? 2 : 1, out var tile))
                    AddArtwork(snapshot, pair.Value, WorldRect(new RectInt(pair.Key, Vector2Int.one)), tile, 1, GridArtReferenceLayer.Water);
            }
            var vineSlices = BuildVineSlices(sources);
            var library = ObjectArtworkLibrary.Load();
            foreach (var source in sources)
            {
                var box = source.box;
                if (PlanningWaterLayerUtility.IsWater(box, palette)) continue;
                if (vineSlices.TryGetValue(box, out var slices))
                {
                    foreach (var slice in slices)
                        AddArtwork(snapshot, box.id, WorldRect(new RectInt(slice.Key + source.offset, Vector2Int.one)), slice.Value, 1);
                    continue;
                }
                if (source.entry?.Prefab == null || !ObjectArtworkPlanningPreview.TryGet(source.entry.Prefab,
                    new Vector2(box.width, box.height), Values(source), out var artwork))
                {
                    foreach (var part in PlanningShapeUtility.Rectangles(box))
                    {
                        var rect = part; rect.position += source.offset;
                        AddPlaceholder(snapshot, box.id, source.entry?.DisplayName ?? box.propEntryName, WorldRect(rect));
                    }
                    continue;
                }
                if (box.propEntryId == PlanningDoorUtility.ButtonEntryId && box.doorButtonOrder > 0 && library != null)
                {
                    Sprite sprite = library.FirstFrame(ObjectArtworkLibrary.DoorButtonKind(box.doorButtonOrder - 1));
                    if (sprite != null) artwork = new PlanningArtworkPreview(sprite, artwork.Tile, artwork.RotationDegrees, artwork.FlipX);
                }
                foreach (var part in PlanningShapeUtility.Rectangles(box))
                {
                    var rect = part; rect.position += source.offset;
                    if (AddPlatform(snapshot, source, rect, library)) continue;
                    if (!box.isMerged && !box.singleInstance && !artwork.Tile)
                        foreach (var cell in rect.allPositionsWithin) AddArtwork(snapshot, box.id, WorldRect(new RectInt(cell, Vector2Int.one)), artwork, 1);
                    else AddArtwork(snapshot, box.id, WorldRect(rect), artwork, 1);
                }
            }
            snapshot.RecalculateBounds();

            void AddSource(PlanningBox box, Vector2Int offset, IList<PlanningBox> siblings, GridArtLevelGroup group)
            {
                // SceneBuilder chooses the block/prop path by the palette reference,
                // not the legacy detail enum. Existing white blocks often use Platform.
                if (string.IsNullOrEmpty(box.propEntryId) && string.IsNullOrEmpty(box.propEntryName))
                {
                    Color tint = Color.white;
                    var block = FindBlockEntry(palette, box);
                    if (block != null) tint = block.Color;
                    foreach (var part in PlanningShapeUtility.Rectangles(box))
                    {
                        var r = part; r.position += offset; Rect world = WorldRect(r); group.bounds.Add(world);
                        snapshot.AddReference(box.id, new GridArtQuad(world, new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, GridArtReferenceLayer.Wall);
                    }
                }
                else sources.Add(new Source { box = box, offset = offset, siblings = siblings, entry = FindEntry(palette, box) });
            }
        }

        private static IReadOnlyList<LevelEditorComponentValueOverride> Values(Source source) =>
            source.box.hasComponentOverrides ? source.box.componentOverrides : source.entry?.ComponentValueOverrides;
        private static Rect WorldRect(RectInt r) => new(r.x, -r.yMax, r.width, r.height);

        private static LevelEditorBlockEntry FindBlockEntry(LevelEditorPalette palette, PlanningBox box)
        {
            if (palette == null) return null;
            // Match SceneBuilder's ID -> name -> legacy-type -> first-entry fallback.
            if (!string.IsNullOrEmpty(box.paletteEntryId)) foreach (var entry in palette.Entries)
                if (entry != null && entry.EntryId == box.paletteEntryId) return entry;
            if (!string.IsNullOrEmpty(box.paletteEntryName)) foreach (var entry in palette.Entries)
                if (entry != null && entry.DisplayName == box.paletteEntryName) return entry;
            string name = box.type switch
            {
                PlanningDetailType.Solid => "黑方块",
                PlanningDetailType.Platform => "白方块",
                PlanningDetailType.Hazard => "红方块",
                PlanningDetailType.Water => "蓝方块",
                _ => "绿方块"
            };
            foreach (var entry in palette.Entries) if (entry != null && entry.DisplayName == name) return entry;
            return palette.Entries.Count > 0 ? palette.Entries[0] : null;
        }

        private static LevelEditorPropEntry FindEntry(LevelEditorPalette palette, PlanningBox box)
        {
            if (box.propEntryId == PlanningDoorUtility.ButtonEntryId) return PlanningDoorUtility.GetButtonEntry();
            if (palette == null) return null;
            if (!string.IsNullOrEmpty(box.propEntryId)) foreach (var entry in palette.PropEntries)
                if (entry != null && entry.EntryId == box.propEntryId) return entry;
            if (!string.IsNullOrEmpty(box.propEntryName)) foreach (var entry in palette.PropEntries)
                if (entry != null && entry.DisplayName == box.propEntryName) return entry;
            return null;
        }

        private static void AddPlaceholder(GridArtLevelSnapshot snapshot, string id, string label, Rect rect)
        {
            // Read-only editor guide for markers/props without formal 2D art.
            // Never instantiate a prefab, draw its 3D thumbnail or add it to wall fill.
            snapshot.referenceOnlyNames.Add(string.IsNullOrEmpty(label) ? "未找到素材的道具" : label);
            float edge = Mathf.Min(snapshot.cellSize / GridArtworkMetrics.PixelsPerCell, Mathf.Min(rect.width, rect.height) * .5f);
            Color tint = new(.95f, .64f, .25f);
            Add(new Rect(rect.x, rect.y, rect.width, edge));
            Add(new Rect(rect.x, rect.yMax - edge, rect.width, edge));
            Add(new Rect(rect.x, rect.y, edge, rect.height));
            Add(new Rect(rect.xMax - edge, rect.y, edge, rect.height));
            void Add(Rect strip) => snapshot.AddReference(id,
                new GridArtQuad(strip, new Rect(0, 0, 1, 1), Texture2D.whiteTexture), tint, GridArtReferenceLayer.EditorOverlay);
        }

        private static void ResolveCurrentRoom(Scene scene, GridArtLevelSnapshot snapshot)
        {
            if (!string.IsNullOrEmpty(snapshot.currentRoomId)) return;
            Vector3? position = Selection.activeGameObject != null && Selection.activeGameObject.scene == scene ? Selection.activeGameObject.transform.position : null;
            if (!position.HasValue) foreach (var root in scene.GetRootGameObjects())
            {
                var player = root.GetComponentInChildren<Project.Player.PlayerController>(true);
                if (player != null) { position = player.transform.position; break; }
            }
            if (position.HasValue) foreach (var room in snapshot.rooms)
                if (room.boundsWorld.Contains(position.Value)) { snapshot.currentRoomId = room.id; break; }
            if (string.IsNullOrEmpty(snapshot.currentRoomId) && snapshot.rooms.Count > 0) snapshot.currentRoomId = snapshot.rooms[0].id;
        }

        private static void AddArtwork(GridArtLevelSnapshot snapshot, string id, Rect target, PlanningArtworkPreview artwork, float size,
            GridArtReferenceLayer layer = GridArtReferenceLayer.Object)
        {
            if (artwork.Hidden || artwork.Texture == null || artwork.Cells.x <= 0 || artwork.Cells.y <= 0) return;
            if (artwork.EditorOverlay) layer = GridArtReferenceLayer.EditorOverlay;
            int turns = Mathf.RoundToInt(artwork.RotationDegrees / 90f);
            Vector2 native = artwork.Cells * size, rotated = GridArtRotation.Size(native, turns);
            if (!artwork.Tile)
            {
                Add(new Rect(new Vector2(target.center.x - rotated.x * .5f, target.yMin), rotated), artwork.UV); return;
            }
            for (float y = 0; y < target.height - .0001f; y += rotated.y)
                for (float x = 0; x < target.width - .0001f; x += rotated.x)
                {
                    float w = Mathf.Min(rotated.x, target.width - x), h = Mathf.Min(rotated.y, target.height - y);
                    Rect uv = artwork.UV;
                    uv.width *= GridArtRotation.Normalize(turns) % 2 == 0 ? w / rotated.x : h / rotated.y;
                    uv.height *= GridArtRotation.Normalize(turns) % 2 == 0 ? h / rotated.y : w / rotated.x;
                    Add(new Rect(target.x + x, target.y + y, w, h), uv);
                }
            void Add(Rect rect, Rect uv)
            {
                if (artwork.FlipX) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                snapshot.AddReference(id, new GridArtQuad(rect, uv, artwork.Texture, rotation: turns), Color.white, layer);
            }
        }

        private static Dictionary<PlanningBox, Dictionary<Vector2Int, PlanningArtworkPreview>> BuildVineSlices(List<Source> sources)
        {
            var result = new Dictionary<PlanningBox, Dictionary<Vector2Int, PlanningArtworkPreview>>();
            var scopes = new Dictionary<IList<PlanningBox>, Dictionary<Vector2Int, Source>>();
            foreach (var source in sources)
            {
                var box = source.box; var feature = source.entry?.Prefab != null ? source.entry.Prefab.GetComponent<ClimbableVineFeature>() : null;
                if (feature == null || feature is LadderSonFeature || feature.GrowthSegmentPrefab == null ||
                    feature.GrowthSegmentPrefab.GetComponent<LadderSonFeature>() == null ||
                    (box.isMerged || box.singleInstance) && (box.width != 1 || box.height != 1)) continue;
                if (!scopes.TryGetValue(source.siblings, out var cells)) scopes.Add(source.siblings, cells = new());
                foreach (var cell in PlanningShapeUtility.Cells(box)) cells.TryAdd(cell, source);
            }
            foreach (var cells in scopes.Values) foreach (var pair in cells)
            {
                if (cells.TryGetValue(pair.Key + Vector2Int.up, out var lower) && lower.entry.Prefab == pair.Value.entry.Prefab) continue;
                int rows = 1;
                while (cells.TryGetValue(pair.Key + Vector2Int.down * rows, out var upper) && upper.entry.Prefab == pair.Value.entry.Prefab) rows++;
                var feature = pair.Value.entry.Prefab.GetComponent<ClimbableVineFeature>();
                if (!ObjectArtworkPlanningPreview.TryGetVine(feature, new Vector2(1, rows), Values(pair.Value),
                    rows > 1 ? 1f : feature.AuthoredArtworkGrowthProgress, out var root)) continue;
                for (int row = 0; row < rows; row++)
                {
                    var cell = pair.Key + Vector2Int.down * row; var box = cells[cell].box;
                    if (!result.TryGetValue(box, out var slices)) result.Add(box, slices = new());
                    slices[cell] = ObjectArtworkPlanningPreview.VineSlice(root, row, rows);
                }
            }
            return result;
        }

        private static bool AddPlatform(GridArtLevelSnapshot snapshot, Source source, RectInt rect, ObjectArtworkLibrary library)
        {
            var prefab = source.entry.Prefab;
            var platform = prefab.GetComponent<MovingPlatformFeature>();
            var oneWay = prefab.GetComponent<Project.Mechanisms.OneWayPlatform>();
            if (platform == null && oneWay == null || library == null) return false;
            var kind = oneWay != null ? ObjectArtworkPlanningPreview.OneWayKind(oneWay, Values(source)) :
                platform.IsCrankControlled ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform;
            Sprite sprite = library.FirstFrame(kind); if (sprite == null) return false;
            bool separate = !source.box.isMerged && !source.box.singleInstance;
            bool vertical = !separate && (platform != null ? PlanningPlatformShapeUtility.IsVertical(platform, Values(source),
                new Vector2(source.box.width, source.box.height)) : ObjectArtworkLibrary.PlatformVisualIsVertical(new Vector2(source.box.width, source.box.height)));
            int length = vertical ? rect.height : rect.width, tracks = vertical ? rect.width : rect.height;
            int stride = separate ? 1 : Mathf.Max(1, Mathf.RoundToInt(sprite.textureRect.width / GridArtworkMetrics.PixelsPerCell));
            float height = sprite.textureRect.height / GridArtworkMetrics.PixelsPerCell;
            float thickness = PlatformArtworkLayout.VisibleThicknessCells(kind, sprite, library);
            var collider = prefab.GetComponent<BoxCollider>();
            float topInset = collider != null ? (.5f - (collider.center.y + collider.size.y * .5f) * prefab.transform.localScale.y) * (separate ? 1 : rect.height) : 0;
            bool explicitVertical = platform != null && PlanningPlatformShapeUtility.Orientation(platform, Values(source)) == MovingPlatformBodyOrientation.Vertical;
            float leftInset = collider == null ? 0 : explicitVertical ?
                (.5f - (collider.center.y + collider.size.y * .5f) * prefab.transform.localScale.y) * rect.width :
                (.5f + (collider.center.x - collider.size.x * .5f) * prefab.transform.localScale.x) * rect.width;
            for (int track = 0; track < tracks; track++) for (int start = 0; start < length; start += stride)
            {
                int count = Mathf.Min(stride, length - start);
                Rect target = WorldRect(vertical ? new RectInt(rect.x + track, rect.y + start, 1, count) : new RectInt(rect.x + start, rect.y + track, count, 1));
                Rect bounds = vertical ? new Rect(target.xMin + leftInset + thickness - height, target.yMin, height, count) :
                    new Rect(target.xMin, target.yMax - topInset - thickness, count, height);
                snapshot.AddReference(source.box.id, new GridArtQuad(bounds, PlatformArtworkLayout.SliceUV(sprite, count), sprite.texture,
                    rotation: vertical ? 1 : 0), Color.white, GridArtReferenceLayer.Object);
            }
            return true;
        }
    }
}
