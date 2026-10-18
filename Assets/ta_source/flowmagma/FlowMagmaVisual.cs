using System;
using UnityEngine;

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

        [Header("材质和尺寸")]
        [SerializeField] private Material magmaMaterial;
        [SerializeField] private Material splashMaterial;
        [SerializeField] private MeshFilter splashFilter;
        [SerializeField] private MeshRenderer splashRenderer;
        [SerializeField, Min(0.1f)] private float width = 1f;
        [SerializeField, Min(0.1f)] private float height = 1f;

        [Header("岩浆流动")]
        [SerializeField, Min(0f)] private float flowSpeed = 0.28f;
        [SerializeField, Range(0f, 0.03f)] private float waveAmplitude = 0.008f;
        [SerializeField, Range(0f, 0.03f)] private float verticalWarpStrength = 0.006f;
        [SerializeField, Min(0.1f)] private float waveFrequency = 1.5f;

        [Header("喷发节奏")]
        [SerializeField, Range(0f, 80f)] private float particlesPerSecond = 10f;
        [SerializeField, Range(0f, 0.9f)] private float emissionIntervalRandomness = 0.75f;
        [SerializeField, Range(0f, 1f)] private float clusterChance = 0.22f;

        [Header("粒子运动")]
        [SerializeField, Min(0f)] private float launchSpeed = 1.2f;
        [SerializeField, Min(0f)] private float fallSpeed = 3.8f;
        [SerializeField, Range(0f, 1f)] private float settleChance = 0.3f;
        [SerializeField, Min(0.01f)] private float settleDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float particleSize = 0.09f;
        [SerializeField, Range(0f, 1f)] private float shrinkingParticleChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float largeParticleChance = 0.22f;
        [SerializeField, Range(0f, 1f)] private float sizeRandomness = 0.35f;
        [SerializeField, Range(0f, 1f)] private float trajectoryRandomness = 0.6f;
        [SerializeField, Min(0.2f)] private float particleLifetime = 1.8f;
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
        public float WaveAmplitude { get => waveAmplitude; set { waveAmplitude = Mathf.Clamp(value, 0f, 0.03f); ApplyProperties(); } }
        public float VerticalWarpStrength { get => verticalWarpStrength; set { verticalWarpStrength = Mathf.Clamp(value, 0f, 0.03f); ApplyProperties(); } }
        public float ParticlesPerSecond { get => particlesPerSecond; set => particlesPerSecond = Mathf.Clamp(value, 0f, 80f); }
        public float EmissionIntervalRandomness { get => emissionIntervalRandomness; set => emissionIntervalRandomness = Mathf.Clamp(value, 0f, 0.9f); }
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
            waveAmplitude = Mathf.Clamp(waveAmplitude, 0f, 0.03f);
            verticalWarpStrength = Mathf.Clamp(verticalWarpStrength, 0f, 0.03f);
            emissionIntervalRandomness = Mathf.Clamp(emissionIntervalRandomness, 0f, 0.9f);
            particleLifetime = Mathf.Max(0.2f, particleLifetime);
            dirty = true;
        }

        private void Update()
        {
            if (dirty) Rebuild();
            if (!Application.IsPlaying(gameObject)) return;

            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (dt <= 0f) return;
            effectTime += dt;
            ApplyProperties();
            AdvanceSplashes(dt);
            if (particlesPerSecond > 0f)
            {
                timeUntilEmission -= dt;
                for (int i = 0; i < 12 && timeUntilEmission <= 0f; i++)
                {
                    SpawnSplash();
                    if (RandomRange(0f, 1f) < clusterChance)
                    {
                        SpawnSplash();
                        if (RandomRange(0f, 1f) < 0.35f) SpawnSplash();
                    }
                    float interval = (1f / particlesPerSecond) *
                        (1f + RandomRange(-emissionIntervalRandomness, emissionIntervalRandomness));
                    timeUntilEmission += interval;
                }
            }
            else timeUntilEmission = 0f;
            DrawSplashes(false);
        }

        private void Rebuild()
        {
            dirty = false;
            DisposeMeshes();
            var filter = GetComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = magmaMaterial;
            if (splashRenderer != null) splashRenderer.sharedMaterial = splashMaterial;
            if (filter == null || splashFilter == null || splashRenderer == null) return;

            bodyMesh = new Mesh { name = "Flow Magma Body (generated)", hideFlags = HideFlags.DontSave };
            float left = -width * 0.5f;
            float right = width * 0.5f;
            float bottom = -height * 0.5f;
            float top = height * 0.5f + MeshTopAllowance;
            bodyMesh.vertices = new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(left, top, 0f), new Vector3(right, top, 0f)
            };
            bodyMesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f + MeshTopAllowance / height), new Vector2(1f, 1f + MeshTopAllowance / height)
            };
            bodyMesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            bodyMesh.RecalculateBounds();
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

        private void ApplyProperties()
        {
            if (bodyMesh == null) return;
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) return;
            bodyProperties ??= new MaterialPropertyBlock();
            bodyProperties.SetFloat(EffectTimeId, effectTime);
            bodyProperties.SetFloat(FlowSpeedId, flowSpeed);
            bodyProperties.SetFloat(WaveAmplitudeId, waveAmplitude);
            bodyProperties.SetFloat(VerticalWarpStrengthId, verticalWarpStrength);
            bodyProperties.SetFloat(WaveFrequencyId, waveFrequency);
            bodyProperties.SetVector(BodySizeId, new Vector4(width, height, 0f, 0f));
            renderer.SetPropertyBlock(bodyProperties);
        }

        private void SpawnSplash()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                if (splashes[i].active) continue;
                float x = RandomRange(-width * 0.46f, width * 0.46f);
                float sizeTier = RandomRange(0f, 1f) < largeParticleChance ? 1.6f : 1f;
                splashes[i] = new Splash
                {
                    active = true,
                    maySettle = RandomRange(0f, 1f) < settleChance,
                    shrinking = RandomRange(0f, 1f) < shrinkingParticleChance,
                    heatBias = RandomRange(-0.07f, 0.07f),
                    x = x,
                    y = height * 0.5f + waveAmplitude * Mathf.Sin((x / width + 0.5f) * waveFrequency * Mathf.PI * 2f + effectTime * 1.7f),
                    vx = RandomRange(-0.45f, 0.45f) * launchSpeed * trajectoryRandomness,
                    vy = launchSpeed * (1f + RandomRange(-0.3f, 0.3f) * trajectoryRandomness),
                    size = particleSize * sizeTier * Mathf.Max(0.35f, 1f + RandomRange(-1f, 1f) * sizeRandomness),
                    lifetime = particleLifetime * RandomRange(0.8f, 1.2f)
                };
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
                    p.vy -= fallSpeed * dt;
                    if (p.y <= -height * 0.5f + p.size * 0.5f)
                    {
                        if (p.maySettle && Mathf.Abs(p.x) <= width * 0.5f)
                        {
                            p.settled = true;
                            p.y = -height * 0.5f + p.size * 0.5f;
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
            for (int i = 0; i < MaxParticles; i++)
            {
                Splash p = splashes[i];
                bool visible = p.active;
                if (preview && i < 7)
                {
                    visible = true;
                    p.x = (-0.37f + i * 0.12f) * width;
                    p.y = height * 0.5f + 0.08f + Mathf.Sin(i * 1.7f) * 0.09f;
                    p.size = particleSize;
                    if (i % 4 == 0) p.size *= 1.6f;
                    p.shrinking = i % 2 == 0;
                    p.heatBias = (i % 3 - 1) * 0.05f;
                    p.age = i * 0.18f;
                    p.lifetime = particleLifetime;
                }
                float normalizedAge = Mathf.Clamp01(p.age / Mathf.Max(p.lifetime, 0.2f));
                float coolingSize = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.95f, normalizedAge));
                float sizeRatio = p.shrinking ? Mathf.Lerp(1.15f, 0.18f, coolingSize) : 1f;
                float s = p.size * sizeRatio * (p.settled ? 0.65f : 1f) * 0.5f;
                int v = i * 4;
                splashVertices[v] = new Vector3(p.x - s, p.y - s, 0f);
                splashVertices[v + 1] = new Vector3(p.x + s, p.y - s, 0f);
                splashVertices[v + 2] = new Vector3(p.x - s, p.y + s, 0f);
                splashVertices[v + 3] = new Vector3(p.x + s, p.y + s, 0f);
                float lifeFade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1f, normalizedAge));
                float settleFade = p.settled ? 1f - Mathf.Clamp01(p.settleAge / settleDuration) : 1f;
                byte alpha = visible ? (byte)Mathf.RoundToInt(255f * lifeFade * settleFade) : (byte)0;
                float darkening = Mathf.Clamp01(normalizedAge + p.heatBias);
                var color = new Color32((byte)Mathf.RoundToInt(255f * darkening), 255, 255, alpha);
                splashColors[v] = splashColors[v + 1] = splashColors[v + 2] = splashColors[v + 3] = color;
            }
            splashMesh.vertices = splashVertices;
            splashMesh.colors32 = splashColors;
            splashMesh.bounds = new Bounds(Vector3.zero, new Vector3(width + launchSpeed * 3f + 1f, height + launchSpeed * 3f + 1f, 1f));
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
