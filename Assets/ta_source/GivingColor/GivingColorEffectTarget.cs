using System;
using System.Collections.Generic;
using UnityEngine;

namespace TaSource.GivingColor
{
    /// <summary>
    /// Owns transparent overlay renderers for one successfully colored target.
    /// Source materials and renderers are kept untouched and restored by simply
    /// destroying the overlays or resetting the room.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GivingColorEffectTarget : MonoBehaviour
    {
        private const string OverlayPrefix = "__GivingColorOverlay_";
        private static readonly int SourceMapId = Shader.PropertyToID("_SourceMap");
        private static readonly int SourceTintId = Shader.PropertyToID("_SourceTint");
        private static readonly int EffectUvRectId = Shader.PropertyToID("_EffectUvRect");
        private static readonly int EffectAspectId = Shader.PropertyToID("_EffectAspect");
        private static readonly int DyeColorId = Shader.PropertyToID("_DyeColor");
        private static readonly int EffectModeId = Shader.PropertyToID("_EffectMode");
        private static readonly int EffectProgressId = Shader.PropertyToID("_EffectProgress");
        private static readonly int EffectVisibilityId = Shader.PropertyToID("_EffectVisibility");
        private static readonly int DyeStrengthId = Shader.PropertyToID("_DyeStrength");
        private static readonly int EdgeWidthId = Shader.PropertyToID("_EdgeWidth");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int FireGlowId = Shader.PropertyToID("_FireGlow");
        private static readonly int WaterDiffusionId = Shader.PropertyToID("_WaterDiffusion");
        private static readonly int BranchCountId = Shader.PropertyToID("_BranchCount");
        private static readonly int BranchWidthId = Shader.PropertyToID("_BranchWidth");
        private static readonly int AlphaThresholdId = Shader.PropertyToID("_AlphaThreshold");
        private static readonly int PixelationId = Shader.PropertyToID("_Pixelation");
        private static readonly int RedRimWidthId = Shader.PropertyToID("_RedRimWidth");
        private static readonly int RedNoiseAmountId = Shader.PropertyToID("_RedNoiseAmount");
        private static readonly int BlueNoiseAmountId = Shader.PropertyToID("_BlueNoiseAmount");
        private static readonly int GreenNoiseAmountId = Shader.PropertyToID("_GreenNoiseAmount");

        private readonly List<OverlayBinding> overlays = new List<OverlayBinding>();
        private float effectDuration = 1.5f;
        private float elapsed;
        private float revealElapsed;
        private bool playing;
        private bool revealingOriginal;
        private const float RevealOriginalDuration = .45f;

        public bool IsPlaying => playing || revealingOriginal;
        public float Progress => effectDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / effectDuration);

        private sealed class OverlayBinding
        {
            public Renderer Source;
            public Renderer Overlay;
            public Material[] Materials;
            public bool ScreenQuad;
        }

        private void OnEnable()
        {
            EventMgr.OnRoomColorReset += ResetEffect;
        }

        private void OnDisable()
        {
            EventMgr.OnRoomColorReset -= ResetEffect;
            ResetEffect();
        }

        private void Update()
        {
            Project.ColorBlocks.UniversalColorBlock universal =
                GetComponent<Project.ColorBlocks.UniversalColorBlock>();
            if (universal != null && string.IsNullOrEmpty(universal.CurrentColorId))
            {
                if (overlays.Count > 0)
                    ResetEffect();
                return;
            }

            for (int index = 0; index < overlays.Count; index++)
            {
                OverlayBinding binding = overlays[index];
                if (binding.Overlay == null || binding.Source == null)
                    continue;
                binding.Overlay.enabled = binding.Source.enabled &&
                    binding.Source.gameObject.activeInHierarchy;
            }

            if (!playing)
            {
                if (!revealingOriginal)
                    return;

                revealElapsed += Time.deltaTime;
                float revealProgress = Mathf.Clamp01(revealElapsed / RevealOriginalDuration);
                SetVisibility(Mathf.Lerp(1f, 0f, revealProgress));
                if (revealProgress >= 1f)
                    ResetEffect();
                return;
            }

            elapsed += Time.deltaTime;
            float progress = Progress;
            SetProgress(progress);
            if (progress >= 1f)
            {
                playing = false;
                revealingOriginal = true;
                revealElapsed = 0f;
            }
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null)
                return;
            for (int index = 0; index < overlays.Count; index++)
                if (overlays[index].ScreenQuad && overlays[index].Overlay != null &&
                    overlays[index].Source != null)
                    PositionScreenQuad(overlays[index].Source,
                        overlays[index].Overlay.transform, camera);
        }

        private void OnDestroy()
        {
            DestroyOverlays();
        }

        public void Play(
            Shader overlayShader,
            Color color,
            int mode,
            float duration,
            float strength,
            float spreadEdgeWidth,
            float noiseScale,
            float fireGlow,
            float waterDiffusion,
            int branchCount,
            float branchWidth,
            float alphaThreshold,
            bool ignoreSourceOpacity = false,
            bool includeDisabledSource = false,
            float pixelation = .7f,
            float redRimWidth = .035f,
            float redNoiseAmount = .65f,
            float blueNoiseAmount = .55f,
            float greenNoiseAmount = .7f)
        {
            if (overlayShader == null || !overlayShader.isSupported ||
                mode < 1 || mode > 3)
                return;

            if (!EnsureOverlays(overlayShader, includeDisabledSource))
                return;

            effectDuration = Mathf.Max(.05f, duration);
            elapsed = 0f;
            revealElapsed = 0f;
            playing = true;
            revealingOriginal = false;

            for (int index = 0; index < overlays.Count; index++)
            {
                OverlayBinding binding = overlays[index];
                if (binding.Overlay == null)
                    continue;

                binding.Overlay.enabled = true;
                for (int materialIndex = 0; materialIndex < binding.Materials.Length; materialIndex++)
                {
                    Material material = binding.Materials[materialIndex];
                    if (material == null)
                        continue;
                    if (ignoreSourceOpacity)
                    {
                        Color tint = material.GetColor(SourceTintId);
                        tint.a = 1f;
                        material.SetColor(SourceTintId, tint);
                    }
                    material.SetColor(DyeColorId, color);
                    material.SetFloat(EffectModeId, mode);
                    material.SetFloat(EffectProgressId, 0f);
                    material.SetFloat(EffectVisibilityId, 1f);
                    material.SetFloat(DyeStrengthId, Mathf.Clamp01(strength));
                    material.SetFloat(EdgeWidthId, Mathf.Clamp(spreadEdgeWidth, .005f, .15f));
                    material.SetFloat(NoiseScaleId, Mathf.Max(1f, noiseScale));
                    material.SetFloat(FireGlowId, Mathf.Max(0f, fireGlow));
                    material.SetFloat(WaterDiffusionId, Mathf.Max(0f, waterDiffusion));
                    material.SetFloat(BranchCountId, Mathf.Clamp(branchCount, 3, 12));
                    material.SetFloat(BranchWidthId, Mathf.Clamp(branchWidth, .005f, .08f));
                    material.SetFloat(AlphaThresholdId, Mathf.Clamp(alphaThreshold, 0f, .25f));
                    material.SetFloat(PixelationId, Mathf.Clamp01(pixelation));
                    material.SetFloat(RedRimWidthId, Mathf.Clamp(redRimWidth, .005f, .12f));
                    material.SetFloat(RedNoiseAmountId, Mathf.Clamp01(redNoiseAmount));
                    material.SetFloat(BlueNoiseAmountId, Mathf.Clamp01(blueNoiseAmount));
                    material.SetFloat(GreenNoiseAmountId, Mathf.Clamp01(greenNoiseAmount));
                }
            }
        }

        public void ResetEffect()
        {
            playing = false;
            revealingOriginal = false;
            elapsed = 0f;
            revealElapsed = 0f;
            DestroyOverlays();
        }

        private bool EnsureOverlays(Shader shader, bool includeDisabledSource)
        {
            if (overlays.Count > 0)
            {
                bool shaderMatches = true;
                for (int i = 0; i < overlays.Count && shaderMatches; i++)
                {
                    OverlayBinding binding = overlays[i];
                    for (int j = 0; j < binding.Materials.Length; j++)
                        if (binding.Materials[j] == null || binding.Materials[j].shader != shader)
                            shaderMatches = false;
                }
                if (shaderMatches)
                    return true;
                DestroyOverlays();
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer source = renderers[index];
                if (source == null || !source.enabled ||
                    !source.gameObject.activeInHierarchy ||
                    source.gameObject.name.StartsWith(OverlayPrefix, StringComparison.Ordinal))
                    continue;

                OverlayBinding binding = CreateOverlay(source, shader, overlays.Count);
                if (binding != null)
                    overlays.Add(binding);
            }

            // Some neutral ColorObjects hide their authored cube when their
            // fixed visual is a particle system. Use that original cube only
            // for the finite reveal, then remove it again.
            if (overlays.Count == 0 && includeDisabledSource)
            {
                Project.ColorBlocks.ColorObject colorObject =
                    GetComponent<Project.ColorBlocks.ColorObject>();
                Renderer source = colorObject != null
                    ? colorObject.TargetRenderer
                    : null;
                if (source != null && source.gameObject.activeInHierarchy)
                {
                    OverlayBinding binding = CreateOverlay(source, shader, 0);
                    if (binding != null)
                        overlays.Add(binding);
                }
            }

            return overlays.Count > 0;
        }

        private OverlayBinding CreateOverlay(Renderer source, Shader shader, int index)
        {
            if (source is SpriteRenderer spriteSource)
                return CreateSpriteOverlay(spriteSource, shader, index);
            if (source is MeshRenderer meshSource)
                return CreateMeshOverlay(meshSource, shader, index);
            return null;
        }

        private OverlayBinding CreateSpriteOverlay(SpriteRenderer source, Shader shader, int index)
        {
            GameObject overlayObject = CreateOverlayObject(source.transform, index);
            SpriteRenderer overlay = overlayObject.AddComponent<SpriteRenderer>();
            overlay.sprite = source.sprite;
            overlay.drawMode = source.drawMode;
            overlay.size = source.size;
            overlay.flipX = source.flipX;
            overlay.flipY = source.flipY;
            overlay.color = source.color;
            overlay.maskInteraction = source.maskInteraction;
            CopySorting(source, overlay);

            Material sourceMaterial = source.sharedMaterial;
            Material material = CreateOverlayMaterial(sourceMaterial, source.sprite != null ? source.sprite.texture : null, shader,
                source.sprite != null ? source.sprite.uv : null, source.bounds.size);
            overlay.sharedMaterial = material;
            overlay.enabled = source.enabled;
            return new OverlayBinding
            {
                Source = source,
                Overlay = overlay,
                Materials = new[] { material }
            };
        }

        private OverlayBinding CreateMeshOverlay(MeshRenderer source, Shader shader, int index)
        {
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
                return null;

            // Color the visible 2D projection. Duplicating the entire source
            // mesh caused z-fighting and sampled rear/side UVs.
            GameObject overlayObject = CreateOverlayObject(null, index);
            MeshFilter filter = overlayObject.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateScreenQuadMesh();
            MeshRenderer overlay = overlayObject.AddComponent<MeshRenderer>();
            overlay.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            overlay.receiveShadows = false;
            CopySorting(source, overlay);

            Material[] sourceMaterials = source.sharedMaterials;
            Material sourceMaterial = sourceMaterials.Length > 0 ? sourceMaterials[0] : null;
            Material material = CreateOverlayMaterial(sourceMaterial, null, shader, null,
                source.bounds.size);
            overlay.sharedMaterial = material;
            overlay.enabled = source.enabled;
            if (Camera.main != null)
                PositionScreenQuad(source, overlay.transform, Camera.main);
            return new OverlayBinding
            {
                Source = source,
                Overlay = overlay,
                Materials = new[] { material },
                ScreenQuad = true
            };
        }

        private static Mesh CreateScreenQuadMesh()
        {
            var mesh = new Mesh { name = "GivingColor Screen Quad",
                hideFlags = HideFlags.DontSave };
            mesh.vertices = new[]
            {
                new Vector3(-.5f, -.5f, 0f), new Vector3(.5f, -.5f, 0f),
                new Vector3(-.5f, .5f, 0f), new Vector3(.5f, .5f, 0f)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, 1f), new Vector2(1f, 1f)
            };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PositionScreenQuad(Renderer source, Transform overlay,
            Camera camera)
        {
            Bounds bounds = source.bounds;
            Vector3 origin = camera.transform.position;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            Vector3 forward = camera.transform.forward;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
            float nearestDepth = float.PositiveInfinity;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3((corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z) - origin;
                float x = Vector3.Dot(point, right);
                float y = Vector3.Dot(point, up);
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                nearestDepth = Mathf.Min(nearestDepth, Vector3.Dot(point, forward));
            }
            overlay.SetPositionAndRotation(
                origin + forward * Mathf.Max(.01f, nearestDepth - .01f) +
                right * ((minX + maxX) * .5f) +
                up * ((minY + maxY) * .5f),
                camera.transform.rotation);
            overlay.localScale = new Vector3(
                Mathf.Max(.001f, maxX - minX), Mathf.Max(.001f, maxY - minY), 1f);
        }

        private GameObject CreateOverlayObject(Transform parent, int index)
        {
            var overlayObject = new GameObject(OverlayPrefix + index.ToString("00"))
            {
                hideFlags = HideFlags.DontSave
            };
            overlayObject.transform.SetParent(parent, false);
            overlayObject.transform.localPosition = Vector3.zero;
            overlayObject.transform.localRotation = Quaternion.identity;
            overlayObject.transform.localScale = Vector3.one;
            return overlayObject;
        }

        private Material CreateOverlayMaterial(
            Material source,
            Texture spriteTexture,
            Shader shader,
            Vector2[] uvs,
            Vector3 boundsSize)
        {
            var material = new Material(shader)
            {
                name = $"{name} GivingColor Overlay",
                hideFlags = HideFlags.DontSave,
                renderQueue = 3100
            };

            Texture texture = spriteTexture;
            if (texture == null && source != null)
            {
                if (source.HasProperty("_BaseMap") && source.GetTexture("_BaseMap") != null)
                    texture = source.GetTexture("_BaseMap");
                else if (source.HasProperty("_MainTex") && source.GetTexture("_MainTex") != null)
                    texture = source.GetTexture("_MainTex");
            }
            if (texture == null)
                texture = Texture2D.whiteTexture;
            material.SetTexture(SourceMapId, texture);

            Color sourceTint = Color.white;
            string tintProperty = null;
            if (source != null)
            {
                if (source.HasProperty("_BaseColor")) tintProperty = "_BaseColor";
                else if (source.HasProperty("_Color")) tintProperty = "_Color";
                else if (source.HasProperty("_TintColor")) tintProperty = "_TintColor";
                if (tintProperty != null)
                    sourceTint = source.GetColor(tintProperty);
            }
            material.SetColor(SourceTintId, sourceTint);

            if (source != null)
            {
                string textureProperty = source.HasProperty("_BaseMap") ? "_BaseMap" :
                    source.HasProperty("_MainTex") ? "_MainTex" : null;
                if (textureProperty != null)
                {
                    material.SetTextureScale("_SourceMap", source.GetTextureScale(textureProperty));
                    material.SetTextureOffset("_SourceMap", source.GetTextureOffset(textureProperty));
                }
            }

            Vector4 uvRect = CalculateUvRect(uvs);
            material.SetVector(EffectUvRectId, uvRect);
            float aspect = Mathf.Abs(boundsSize.y) > .0001f
                ? Mathf.Abs(boundsSize.x / boundsSize.y)
                : 1f;
            material.SetFloat(EffectAspectId, Mathf.Clamp(aspect, .1f, 10f));
            return material;
        }

        private static Vector4 CalculateUvRect(Vector2[] uvs)
        {
            if (uvs == null || uvs.Length == 0)
                return new Vector4(0f, 0f, 1f, 1f);
            Vector2 min = uvs[0];
            Vector2 max = uvs[0];
            for (int index = 1; index < uvs.Length; index++)
            {
                min = Vector2.Min(min, uvs[index]);
                max = Vector2.Max(max, uvs[index]);
            }
            return new Vector4(min.x, min.y,
                Mathf.Max(.0001f, max.x - min.x),
                Mathf.Max(.0001f, max.y - min.y));
        }

        private static void CopySorting(Renderer source, Renderer target)
        {
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder + 1;
        }

        private void SetProgress(float progress)
        {
            for (int index = 0; index < overlays.Count; index++)
            {
                OverlayBinding binding = overlays[index];
                if (binding.Overlay == null)
                    continue;
                for (int materialIndex = 0; materialIndex < binding.Materials.Length; materialIndex++)
                    if (binding.Materials[materialIndex] != null)
                        binding.Materials[materialIndex].SetFloat(EffectProgressId, progress);
            }
        }

        private void SetVisibility(float visibility)
        {
            for (int index = 0; index < overlays.Count; index++)
            {
                OverlayBinding binding = overlays[index];
                if (binding.Overlay == null)
                    continue;
                for (int materialIndex = 0; materialIndex < binding.Materials.Length; materialIndex++)
                    if (binding.Materials[materialIndex] != null)
                        binding.Materials[materialIndex].SetFloat(
                            EffectVisibilityId, visibility);
            }
        }

        private void DestroyOverlays()
        {
            for (int index = 0; index < overlays.Count; index++)
            {
                OverlayBinding binding = overlays[index];
                if (binding.Overlay != null)
                {
                    if (binding.ScreenQuad)
                    {
                        MeshFilter filter = binding.Overlay.GetComponent<MeshFilter>();
                        if (filter != null && filter.sharedMesh != null)
                            Destroy(filter.sharedMesh);
                    }
                    Destroy(binding.Overlay.gameObject);
                }
                for (int materialIndex = 0; materialIndex < binding.Materials.Length; materialIndex++)
                    if (binding.Materials[materialIndex] != null)
                        Destroy(binding.Materials[materialIndex]);
            }
            overlays.Clear();
        }
    }
}
