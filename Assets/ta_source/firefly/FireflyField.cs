using System;
using Project.BlockFeatures;
using Project.ObjectArtwork;
using UnityEngine;

namespace Project.Fireflies
{
    /// <summary>A bounded, deterministic flock of single-pixel fireflies rendered in one mesh.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class FireflyField : MonoBehaviour
    {
        private const int MaximumCount = 128;
        private const int MaximumLights = 4;

        [Header("活动框与数量")]
        [SerializeField] private Vector2 areaSize = new Vector2(3f, 2f);
        [SerializeField, Range(0, MaximumCount)] private int fireflyCount = 22;
        [SerializeField, InspectorName("可见平面（格）")] private float renderDepthCells = -.25f;

        [Header("游动与随机性")]
        [SerializeField, Min(0.01f)] private float flightSpeed = 0.22f;
        [SerializeField, Range(0.5f, 12f)] private float flightPrecision = 3.5f;
        [SerializeField, Range(0.02f, 3f)] private float flowSpeed = 0.45f;
        [SerializeField, Range(0f, 2f)] private float wanderStrength = 1f;
        [SerializeField, Range(0f, 2f)] private float flockStrength = 0.3f;
        [SerializeField] private int layoutSeed = 107;
        [SerializeField] private int motionSeed = 293;
        [SerializeField] private int pulseSeed = 521;

        [Header("接近与受惊")]
        [SerializeField] private Transform threat;
        [SerializeField] private bool findPlayerByTag = true;
        [SerializeField, Min(0.05f)] private float detectionRadius = 0.8f;
        [SerializeField, Min(0.02f)] private float fleeSpeed = 0.58f;
        [SerializeField, Min(0.05f)] private float calmDownSeconds = 1.4f;

        [Header("微光与呼吸")]
        [SerializeField] private Material haloMaterial;
        [SerializeField] private Material pixelMaterial;
        [SerializeField] private Color haloColor = new Color(0.72f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color pixelColor = new Color(1f, 0.98f, 0.58f, 1f);
        [SerializeField, Min(0.02f)] private float glowRadius = 0.22f;
        [SerializeField, Range(0f, 1f)] private float glowIntensity = 0.17f;
        [SerializeField, Range(0.2f, 1f)] private float minimumPulseSize = 0.65f;
        [SerializeField, Range(1f, 2f)] private float maximumPulseSize = 1.35f;
        [SerializeField, Range(0.05f, 3f)] private float pulseSpeed = 0.7f;
        [SerializeField, Range(0, MaximumLights)] private int realtimeLightCount;
        [SerializeField, Range(0f, 1f)] private float lightIntensity = 0.16f;
        [SerializeField, Min(0.05f)] private float lightRange = 0.5f;
        [SerializeField] private LayerMask illuminatedLayers = ~0;

        private struct Fly
        {
            public Vector2 position;
            public Vector2 velocity;
            public Vector2 lastFleeDirection;
            public float noiseX;
            public float noiseY;
            public float pulsePhase;
            public float pulseRate;
            public float speedMultiplier;
            public float scare;
            public float brightness;
        }

        private Fly[] flies;
        private Vector2[] oldPositions;
        private Vector2[] oldVelocities;
        private Vector3[] vertices;
        private Vector2[] uvs;
        private Color32[] colors;
        private Mesh mesh;
        private Light[] lights;
        private bool dirty = true;
        private float elapsed;
        private float playerSearchTimer;
        private float cell = 1f;
        private Vector3 renderScale = Vector3.one;
        private Matrix4x4 worldToLocal;

        public Vector2 AreaCells => areaSize;
        public int Count => flies?.Length ?? 0;
        public float CellWorldSize => cell;
        public int LayoutSeed => layoutSeed;
        public int MotionSeed => motionSeed;
        public int PulseSeed => pulseSeed;
        public Mesh GeneratedMesh => mesh;
        public int ActiveLightCount => lights?.Length ?? 0;
        public int GeometryRevision { get; private set; }
        public float RenderDepthCells => renderDepthCells;

        private void OnEnable()
        {
            dirty = true;
            Rebuild();
        }

        private void OnValidate()
        {
            areaSize.x = Mathf.Max(0.1f, areaSize.x);
            areaSize.y = Mathf.Max(0.1f, areaSize.y);
            fleeSpeed = Mathf.Max(flightSpeed * 1.1f, fleeSpeed);
            fireflyCount = Mathf.Clamp(fireflyCount, 0, MaximumCount);
            realtimeLightCount = Mathf.Clamp(realtimeLightCount, 0, MaximumLights);
            if (!float.IsFinite(renderDepthCells)) renderDepthCells = -.25f;
            dirty = true;
        }

        private void OnTransformParentChanged() => dirty = true;

        private void OnDisable()
        {
            DestroyLights();
            DestroyMesh();
        }

        private void Update()
        {
            if (dirty) { Rebuild(); return; }
            if (flies == null || mesh == null) return;

            if (Application.IsPlaying(gameObject))
            {
                float dt = Mathf.Min(Time.deltaTime, 0.05f);
                elapsed += dt;
                SearchForPlayer(dt);
                RefreshCoordinates();
                Simulate(dt);
                Draw();
                UpdateLights();
            }
            else if (transform.hasChanged) Draw();
        }

        private void Rebuild()
        {
            dirty = false;
            elapsed = 0f;
            cell = GridCellSizeUtility.Resolve(this);
            DestroyMesh();
            if (haloMaterial == null || pixelMaterial == null)
            {
                flies = null;
                DestroyLights();
                return;
            }

            int count = Mathf.Clamp(fireflyCount, 0, MaximumCount);
            flies = new Fly[count];
            oldPositions = new Vector2[count];
            oldVelocities = new Vector2[count];
            vertices = new Vector3[count * 8];
            uvs = new Vector2[vertices.Length];
            colors = new Color32[vertices.Length];
            int[] haloTriangles = new int[count * 6];
            int[] pixelTriangles = new int[count * 6];
            var layoutRandom = new System.Random(layoutSeed);
            var motionRandom = new System.Random(motionSeed);
            var pulseRandom = new System.Random(pulseSeed);

            for (int i = 0; i < count; i++)
            {
                float angle = (float)motionRandom.NextDouble() * Mathf.PI * 2f;
                flies[i] = new Fly
                {
                    position = new Vector2(((float)layoutRandom.NextDouble() - 0.5f) * areaSize.x,
                        ((float)layoutRandom.NextDouble() - 0.5f) * areaSize.y),
                    velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * flightSpeed,
                    lastFleeDirection = Vector2.up,
                    noiseX = (float)motionRandom.NextDouble() * 1000f,
                    noiseY = (float)motionRandom.NextDouble() * 1000f,
                    speedMultiplier = Mathf.Lerp(0.75f, 1.2f, (float)motionRandom.NextDouble()),
                    pulsePhase = (float)pulseRandom.NextDouble() * Mathf.PI * 2f,
                    pulseRate = Mathf.Lerp(0.75f, 1.3f, (float)pulseRandom.NextDouble())
                };

                int vertex = i * 8;
                for (int quad = 0; quad < 2; quad++)
                {
                    int v = vertex + quad * 4;
                    uvs[v] = new Vector2(0f, 0f);
                    uvs[v + 1] = new Vector2(1f, 0f);
                    uvs[v + 2] = new Vector2(0f, 1f);
                    uvs[v + 3] = new Vector2(1f, 1f);
                    int[] target = quad == 0 ? haloTriangles : pixelTriangles;
                    int t = i * 6;
                    target[t] = v;
                    target[t + 1] = v + 2;
                    target[t + 2] = v + 1;
                    target[t + 3] = v + 1;
                    target[t + 4] = v + 2;
                    target[t + 5] = v + 3;
                }
            }

            mesh = new Mesh { name = "Firefly field (generated)", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors32 = colors;
            mesh.subMeshCount = 2;
            mesh.SetTriangles(haloTriangles, 0, false);
            mesh.SetTriangles(pixelTriangles, 1, false);
            mesh.bounds = new Bounds(Vector3.zero,
                new Vector3(areaSize.x + glowRadius * 4f, areaSize.y + glowRadius * 4f, 2f));
            GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = new[] { haloMaterial, pixelMaterial };
            meshRenderer.sortingOrder = 20;
            meshRenderer.renderingLayerMask &= ~128u;
            Draw();
            if (Application.IsPlaying(gameObject)) UpdateLights();
        }

        public void Configure(Vector2 cells, int count, int layout, int motion, int pulse)
        {
            areaSize = new Vector2(Mathf.Max(.1f, cells.x), Mathf.Max(.1f, cells.y));
            fireflyCount = Mathf.Clamp(count, 0, MaximumCount);
            layoutSeed = layout;
            motionSeed = motion;
            pulseSeed = pulse;
            if (isActiveAndEnabled) Rebuild();
        }

        /// <summary>Moves only the visible plane. The area root and its room-space coordinates stay unchanged.</summary>
        public void SetRenderDepthCells(float value)
        {
            renderDepthCells = float.IsFinite(value) ? value : -.25f;
            if (isActiveAndEnabled && mesh != null) Draw();
        }

#if UNITY_EDITOR
        public void RefreshPreview()
        {
            if (!Application.IsPlaying(gameObject) && isActiveAndEnabled) Rebuild();
        }
#endif

        private void SearchForPlayer(float dt)
        {
            if (threat != null || !findPlayerByTag) return;
            playerSearchTimer -= dt;
            if (playerSearchTimer > 0f) return;
            playerSearchTimer = 1f;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) threat = player.transform;
        }

        private void Simulate(float dt)
        {
            int count = flies.Length;
            for (int i = 0; i < count; i++)
            {
                oldPositions[i] = flies[i].position;
                oldVelocities[i] = flies[i].velocity;
            }

            Vector3 actorLocal = threat == null ? Vector3.zero : transform.InverseTransformPoint(threat.position);
            Vector2 localThreat = threat == null ? Vector2.positiveInfinity :
                new Vector2(actorLocal.x / renderScale.x, actorLocal.y / renderScale.y);
            float neighborRadiusSquared = 0.42f * 0.42f;
            float separationRadiusSquared = 0.13f * 0.13f;
            float halfX = areaSize.x * 0.5f;
            float halfY = areaSize.y * 0.5f;
            float margin = Mathf.Min(0.3f, Mathf.Min(halfX, halfY) * 0.5f);

            for (int i = 0; i < count; i++)
            {
                Fly fly = flies[i];
                Vector2 position = oldPositions[i];
                Vector2 alignment = Vector2.zero;
                Vector2 cohesion = Vector2.zero;
                Vector2 separation = Vector2.zero;
                int neighbors = 0;
                for (int j = 0; j < count; j++)
                {
                    if (i == j) continue;
                    Vector2 away = position - oldPositions[j];
                    float distanceSquared = away.sqrMagnitude;
                    if (distanceSquared > neighborRadiusSquared || distanceSquared < 0.00001f) continue;
                    alignment += oldVelocities[j].normalized;
                    cohesion += oldPositions[j];
                    neighbors++;
                    if (distanceSquared < separationRadiusSquared)
                        separation += away / distanceSquared;
                }

                Vector2 flock = Vector2.zero;
                if (neighbors > 0)
                    flock = alignment / neighbors * 0.45f +
                        ((cohesion / neighbors - position).normalized * 0.35f) +
                        separation.normalized * 1.25f;

                float noiseTime = elapsed * flowSpeed;
                Vector2 wander = new Vector2(
                    Mathf.PerlinNoise(fly.noiseX, noiseTime) - 0.5f,
                    Mathf.PerlinNoise(fly.noiseY, noiseTime) - 0.5f).normalized;
                Vector2 edge = Vector2.zero;
                if (position.x < -halfX + margin) edge.x = 1f;
                else if (position.x > halfX - margin) edge.x = -1f;
                if (position.y < -halfY + margin) edge.y = 1f;
                else if (position.y > halfY - margin) edge.y = -1f;

                Vector2 awayFromThreat = position - localThreat;
                bool startled = threat != null && awayFromThreat.sqrMagnitude < detectionRadius * detectionRadius;
                fly.scare = Mathf.MoveTowards(fly.scare, startled ? 1f : 0f,
                    dt * (startled ? 4f : 1f / calmDownSeconds));
                if (startled && awayFromThreat.sqrMagnitude > 0.0001f)
                    fly.lastFleeDirection = awayFromThreat.normalized;

                Vector2 desired = wander * wanderStrength + flock * flockStrength + edge * 2.5f +
                    fly.lastFleeDirection * fly.scare * 4f;
                if (desired.sqrMagnitude < 0.001f) desired = fly.velocity.normalized;
                float speed = Mathf.Lerp(flightSpeed * fly.speedMultiplier,
                    Mathf.Max(fleeSpeed, flightSpeed * 1.1f), fly.scare);
                fly.velocity = Vector2.Lerp(fly.velocity, desired.normalized * speed,
                    1f - Mathf.Exp(-flightPrecision * dt));
                fly.position += fly.velocity * dt;
                fly.position.x = Mathf.Clamp(fly.position.x, -halfX, halfX);
                fly.position.y = Mathf.Clamp(fly.position.y, -halfY, halfY);
                flies[i] = fly;
            }
        }

        private void Draw()
        {
            if (flies == null || mesh == null) return;
            RefreshCoordinates();
            for (int i = 0; i < flies.Length; i++)
            {
                Fly fly = flies[i];
                float phase = fly.pulsePhase + elapsed * pulseSpeed * fly.pulseRate * Mathf.PI * 2f;
                float breath = (Mathf.Sin(phase) + 1f) * 0.5f;
                breath = breath * breath * (3f - 2f * breath);
                float radius = glowRadius * Mathf.Lerp(minimumPulseSize, maximumPulseSize, breath);
                fly.brightness = Mathf.Lerp(0.38f, 1f, breath);
                flies[i] = fly;
                int v = i * 8;
                // Both submeshes share the same adjustable visible plane. ZTest stays LEqual:
                // ordinary transparent art is followed by this overlay, while opaque wall
                // front faces (normally -0.5 cell) still occlude the default -0.25 plane.
                SetQuad(v, fly.position, radius, renderDepthCells);
                SetQuad(v + 4, fly.position, .5f / GridArtworkMetrics.PixelsPerCell, renderDepthCells);
                Color32 halo = new Color(haloColor.r, haloColor.g, haloColor.b,
                    glowIntensity * fly.brightness * haloColor.a);
                Color32 pixel = new Color(pixelColor.r, pixelColor.g, pixelColor.b,
                    Mathf.Lerp(0.62f, 1f, fly.brightness) * pixelColor.a);
                for (int q = 0; q < 4; q++)
                {
                    colors[v + q] = halo;
                    colors[v + 4 + q] = pixel;
                }
            }
            mesh.vertices = vertices;
            mesh.colors32 = colors;
            mesh.RecalculateBounds();
            GeometryRevision++;
            if (!Application.IsPlaying(gameObject)) transform.hasChanged = false;
        }

        private void RefreshCoordinates()
        {
            Vector3 scale = transform.lossyScale;
            renderScale = new Vector3(cell / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                cell / Mathf.Max(.001f, Mathf.Abs(scale.y)), cell / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            worldToLocal = transform.worldToLocalMatrix;
        }

        private Vector3 SnappedCenter(Vector2 point, float z)
        {
            Vector3 center = transform.TransformPoint(Vector3.Scale(new Vector3(point.x, point.y, z), renderScale));
            float pixel = GridArtworkMetrics.PixelWorldSize(cell);
            center.x = (Mathf.Round(center.x / pixel - .5f) + .5f) * pixel;
            center.y = (Mathf.Round(center.y / pixel - .5f) + .5f) * pixel;
            return worldToLocal.MultiplyPoint3x4(center);
        }

        private void SetQuad(int vertex, Vector2 center, float halfSize, float z)
        {
            Vector3 localCenter = SnappedCenter(center, z);
            Vector3 x = worldToLocal.MultiplyVector(Vector3.right * (halfSize * cell));
            Vector3 y = worldToLocal.MultiplyVector(Vector3.up * (halfSize * cell));
            vertices[vertex] = localCenter - x - y;
            vertices[vertex + 1] = localCenter + x - y;
            vertices[vertex + 2] = localCenter - x + y;
            vertices[vertex + 3] = localCenter + x + y;
        }

        private void UpdateLights()
        {
            int wanted = Mathf.Min(Mathf.Clamp(realtimeLightCount, 0, MaximumLights), flies.Length);
            if (wanted == 0 || lightIntensity <= 0f)
            {
                DestroyLights();
                return;
            }
            EnsureLights(wanted);
            for (int slot = 0; slot < wanted; slot++)
            {
                Fly fly = flies[slot * flies.Length / wanted];
                Light light = lights[slot];
                light.transform.localPosition = SnappedCenter(fly.position, -.25f);
                light.color = haloColor;
                light.intensity = lightIntensity * fly.brightness;
                light.range = lightRange * cell * Mathf.Lerp(minimumPulseSize, maximumPulseSize, fly.brightness);
                light.cullingMask = illuminatedLayers;
            }
        }

        private void EnsureLights(int count)
        {
            if (lights != null && lights.Length == count) return;
            DestroyLights();
            lights = new Light[count];
            for (int i = 0; i < count; i++)
            {
                var lightObject = new GameObject("Firefly local light") { hideFlags = HideFlags.DontSave };
                lightObject.transform.SetParent(transform, false);
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForcePixel;
                lights[i] = light;
            }
        }

        private void DestroyLights()
        {
            if (lights == null) return;
            foreach (Light light in lights)
            {
                if (light == null) continue;
                if (Application.IsPlaying(gameObject)) Destroy(light.gameObject);
                else DestroyImmediate(light.gameObject);
            }
            lights = null;
        }

        private void DestroyMesh()
        {
            if (mesh == null) return;
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh == mesh) filter.sharedMesh = null;
            if (Application.IsPlaying(gameObject)) Destroy(mesh);
            else DestroyImmediate(mesh);
            mesh = null;
        }

        /// <summary>Assign a player or other actor at runtime; pass null to resume tag lookup.</summary>
        public void SetThreat(Transform actor)
        {
            threat = actor;
            playerSearchTimer = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.7f, 1f, 0.35f, 0.9f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * cell);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(areaSize.x, areaSize.y, 0.01f));
        }
    }
}
