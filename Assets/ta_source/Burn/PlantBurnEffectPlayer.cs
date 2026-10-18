using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.BurnEffects
{
    /// <summary>TA burn on existing artwork; its shared reveal material stays authoritative.</summary>
    [AddComponentMenu(""), DisallowMultipleComponent, DefaultExecutionOrder(1500)]
    public sealed class PlantBurnEffectPlayer : MonoBehaviour
    {
        private static readonly int EnabledId = Shader.PropertyToID("_BurnEnabled");
        private static readonly int ElapsedId = Shader.PropertyToID("_BurnElapsed");
        private static readonly int DurationId = Shader.PropertyToID("_BurnDuration");
        private static readonly int BoundsId = Shader.PropertyToID("_BurnWorldBounds");
        private static readonly int DirectionId = Shader.PropertyToID("_BurnTopDown");
        private static readonly int WidthId = Shader.PropertyToID("_FlameEdgeWidth");
        private static readonly int IntensityId = Shader.PropertyToID("_FlameEdgeIntensity");
        private static readonly int NoiseId = Shader.PropertyToID("_EdgeNoise");
        private static readonly int AshId = Shader.PropertyToID("_AshFadeSeconds");
        private static readonly int CoreId = Shader.PropertyToID("_FlameCoreColor");
        private static readonly int OuterId = Shader.PropertyToID("_FlameOuterColor");
        private static readonly int AshColorId = Shader.PropertyToID("_AshColor");
        private static readonly int PixelId = Shader.PropertyToID("_BurnPixelWorldSize");
        private readonly List<Renderer> targets = new List<Renderer>();
        private MaterialPropertyBlock properties;
        private PlantBurnEffectSettings settings;
        private Action completion;
        private float elapsed, duration, ashFade, pixelSize;
        private Vector4 worldBounds;
        private bool topDown;
        public bool IsPlaying { get; private set; }
        public int RendererCount => targets.Count;

        public static PlantBurnEffectPlayer GetOrCreate(GameObject owner) => owner == null ? null
            : owner.GetComponent<PlantBurnEffectPlayer>() ?? owner.AddComponent<PlantBurnEffectPlayer>();

        public bool Play(Renderer[] renderers, PlantBurnEffectSettings configuration,
            float durationOverrideSeconds, Action onComplete, bool fromTop = false, bool exactDuration = false,
            float cellWorldSize = 1f)
        {
            if (IsPlaying || !Application.isPlaying || !isActiveAndEnabled || renderers == null) return false;
            Cancel();
            settings = configuration != null ? configuration : PlantBurnEffectSettings.LoadDefault();
            float baseDuration = durationOverrideSeconds > 0 ? durationOverrideSeconds : settings != null ? settings.DurationSeconds : 1.08f;
            duration = Mathf.Max(.05f, baseDuration / (exactDuration ? 1f : settings != null ? settings.SpeedMultiplier : 1f));
            ashFade = exactDuration ? 0f : settings != null ? settings.AshFadeSeconds : .7f;
            topDown = fromTop;
            pixelSize = Mathf.Max(.001f, cellWorldSize) / 16f;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                // Do not turn invisible legacy placeholders into artwork.
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty(EnabledId) || targets.Contains(renderer)) continue;
                if (targets.Count == 0) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds);
                targets.Add(renderer);
            }
            if (targets.Count == 0) return false;
            worldBounds = new Vector4(bounds.min.y, Mathf.Max(bounds.max.y, bounds.min.y + .001f), 0, 0);
            completion = onComplete;
            elapsed = 0f;
            IsPlaying = true;
            Apply();
            return true;
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            elapsed += Time.deltaTime;
            // Reapply after sequence/frame presentation, preserving its newest atlas/mask data.
            Apply();
            if (elapsed < duration + ashFade) return;
            Action finished = completion;
            Cancel();
            finished?.Invoke();
        }

        private void Apply()
        {
            properties ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in targets)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(EnabledId, 1f);
                properties.SetFloat(ElapsedId, elapsed);
                properties.SetFloat(DurationId, duration);
                properties.SetVector(BoundsId, worldBounds);
                properties.SetFloat(DirectionId, topDown ? 1f : 0f);
                properties.SetFloat(PixelId, pixelSize);
                properties.SetFloat(WidthId, settings != null ? settings.FlameEdgeWidth : .059f);
                properties.SetFloat(IntensityId, settings != null ? settings.FlameEdgeIntensity : 1.9f);
                properties.SetFloat(NoiseId, settings != null ? settings.EdgeNoise : .867f);
                properties.SetFloat(AshId, settings != null ? settings.AshFadeSeconds : .7f);
                properties.SetColor(CoreId, settings != null ? settings.FlameCoreColor : new Color(1, .94f, .46f));
                properties.SetColor(OuterId, settings != null ? settings.FlameOuterColor : new Color(1, .19f, .025f));
                properties.SetColor(AshColorId, settings != null ? settings.AshColor : new Color(.19f, .17f, .16f));
                renderer.SetPropertyBlock(properties);
            }
        }

        public void Cancel()
        {
            properties ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in targets)
            {
                if (renderer == null) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetFloat(EnabledId, 0);
                renderer.SetPropertyBlock(properties);
            }
            targets.Clear();
            completion = null;
            IsPlaying = false;
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
