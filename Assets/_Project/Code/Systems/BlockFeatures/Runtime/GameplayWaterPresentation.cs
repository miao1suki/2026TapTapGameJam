using System.Collections.Generic;
using InteractiveWater;
using Project.ColorBlocks;
using Project.WaterBubbles;
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
        private int builtRevision = -1;
        private WaterVisualFeature owner;
        private WaterPresentationSettings settings;
        private readonly List<GameObject> parts = new List<GameObject>();
        private readonly List<Mesh> meshes = new List<Mesh>();
        private Bounds lastBounds;
        private Collider volumeCollider;
        private float cellSize = 1f;
        private Vector4 flowSettings;
        public int SurfaceCellCount { get; private set; }
        public int BodyCellCount { get; private set; }
        public int InteriorCellCount { get; private set; }
        public Bounds Volume => volumeCollider != null
            ? volumeCollider.bounds : new Bounds(transform.position, Vector3.one * CellSize);
        private float CellSize => cellSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { active.Clear(); revision = 0; }

        public void Initialize(WaterVisualFeature feature, WaterPresentationSettings configuration)
        {
            owner = feature;
            settings = configuration;
            volumeCollider = feature.GetComponent<Collider>();
            cellSize = GridCellSizeUtility.Resolve(feature);
            flowSettings = ReadFlowSettings();
            active.Add(this);
            revision++;
            lastBounds = Volume;
        }

        private void OnEnable()
        {
            if (owner != null && active.Add(this)) { revision++; builtRevision = -1; }
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
            if (bounds != lastBounds) { lastBounds = bounds; revision++; }
            Vector4 nextSettings = ReadFlowSettings();
            if (nextSettings != flowSettings) { flowSettings = nextSettings; revision++; }
            if (builtRevision != revision) Rebuild();
        }

        private void OnDisable()
        {
            if (active.Remove(this)) revision++;
            ClearParts();
        }

        private void ClearParts()
        {
            foreach (var part in parts)
                if (part != null) { part.SetActive(false); Destroy(part); }
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
            parts.Clear(); meshes.Clear();
        }

        private bool ContainsWater(Vector3 world)
        {
            foreach (var water in active)
            {
                if (water == null || water.owner == null || water.gameObject.scene != gameObject.scene) continue;
                Bounds b = water.Volume;
                float epsilon = CellSize * .001f;
                if (world.x >= b.min.x + epsilon && world.x <= b.max.x - epsilon &&
                    world.y >= b.min.y + epsilon && world.y <= b.max.y - epsilon &&
                    world.z >= b.min.z - epsilon && world.z <= b.max.z + epsilon) return true;
            }
            return false;
        }

        private void Rebuild()
        {
            ClearParts();
            SurfaceCellCount = BodyCellCount = InteriorCellCount = 0;
            Bounds b = Volume;
            float cell = CellSize;
            int columns = Mathf.Max(1, Mathf.CeilToInt(b.size.x / cell - .001f));
            int rows = Mathf.Max(1, Mathf.CeilToInt(b.size.y / cell - .001f));
            var vertices = new List<Vector3>[3];
            var uvs = new List<Vector2>[3];
            var triangles = new List<int>[3];
            for (int i = 0; i < 3; i++) { vertices[i] = new List<Vector3>(); uvs[i] = new List<Vector2>(); triangles[i] = new List<int>(); }
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    Vector3 min = new Vector3(b.min.x + x * cell, b.min.y + y * cell, b.max.z + cell * .02f);
                    Vector3 max = new Vector3(Mathf.Min(b.max.x, min.x + cell), Mathf.Min(b.max.y, min.y + cell), min.z);
                    Vector3 center = (min + max) * .5f; center.z = b.center.z;
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
                }
            for (int kind = 0; kind < 3; kind++)
                if (vertices[kind].Count > 0)
                    CreateMeshPart(new[] { "Water Surface", "Water Body", "Water Interior" }[kind],
                        vertices[kind], uvs[kind], triangles[kind], settings.tileMaterials[kind], -100, settings.tiles[kind].texture);

            var frontVertices = new List<Vector3>(); var frontUvs = new List<Vector2>(); var frontTriangles = new List<int>();
            Vector3 frontMin = b.min; frontMin.z -= cell * .02f;
            Vector3 frontMax = b.max; frontMax.z = frontMin.z;
            AddQuad(frontVertices, frontUvs, frontTriangles, frontMin, frontMax, new Rect(0, 0, b.size.x / cell, b.size.y / cell));
            var front = CreateMeshPart("Water Refraction", frontVertices, frontUvs, frontTriangles, settings.filterMaterial, 100, Texture2D.whiteTexture);
            front.AddComponent<WaterUnderlayCaptureRequester>();
            BuildBubbles(b, cell);
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
            var colors = new Color[vertices.Count]; for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors; mesh.RecalculateBounds(); meshes.Add(mesh);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ColorAppearanceManager.Instance.GetSharedMaterial(source, "blue", true);
            renderer.sortingOrder = order; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            ConfigureRenderer(renderer, alphaTexture);
            return part;
        }

        private static void ConfigureRenderer(Renderer renderer, Texture alphaTexture)
        {
            var definition = ColorRuntimeService.Instance.Catalog?.Find("blue");
            if (definition != null && definition.unityLayer >= 0 && definition.unityLayer < 32)
                renderer.gameObject.layer = definition.unityLayer;
            var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            properties.SetFloat("_ColorMaskGroup", ColorAppearanceManager.GetGroupIndex("blue"));
            properties.SetFloat("_ColorMaskReal", 1f); properties.SetFloat("_ColorMaskUseVertexAlpha", 1f);
            properties.SetTexture("_ColorMaskAlphaTexture", alphaTexture);
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
