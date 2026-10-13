using UnityEngine;

namespace Project.SteamEfect
{
    /// <summary>
    /// Self-contained one-shot steam plume. Attach this prefab through
    /// WaterVisualFeature.steamVfxPrefab; it does not change water gameplay.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SteamBurstEffect : MonoBehaviour
    {
        private static readonly int EffectSizeProperty =
            Shader.PropertyToID("_EffectSize");
        private static readonly int EruptionHeightProperty =
            Shader.PropertyToID("_EruptionHeight");
        private static readonly int DensityProperty =
            Shader.PropertyToID("_Density");

        [SerializeField] private Material particleMaterial;
        [SerializeField, Range(0.35f, 1.2f)] private float lifetimeSeconds = 0.82f;
        [SerializeField, Min(0.05f)] private float waterCellWorldSize = 1f;
        [SerializeField, Range(16, 128)] private int cloudParticleCount = 56;
        [SerializeField, Range(4, 48)] private int coreParticleCount = 14;
        [SerializeField, Range(8, 96)] private int crownParticleCount = 32;

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
                Debug.LogWarning("SteamBurstEffect 缺少粒子材质。", this);
                Destroy(gameObject);
                return;
            }

            // The prefab may be instantiated under a rotated water container.
            // Keep the plume aligned with world up in every blue water prefab.
            transform.rotation = Quaternion.identity;
            CreateCloudLayer();
            CreateCoreLayer();
            CreateCrownLayer();
            Destroy(gameObject, lifetimeSeconds);
        }

        private void CreateCloudLayer()
        {
            ParticleSystem system = CreateParticleSystem("Steam Cloud");
            float effectSize = GetEffectSizeMultiplier();
            float eruptionHeight = GetEruptionHeightMultiplier();
            int particleCount = GetParticleCount(cloudParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.58f, 0.78f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                3.0f * eruptionHeight,
                4.2f * eruptionHeight);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.22f * effectSize,
                0.35f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.9f, 0.96f, 1f, 0.74f));

            ConfigureBurstShape(system, 6f, 0.08f * effectSize);
            ConfigureSoftDissolve(system, 0.55f, 1.45f, 1.15f);
            EmitOnce(system, particleCount);
        }

        private void CreateCoreLayer()
        {
            ParticleSystem system = CreateParticleSystem("Steam Flash Core");
            float effectSize = GetEffectSizeMultiplier();
            float eruptionHeight = GetEruptionHeightMultiplier();
            int particleCount = GetParticleCount(coreParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.24f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                5.2f * eruptionHeight,
                6.4f * eruptionHeight);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.12f * effectSize,
                0.23f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.94f, 0.78f, 0.92f));

            ConfigureBurstShape(system, 2.5f, 0.035f * effectSize);
            ConfigureSoftDissolve(system, 0.52f, 1.15f, 0.85f);
            EmitOnce(system, particleCount);
        }

        private void CreateCrownLayer()
        {
            ParticleSystem system = CreateParticleSystem("Steam Crown");
            float effectSize = GetEffectSizeMultiplier();
            float eruptionHeight = GetEruptionHeightMultiplier();
            int particleCount = GetParticleCount(crownParticleCount);
            var main = system.main;
            main.duration = lifetimeSeconds;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = particleCount;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.46f, 0.64f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(
                4.0f * eruptionHeight,
                5.2f * eruptionHeight);
            main.startSize = new ParticleSystem.MinMaxCurve(
                0.20f * effectSize,
                0.30f * effectSize);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.94f, 0.98f, 1f, 0.68f));

            ConfigureBurstShape(system, 14f, 0.07f * effectSize);
            ConfigureSoftDissolve(system, 0.38f, 1.4f, 1.25f);
            EmitOnce(system, particleCount);
        }

        private ParticleSystem CreateParticleSystem(string childName)
        {
            var child = new GameObject(childName);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one *
                Mathf.Max(0.05f, waterCellWorldSize);

            ParticleSystem system = child.AddComponent<ParticleSystem>();
            var emission = system.emission;
            emission.enabled = false;

            var renderer = child.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.5f;
            renderer.sortingFudge = 0.15f;
            renderer.sharedMaterial = particleMaterial;

            var noise = system.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.34f);
            noise.frequency = 1.45f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.82f);
            noise.damping = true;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            return system;
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
                    new GradientAlphaKey(0.92f, 0f),
                    new GradientAlphaKey(0.76f, 0.18f),
                    new GradientAlphaKey(0f, 1f)
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);

            var size = system.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(
                new Keyframe(0f, initialSize),
                new Keyframe(0.34f, midSize),
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
                3f);
        }

        private float GetEruptionHeightMultiplier()
        {
            if (particleMaterial == null ||
                !particleMaterial.HasProperty(EruptionHeightProperty))
            {
                return 1f;
            }

            return Mathf.Clamp(
                particleMaterial.GetFloat(EruptionHeightProperty),
                0.5f,
                4f);
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
