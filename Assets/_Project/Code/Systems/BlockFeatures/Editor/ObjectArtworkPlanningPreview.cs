using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Project.ColorBlocks;
using Project.ContactCubes;
using Project.LevelEditor;
using Project.Mechanisms;
using Project.ObjectArtwork;
using Project.Subtitles;
using Project.WorldItems;
using UnityEditor;
using UnityEngine;

namespace Project.BlockFeatures.Editor
{
    public readonly struct PlanningArtworkPreview
    {
        public readonly Sprite Sprite;
        public readonly Texture2D Texture;
        public readonly Rect UV;
        public readonly Vector2 Cells;
        public readonly float RotationDegrees;
        public readonly bool FlipX, Tile;
        public readonly bool Hidden;
        public readonly bool EditorOverlay;
        public PlanningArtworkPreview(Sprite sprite, bool tile = false, float rotationDegrees = 0, bool flipX = false)
        {
            Sprite = sprite; Texture = sprite != null ? sprite.texture : null;
            Rect rect = sprite != null ? sprite.textureRect : default;
            UV = Texture != null ? new Rect(rect.x / Texture.width, rect.y / Texture.height,
                rect.width / Texture.width, rect.height / Texture.height) : default;
            Cells = GridArtworkMetrics.SpriteCells(sprite, GridArtworkMetrics.SourcePixelScale(sprite));
            RotationDegrees = rotationDegrees; FlipX = flipX; Tile = tile; Hidden = sprite == null; EditorOverlay = false;
        }
        internal PlanningArtworkPreview(Texture2D texture, Vector2 footprint)
        {
            Sprite = null; Texture = texture; UV = new Rect(0, 0, 1, 1);
            float fit = texture != null ? Mathf.Min(footprint.x / texture.width, footprint.y / texture.height) : 0f;
            Cells = texture != null ? new Vector2(texture.width, texture.height) * fit : Vector2.zero;
            RotationDegrees = 0; FlipX = Tile = false; Hidden = texture == null; EditorOverlay = true;
        }
        internal PlanningArtworkPreview(PlanningArtworkPreview source, Rect uv, Vector2 cells)
        {
            Sprite = source.Sprite; Texture = source.Texture; UV = uv; Cells = cells;
            RotationDegrees = source.RotationDegrees; FlipX = source.FlipX; Tile = source.Tile; Hidden = source.Hidden; EditorOverlay = source.EditorOverlay;
        }
    }

    /// <summary>Read-only 2D artwork shared by palette and planning canvas. Never renders a prefab camera or creates scene objects.</summary>
    [InitializeOnLoad]
    public static class ObjectArtworkPlanningPreview
    {
        private static ObjectArtworkLibrary library;
        private static readonly Dictionary<(int, Vector2), PlanningArtworkPreview> defaults = new();
        private static readonly Dictionary<(int, float, int), PlanningArtworkPreview> vines = new();
        private static readonly Dictionary<string, Texture2D> readableSources = new();
        private static readonly List<UnityEngine.Object> temporary = new();
        static ObjectArtworkPlanningPreview()
        {
            EditorApplication.projectChanged += ClearCache;
            AssemblyReloadEvents.beforeAssemblyReload += ClearCache;
        }
        public static void ClearCache()
        {
            defaults.Clear(); vines.Clear(); readableSources.Clear(); library = null;
            foreach (var item in temporary) if (item != null) UnityEngine.Object.DestroyImmediate(item);
            temporary.Clear();
        }
        public static bool TryGet(GameObject prefab, out PlanningArtworkPreview preview) =>
            TryGet(prefab, Vector2.one, null, out preview);
        public static bool TryGet(GameObject prefab, IReadOnlyList<LevelEditorComponentValueOverride> overrides,
            out PlanningArtworkPreview preview) => TryGet(prefab, Vector2.one, overrides, out preview);
        public static bool TryGet(GameObject prefab, Vector2 footprintCells,
            IReadOnlyList<LevelEditorComponentValueOverride> overrides, out PlanningArtworkPreview preview)
        {
            preview = default;
            if (prefab == null) return false;
            if (overrides != null && overrides.Count == 0) overrides = null;
            var cacheKey = (prefab.GetInstanceID(), footprintCells);
            // Palette assets have stable defaults between project-change events;
            // Scene instances do not (spike rotation, ladder side, door index,
            // etc. can be edited independently). Their caller caches the mesh,
            // so caching instance parameters here would only preserve stale art.
            bool cacheDefault = overrides == null && EditorUtility.IsPersistent(prefab);
            if (cacheDefault && defaults.TryGetValue(cacheKey, out preview)) return true;
            if (prefab.GetComponent<ContactTimelineTrigger>() != null)
            {
                preview = new PlanningArtworkPreview(ContactTimelineEditorPreview.Texture, footprintCells);
                if (cacheDefault) defaults[cacheKey] = preview;
                return true;
            }
            if (library == null) library = ObjectArtworkLibrary.Load();
            if (library == null) return false;

            if (prefab.TryGetComponent<SubtitleTrigger>(out var sign))
            {
                bool show = sign.ShowSign;
                Sprite sprite = sign.SignSprite;
                if (overrides != null) foreach (var value in overrides)
                {
                    if (value == null || !string.IsNullOrEmpty(value.ComponentPath) ||
                        value.ComponentTypeName != typeof(SubtitleTrigger).FullName &&
                        value.ComponentTypeName != typeof(SubtitleTrigger).AssemblyQualifiedName) continue;
                    if (value.PropertyPath == "showSign") show = value.Value == "True" || value.Value == "true" || value.Value == "1";
                    if (value.PropertyPath != "signSprite") continue;
                    if (value.Value == "reference:null") sprite = null;
                    else if (GlobalObjectId.TryParse(value.Value, out var id)) sprite = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) as Sprite;
                    else if (value.ObjectReferenceValue is Sprite reference && EditorUtility.IsPersistent(reference)) sprite = reference;
                }
                if (sprite == null && library.signboards != null && library.signboards.Length > 0) sprite = library.signboards[0];
                preview = new PlanningArtworkPreview(show ? sprite : null);
            }
            else if (prefab.GetComponent<WaterVisualFeature>() != null)
            {
                if (!TryGetWaterTile(0, out preview)) return false;
            }
            else if (prefab.TryGetComponent<ClimbableVineFeature>(out var vine))
            {
                if (!TryGetVine(vine, footprintCells, overrides, vine.AuthoredArtworkGrowthProgress, out preview)) return false;
            }
            else if (prefab.GetComponent<RespawnAnchorFeature>() != null) preview = new PlanningArtworkPreview(library.respawnLocked);
            else if (prefab.GetComponent<BouncePlantFeature>() != null)
            {
                if (library.plantBounce == null || library.plantBounce.Length == 0) return false;
                preview = new PlanningArtworkPreview(library.plantBounce[Mathf.Clamp(library.plantIdleFrame, 0, library.plantBounce.Length - 1)], true);
            }
            else if (prefab.TryGetComponent<OneWayPlatform>(out var oneWay))
            {
                preview = new PlanningArtworkPreview(library.FirstFrame(OneWayKind(oneWay, overrides)), true);
            }
            else
            {
                if (!TryGetKind(prefab, out var kind, out float rotation, out bool flip, out bool tile))
                {
                    // Custom authored sprite prefabs use their own 2D art, not a thumbnail of a 3D model.
                    var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                    if (renderer == null || renderer.sprite == null) return false;
                    preview = new PlanningArtworkPreview(renderer.sprite, renderer.drawMode == SpriteDrawMode.Tiled,
                        renderer.transform.localEulerAngles.z, renderer.flipX);
                }
                else
                {
                    Sprite sprite = library.FirstFrame(kind);
                    if (sprite == null) return false;
                    if (prefab.TryGetComponent<DoorButton>(out var button))
                        rotation = ButtonRotation(button.Facing, typeof(DoorButton), overrides);
                    else if (prefab.TryGetComponent<ButtonLockFeature>(out var redButton))
                        rotation = ButtonRotation(redButton.Facing, typeof(ButtonLockFeature), overrides);
                    preview = new PlanningArtworkPreview(sprite, tile, rotation, flip);
                }
            }
            if (cacheDefault) defaults[cacheKey] = preview;
            return true;
        }

        public static bool TryGetVine(ClimbableVineFeature feature, Vector2 footprintCells,
            out PlanningArtworkPreview preview) => TryGetVine(feature, footprintCells, null,
                feature != null ? feature.AuthoredArtworkGrowthProgress : 0f, out preview);

        public static bool TryGetVine(ClimbableVineFeature feature, Vector2 footprintCells,
            IReadOnlyList<LevelEditorComponentValueOverride> overrides, float growthProgress,
            out PlanningArtworkPreview preview)
        {
            preview = default;
            if (feature == null) return false;
            feature = feature.ArtworkRoot;
            var sourceLibrary = feature.SequenceArtwork != null ? feature.SequenceArtwork : library;
            if (sourceLibrary == null) sourceLibrary = library = ObjectArtworkLibrary.Load();
            float width = feature.ArtworkWidthCells;
            if (overrides != null) foreach (var value in overrides)
            {
                if (value == null || !string.IsNullOrEmpty(value.ComponentPath) ||
                    value.ComponentTypeName != typeof(ClimbableVineFeature).FullName &&
                    value.ComponentTypeName != typeof(ClimbableVineFeature).AssemblyQualifiedName &&
                    value.ComponentTypeName != typeof(LadderSonFeature).FullName &&
                    value.ComponentTypeName != typeof(LadderSonFeature).AssemblyQualifiedName) continue;
                if (value.PropertyPath == "vineWidth" && float.TryParse(value.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out float authoredWidth)) width = Mathf.Max(.1f, authoredWidth);
                if (value.PropertyPath == "sequenceArtwork")
                {
                    if (GlobalObjectId.TryParse(value.Value, out var id) &&
                        GlobalObjectId.GlobalObjectIdentifierToObjectSlow(id) is ObjectArtworkLibrary custom) sourceLibrary = custom;
                    else if (value.ObjectReferenceValue is ObjectArtworkLibrary reference && EditorUtility.IsPersistent(reference)) sourceLibrary = reference;
                }
            }
            if (sourceLibrary?.vineGrowth == null || sourceLibrary.vineGrowth.Length == 0) return false;
            int index = sourceLibrary.VineFrame(growthProgress, 0f, false);
            Sprite source = sourceLibrary.vineGrowth[index];
            if (source == null) return false;
            preview = SampleVine(source, width, Mathf.Max(1, Mathf.RoundToInt(footprintCells.y)));
            return preview.Texture != null;
        }

        // All cells of a prebuilt column sample one root image. They are not
        // independent initial-frame thumbnails with a repeated dangling tip.
        public static PlanningArtworkPreview VineSlice(PlanningArtworkPreview root, int rowFromBottom, int rowCount)
        {
            rowCount = Mathf.Max(1, rowCount);
            Rect uv = root.UV;
            uv.y += uv.height * Mathf.Clamp(rowFromBottom, 0, rowCount - 1) / rowCount;
            uv.height /= rowCount;
            return new PlanningArtworkPreview(root, uv, new Vector2(root.Cells.x, root.Cells.y / rowCount));
        }

        public static bool TryGetWaterTile(int kind, out PlanningArtworkPreview preview)
        {
            var water = WaterPresentationSettings.Load();
            if (water?.tiles == null || water.tiles.Length < 3 || water.tiles[Mathf.Clamp(kind, 0, 2)] == null)
            { preview = default; return false; }
            var value = new PlanningArtworkPreview(water.tiles[Mathf.Clamp(kind, 0, 2)], true);
            // The same transparent bottom row is excluded by GameplayWaterPresentation.
            var uv = value.UV;
            if (value.Sprite.textureRect.height == 17) { uv.y += 1f / value.Texture.height; uv.height -= 1f / value.Texture.height; }
            preview = new PlanningArtworkPreview(value, uv, Vector2.one);
            return true;
        }

        private static bool TryGetKind(GameObject owner, out MechanismArtworkKind kind, out float rotation, out bool flip, out bool tile)
        {
            rotation = 0; flip = false; tile = false; kind = default;
            if (owner.TryGetComponent<DoorController>(out var door)) { kind = MechanismArtworkKind.Door; rotation = door.IsHorizontal ? 90 : 0; }
            else if (owner.TryGetComponent<DoorButton>(out var button)) kind = ObjectArtworkLibrary.DoorButtonKind(button.BindingIndex);
            else if (owner.GetComponent<CrankPlatformFeature>() != null) kind = MechanismArtworkKind.Crank;
            else if (owner.TryGetComponent<MovingPlatformFeature>(out var platform)) { kind = platform.IsCrankControlled ? MechanismArtworkKind.CrankPlatform : MechanismArtworkKind.MovingPlatform; tile = true; }
            else if (owner.TryGetComponent<ContactCube>(out var cube)) { kind = cube.Color == CubeColor.Red ? MechanismArtworkKind.RedCube : cube.Color == CubeColor.Green ? MechanismArtworkKind.GreenCube : MechanismArtworkKind.BlueCube; tile = true; }
            else if (owner.TryGetComponent<ColorKeyPickup>(out var key)) kind = key.ColorTypeId == "red" ? MechanismArtworkKind.RedKey : key.ColorTypeId == "green" ? MechanismArtworkKind.GreenKey : MechanismArtworkKind.BlueKey;
            else if (owner.GetComponent<ButtonLockFeature>() != null) kind = MechanismArtworkKind.RedButton;
            else if (owner.GetComponent<MechanismBaseFeature>() != null) kind = MechanismArtworkKind.MechanismBase;
            else if (owner.GetComponent<PlantObstacleFeature>() != null) { kind = MechanismArtworkKind.PlantObstacle; tile = true; }
            else if (owner.TryGetComponent<NeutralLadder>(out var ladder)) { kind = ladder.SideFacing ? MechanismArtworkKind.LadderSide : MechanismArtworkKind.LadderFront; flip = ladder.SideFacing && ladder.WallSide == LadderWallSide.Left; tile = true; }
            else if (owner.GetComponent<NeutralSpike>() != null) { kind = MechanismArtworkKind.Spike; tile = true; var collider = owner.GetComponentInChildren<MeshCollider>(true); if (collider != null) rotation = collider.transform.localEulerAngles.z; }
            else if (owner.GetComponent<LavaHazardFeature>() != null) { kind = MechanismArtworkKind.Lava; tile = true; }
            else if (owner.TryGetComponent<OneWayPlatform>(out var oneWay)) { kind = oneWay.ArtworkKind; tile = true; }
            else if (owner.TryGetComponent<MechanismArtworkPresentation>(out var presentation)) { kind = presentation.Kind; tile = kind == MechanismArtworkKind.Obsidian; }
            else return false;
            return true;
        }

        public static MechanismArtworkKind OneWayKind(OneWayPlatform platform, IReadOnlyList<LevelEditorComponentValueOverride> overrides)
        {
            int color = platform != null ? (int)platform.AppearanceColor : 0;
            if (overrides != null) foreach (var value in overrides)
                if (value != null && string.IsNullOrEmpty(value.ComponentPath) &&
                    (value.ComponentTypeName == typeof(OneWayPlatform).FullName || value.ComponentTypeName == typeof(OneWayPlatform).AssemblyQualifiedName) &&
                    value.PropertyPath == "appearanceColor" && int.TryParse(value.Value, out int selected)) color = selected;
            return ObjectArtworkLibrary.OneWayPlatformKind(color);
        }

        private static float ButtonRotation(ButtonFacing facing, Type component,
            IReadOnlyList<LevelEditorComponentValueOverride> overrides)
        {
            if (overrides != null) foreach (var value in overrides)
                if (value != null && string.IsNullOrEmpty(value.ComponentPath) &&
                    (value.ComponentTypeName == component.FullName || value.ComponentTypeName == component.AssemblyQualifiedName) &&
                    value.PropertyPath == "facing" && int.TryParse(value.Value, out int selected))
                    facing = (ButtonFacing)selected;
            return ButtonPressureGeometry.RotationDegrees(facing);
        }

        private static PlanningArtworkPreview SampleVine(Sprite source, float width, int rows)
        {
            if (source == null) return default;
            int columns = Mathf.CeilToInt(width * GridArtworkMetrics.PixelsPerCell);
            var key = (source.GetInstanceID(), width, rows);
            if (vines.TryGetValue(key, out var cached)) return cached;
            string path = AssetDatabase.GetAssetPath(source.texture);
            if (!readableSources.TryGetValue(path, out var readable))
            {
                readable = new Texture2D(2, 2, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                if (!readable.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(readable); return default; }
                readableSources[path] = readable; temporary.Add(readable);
            }
            int height = rows * (int)GridArtworkMetrics.PixelsPerCell;
            var texture = new Texture2D(columns, height, TextureFormat.RGBA32, false)
            { name = "Vine planning pixels", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            Rect rect = source.textureRect;
            float coveredWidth = (float)columns / GridArtworkMetrics.PixelsPerCell;
            float padding = (coveredWidth - width) * .5f;
            var colors = new Color32[columns * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < columns; x++)
            {
                float u = Mathf.Clamp01(((x + .5f) / GridArtworkMetrics.PixelsPerCell - padding) / width);
                int sx = Mathf.Clamp(Mathf.FloorToInt(rect.x + rect.width * u), (int)rect.xMin, (int)rect.xMax - 1);
                int sy = Mathf.Clamp(Mathf.FloorToInt(rect.y + rect.height * (y + .5f) / height), (int)rect.yMin, (int)rect.yMax - 1);
                colors[y * columns + x] = readable.GetPixel(sx, sy);
            }
            texture.SetPixels32(colors); texture.Apply(); temporary.Add(texture);
            var sprite = Sprite.Create(texture, new Rect(0, 0, columns, height), new Vector2(.5f, 0), GridArtworkMetrics.PixelsPerCell);
            sprite.name = source.name + " 16px planning"; sprite.hideFlags = HideFlags.HideAndDontSave; temporary.Add(sprite);
            cached = new PlanningArtworkPreview(new PlanningArtworkPreview(sprite),
                new Rect(padding / coveredWidth, 0, width / coveredWidth, 1), new Vector2(width, rows));
            vines[key] = cached;
            return cached;
        }
    }
}
