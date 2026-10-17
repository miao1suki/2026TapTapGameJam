using System;
using System.Collections.Generic;
using System.IO;
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
        public PlanningArtworkPreview(Sprite sprite, bool tile = false, float rotationDegrees = 0, bool flipX = false)
        {
            Sprite = sprite; Texture = sprite != null ? sprite.texture : null;
            Rect rect = sprite != null ? sprite.textureRect : default;
            UV = Texture != null ? new Rect(rect.x / Texture.width, rect.y / Texture.height,
                rect.width / Texture.width, rect.height / Texture.height) : default;
            Cells = GridArtworkMetrics.SpriteCells(sprite, GridArtworkMetrics.SourcePixelScale(sprite));
            RotationDegrees = rotationDegrees; FlipX = flipX; Tile = tile; Hidden = sprite == null;
        }
        internal PlanningArtworkPreview(PlanningArtworkPreview source, Rect uv, Vector2 cells)
        {
            Sprite = source.Sprite; Texture = source.Texture; UV = uv; Cells = cells;
            RotationDegrees = source.RotationDegrees; FlipX = source.FlipX; Tile = source.Tile; Hidden = source.Hidden;
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
            if (overrides == null && defaults.TryGetValue(cacheKey, out preview)) return true;
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
            else if (prefab.GetComponent<ClimbableVineFeature>() != null)
            {
                int rows = Mathf.Max(1, Mathf.RoundToInt(footprintCells.y));
                int frame = rows > 1 ? library.vineGrownFrame : library.vineInitialFrame;
                if (library.vineGrowth == null || library.vineGrowth.Length == 0) return false;
                Sprite source = library.vineGrowth[Mathf.Clamp(frame, 0, library.vineGrowth.Length - 1)];
                var properties = new SerializedObject(prefab.GetComponent<ClimbableVineFeature>());
                float width = properties.FindProperty("vineWidth")?.floatValue ?? 1.2f;
                preview = SampleVine(source, width, rows);
            }
            else if (prefab.GetComponent<RespawnAnchorFeature>() != null) preview = new PlanningArtworkPreview(library.respawnLocked);
            else if (prefab.GetComponent<BouncePlantFeature>() != null)
            {
                if (library.plantBounce == null || library.plantBounce.Length == 0) return false;
                preview = new PlanningArtworkPreview(library.plantBounce[Mathf.Clamp(library.plantIdleFrame, 0, library.plantBounce.Length - 1)], true);
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
                    preview = new PlanningArtworkPreview(sprite, tile, rotation, flip);
                }
            }
            if (overrides == null) defaults[cacheKey] = preview;
            return true;
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
            else if (owner.GetComponent<OneWayPlatform>() != null) { kind = MechanismArtworkKind.OneWayPlatform; tile = true; }
            else if (owner.TryGetComponent<MechanismArtworkPresentation>(out var presentation)) { kind = presentation.Kind; tile = kind == MechanismArtworkKind.Obsidian; }
            else return false;
            return true;
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
