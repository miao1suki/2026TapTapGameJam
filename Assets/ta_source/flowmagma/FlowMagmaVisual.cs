using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.FlowMagma
{
    /// <summary>One tile of animated magma and its pooled, cosmetic splash mesh.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FlowMagmaVisual : MonoBehaviour
    {
        private const int MaxParticles = 96;
        private const float MeshTopAllowance = 0.25f;
        private static readonly int EffectTimeId = Shader.PropertyToID("_EffectTime");
        private static readonly int FlowSpeedId = Shader.PropertyToID("_FlowSpeed");
        private static readonly int WaveAmplitudeId = Shader.PropertyToID("_WaveAmplitude");
        private static readonly int VerticalWarpStrengthId = Shader.PropertyToID("_VerticalWarpStrength");
        private static readonly int WaveFrequencyId = Shader.PropertyToID("_WaveFrequency");
        private static readonly int BodySizeId = Shader.PropertyToID("_BodySize");
        private readonly List<Rect> coverage = new List<Rect>();
        private readonly List<Vector2> exposedSurface = new List<Vector2>();
        private readonly List<float> surfaceFloors = new List<float>();
        private float cellWorldSize = 1f;
        private bool formalArtwork;
        private MeshRenderer bodyRenderer;
        private MaterialPropertyBlock splashProperties;
        public int SurfaceCellCount => exposedSurface.Count;
        public int ParticleCapacity => MaxParticles;
        public int EmittedParticleCount { get; private set; }

        /// <summary>World-sized exact coverage, relative to this unscaled visual root.</summary>
        public void ConfigureCoverage(IReadOnlyList<Rect> rectangles, float cellSize, bool useColorAppearance)
        {
            coverage.Clear();
            if (rectangles != null) coverage.AddRange(rectangles);
            cellWorldSize = Mathf.Max(.001f, cellSize);
            formalArtwork = useColorAppearance;
            Rebuild();
            Restart();
        }

        public void CollectSurfaceOrigins(List<Vector3> output, int budget = 8)
        {
            if (output == null || exposedSurface.Count == 0) return;
            int count = Mathf.Min(Mathf.Max(1, budget), exposedSurface.Count);
            for (int i = 0; i < count; i++)
            {
                int index = Mathf.Min(exposedSurface.Count - 1, (int)((i + .5f) * exposedSurface.Count / count));
                output.Add(transform.TransformPoint(new Vector3(exposedSurface[index].x, exposedSurface[index].y, 0)));
            }
        }

        [Header("材质和尺寸")]
        [SerializeField] private Material magmaMaterial;
        [SerializeField] private Material splashMaterial;
        [SerializeField] private MeshFilter splashFilter;
        [SerializeField] private MeshRenderer splashRenderer;
        [SerializeField, Min(0.1f)] private float width = 1f;
        [SerializeField, Min(0.1f)] private float height = 1f;

        [Header("岩浆流动")]
        [SerializeField, Min(0f)] private float flowSpeed = 0.28f;
        [SerializeField, Range(0f, .03f)] private float waveAmplitude = .008f;
        [SerializeField, Range(0f, .03f)] private float verticalWarpStrength = .006f;
        [SerializeField, Min(0.1f)] private float waveFrequency = 1.5f;

        [Header("喷发节奏")]
        [SerializeField, Range(0f, 80f)] private float particlesPerSecond = 10f;
        [SerializeField, Range(0f, .9f)] private float emissionIntervalRandomness = .75f;
        [SerializeField, Range(0f, 1f)] private float clusterChance = .22f;

        [Header("粒子运动")]
        [SerializeField, Min(0f)] private float launchSpeed = 1.2f;
        [SerializeField, Min(0f)] private float fallSpeed = 3.8f;
        [SerializeField, Range(0f, 1f)] private float settleChance = 0.3f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float particleSize = 0.09f;
        [SerializeField, Range(0f, 1f)] private float shrinkingParticleChance = .55f;
        [SerializeField, Range(0f, 1f)] private float largeParticleChance = .22f;
        [SerializeField, Range(0f, 1f)] private float sizeRandomness = .35f;
        [SerializeField, Range(0f, 1f)] private float trajectoryRandomness = .6f;
        [SerializeField, Min(.2f)] private float particleLifetime = 1.8f;
        [SerializeField] private int randomSeed = 51;

        private struct Splash
        {
            public bool active;
            public bool settled;
            public bool maySettle;
            public bool shrinking;
            public float heatBias;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float settleAge;
            public float size;
            public float lifetime;
            public float floorY;
        }

        private readonly Splash[] splashes = new Splash[MaxParticles];
        private readonly Vector3[] splashVertices = new Vector3[MaxParticles * 4];
        private readonly Vector2[] splashUvs = new Vector2[MaxParticles * 4];
        private readonly Color32[] splashColors = new Color32[MaxParticles * 4];
        private Mesh bodyMesh;
        private Mesh splashMesh;
        private MaterialPropertyBlock bodyProperties;
        private System.Random random;
        private float effectTime;
        private float timeUntilEmission;
        private bool dirty = true;

        public float FlowSpeed { get => flowSpeed; set { flowSpeed = Mathf.Max(0f, value); ApplyProperties(); } }
        public float WaveAmplitude { get => waveAmplitude; set { waveAmplitude = Mathf.Clamp(value, 0f, .03f); ApplyProperties(); } }
        public float VerticalWarpStrength { get => verticalWarpStrength; set { verticalWarpStrength = Mathf.Clamp(value, 0f, .03f); ApplyProperties(); } }
        public float ParticlesPerSecond { get => particlesPerSecond; set => particlesPerSecond = Mathf.Clamp(value, 0f, 80f); }
        public float EmissionIntervalRandomness { get => emissionIntervalRandomness; set => emissionIntervalRandomness = Mathf.Clamp(value, 0f, .9f); }
        public float ClusterChance { get => clusterChance; set => clusterChance = Mathf.Clamp01(value); }
        public float LaunchSpeed { get => launchSpeed; set => launchSpeed = Mathf.Max(0f, value); }
        public float FallSpeed { get => fallSpeed; set => fallSpeed = Mathf.Max(0f, value); }
        public float ShrinkingParticleChance { get => shrinkingParticleChance; set => shrinkingParticleChance = Mathf.Clamp01(value); }
        public float LargeParticleChance { get => largeParticleChance; set => largeParticleChance = Mathf.Clamp01(value); }
        public float SizeRandomness { get => sizeRandomness; set => sizeRandomness = Mathf.Clamp01(value); }
        public float TrajectoryRandomness { get => trajectoryRandomness; set => trajectoryRandomness = Mathf.Clamp01(value); }

        public void Restart()
        {
            effectTime = 0f;
            timeUntilEmission = 0f;
            EmittedParticleCount = 0;
            Array.Clear(splashes, 0, splashes.Length);
            random = new System.Random(randomSeed ^ GetInstanceID());
            ApplyProperties();
            DrawSplashes(!Application.IsPlaying(gameObject));
        }

        public void RefreshPreview()
        {
            dirty = true;
            if (!Application.IsPlaying(gameObject)) Rebuild();
        }

        private void OnEnable()
        {
            Rebuild();
            Restart();
        }

        private void OnDisable()
        {
            DisposeMeshes();
        }

        private void OnValidate()
        {
            width = Mathf.Max(0.1f, width);
            height = Mathf.Max(0.1f, height);
            waveAmplitude = Mathf.Clamp(waveAmplitude, 0, .03f);
            verticalWarpStrength = Mathf.Clamp(verticalWarpStrength, 0, .03f);
            emissionIntervalRandomness = Mathf.Clamp(emissionIntervalRandomness, 0, .9f);
            particleLifetime = Mathf.Max(.2f, particleLifetime);
            dirty = true;
        }

        private void Update()
        {
            if (dirty) Rebuild();
            if (!Application.IsPlaying(gameObject)) return;

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            effectTime += dt;
            if (formalArtwork && bodyRenderer != null && !bodyRenderer.isVisible &&
                splashRenderer != null && !splashRenderer.isVisible) return;
            ApplyProperties();
            AdvanceSplashes(dt);
            if (particlesPerSecond > 0f)
            {
                float rate = Mathf.Min(80f, particlesPerSecond * Mathf.Min(6, exposedSurface.Count));
                timeUntilEmission -= dt;
                for (int i = 0; i < 12 && timeUntilEmission <= 0f && rate > 0; i++)
                {
                    SpawnSplash();
                    if (RandomRange(0, 1) < clusterChance)
                    {
                        SpawnSplash();
                        if (RandomRange(0, 1) < .35f) SpawnSplash();
                    }
                    timeUntilEmission += (1f + RandomRange(-emissionIntervalRandomness, emissionIntervalRandomness)) / rate;
                }
            }
            else timeUntilEmission = 0;
            DrawSplashes(false);
        }

        private void Rebuild()
        {
            dirty = false;
            DisposeMeshes();
            var filter = GetComponent<MeshFilter>();
            bodyRenderer = GetComponent<MeshRenderer>();
            if (formalArtwork)
            {
                // Transparent sprites do not write depth. A slight Z offset alone
                // cannot stop magma drawing over a cube occupying the same cell.
                bodyRenderer.sortingOrder = -10;
                if (splashRenderer != null) splashRenderer.sortingOrder = -9;
            }
            bodyRenderer.sharedMaterial = formalArtwork && Application.isPlaying
                ? ColorAppearanceManager.Instance.GetSharedMaterial(magmaMaterial, "red", true) : magmaMaterial;
            if (splashRenderer != null) splashRenderer.sharedMaterial = formalArtwork && Application.isPlaying
                ? ColorAppearanceManager.Instance.GetSharedMaterial(splashMaterial, "red", true) : splashMaterial;
            if (filter == null || splashFilter == null || splashRenderer == null) return;

            bodyMesh = BuildBody();
            filter.sharedMesh = bodyMesh;

            splashMesh = new Mesh { name = "Flow Magma Splashes (generated)", hideFlags = HideFlags.DontSave };
            splashMesh.MarkDynamic();
            int[] indices = new int[MaxParticles * 6];
            for (int i = 0; i < MaxParticles; i++)
            {
                int v = i * 4;
                int t = i * 6;
                indices[t] = v;
                indices[t + 1] = v + 2;
                indices[t + 2] = v + 1;
                indices[t + 3] = v + 1;
                indices[t + 4] = v + 2;
                indices[t + 5] = v + 3;
                splashUvs[v] = new Vector2(0f, 0f);
                splashUvs[v + 1] = new Vector2(1f, 0f);
                splashUvs[v + 2] = new Vector2(0f, 1f);
                splashUvs[v + 3] = new Vector2(1f, 1f);
            }
            splashMesh.vertices = splashVertices;
            splashMesh.uv = splashUvs;
            splashMesh.colors32 = splashColors;
            splashMesh.triangles = indices;
            splashFilter.sharedMesh = splashMesh;
            bodyProperties ??= new MaterialPropertyBlock();
            ApplyProperties();
            DrawSplashes(!Application.IsPlaying(gameObject));
        }

        private Mesh BuildBody()
        {
            exposedSurface.Clear(); surfaceFloors.Clear();
            var vertices = new List<Vector3>(); var uv = new List<Vector2>();
            var surfaces = new List<Vector2>(); var triangles = new List<int>();
            var rectangles = coverage.Count > 0 ? coverage : new List<Rect> { new Rect(-width * .5f, -height * .5f, width, height) };
            Vector2 min = rectangles[0].min, max = rectangles[0].max;
            foreach (Rect rect in rectangles) { min = Vector2.Min(min, rect.min); max = Vector2.Max(max, rect.max); }
            width = Mathf.Max(.001f, max.x - min.x); height = Mathf.Max(.001f, max.y - min.y);
            var cells = new Dictionary<Vector2Int, Rect>();
            foreach (Rect rect in rectangles)
            {
                int nx = Mathf.Max(1, Mathf.CeilToInt(rect.width / cellWorldSize - .001f));
                int ny = Mathf.Max(1, Mathf.CeilToInt(rect.height / cellWorldSize - .001f));
                for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
                {
                    float left = rect.xMin + x * cellWorldSize, bottom = rect.yMin + y * cellWorldSize;
                    var part = Rect.MinMaxRect(left, bottom, Mathf.Min(left + cellWorldSize, rect.xMax), Mathf.Min(bottom + cellWorldSize, rect.yMax));
                    var key = new Vector2Int(Mathf.RoundToInt((left - min.x) / cellWorldSize), Mathf.RoundToInt((bottom - min.y) / cellWorldSize));
                    cells[key] = part;
                }
            }
            foreach (var pair in cells)
            {
                Rect rect = pair.Value;
                bool top = !cells.ContainsKey(pair.Key + Vector2Int.up);
                float allowance = top ? MeshTopAllowance * cellWorldSize : 0f;
                int n = vertices.Count;
                vertices.Add(new Vector3(rect.xMin, rect.yMin, 0)); vertices.Add(new Vector3(rect.xMax, rect.yMin, 0));
                vertices.Add(new Vector3(rect.xMin, rect.yMax + allowance, 0)); vertices.Add(new Vector3(rect.xMax, rect.yMax + allowance, 0));
                for (int i = n; i < n + 4; i++)
                {
                    uv.Add(new Vector2(vertices[i].x / cellWorldSize, vertices[i].y / cellWorldSize));
                    surfaces.Add(new Vector2(rect.yMax, top ? 1f : 0f));
                }
                triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
                triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n + 3);
                if (top)
                {
                    exposedSurface.Add(new Vector2(rect.center.x, rect.yMax));
                    Vector2Int floorKey = pair.Key;
                    while (cells.ContainsKey(floorKey + Vector2Int.down)) floorKey += Vector2Int.down;
                    surfaceFloors.Add(cells[floorKey].yMin);
                }
            }
            var mesh = new Mesh { name = "Flow Magma Body (exact coverage)", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetUVs(1, surfaces); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); return mesh;
        }

        private void ApplyProperties()
        {
            if (bodyMesh == null) return;
            var renderer = bodyRenderer != null ? bodyRenderer : GetComponent<MeshRenderer>();
            if (renderer == null) return;
            bodyProperties ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(bodyProperties);
            bodyProperties.SetFloat("_FlowMagmaBody", 1f);
            bodyProperties.SetFloat("_FlowCellWorldSize", cellWorldSize);
            bodyProperties.SetFloat("_ColorMaskGroup", formalArtwork ? 0f : -1f);
            bodyProperties.SetFloat("_ColorMaskReal", 1f);
            bodyProperties.SetFloat(EffectTimeId, effectTime);
            bodyProperties.SetFloat(FlowSpeedId, flowSpeed);
            bodyProperties.SetFloat(WaveAmplitudeId, waveAmplitude);
            bodyProperties.SetFloat(VerticalWarpStrengthId, verticalWarpStrength);
            bodyProperties.SetFloat(WaveFrequencyId, waveFrequency);
            bodyProperties.SetVector(BodySizeId, new Vector4(width, height, 0f, 0f));
            renderer.SetPropertyBlock(bodyProperties);
            if (splashRenderer != null)
            {
                splashProperties ??= new MaterialPropertyBlock();
                splashRenderer.GetPropertyBlock(splashProperties);
                splashProperties.SetFloat("_FlowMagmaSplash", 1f);
                splashProperties.SetFloat("_FlowCellWorldSize", cellWorldSize);
                splashProperties.SetFloat("_ColorMaskGroup", formalArtwork ? 0f : -1f);
                splashProperties.SetFloat("_ColorMaskReal", 1f);
                splashProperties.SetFloat("_ColorMaskUseVertexAlpha", 1f);
                splashRenderer.SetPropertyBlock(splashProperties);
            }
        }

        private void SpawnSplash()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (splashes[i].active) continue;
                if (exposedSurface.Count == 0) return;
                int originIndex = random.Next(exposedSurface.Count);
                Vector2 origin = exposedSurface[originIndex];
                float x = origin.x + RandomRange(-.4f, .4f) * cellWorldSize;
                float sizeTier = RandomRange(0, 1) < largeParticleChance ? 1.6f : 1f;
                splashes[i] = new Splash
                {
                    active = true,
                    maySettle = RandomRange(0f, 1f) < settleChance,
                    shrinking = RandomRange(0, 1) < shrinkingParticleChance,
                    heatBias = RandomRange(-.07f, .07f),
                    x = x,
                    y = origin.y + waveAmplitude * cellWorldSize * Mathf.Sin(x / cellWorldSize * waveFrequency * Mathf.PI * 2f + effectTime * 1.7f),
                    vx = RandomRange(-.45f, .45f) * launchSpeed * trajectoryRandomness * cellWorldSize,
                    vy = launchSpeed * (1f + RandomRange(-.3f, .3f) * trajectoryRandomness) * cellWorldSize,
                    size = Mathf.Max(2f, Mathf.Round(particleSize * sizeTier * Mathf.Max(.35f,
                        1f + RandomRange(-1, 1) * sizeRandomness) * 16f)) / 16f * cellWorldSize,
                    lifetime = particleLifetime * RandomRange(.8f, 1.2f),
                    floorY = surfaceFloors[originIndex]
                };
                EmittedParticleCount++;
                return;
            }
        }

        private void AdvanceSplashes(float dt)
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                Splash p = splashes[i];
                if (!p.active) continue;
                p.age += dt;
                if (p.settled)
                {
                    p.settleAge += dt;
                    if (p.settleAge >= settleDuration) p.active = false;
                }
                else
                {
                    p.x += p.vx * dt;
                    p.y += p.vy * dt;
                    p.vy -= fallSpeed * cellWorldSize * dt;
                    if (p.y <= p.floorY + p.size * 0.5f)
                    {
                        if (p.maySettle && Mathf.Abs(p.x) <= width * 0.5f)
                        {
                            p.settled = true;
                            p.y = p.floorY + p.size * 0.5f;
                            p.lifetime = Mathf.Max(p.lifetime, p.age + settleDuration);
                        }
                        else p.active = false;
                    }
                    if (p.age >= p.lifetime) p.active = false;
                }
                splashes[i] = p;
            }
        }

        private void DrawSplashes(bool preview)
        {
            if (splashMesh == null) return;
            float pixel = cellWorldSize / 16f;
            Matrix4x4 localToWorld = transform.localToWorldMatrix, worldToLocal = transform.worldToLocalMatrix;
            for (int i = 0; i < MaxParticles; i++)
            {
                Splash p = splashes[i];
                bool visible = p.active;
                if (preview && i < 7)
                {
                    visible = true;
                    Vector2 origin = exposedSurface.Count > 0 ? exposedSurface[i % exposedSurface.Count] : Vector2.zero;
                    p.x = origin.x;
                    p.y = origin.y + (.08f + Mathf.Sin(i * 1.7f) * .09f) * cellWorldSize;
                    p.size = Mathf.Max(2, Mathf.Round(particleSize * (i % 4 == 0 ? 1.6f : 1f) * 16f)) / 16f * cellWorldSize;
                    p.shrinking = i % 2 == 0;
                    p.heatBias = (i % 3 - 1) * .05f;
                    p.age = i * .18f;
                    p.lifetime = particleLifetime;
                }
                float age = Mathf.Clamp01(p.age / Mathf.Max(.2f, p.lifetime));
                float coolingSize = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, .95f, age));
                float ratio = p.shrinking ? Mathf.Lerp(1.15f, .18f, coolingSize) : 1f;
                float diameter = Mathf.Max(1, Mathf.Round(p.size * ratio * (p.settled ? .65f : 1f) / pixel)) * pixel;
                Vector3 centerWorld = localToWorld.MultiplyPoint3x4(new Vector3(p.x,p.y,0));
                float left = Mathf.Round((centerWorld.x - diameter * .5f) / pixel) * pixel;
                float bottom = Mathf.Round((centerWorld.y - diameter * .5f) / pixel) * pixel;
                int v = i * 4;
                splashVertices[v] = worldToLocal.MultiplyPoint3x4(new Vector3(left,bottom,centerWorld.z));
                splashVertices[v + 1] = worldToLocal.MultiplyPoint3x4(new Vector3(left + diameter,bottom,centerWorld.z));
                splashVertices[v + 2] = worldToLocal.MultiplyPoint3x4(new Vector3(left,bottom + diameter,centerWorld.z));
                splashVertices[v + 3] = worldToLocal.MultiplyPoint3x4(new Vector3(left + diameter,bottom + diameter,centerWorld.z));
                float lifeFade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.82f, 1f, age));
                float settleFade = p.settled ? 1f - Mathf.Clamp01(p.settleAge / settleDuration) : 1f;
                byte alpha = visible ? (byte)Mathf.RoundToInt(255f * lifeFade * settleFade) : (byte)0;
                var color = new Color32((byte)Mathf.RoundToInt(255f * Mathf.Clamp01(age + p.heatBias)),255,255,alpha);
                splashColors[v] = splashColors[v + 1] = splashColors[v + 2] = splashColors[v + 3] = color;
            }
            splashMesh.vertices = splashVertices;
            splashMesh.colors32 = splashColors;
            Bounds bounds = bodyMesh != null ? bodyMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            bounds.Expand((launchSpeed * 3f + 1f) * cellWorldSize);
            splashMesh.bounds = bounds;
        }

        private float RandomRange(float min, float max)
        {
            random ??= new System.Random(randomSeed ^ GetInstanceID());
            return min + (float)random.NextDouble() * (max - min);
        }

        private void DisposeMeshes()
        {
            var filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == bodyMesh) filter.sharedMesh = null;
            if (splashFilter != null && splashFilter.sharedMesh == splashMesh) splashFilter.sharedMesh = null;
            Dispose(bodyMesh);
            Dispose(splashMesh);
            bodyMesh = null;
            splashMesh = null;
        }

        private void Dispose(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.IsPlaying(gameObject)) Destroy(mesh);
            else DestroyImmediate(mesh);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.12f, 0.75f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(width, height, 0.01f));
        }
    }
}
