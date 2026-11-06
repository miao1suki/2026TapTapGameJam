using System.Collections.Generic;
using Project.Planar;
using InteractiveWater;
using Project.ColorBlocks;
using Project.WaterBubbles;
using Project.LevelEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.BlockFeatures
{
    /// <summary>Visual-only, pooled-material bridge. Gameplay volume and force ownership stay on the feature.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayWaterPresentation : MonoBehaviour
    {
        private static readonly HashSet<GameplayWaterPresentation> active = new HashSet<GameplayWaterPresentation>();
        private static int revision;
        private static int wallRevision = -1;
        private static int budgetFrame = -1;
        private static double initialBuildSeconds;
        private static int lookupRevision = -1;
        private static readonly Dictionary<(int scene, float size, int x, int y), List<GameplayWaterPresentation>> lookup = new();
        private static readonly List<(int scene, float size)> grids = new();
        private readonly HashSet<GameplayWaterPresentation> neighbors = new();
        private int builtRevision = -1;
        private WaterVisualFeature owner;
        private WaterPresentationSettings settings;
        private readonly List<GameObject> parts = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Bounds lastBounds;
        private Collider2D volumeCollider;
        private LevelEditorMergedShape shape;
        private MeshFilter neutralFilter;
        private Mesh originalNeutralMesh;
        private float cellSize = 1f;
        private Vector4 flowSettings;
        private int topologySignature;
        private bool geometryDirty = true;
        public int MeshBuildCount { get; private set; }
        public int SurfaceCellCount { get; private set; }
        public int BodyCellCount { get; private set; }
        public int InteriorCellCount { get; private set; }
        public Bounds Volume => shape != null ? shape.WorldBounds : volumeCollider != null
            ? volumeCollider.bounds : new Bounds(transform.position, Vector3.one * CellSize);
        private float CellSize => cellSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        { active.Clear(); lookup.Clear(); grids.Clear(); lookupRevision = -1; revision = 0;
            wallRevision = -1; budgetFrame = -1; initialBuildSeconds = 0; }

        public void Initialize(WaterVisualFeature feature, WaterPresentationSettings configuration)
        {
            owner = feature;
            settings = configuration;
            volumeCollider = feature.GetComponent<Collider2D>();
            shape = feature.GetComponent<LevelEditorMergedShape>();
            neutralFilter = feature.GetComponent<MeshFilter>();
            if (neutralFilter != null) originalNeutralMesh = neutralFilter.sharedMesh;
            cellSize = GridCellSizeUtility.Resolve(feature);
            flowSettings = ReadFlowSettings();
            active.Add(this);
            revision++;
            lastBounds = Volume;
        }

        private void OnEnable()
        {
            if (owner != null && active.Add(this)) { revision++; builtRevision = -1; geometryDirty = true; }
        }

        private Vector4 ReadFlowSettings()
        {
            Vector2 direction = owner is DirectionalCurrentFeature current ? current.VisualFlowDirection : Vector2.up;
            return new Vector4(owner.ShowFlowBubbles ? owner.FlowBubbleDensity : -1f,
                owner.FlowBubbleSpeed, direction.x, direction.y);
        }

        private void LateUpdate()
        {
            if (owner == null || settings == null) return;
            Bounds bounds = Volume;
            if (bounds != lastBounds) { lastBounds = bounds; geometryDirty = true; revision++; }
            Vector4 nextSettings = ReadFlowSettings();
            if (nextSettings != flowSettings) { flowSettings = nextSettings; geometryDirty = true; revision++; }
            if (wallRevision != LevelEditorPlacedBlock.SolidRevision) { wallRevision = LevelEditorPlacedBlock.SolidRevision; revision++; }
            if (builtRevision == revision || !ColorPresentationVisibility.IsNearCamera(bounds, CellSize)) return;
            int signature = GetTopologySignature(bounds);
            if (geometryDirty || MeshBuildCount == 0 || signature != topologySignature)
            {
                bool prewarming = MeshBuildCount == 0 && ColorAppearanceManager.Existing != null &&
                    !ColorAppearanceManager.Existing.ShowsUnlockedAppearance("blue");
                if (budgetFrame != Time.frameCount) { budgetFrame = Time.frameCount; initialBuildSeconds = 0; }
                // Finish during the neutral fade, before the real appearance handoff.
                // Restored progress, entry into view and live topology changes stay synchronous.
                if (prewarming && initialBuildSeconds >= .006) return;
                double began = Time.realtimeSinceStartupAsDouble;
                Rebuild();
                if (prewarming) initialBuildSeconds += Time.realtimeSinceStartupAsDouble - began;
                topologySignature = signature;
                geometryDirty = false;
            }
            builtRevision = revision;
        }

        private int GetTopologySignature(Bounds bounds)
        {
            // A distant source changing must not recreate an unchanged visible pool.
            bounds.Expand(CellSize * 4.01f);
            int signature = LevelEditorPlacedBlock.SolidRevision;
            EnsureLookup();
            neighbors.Clear();
            foreach (var grid in grids)
            {
                if (grid.scene != gameObject.scene.handle) continue;
                int x0 = Mathf.FloorToInt(bounds.min.x / grid.size), x1 = Mathf.FloorToInt(bounds.max.x / grid.size);
                int y0 = Mathf.FloorToInt(bounds.min.y / grid.size), y1 = Mathf.FloorToInt(bounds.max.y / grid.size);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    if (lookup.TryGetValue((grid.scene, grid.size, x, y), out var candidates))
                        foreach (var other in candidates)
                            if (neighbors.Add(other)) signature = unchecked(signature + (other.GetInstanceID() * 397 ^ other.Volume.GetHashCode()));
            }
            return signature;
        }

        private static void EnsureLookup()
        {
            if (lookupRevision == revision) return;
            lookup.Clear(); grids.Clear();
            foreach (var water in active)
            {
                if (water == null || water.owner == null || !water.owner.isActiveAndEnabled) continue;
                int scene = water.gameObject.scene.handle; float size = water.CellSize;
                if (!grids.Contains((scene, size))) grids.Add((scene, size));
                Bounds b = water.Volume;
                int x0 = Mathf.FloorToInt(b.min.x / size), x1 = Mathf.CeilToInt(b.max.x / size);
                int y0 = Mathf.FloorToInt(b.min.y / size), y1 = Mathf.CeilToInt(b.max.y / size);
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++)
                {
                    var key = (scene, size, x, y);
                    if (!lookup.TryGetValue(key, out var bucket)) lookup.Add(key, bucket = new List<GameplayWaterPresentation>(1));
                    bucket.Add(water);
                }
            }
            lookupRevision = revision;
        }

        private void OnDisable()
        {
            if (active.Remove(this)) revision++;
            geometryDirty = true;
            ClearParts();
        }

        private void ClearParts()
        {
            if (neutralFilter != null) neutralFilter.sharedMesh = originalNeutralMesh;
            foreach (var part in parts)
                if (part != null) { part.SetActive(false); Destroy(part); }
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            parts.Clear(); meshes.Clear();
        }

        private bool ContainsWater(Vector3 world) => ContainsWater(gameObject.scene, world);

        public static bool ContainsWater(UnityEngine.SceneManagement.Scene scene, Vector3 world) =>
            ContainsWater(scene, world, false, out _);

        // Click-time steam placement also needs the exact containing rectangle's top,
        // not the merged owner's bounding-box top. Existing mesh adjacency keeps its
        // inexpensive boolean path and does not traverse volume lists per cell.
        public static bool ContainsWater(UnityEngine.SceneManagement.Scene scene, Vector3 world, out float topY) =>
            ContainsWater(scene, world, true, out topY);

        private static bool ContainsWater(UnityEngine.SceneManagement.Scene scene, Vector3 world,
            bool resolveTop, out float topY)
        {
            topY = float.NegativeInfinity;
            // Click-time gameplay queries use live volumes, including motion before LateUpdate
            // invalidates the presentation index. They are rare and must not depend on a cache.
            if (resolveTop) return ResolveLiveTop(scene, world, out topY);
            EnsureLookup();
            foreach (var grid in grids)
            {
                if (grid.scene != scene.handle || !lookup.TryGetValue((grid.scene, grid.size,
                    Mathf.FloorToInt(world.x / grid.size), Mathf.FloorToInt(world.y / grid.size)), out var candidates)) continue;
                foreach (var water in candidates)
                {
                    if (water == null || water.owner == null || !water.isActiveAndEnabled ||
                        !water.owner.isActiveAndEnabled || water.gameObject.scene != scene) continue;
                    Bounds b = water.Volume;
                    float epsilon = water.CellSize * .001f;
                    if (world.x >= b.min.x + epsilon && world.x <= b.max.x - epsilon &&
                        world.y >= b.min.y + epsilon && world.y <= b.max.y - epsilon &&
                        (water.shape == null || water.shape.ContainsWorldPoint(world))) return true;
                }
            }
            return false;
        }

        private static bool ResolveLiveTop(UnityEngine.SceneManagement.Scene scene, Vector3 world, out float topY)
        {
            topY = float.NegativeInfinity;
            bool found = false;
            foreach (var water in active)
            {
                if (water == null || water.owner == null || !water.isActiveAndEnabled ||
                    !water.owner.isActiveAndEnabled || water.gameObject.scene != scene) continue;
                Bounds b = water.Volume;
                float epsilon = water.CellSize * .001f;
                if (world.x < b.min.x + epsilon || world.x > b.max.x - epsilon ||
                    world.y < b.min.y + epsilon || world.y > b.max.y - epsilon) continue;
                if (water.shape == null)
                {
                    if (water.volumeCollider != null && !water.volumeCollider.enabled) continue;
                    found = true;
                    topY = Mathf.Max(topY, b.max.y);
                }
                else foreach (BoxCollider2D volume in water.shape.Volumes)
                {
                    if (volume == null || !volume.enabled || !PlanarPhysics.ContainsXY(volume.bounds, world)) continue;
                    found = true;
                    topY = Mathf.Max(topY, volume.bounds.max.y);
                }
            }
            return found;
        }

        // Overlapping water/current/buoyancy volumes keep separate gameplay sources, but
        // one deterministic renderer owns each surface cell. Re-evaluated only on rebuild.
        private bool OwnsSurfaceCell(Vector3 world)
        {
            int id = GetInstanceID();
            EnsureLookup();
            foreach (var grid in grids)
            {
                if (grid.scene != gameObject.scene.handle || !lookup.TryGetValue((grid.scene, grid.size,
                    Mathf.FloorToInt(world.x / grid.size), Mathf.FloorToInt(world.y / grid.size)), out var candidates)) continue;
                foreach (var other in candidates)
                {
                    if (other == null || other == this || other.owner == null ||
                        other.gameObject.scene != gameObject.scene || other.GetInstanceID() >= id) continue;
                    Bounds b = other.Volume;
                    float epsilon = CellSize * .001f;
                    if (world.x >= b.min.x + epsilon && world.x <= b.max.x - epsilon &&
                        world.y >= b.min.y + epsilon && world.y <= b.max.y - epsilon &&
                        (other.shape == null || other.shape.ContainsWorldPoint(world))) return false;
                }
            }
            return true;
        }

        private void Rebuild()
        {
            MeshBuildCount++;
            ClearParts();
            SurfaceCellCount = BodyCellCount = InteriorCellCount = 0;
            Bounds b = Volume;
            float cell = CellSize;
            int columns = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cell - .001f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(b.size.y / cell - .001f));
            var vertices = new List<Vector3>[3];
            var uvs = new List<Vector2>[3];
            var triangles = new List<int>[3];
            var frontVertices = new List<Vector3>(); var frontUvs = new List<Vector2>(); var frontTriangles = new List<int>();
            var visibleCells = new List<RectInt>();
            for (int i = 0; i < 3; i++) { vertices[i] = new List<Vector3>(); uvs[i] = new List<Vector2>(); triangles[i] = new List<int>(); }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    Vector3 min = new Vector3(b.min.x + x * cell, b.min.y + y * cell, b.max.z + cell * .02f);
                    Vector3 max = new Vector3(Mathf.Min(b.max.x, min.x + cell), Mathf.Min(b.max.y, min.y + cell), min.z);
                    Vector3 center = (min + max) * .5f; center.z = b.center.z;
                    if (shape != null && !shape.ContainsWorldPoint(center)) continue;
                    if (WaterCellVisibility.Covered(owner, center, cell)) continue;
                    if (!OwnsSurfaceCell(center)) continue;
                    visibleCells.Add(new RectInt(x, rows - y - 1, 1, 1));
                    int kind = !ContainsWater(center + Vector3.up * cell) ? 0
                        : !ContainsWater(center + Vector3.up * cell * 2f) ? 1 : 2;
                    if (kind == 0) SurfaceCellCount++; else if (kind == 1) BodyCellCount++; else InteriorCellCount++;
                    Sprite sprite = settings.tiles[kind];
                    Rect rect = sprite.textureRect;
                    // TA's 16x17 source contains one transparent bottom row; keep a full visible 16x16 cell.
                    float bottom = rect.yMin + 1f;
                    Rect uv = Rect.MinMaxRect(rect.xMin / sprite.texture.width, bottom / sprite.texture.height,
                        rect.xMax / sprite.texture.width, rect.yMax / sprite.texture.height);
                    AddQuad(vertices[kind], uvs[kind], triangles[kind], min, max, uv);
                    Vector3 frontMin = min; frontMin.z = b.min.z - cell * .02f;
                    Vector3 frontMax = max; frontMax.z = frontMin.z;
                    AddQuad(frontVertices, frontUvs, frontTriangles, frontMin, frontMax, new Rect(x, y, 1, 1));
                }
            for (int kind = 0; kind < 3; kind++)
                if (vertices[kind].Count > 0)
                    CreateMeshPart(new[] { "Water Surface", "Water Body", "Water Interior" }[kind],
                        vertices[kind], uvs[kind], triangles[kind], settings.tileMaterials[kind], -100, settings.tiles[kind].texture);

            if (frontVertices.Count > 0)
            {
                var front = CreateMeshPart("Water Refraction", frontVertices, frontUvs, frontTriangles, settings.filterMaterial, 100, Texture2D.whiteTexture);
                front.AddComponent<WaterUnderlayCaptureRequester>();
            }
            if (neutralFilter != null && originalNeutralMesh != null)
            {
                Bounds localBounds = shape != null ? shape.LocalBounds : originalNeutralMesh.bounds;
                var neutralMesh = LevelEditorMergedShape.CreateMesh(localBounds, new Vector2Int(columns, rows), visibleCells);
                meshes.Add(neutralMesh); neutralFilter.sharedMesh = neutralMesh;
            }
            if (shape != null)
            {
                foreach (var volume in shape.Volumes) if (volume != null) BuildBubbles(volume.bounds, cell);
            }
            else BuildBubbles(b, cell);
            lastBounds = b;
            builtRevision = revision;
        }

        private void AddQuad(List<Vector3> vertices, List<Vector2> uv, List<int> triangles, Vector3 min, Vector3 max, Rect rect)
        {
            int start = vertices.Count;
            vertices.Add(transform.InverseTransformPoint(min));
            vertices.Add(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)));
            vertices.Add(transform.InverseTransformPoint(max));
            vertices.Add(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)));
            uv.Add(new Vector2(rect.xMin, rect.yMin)); uv.Add(new Vector2(rect.xMax, rect.yMin));
            uv.Add(new Vector2(rect.xMax, rect.yMax)); uv.Add(new Vector2(rect.xMin, rect.yMax));
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        private GameObject CreateMeshPart(string name, List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Material source, int order, Texture alphaTexture)
        {
            var part = new GameObject(name); part.transform.SetParent(transform, false); parts.Add(part);
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            bool clampTileSlice = alphaTexture != Texture2D.whiteTexture;
            if (clampTileSlice)
            {
                // Keep UV0 exactly 16 source pixels per world cell. UV1 only
                // bounds the final sample, excluding the source's transparent 17th row.
                var slices = new List<Vector4>(uv.Count);
                for (int first = 0; first < uv.Count; first += 4)
                {
                    Vector2 min = uv[first], max = min;
                    for (int j = 1; j < 4; j++) { min = Vector2.Min(min, uv[first+j]); max = Vector2.Max(max, uv[first+j]); }
                    for (int j = 0; j < 4; j++) slices.Add(new Vector4(min.x, min.y, max.x, max.y));
                }
                mesh.SetUVs(1, slices);
            }
            var colors = new Color[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors; mesh.RecalculateBounds(); meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ColorAppearanceManager.Instance.GetSharedMaterial(source, "blue", true);
            renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            ConfigureRenderer(renderer, alphaTexture, clampTileSlice);
            return part;
        }

        private static void ConfigureRenderer(Renderer renderer, Texture alphaTexture, bool clampTileSlice = false)
        {
            var definition = ColorRuntimeService.Instance.Catalog?.Find("blue");
            if (definition != null && definition.unityLayer >= 0 && definition.unityLayer < 32)
                renderer.gameObject.layer = definition.unityLayer;
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            properties.SetFloat("_ColorMaskGroup", ColorAppearanceManager.GetGroupIndex("blue"));
            properties.SetFloat("_ColorMaskReal", 1f); properties.SetFloat("_ColorMaskUseVertexAlpha", 1f);
            properties.SetTexture("_ColorMaskAlphaTexture", alphaTexture);
            properties.SetFloat("_GridArtSliceClamp", clampTileSlice ? 1f : 0f);
            properties.SetFloat("_WaterCellWorldSize", GridCellSizeUtility.Resolve(renderer));
            renderer.SetPropertyBlock(properties);
        }

        private void BuildBubbles(Bounds b, float cell)
        {
            if (settings.bubblePrefab == null || settings.bubbleMaterial == null || !owner.ShowFlowBubbles) return;
            bool isColumn = owner is BuoyancyColumnFeature;
            if (!(owner is DirectionalCurrentFeature) && !isColumn) return;
            Vector2 direction = owner is DirectionalCurrentFeature current ? current.VisualFlowDirection : Vector2.up;
            bool vertical = Mathf.Abs(direction.y) > .5f;
            float length = (vertical ? b.size.y : b.size.x) / cell;
            // A buoyancy source continues through connected water above, just like its gameplay lift.
            if (isColumn)
                for (int i = 0; i < 512; i++)
                {
                    if (!ContainsWater(new Vector3(b.center.x, b.min.y + (length + .5f) * cell, b.center.z))) break;
                    length++;
                }
            float width = (vertical ? b.size.x : b.size.y) / cell;
            int lanes = Mathf.Max(1, Mathf.CeilToInt(width / 3f));
            for (int lane = 0; lane < lanes; lane++)
                for (float offset = 0; offset < length - .001f; offset += 10f)
                {
                    var part = Instantiate(settings.bubblePrefab, transform); parts.Add(part);
                    part.name = "Flow Bubbles";
                    // Compensate merged host scale: all dimensions below are in actual grid units.
                    part.transform.rotation = Quaternion.identity;
                    Vector3 scale = transform.lossyScale;
                    part.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
                    float across = (lane + .5f) * width / lanes * cell;
                    Vector3 position = b.center;
                    if (vertical) { position.x = b.min.x + across; position.y = direction.y > 0 ? b.min.y : b.max.y; }
                    else { position.y = b.min.y + across; position.x = direction.x > 0 ? b.min.x : b.max.x; }
                    position += new Vector3(direction.x, direction.y, 0f) * offset * cell;
                    position.z = b.center.z - cell * .05f; part.transform.position = position;
                    var stream = part.GetComponent<DirectionalWaterBubbleStream>();
                    stream.Direction = direction.y < -.5f ? DirectionalWaterBubbleStream.FlowDirection.Down
                        : direction.x < -.5f ? DirectionalWaterBubbleStream.FlowDirection.Left
                        : direction.x > .5f ? DirectionalWaterBubbleStream.FlowDirection.Right : DirectionalWaterBubbleStream.FlowDirection.Up;
                    stream.WorldUnitsPerCell = cell; stream.LengthInCells = Mathf.Min(10f, length - offset);
                    stream.DensityPerCell = owner.FlowBubbleDensity; stream.BubblesPerFlowSlice = 2;
                    stream.StreamWidthInCells = Mathf.Max(0f, width / lanes - .4f);
                    stream.SpeedInCellsPerSecond = owner.FlowBubbleSpeed;
                    stream.RefreshBubblePool();
                    foreach (var renderer in part.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        renderer.sharedMaterial = ColorAppearanceManager.Instance.GetSharedMaterial(settings.bubbleMaterial, "blue", true);
                        ConfigureRenderer(renderer, renderer.sprite != null ? renderer.sprite.texture : Texture2D.whiteTexture);
                    }
                }
        }
    }
}
