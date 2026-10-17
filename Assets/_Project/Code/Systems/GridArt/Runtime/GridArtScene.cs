using System;
using UnityEngine;

namespace Project.GridArt
{
    [Serializable]
    public sealed class GridArtLayerBinding
    {
        public string id;
        public Transform root;
        public Vector3 restPosition;
        public Vector2 parallax = Vector2.one;
    }

    [Serializable]
    public sealed class GridArtColorBinding
    {
        public Renderer renderer;
        public Texture2D alphaTexture;
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GridArtScene : MonoBehaviour
    {
        public GridArtMap map;
        public Camera targetCamera;
        [Min(0)] public int pixelsPerCell = Project.ObjectArtwork.GridArtworkMetrics.PixelsPerCell;
        [SerializeField, HideInInspector] private GridArtLayerBinding[] bindings = Array.Empty<GridArtLayerBinding>();
        [SerializeField, HideInInspector] private GridArtColorBinding[] colorBindings = Array.Empty<GridArtColorBinding>();
        [SerializeField, HideInInspector] private int appliedRevision = -1;
        [SerializeField, HideInInspector] private string appliedPaletteHash;
        private Camera boundCamera;
        private Vector3 reference;
        private bool initialized;
        public bool IsBuiltFor(int revision, string hash) => appliedRevision == revision && appliedPaletteHash == hash;
        public void SetBuildSignature(int revision, string hash) { appliedRevision = revision; appliedPaletteHash = hash; }
        public void SetBindings(GridArtLayerBinding[] values) { bindings = values; initialized = false; }
        public void SetColorBindings(GridArtColorBinding[] values) { colorBindings = values; ApplyColorMasks(); }
        private void ApplyColorMasks()
        {
            if (colorBindings == null || colorBindings.Length == 0) return;
            var properties = new MaterialPropertyBlock();
            foreach (var binding in colorBindings)
            {
                if (binding?.renderer == null) continue;
                binding.renderer.GetPropertyBlock(properties);
                properties.SetFloat("_ColorMaskGroup",-1f);
                properties.SetFloat("_ColorMaskReal",1f);
                properties.SetFloat("_ColorMaskUseVertexAlpha",0f);
                Material material = binding.renderer.sharedMaterial;
                Texture texture = material != null && material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : binding.alphaTexture;
                float opacity = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor").a : 1f;
                properties.SetFloat("_ColorMaskOpacity",Mathf.Clamp01(opacity));
                properties.SetTexture("_ColorMaskAlphaTexture",texture != null ? texture : Texture2D.whiteTexture);
                binding.renderer.SetPropertyBlock(properties);
                properties.Clear();
            }
        }
        public void SetCamera(Camera value) { targetCamera = value; initialized = false; }
        public void RebaseParallax()
        {
            foreach (var binding in bindings)
                if (binding.root != null) binding.root.localPosition = binding.restPosition;
            initialized = false;
        }
        public static Vector3 ParallaxOffset(Vector3 cameraDelta, Vector2 factor, float pixelUnit)
        {
            var offset = new Vector3(cameraDelta.x * (1f - factor.x), cameraDelta.y * (1f - factor.y), 0);
            if (pixelUnit > 0)
            {
                offset.x = Mathf.Round(offset.x / pixelUnit) * pixelUnit;
                offset.y = Mathf.Round(offset.y / pixelUnit) * pixelUnit;
            }
            return offset;
        }
        private void OnEnable() { initialized = false; ApplyColorMasks(); }
        private void OnDisable()
        {
            foreach (var binding in bindings)
                if (binding.root != null) binding.root.localPosition = binding.restPosition;
        }
        private void LateUpdate()
        {
            var camera = targetCamera != null ? targetCamera : boundCamera;
            if (camera == null || !camera.isActiveAndEnabled) camera = Camera.main;
            if (camera == null) return;
            if (!initialized || boundCamera != camera)
            {
                reference = camera.transform.position;
                boundCamera = camera;
                initialized = true;
            }
            Vector3 delta = transform.InverseTransformVector(camera.transform.position - reference);
            float unit = pixelsPerCell > 0 && map != null ? map.cellSize / pixelsPerCell : 0f;
            foreach (var binding in bindings)
                if (binding.root != null)
                    binding.root.localPosition = binding.restPosition + ParallaxOffset(delta, binding.parallax, unit);
        }
    }
}
