using UnityEngine;

namespace InteractiveWater
{
    /// <summary>
    /// Keeps a pixel-water refraction quad registered to its rear sprite and exposes
    /// per-instance controls for tint and animated screen-space distortion.
    /// </summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class PixelWaterFilterController : MonoBehaviour
    {
        private static readonly int DistortionStrengthId = Shader.PropertyToID("_DistortionStrength");
        private static readonly int DistortionVariationId = Shader.PropertyToID("_DistortionVariation");
        private static readonly int DistortionChangeSpeedId = Shader.PropertyToID("_DistortionChangeSpeed");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");

        [Header("Panel Registration")]
        [SerializeField] private SpriteRenderer backingSprite;
        [SerializeField] private MeshRenderer filterRenderer;
        [SerializeField, Tooltip("Transparent padding in pixels, ordered left/bottom/right/top.")]
        private Vector4 filterBoundsInsetsPixels;

        [Header("Water Filter")]
        [SerializeField, ColorUsage(false, false)] private Color waterTint = new Color(0.72f, 0.92f, 1f, 1f);
        [SerializeField, Range(0f, 0.25f)] private float tintStrength = 0.1f;
        [SerializeField, Range(0f, 0.02f)] private float distortionStrength = 0.004f;
        [SerializeField, Range(0f, 1f)] private float distortionTimeVariation = 0.75f;
        [SerializeField, Range(0f, 4f)] private float distortionChangeSpeed = 0.9f;
        [SerializeField, Range(0.5f, 32f)] private float noiseScale = 6f;
        [SerializeField, Range(0f, 4f)] private float noiseFlowSpeed = 0.9f;
        [SerializeField, Range(0f, 1f)] private float opacity = 0.9f;

        private MaterialPropertyBlock properties;
        private bool propertiesDirty;

        public Color WaterTint
        {
            get => waterTint;
            set { waterTint = value; ApplyFilterProperties(); }
        }

        public float TintStrength
        {
            get => tintStrength;
            set { tintStrength = Mathf.Clamp(value, 0f, 0.25f); ApplyFilterProperties(); }
        }

        public float DistortionStrength
        {
            get => distortionStrength;
            set { distortionStrength = Mathf.Clamp(value, 0f, 0.02f); ApplyFilterProperties(); }
        }

        public float DistortionTimeVariation
        {
            get => distortionTimeVariation;
            set { distortionTimeVariation = Mathf.Clamp01(value); ApplyFilterProperties(); }
        }

        public float DistortionChangeSpeed
        {
            get => distortionChangeSpeed;
            set { distortionChangeSpeed = Mathf.Clamp(value, 0f, 4f); ApplyFilterProperties(); }
        }

        public float NoiseScale
        {
            get => noiseScale;
            set { noiseScale = Mathf.Clamp(value, 0.5f, 32f); ApplyFilterProperties(); }
        }

        public float NoiseFlowSpeed
        {
            get => noiseFlowSpeed;
            set { noiseFlowSpeed = Mathf.Clamp(value, 0f, 4f); ApplyFilterProperties(); }
        }

        public float Opacity
        {
            get => opacity;
            set { opacity = Mathf.Clamp01(value); ApplyFilterProperties(); }
        }

        private void OnEnable()
        {
            ResolveReferences();
            AlignFilterToBackingSprite();
            ApplyFilterProperties();
        }

        private void OnValidate()
        {
            ClampSettings();
            propertiesDirty = true;
        }

        private void LateUpdate()
        {
            // Also handles runtime sprite swaps and child-transform changes without letting
            // the independently offset front panel drift away from the rear image.
            AlignFilterToBackingSprite();
            if (propertiesDirty)
                ApplyFilterProperties();
        }

        /// <summary>Re-registers the front quad to the rear sprite while preserving its Z gap.</summary>
        [ContextMenu("Align Filter To Backing Sprite")]
        public void AlignFilterToBackingSprite()
        {
            if (!backingSprite || !filterRenderer || !backingSprite.sprite)
                return;

            Transform backingTransform = backingSprite.transform;
            Transform filterTransform = filterRenderer.transform;
            Transform alignmentSpace = filterTransform.parent ? filterTransform.parent : transform;
            Bounds spriteBounds = GetFilterBounds(backingSprite.sprite);

            Vector3 filterLocalPosition = alignmentSpace.InverseTransformPoint(
                backingTransform.TransformPoint(spriteBounds.center));
            filterLocalPosition.z = filterTransform.localPosition.z;
            Quaternion filterRotation = Quaternion.Inverse(alignmentSpace.rotation) * backingTransform.rotation;

            Vector3 desiredWorldX = backingTransform.TransformVector(
                Vector3.right * spriteBounds.size.x);
            Vector3 desiredWorldY = backingTransform.TransformVector(
                Vector3.up * spriteBounds.size.y);
            Vector3 unitWorldX = alignmentSpace.TransformVector(filterRotation * Vector3.right);
            Vector3 unitWorldY = alignmentSpace.TransformVector(filterRotation * Vector3.up);

            Vector3 filterScale = filterTransform.localScale;
            if (unitWorldX.sqrMagnitude > 0.000001f)
                filterScale.x = desiredWorldX.magnitude / unitWorldX.magnitude;
            if (unitWorldY.sqrMagnitude > 0.000001f)
                filterScale.y = desiredWorldY.magnitude / unitWorldY.magnitude;

            if ((filterTransform.localPosition - filterLocalPosition).sqrMagnitude > 0.00000001f)
                filterTransform.localPosition = filterLocalPosition;
            if (Quaternion.Angle(filterTransform.localRotation, filterRotation) > 0.001f)
                filterTransform.localRotation = filterRotation;
            if ((filterTransform.localScale - filterScale).sqrMagnitude > 0.00000001f)
                filterTransform.localScale = filterScale;
        }

        private Bounds GetFilterBounds(Sprite sprite)
        {
            Bounds bounds = sprite.bounds;
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float pixelSize = 1f / sprite.pixelsPerUnit;

            minimum.x += filterBoundsInsetsPixels.x * pixelSize;
            minimum.y += filterBoundsInsetsPixels.y * pixelSize;
            maximum.x -= filterBoundsInsetsPixels.z * pixelSize;
            maximum.y -= filterBoundsInsetsPixels.w * pixelSize;

            if (minimum.x > maximum.x)
                minimum.x = maximum.x = bounds.center.x;
            if (minimum.y > maximum.y)
                minimum.y = maximum.y = bounds.center.y;

            Bounds filteredBounds = new Bounds();
            filteredBounds.SetMinMax(minimum, maximum);
            return filteredBounds;
        }
        /// <summary>Sets all per-instance water-filter look and motion controls at runtime.</summary>
        public void SetFilter(Color tint, float tintAmount, float distortion, float variation,
            float variationSpeed, float spatialScale, float flowSpeed, float filterOpacity)
        {
            waterTint = tint;
            tintStrength = Mathf.Clamp(tintAmount, 0f, 0.25f);
            distortionStrength = Mathf.Clamp(distortion, 0f, 0.02f);
            distortionTimeVariation = Mathf.Clamp01(variation);
            distortionChangeSpeed = Mathf.Clamp(variationSpeed, 0f, 4f);
            noiseScale = Mathf.Clamp(spatialScale, 0.5f, 32f);
            noiseFlowSpeed = Mathf.Clamp(flowSpeed, 0f, 4f);
            opacity = Mathf.Clamp01(filterOpacity);
            ApplyFilterProperties();
        }

        private void ResolveReferences()
        {
            if (!backingSprite)
                backingSprite = GetComponentInChildren<SpriteRenderer>(true);
            if (!filterRenderer)
                filterRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        private void ClampSettings()
        {
            tintStrength = Mathf.Clamp(tintStrength, 0f, 0.25f);
            distortionStrength = Mathf.Clamp(distortionStrength, 0f, 0.02f);
            distortionTimeVariation = Mathf.Clamp01(distortionTimeVariation);
            distortionChangeSpeed = Mathf.Clamp(distortionChangeSpeed, 0f, 4f);
            noiseScale = Mathf.Clamp(noiseScale, 0.5f, 32f);
            noiseFlowSpeed = Mathf.Clamp(noiseFlowSpeed, 0f, 4f);
            opacity = Mathf.Clamp01(opacity);
            filterBoundsInsetsPixels.x = Mathf.Max(0f, filterBoundsInsetsPixels.x);
            filterBoundsInsetsPixels.y = Mathf.Max(0f, filterBoundsInsetsPixels.y);
            filterBoundsInsetsPixels.z = Mathf.Max(0f, filterBoundsInsetsPixels.z);
            filterBoundsInsetsPixels.w = Mathf.Max(0f, filterBoundsInsetsPixels.w);
        }

        private void ApplyFilterProperties()
        {
            if (!filterRenderer)
                return;

            if (properties == null)
                properties = new MaterialPropertyBlock();

            filterRenderer.GetPropertyBlock(properties);
            properties.SetColor(TintId, waterTint);
            properties.SetFloat(TintStrengthId, tintStrength);
            properties.SetFloat(DistortionStrengthId, distortionStrength);
            properties.SetFloat(DistortionVariationId, distortionTimeVariation);
            properties.SetFloat(DistortionChangeSpeedId, distortionChangeSpeed);
            properties.SetFloat(NoiseScaleId, noiseScale);
            properties.SetFloat(AnimationSpeedId, noiseFlowSpeed);
            properties.SetFloat(OpacityId, opacity);
            filterRenderer.SetPropertyBlock(properties);
            propertiesDirty = false;
        }
    }
}