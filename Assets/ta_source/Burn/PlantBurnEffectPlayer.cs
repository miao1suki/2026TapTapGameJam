using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.BurnEffects
{
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class PlantBurnEffectPlayer : MonoBehaviour
    {
        private const string MaterialResourcePath = "Burn/PlantBurnEffect";

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int SourceTintId = Shader.PropertyToID("_SourceTint");
        private static readonly int UvRectId = Shader.PropertyToID("_BurnUvRect");
        private static readonly int YBoundsId = Shader.PropertyToID("_BurnYBounds");
        private static readonly int ElapsedId = Shader.PropertyToID("_BurnElapsed");
        private static readonly int DurationId = Shader.PropertyToID("_BurnDuration");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_FlameEdgeWidth");
        private static readonly int EdgeIntensityId = Shader.PropertyToID("_FlameEdgeIntensity");
        private static readonly int PixelationId = Shader.PropertyToID("_Pixelation");
        private static readonly int EdgeNoiseId = Shader.PropertyToID("_EdgeNoise");
        private static readonly int AshFadeId = Shader.PropertyToID("_AshFadeSeconds");
        private static readonly int CoreColorId = Shader.PropertyToID("_FlameCoreColor");
        private static readonly int OuterColorId = Shader.PropertyToID("_FlameOuterColor");
        private static readonly int AshColorId = Shader.PropertyToID("_AshColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        private sealed class RendererState
        {
            public Renderer Renderer;
            public bool WasEnabled;
            public Material[] OriginalMaterials;
            public Material[] BurnMaterials;
        }

        private static Material materialTemplate;
        private readonly List<RendererState> rendererStates =
            new List<RendererState>();
        private Coroutine playback;
        private Action completion;

        public bool IsPlaying => playback != null;

        public static PlantBurnEffectPlayer GetOrCreate(GameObject owner)
        {
            if (owner == null)
            {
                return null;
            }

            PlantBurnEffectPlayer player =
                owner.GetComponent<PlantBurnEffectPlayer>();
            return player != null
                ? player
                : owner.AddComponent<PlantBurnEffectPlayer>();
        }

        public bool Play(
            Renderer[] renderers,
            PlantBurnEffectSettings settings,
            float durationOverrideSeconds,
            Action onComplete)
        {
            if (IsPlaying)
            {
                return false;
            }

            RestoreBindings();
            if (settings == null)
            {
                settings = PlantBurnEffectSettings.LoadDefault();
            }

            Material template = LoadMaterialTemplate();
            if (template == null || renderers == null)
            {
                return false;
            }

            float speed = settings != null
                ? settings.SpeedMultiplier
                : 1f;
            float baseDuration = durationOverrideSeconds > 0f
                ? durationOverrideSeconds
                : settings != null
                    ? settings.DurationSeconds
                    : 2.25f;
            float duration = Mathf.Max(.1f, baseDuration) / Mathf.Max(.25f, speed);
            float ashFade = settings != null
                ? settings.AshFadeSeconds
                : .7f;

            var seen = new HashSet<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled || !seen.Add(renderer) ||
                    !IsSupportedRenderer(renderer))
                {
                    continue;
                }

                Material[] sourceMaterials = renderer.sharedMaterials;
                if (sourceMaterials == null || sourceMaterials.Length == 0)
                {
                    sourceMaterials = new[] { renderer.sharedMaterial };
                }

                var burnMaterials = new Material[sourceMaterials.Length];
                bool valid = true;
                for (int materialIndex = 0;
                     materialIndex < burnMaterials.Length;
                     materialIndex++)
                {
                    Material sourceMaterial = sourceMaterials[materialIndex];
                    Material burnMaterial = new Material(template)
                    {
                        name = sourceMaterial != null
                            ? $"{sourceMaterial.name} (植物燃烧)"
                            : "植物燃烧材质"
                    };
                    ConfigureMaterial(
                        burnMaterial,
                        renderer,
                        sourceMaterial,
                        settings,
                        materialIndex);
                    burnMaterials[materialIndex] = burnMaterial;
                    if (burnMaterial.shader == null ||
                        !burnMaterial.shader.isSupported)
                    {
                        valid = false;
                    }
                }

                if (!valid)
                {
                    DestroyMaterials(burnMaterials);
                    continue;
                }

                rendererStates.Add(new RendererState
                {
                    Renderer = renderer,
                    WasEnabled = renderer.enabled,
                    OriginalMaterials = sourceMaterials,
                    BurnMaterials = burnMaterials
                });
                renderer.sharedMaterials = burnMaterials;
            }

            if (rendererStates.Count == 0)
            {
                return false;
            }

            for (int rendererIndex = 0;
                 rendererIndex < rendererStates.Count;
                 rendererIndex++)
            {
                Material[] burnMaterials = rendererStates[rendererIndex].BurnMaterials;
                for (int materialIndex = 0;
                     materialIndex < burnMaterials.Length;
                     materialIndex++)
                {
                    if (burnMaterials[materialIndex] != null)
                    {
                        burnMaterials[materialIndex].SetFloat(DurationId, duration);
                    }
                }
            }

            completion = onComplete;
            playback = StartCoroutine(PlayRoutine(duration, ashFade));
            return true;
        }

        public void Cancel()
        {
            if (playback != null)
            {
                StopCoroutine(playback);
                playback = null;
            }

            completion = null;
            RestoreBindings();
        }

        private IEnumerator PlayRoutine(float duration, float ashFade)
        {
            float elapsed = 0f;
            float totalDuration = duration + ashFade;
            while (elapsed < totalDuration)
            {
                SetElapsed(elapsed);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetElapsed(totalDuration);
            playback = null;
            RestoreBindings();

            Action finished = completion;
            completion = null;
            finished?.Invoke();
        }

        private void SetElapsed(float elapsed)
        {
            for (int rendererIndex = 0;
                 rendererIndex < rendererStates.Count;
                 rendererIndex++)
            {
                RendererState state = rendererStates[rendererIndex];
                if (state?.BurnMaterials == null)
                {
                    continue;
                }

                for (int materialIndex = 0;
                     materialIndex < state.BurnMaterials.Length;
                     materialIndex++)
                {
                    Material material = state.BurnMaterials[materialIndex];
                    if (material != null)
                    {
                        material.SetFloat(ElapsedId, elapsed);
                    }
                }
            }
        }

        private static Material LoadMaterialTemplate()
        {
            if (materialTemplate == null)
            {
                materialTemplate = Resources.Load<Material>(MaterialResourcePath);
            }

            if (materialTemplate == null)
            {
                Debug.LogError(
                    "缺少植物燃烧材质 Assets/ta_source/Burn/Resources/Burn/PlantBurnEffect.mat。");
            }

            return materialTemplate;
        }

        private static bool IsSupportedRenderer(Renderer renderer) =>
            renderer is SpriteRenderer ||
            renderer is MeshRenderer ||
            renderer is ParticleSystemRenderer;

        private static void ConfigureMaterial(
            Material burnMaterial,
            Renderer renderer,
            Material sourceMaterial,
            PlantBurnEffectSettings settings,
            int materialIndex)
        {
            Texture sourceTexture = GetSourceTexture(renderer, sourceMaterial);
            burnMaterial.SetTexture(
                MainTexId,
                sourceTexture != null ? sourceTexture : Texture2D.whiteTexture);
            burnMaterial.SetColor(
                SourceTintId,
                GetSourceTint(renderer, sourceMaterial, materialIndex));
            burnMaterial.SetVector(UvRectId, GetUvRect(renderer));
            Bounds bounds = renderer.localBounds;
            float yMin = bounds.min.y;
            float yMax = bounds.max.y;
            if (yMax - yMin < .001f)
            {
                yMin = -.5f;
                yMax = .5f;
            }

            burnMaterial.SetVector(YBoundsId, new Vector4(yMin, yMax, 0f, 0f));
            burnMaterial.SetFloat(DurationId, 1f);
            burnMaterial.SetFloat(ElapsedId, 0f);
            burnMaterial.SetFloat(
                EdgeWidthId,
                settings != null ? settings.FlameEdgeWidth : .065f);
            burnMaterial.SetFloat(
                EdgeIntensityId,
                settings != null ? settings.FlameEdgeIntensity : 1.25f);
            burnMaterial.SetFloat(
                PixelationId,
                settings != null ? settings.Pixelation : .24f);
            burnMaterial.SetFloat(
                EdgeNoiseId,
                settings != null ? settings.EdgeNoise : .5f);
            burnMaterial.SetFloat(
                AshFadeId,
                settings != null ? settings.AshFadeSeconds : .7f);
            burnMaterial.SetColor(
                CoreColorId,
                settings != null
                    ? settings.FlameCoreColor
                    : new Color(1f, .94f, .46f, 1f));
            burnMaterial.SetColor(
                OuterColorId,
                settings != null
                    ? settings.FlameOuterColor
                    : new Color(1f, .19f, .025f, 1f));
            burnMaterial.SetColor(
                AshColorId,
                settings != null
                    ? settings.AshColor
                    : new Color(.19f, .17f, .16f, 1f));

            burnMaterial.renderQueue = (int)RenderQueue.Transparent + 20;
            burnMaterial.enableInstancing = false;
        }

        private static Texture GetSourceTexture(
            Renderer renderer,
            Material sourceMaterial)
        {
            if (renderer is SpriteRenderer spriteRenderer &&
                spriteRenderer.sprite != null)
            {
                return spriteRenderer.sprite.texture;
            }

            if (sourceMaterial == null)
            {
                return Texture2D.whiteTexture;
            }

            if (sourceMaterial.HasProperty(BaseMapId))
            {
                Texture baseMap = sourceMaterial.GetTexture(BaseMapId);
                if (baseMap != null)
                {
                    return baseMap;
                }
            }

            if (sourceMaterial.HasProperty(MainTexId))
            {
                Texture mainTexture = sourceMaterial.GetTexture(MainTexId);
                if (mainTexture != null)
                {
                    return mainTexture;
                }
            }

            return sourceMaterial.mainTexture != null
                ? sourceMaterial.mainTexture
                : Texture2D.whiteTexture;
        }

        private static Color GetSourceTint(
            Renderer renderer,
            Material sourceMaterial,
            int materialIndex)
        {
            Color tint = Color.white;
            if (sourceMaterial != null)
            {
                if (sourceMaterial.HasProperty(BaseColorId))
                {
                    tint = sourceMaterial.GetColor(BaseColorId);
                }
                else if (sourceMaterial.HasProperty(ColorId))
                {
                    tint = sourceMaterial.GetColor(ColorId);
                }
                else if (sourceMaterial.HasProperty(TintColorId))
                {
                    tint = sourceMaterial.GetColor(TintColorId);
                }
            }

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, materialIndex);
            if (properties.HasColor(BaseColorId))
            {
                tint = properties.GetColor(BaseColorId);
            }
            else if (properties.HasColor(ColorId))
            {
                tint = properties.GetColor(ColorId);
            }
            else if (properties.HasColor(TintColorId))
            {
                tint = properties.GetColor(TintColorId);
            }

            return tint;
        }

        private static Vector4 GetUvRect(Renderer renderer)
        {
            Vector2[] uvs = null;
            if (renderer is SpriteRenderer spriteRenderer &&
                spriteRenderer.sprite != null)
            {
                uvs = spriteRenderer.sprite.uv;
            }
            else if (renderer is MeshRenderer meshRenderer)
            {
                MeshFilter filter = meshRenderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    uvs = filter.sharedMesh.uv;
                }
            }

            if (uvs == null || uvs.Length == 0)
            {
                return new Vector4(0f, 0f, 1f, 1f);
            }

            Vector2 min = uvs[0];
            Vector2 max = uvs[0];
            for (int index = 1; index < uvs.Length; index++)
            {
                min = Vector2.Min(min, uvs[index]);
                max = Vector2.Max(max, uvs[index]);
            }

            return new Vector4(
                min.x,
                min.y,
                Mathf.Max(.0001f, max.x - min.x),
                Mathf.Max(.0001f, max.y - min.y));
        }

        private void RestoreBindings()
        {
            for (int index = rendererStates.Count - 1;
                 index >= 0;
                 index--)
            {
                RendererState state = rendererStates[index];
                if (state == null)
                {
                    continue;
                }

                if (state.Renderer != null)
                {
                    state.Renderer.sharedMaterials = state.OriginalMaterials;
                    state.Renderer.enabled = state.WasEnabled;
                }

                DestroyMaterials(state.BurnMaterials);
            }

            rendererStates.Clear();
        }

        private static void DestroyMaterials(Material[] materials)
        {
            if (materials == null)
            {
                return;
            }

            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
