using System;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.Rain
{
    /// <summary>Animates many atlas droplets in one local XY mesh without spawning GameObjects.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RainSpriteField : MonoBehaviour
    {
        public enum RainColor
        {
            [InspectorName("红色")] Red,
            [InspectorName("蓝色")] Blue,
            [InspectorName("绿色")] Green
        }

        private const int FrameCount = 4;
        private const int MaxDrops = 512;
        private const float AtlasWidth = 447f;
        private const float AtlasHeight = 336f;

        [SerializeField] private RainColor color = RainColor.Blue;
        [SerializeField] private Material rainMaterial;
        [SerializeField, Min(0.1f)] private float areaWidth = 2.8f;
        [SerializeField, Min(0.1f)] private float areaHeight = 4f;
        [SerializeField, Range(0f, 100f)] private float density = 18f;
        [SerializeField, Min(0.01f)] private float fallSpeed = 3.2f;
        [SerializeField, Range(0f, 0.9f)] private float speedVariation = 0.3f;
        [SerializeField, Range(1f, 24f)] private float frameRate = 9f;
        [SerializeField, Range(0f, 1f)] private float randomness = 0.8f;
        [SerializeField, Range(0f, 1f)] private float reachBottomChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float noiseStrength = 0.65f;
        [SerializeField, Min(0.01f)] private float noiseScale = 1.5f;
        [SerializeField] private int randomSeed = 37;

        private struct Drop
        {
            public float x;
            public float age;
            public float speed;
            public float endProgress;
            public float framePhase;
            public float animationScale;
            public float swayPhase;
        }

        private Drop[] drops;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color32[] colors;
        private Mesh mesh;
        private System.Random random;
        private float lastWidth;
        private float lastDensity;
        private float lastSpeed;
        private float lastSpeedVariation;
        private bool previewDirty;
        private float cell = 1f;
        private Vector3 renderScale = Vector3.one;
        private MaterialPropertyBlock mask;
        private ColorCatalog catalog;

        public string ColorId => color == RainColor.Red ? "red" : color == RainColor.Green ? "green" : "blue";
        public int DropCount => drops?.Length ?? 0;
        public Vector2 AreaCells => new Vector2(areaWidth, areaHeight);
        public int Seed => randomSeed;
        public float CellWorldSize => cell;
        public Mesh GeneratedMesh => mesh;

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            DisposeMesh();
        }

        private void DisposeMesh()
        {
            if (mesh == null) return;
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (Application.IsPlaying(gameObject)) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }

        private void OnValidate()
        {
            areaWidth = Mathf.Max(0.1f, areaWidth);
            areaHeight = Mathf.Max(0.1f, areaHeight);
            fallSpeed = Mathf.Max(0.01f, fallSpeed);
            noiseScale = Mathf.Max(0.01f, noiseScale);
            previewDirty = true;
        }

        private void OnTransformParentChanged() => previewDirty = true;

        public void Configure(RainColor value, Vector2 cells, float dropsPerCell, int seed)
        {
            color = value;
            areaWidth = Mathf.Max(.1f, cells.x);
            areaHeight = Mathf.Max(.1f, cells.y);
            density = Mathf.Clamp(dropsPerCell, 0, 100);
            randomSeed = seed;
            if (isActiveAndEnabled) Build();
        }

        public static Rect FrameUV(RainColor value, int frame)
        {
            frame = ((frame % FrameCount) + FrameCount) % FrameCount;
            int row = value == RainColor.Red ? 160 : value == RainColor.Blue ? 224 : 288;
            float x = 320f + frame % 2 * 32f;
            float top = row + frame / 2 * 32f;
            return new Rect(x / AtlasWidth, (AtlasHeight - top - 16f) / AtlasHeight,
                16f / AtlasWidth, 16f / AtlasHeight);
        }

        private void Build()
        {
            previewDirty = false;
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial != rainMaterial) renderer.sharedMaterial = rainMaterial;
            cell = GridCellSizeUtility.Resolve(this);
            BindAppearance(renderer);
            DisposeMesh();
            if (rainMaterial == null)
            {
                if (Application.IsPlaying(gameObject))
                {
                    Debug.LogWarning("雨滴缺少材质。", this);
                    enabled = false;
                }
                return;
            }

            int count = Mathf.Clamp(Mathf.RoundToInt(areaWidth * density), 0, MaxDrops);
            drops = new Drop[count];
            vertices = new Vector3[count * 4];
            uvs = new Vector2[count * 4];
            colors = new Color32[count * 4];
            int[] triangles = new int[count * 6];
            // Author seed is stable across clients, save/reload, instance IDs and translations.
            random = new System.Random(randomSeed);
            mesh = new Mesh
            {
                name = "Rain Sprite Field (generated)",
                hideFlags = HideFlags.DontSave
            };
            mesh.MarkDynamic();
            for (int i = 0; i < count; i++)
            {
                int v = i * 4;
                int t = i * 6;
                triangles[t] = v;
                triangles[t + 1] = v + 2;
                triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 2;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 1;
                ResetDrop(i, true);
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.triangles = triangles;
            UpdateMeshBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            lastWidth = areaWidth;
            lastDensity = density;
            lastSpeed = fallSpeed;
            lastSpeedVariation = speedVariation;
            Animate(0f);
            if (!Application.IsPlaying(gameObject)) transform.hasChanged = false;
        }

        private void Update()
        {
            if (mesh == null)
            {
                if (previewDirty) Build();
                return;
            }
            if (previewDirty ||
                Mathf.Abs(lastWidth - areaWidth) > 0.001f ||
                Mathf.Abs(lastDensity - density) > 0.001f ||
                Mathf.Abs(lastSpeed - fallSpeed) > 0.001f ||
                Mathf.Abs(lastSpeedVariation - speedVariation) > 0.001f)
            {
                Build();
                return;
            }
            if (Application.IsPlaying(gameObject)) Animate(Time.deltaTime);
            else if (transform.hasChanged) { Animate(0); transform.hasChanged = false; }
        }

#if UNITY_EDITOR
        public void RefreshPreview()
        {
            if (!Application.IsPlaying(gameObject) && isActiveAndEnabled) Build();
        }
#endif

        private void ResetDrop(int index, bool initial)
        {
            float x = NextFloat() * areaWidth - areaWidth * 0.5f;
            float noise = Mathf.PerlinNoise((x + randomSeed * 0.37f) * noiseScale, index * 0.37f + randomSeed * 0.13f);
            float reachChance = Mathf.Clamp01(reachBottomChance + (noise - 0.5f) * noiseStrength * randomness);
            bool reachesBottom = NextFloat() < reachChance;
            float endProgress = reachesBottom ? 1f : Mathf.Lerp(0.28f, 0.82f, noise);
            float speed = fallSpeed * (1f + (NextFloat() * 2f - 1f) * speedVariation);
            float travelTime = areaHeight * endProgress / speed;
            drops[index] = new Drop
            {
                x = x,
                age = initial ? travelTime * Mathf.Lerp(0.08f, 0.88f, NextFloat()) : -Mathf.Lerp(0.08f, 0.7f, NextFloat()) * randomness,
                speed = speed,
                endProgress = endProgress,
                framePhase = NextFloat() * FrameCount * randomness,
                animationScale = 1f + (NextFloat() * 2f - 1f) * randomness * 0.3f,
                swayPhase = NextFloat() * Mathf.PI * 2f
            };
        }

        private void Animate(float deltaTime)
        {
            Vector3 scale = transform.lossyScale;
            renderScale = new Vector3(cell / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                cell / Mathf.Max(.001f, Mathf.Abs(scale.y)), 1);
            float pixel = GridArtworkMetrics.PixelWorldSize(cell);
            for (int i = 0; i < drops.Length; i++)
            {
                Drop drop = drops[i];
                drop.age += deltaTime;
                float endTime = areaHeight * drop.endProgress / drop.speed;
                if (drop.age > endTime)
                {
                    ResetDrop(i, false);
                    drop = drops[i];
                }
                else drops[i] = drop;

                const float halfSize = .5f; // Full 16x16 atlas cell, no random rescaling.
                float progress = drop.age * drop.speed / areaHeight;
                float y = areaHeight * (0.5f - progress) - halfSize;
                float sway = Mathf.Sin(drop.age * 2.1f + drop.swayPhase) * randomness * noiseStrength * 0.06f;
                float x = drop.x + sway;
                Vector3 center = transform.TransformPoint(Vector3.Scale(new Vector3(x, y, 0), renderScale));
                center.x = Mathf.Round(center.x / pixel) * pixel;
                center.y = Mathf.Round(center.y / pixel) * pixel;
                center = transform.InverseTransformPoint(center);
                Vector3 halfX = Vector3.right * (halfSize * renderScale.x);
                Vector3 halfY = Vector3.up * (halfSize * renderScale.y);
                int v = i * 4;
                vertices[v] = center - halfX - halfY;
                vertices[v + 1] = center + halfX - halfY;
                vertices[v + 2] = center - halfX + halfY;
                vertices[v + 3] = center + halfX + halfY;

                // Layout preview uses one still atlas frame; only Play cycles the four frames.
                int frame = Application.IsPlaying(gameObject)
                    ? Mathf.FloorToInt(Mathf.Max(0f, drop.age) * frameRate * drop.animationScale + drop.framePhase) % FrameCount
                    : 0;
                Rect uv = FrameUV(color, frame);
                uvs[v] = new Vector2(uv.xMin, uv.yMin);
                uvs[v + 1] = new Vector2(uv.xMax, uv.yMin);
                uvs[v + 2] = new Vector2(uv.xMin, uv.yMax);
                uvs[v + 3] = new Vector2(uv.xMax, uv.yMax);

                float fade = Mathf.InverseLerp(0f, 0.08f, progress);
                fade *= Mathf.InverseLerp(drop.endProgress, drop.endProgress - 0.12f, progress);
                byte alpha = drop.age < 0f ? (byte)0 : (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(fade));
                Color32 tint = new Color32(255, 255, 255, alpha);
                colors[v] = tint;
                colors[v + 1] = tint;
                colors[v + 2] = tint;
                colors[v + 3] = tint;
            }
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            UpdateMeshBounds();
        }

        private void UpdateMeshBounds()
        {
            mesh.bounds = new Bounds(Vector3.zero,
                new Vector3((areaWidth + 1.2f) * renderScale.x, (areaHeight + 2f) * renderScale.y, .1f));
        }

        private void BindAppearance(MeshRenderer renderer)
        {
            if (catalog == null) catalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
            var definition = catalog != null ? catalog.Find(ColorId) : null;
            if (definition != null && definition.unityLayer >= 0 && definition.unityLayer <= 31)
                gameObject.layer = definition.unityLayer;
            mask ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mask);
            // Decoration only: HSV is selected by ColorCatalog layer. It is not a gameplay ColorObject.
            mask.SetFloat("_ColorMaskGroup", -1);
            mask.SetFloat("_ColorMaskReal", 1);
            mask.SetFloat("_ColorMaskUseVertexAlpha", 1);
            mask.SetFloat("_ColorMaskOpacity", rainMaterial != null && rainMaterial.HasProperty("_Color")
                ? Mathf.Clamp01(rainMaterial.GetColor("_Color").a) : 1);
            Texture alpha = rainMaterial != null ? rainMaterial.mainTexture : null;
            mask.SetTexture("_ColorMaskAlphaTexture", alpha != null ? alpha : Texture2D.whiteTexture);
            renderer.SetPropertyBlock(mask);
            renderer.renderingLayerMask &= ~128u; // Native artwork is already pixel-sized, no second pixel pass.
        }

        private float NextFloat() => (float)random.NextDouble();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.8f, 1f, 0.65f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * cell);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaWidth, areaHeight, 0f));
        }
    }
}
