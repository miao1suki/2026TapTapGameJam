using System;
using UnityEngine;

namespace Project.Rain
{
    /// <summary>Animates many atlas droplets in one local XY mesh without spawning GameObjects.</summary>
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
        [SerializeField, Min(0.01f)] private float dropletSize = 0.35f;
        [SerializeField, Range(0f, 100f)] private float density = 18f;
        [SerializeField, Min(0.01f)] private float fallSpeed = 3.2f;
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

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            Build();
        }

        private void OnDisable()
        {
            if (mesh == null) return;
            GetComponent<MeshFilter>().sharedMesh = null;
            Destroy(mesh);
            mesh = null;
        }

        private void OnValidate()
        {
            areaWidth = Mathf.Max(0.1f, areaWidth);
            areaHeight = Mathf.Max(0.1f, areaHeight);
            dropletSize = Mathf.Max(0.01f, dropletSize);
            fallSpeed = Mathf.Max(0.01f, fallSpeed);
            noiseScale = Mathf.Max(0.01f, noiseScale);
        }

        private void Build()
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = rainMaterial;
            if (rainMaterial == null)
            {
                Debug.LogWarning("雨滴缺少材质。", this);
                enabled = false;
                return;
            }

            if (mesh != null) Destroy(mesh);
            int count = Mathf.Clamp(Mathf.RoundToInt(areaWidth * density), 0, MaxDrops);
            drops = new Drop[count];
            vertices = new Vector3[count * 4];
            uvs = new Vector2[count * 4];
            colors = new Color32[count * 4];
            int[] triangles = new int[count * 6];
            random = new System.Random(randomSeed ^ GetInstanceID());
            mesh = new Mesh { name = "Rain Sprite Field (runtime)" };
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
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(areaWidth + dropletSize, areaHeight + dropletSize, 0.1f));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            lastWidth = areaWidth;
            lastDensity = density;
            Animate(0f);
        }

        private void Update()
        {
            if (mesh == null) return;
            if (Mathf.Abs(lastWidth - areaWidth) > 0.001f || Mathf.Abs(lastDensity - density) > 0.001f)
            {
                Build();
                return;
            }
            Animate(Time.deltaTime);
        }

        private void ResetDrop(int index, bool initial)
        {
            float x = NextFloat() * areaWidth - areaWidth * 0.5f;
            float noise = Mathf.PerlinNoise((x + randomSeed * 0.37f) * noiseScale, index * 0.37f + randomSeed * 0.13f);
            float reachChance = Mathf.Clamp01(reachBottomChance + (noise - 0.5f) * noiseStrength * randomness);
            bool reachesBottom = NextFloat() < reachChance;
            float endProgress = reachesBottom ? 1f : Mathf.Lerp(0.28f, 0.82f, noise);
            float speedFactor = 1f + (NextFloat() * 2f - 1f) * randomness * 0.38f;
            float speed = Mathf.Max(0.05f, fallSpeed * speedFactor);
            float travelTime = areaHeight * endProgress / speed;
            drops[index] = new Drop
            {
                x = x,
                age = initial ? Mathf.Lerp(-0.45f, travelTime, NextFloat()) : -Mathf.Lerp(0.08f, 0.7f, NextFloat()) * randomness,
                speed = speed,
                endProgress = endProgress,
                framePhase = NextFloat() * FrameCount * randomness,
                animationScale = 1f + (NextFloat() * 2f - 1f) * randomness * 0.3f,
                swayPhase = NextFloat() * Mathf.PI * 2f
            };
        }

        private void Animate(float deltaTime)
        {
            float halfSize = dropletSize * 0.5f;
            int row = color == RainColor.Red ? 160 : color == RainColor.Blue ? 192 : 224;
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

                float progress = drop.age * drop.speed / areaHeight;
                float y = areaHeight * (0.5f - progress) - halfSize;
                float sway = Mathf.Sin(drop.age * 2.1f + drop.swayPhase) * randomness * noiseStrength * 0.06f;
                float x = drop.x + sway;
                int v = i * 4;
                vertices[v] = new Vector3(x - halfSize, y - halfSize, 0f);
                vertices[v + 1] = new Vector3(x + halfSize, y - halfSize, 0f);
                vertices[v + 2] = new Vector3(x - halfSize, y + halfSize, 0f);
                vertices[v + 3] = new Vector3(x + halfSize, y + halfSize, 0f);

                int frame = Mathf.FloorToInt(Mathf.Max(0f, drop.age) * frameRate * drop.animationScale + drop.framePhase) % FrameCount;
                float u0 = (96f + frame * 32f + 0.5f) / AtlasWidth;
                float u1 = (96f + frame * 32f + 15.5f) / AtlasWidth;
                float v0 = (AtlasHeight - row - 15.5f) / AtlasHeight;
                float v1 = (AtlasHeight - row - 0.5f) / AtlasHeight;
                uvs[v] = new Vector2(u0, v0);
                uvs[v + 1] = new Vector2(u1, v0);
                uvs[v + 2] = new Vector2(u0, v1);
                uvs[v + 3] = new Vector2(u1, v1);

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
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(areaWidth + dropletSize, areaHeight + dropletSize, 0.1f));
        }

        private float NextFloat() => (float)random.NextDouble();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.55f, 0.8f, 1f, 0.65f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaWidth, areaHeight, 0f));
        }
    }
}
