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

    [DisallowMultipleComponent, DefaultExecutionOrder(10000)]
    public sealed class GridArtScene : MonoBehaviour
    {
        public GridArtMap map;
        public Camera targetCamera;
        [Min(0)] public int pixelsPerCell = 64;
        [SerializeField, HideInInspector] private GridArtLayerBinding[] bindings = Array.Empty<GridArtLayerBinding>();
        [SerializeField, HideInInspector] private int appliedRevision = -1;
        [SerializeField, HideInInspector] private string appliedPaletteHash;
        private Camera boundCamera;
        private Vector3 reference;
        private bool initialized;
        public bool IsBuiltFor(int revision, string hash) => appliedRevision == revision && appliedPaletteHash == hash;
        public void SetBuildSignature(int revision, string hash) { appliedRevision = revision; appliedPaletteHash = hash; }
        public void SetBindings(GridArtLayerBinding[] values) { bindings = values; initialized = false; }
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
        private void OnEnable() => initialized = false;
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
