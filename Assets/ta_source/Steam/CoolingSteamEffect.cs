using UnityEngine;

namespace Project.Steam
{
    /// <summary>
    /// A compact, pixelated cooling-steam puff that drifts upward above a
    /// solidified lava block. It only owns its visual particles.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoolingSteamEffect : MonoBehaviour
    {
        private const int SteamSortingOrder = 100;
        private const float SurfaceOffset = 0.015f;

        private static readonly int EffectSizeProperty =
            Shader.PropertyToID("_EffectSize");
        private static readonly int RiseSpeedProperty =
            Shader.PropertyToID("_RiseSpeed");
        private static readonly int DensityProperty =
            Shader.PropertyToID("_Density");

        [SerializeField] private Material particleMaterial;
        [SerializeField, Range(0.8f, 1.8f)] private float lifetimeSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float cellWorldSize = 1f;
        [SerializeField, Range(8, 48)] private int cloudParticleCount = 28;
        [SerializeField, Range(0, 16)] private int coreParticleCount = 6;
        [SerializeField, Range(4, 32)] private int crownParticleCount = 18;

        private bool hasPlayed;

        private void OnEnable()
        {
            if (!Application.isPlaying || hasPlayed)
            {
                return;
            }

            hasPlayed = true;
            if (particleMaterial == null)
            {
                Debug.LogWarning("CoolingSteamEffect 缺少粒子材质。", this);
                Destroy(gameObject);
                return;
            }

            transform.rotation = Quaternion.identity;
            AlignToVisibleBlockSurface();
            CreateCloudLayer();
            CreateCoreLayer();
            CreateCrownLayer();
            Destroy(gameObject, lifetimeSeconds);
        }

        private void AlignToVisibleBlockSurface()
        {
            Collider sourceCollider = transform.parent != null
                ? transform.parent.GetComponentInChildren<Collider>()
                : null;
            Camera viewCamera = Camera.main;
            if (sourceCollider == null || viewCamera == null)
            {
                return;
            }

            Bounds bounds = sourceCollider.bounds;
            Vector3 target = bounds.center + Vector3.up * bounds.extents.y * 0.4f;
            Vector3 rayDirection = target - viewCamera.transform.position;
            float rayLength = rayDirection.magnitude;
            if (rayLength <= 0.001f)
            {
                return;
            }

            Ray ray = new Ray(viewCamera.transform.position, rayDirection / rayLength);
            if (sourceCollider.Raycast(ray, out RaycastHit hit, rayLength + bounds.size.magnitude))
            {
                transform.position = hit.point + hit.normal * SurfaceOffset;
            }
        }

        private void CreateCloudLayer()
        {
            ParticleSystem system = CreateParticleSystem("冷却雾团");
            float effectSize = GetEffectSizeMultiplier();
            float riseSpeed = GetRiseSpeedMultiplier();
            int particleCount = GetParticleCount(cloudParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.08f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.32f * riseSpeed,
                0.52f * riseSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.27f * effectSize,
                0.42f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.88f, 0.95f, 1f, 0.82f));

            ConfigureBurstShape(system, 24f, 0.11f * effectSize);
            ConfigureSoftDissolve(system, 0.78f, 1.38f, 1.08f);
            EmitOnce(system, particleCount);
        }

        private void CreateCoreLayer()
        {
            ParticleSystem system = CreateParticleSystem("冷却雾芯");
            float effectSize = GetEffectSizeMultiplier();
            float riseSpeed = GetRiseSpeedMultiplier();
            int particleCount = GetParticleCount(coreParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.42f, 0.64f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.48f * riseSpeed,
                0.72f * riseSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.16f * effectSize,
                0.25f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.88f, 0.96f, 1f, 0.62f));

            ConfigureBurstShape(system, 12f, 0.045f * effectSize);
            ConfigureSoftDissolve(system, 0.82f, 1.26f, 1.02f);
            EmitOnce(system, particleCount);
        }

        private void CreateCrownLayer()
        {
            ParticleSystem system = CreateParticleSystem("上浮雾边");
            float effectSize = GetEffectSizeMultiplier();
            float riseSpeed = GetRiseSpeedMultiplier();
            int particleCount = GetParticleCount(crownParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.68f, 0.94f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                0.24f * riseSpeed,
                0.42f * riseSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.23f * effectSize,
                0.36f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.9f, 0.97f, 1f, 0.7f));

            ConfigureBurstShape(system, 34f, 0.1f * effectSize);
            ConfigureSoftDissolve(system, 0.72f, 1.42f, 1.08f);
            EmitOnce(system, particleCount);
        }

        private ParticleSystem CreateParticleSystem(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one * GetCellWorldSize();

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            var emission = system.emission;
            emission.enabled = false;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.65f;
            renderer.sortingFudge = 0.2f;
            renderer.sortingOrder = SteamSortingOrder;
            renderer.sharedMaterial = particleMaterial;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.16f);
            noise.frequency = 0.9f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.28f);
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            return system;
        }

        private float GetCellWorldSize()
        {
            Collider sourceCollider = transform.parent != null
                ? transform.parent.GetComponentInChildren<Collider>()
                : null;
            if (sourceCollider != null && sourceCollider.bounds.size.y > 0.05f)
            {
                return sourceCollider.bounds.size.y;
            }

            return Mathf.Max(0.05f, cellWorldSize);
        }

        private static void ConfigureBurstShape(
            ParticleSystem system,
            float coneAngle,
            float coneRadius)
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.angle = coneAngle;
            shape.radius = coneRadius;
            shape.length = 0.08f;
            shape.radiusThickness = 1f;
            shape.scale = Vector3.one;
        }

        private static void ConfigureSoftDissolve(
            ParticleSystem system,
            float initialSize,
            float finalSize,
            float midSize)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.86f, 0f),
                    new GradientAlphaKey(0.72f, 0.22f),
                    new GradientAlphaKey(0.3f, 0.68f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, initialSize),
                new Keyframe(0.36f, midSize),
                new Keyframe(1f, finalSize));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void EmitOnce(ParticleSystem system, int count)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Emit(Mathf.Max(1, count));
            system.Play();
        }

        private float GetEffectSizeMultiplier()
        {
            if (particleMaterial == null ||
                !particleMaterial.HasProperty(EffectSizeProperty))
            {
                return 1f;
            }

            return Mathf.Clamp(
                particleMaterial.GetFloat(EffectSizeProperty),
                0.5f,
                1.8f);
        }

        private float GetRiseSpeedMultiplier()
        {
            if (particleMaterial == null ||
                !particleMaterial.HasProperty(RiseSpeedProperty))
            {
                return 0.8f;
            }

            return Mathf.Clamp(
                particleMaterial.GetFloat(RiseSpeedProperty),
                0.35f,
                1.25f);
        }

        private int GetParticleCount(int baseCount)
        {
            float density = particleMaterial != null &&
                particleMaterial.HasProperty(DensityProperty)
                ? Mathf.Clamp(particleMaterial.GetFloat(DensityProperty), 0.1f, 2f)
                : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(baseCount * density));
        }
    }
}
