using System;
using Project.BlockFeatures;
using UnityEngine;

namespace Project.ColorBlocks
{
    /// <summary>
    /// A scene object with one fixed unlock color. Its form never changes;
    /// the key only switches it between neutral and active states.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockAbilityHost))]
    public sealed class ColorObject : MonoBehaviour,
        IColorObject,
        IRoomColorResettable
    {
        [SerializeField, Tooltip("固定的颜色解锁组，运行时不会被染色改写。")]
        private string baseColorTypeId = "red";
        [SerializeField, Tooltip("留空时自动查找第一个子渲染器。")]
        private Renderer targetRenderer;

        private bool active;
        private bool registered;
        private Collider[] cachedColliders;
        private bool[] originalTriggerStates;
        private bool[] originalEnabledStates;

        public string BaseColorTypeId => baseColorTypeId;
        public bool IsActive => active;
        public Renderer TargetRenderer => targetRenderer;

        public event Action<IColorObject, bool> ActiveStateChanged;

        private void Awake()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponentInChildren<Renderer>();
            }

            CacheColliderStates();
            ApplyCollisionState(active);
            ApplyVisual();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                ApplyVisual();
                return;
            }

            ColorRuntimeService.Instance.Register(this);
            registered = true;
        }

        private void OnDisable()
        {
            if (!registered)
            {
                return;
            }

            ColorRuntimeService.Existing?.Unregister(this);
            registered = false;
        }

        public void Activate()
        {
            SetActive(true);
        }

        public void Deactivate()
        {
            SetActive(false);
        }

        public void ResetForRoom()
        {
            SetActive(false);
        }

        public void Configure(string colorId, Renderer renderer)
        {
            baseColorTypeId = colorId ?? string.Empty;
            targetRenderer = renderer;
            ApplyVisual();
        }

        private void SetActive(bool value)
        {
            if (active == value)
            {
                ApplyCollisionState(active);
                ApplyVisual();
                return;
            }

            active = value;
            if (active)
            {
                ApplyLayer();
            }

            ApplyCollisionState(active);
            ApplyVisual();
            ActiveStateChanged?.Invoke(this, active);
        }

        private void CacheColliderStates()
        {
            cachedColliders =
                GetComponentsInChildren<Collider>(true);
            originalTriggerStates =
                new bool[cachedColliders.Length];
            originalEnabledStates =
                new bool[cachedColliders.Length];
            for (int index = 0;
                 index < cachedColliders.Length;
                 index++)
            {
                Collider collider = cachedColliders[index];
                originalTriggerStates[index] =
                    collider != null && collider.isTrigger;
                originalEnabledStates[index] =
                    collider != null && collider.enabled;
            }
        }

        private void ApplyCollisionState(bool value)
        {
            if (cachedColliders == null)
            {
                CacheColliderStates();
            }

            for (int index = 0;
                 index < cachedColliders.Length;
                 index++)
            {
                Collider collider = cachedColliders[index];
                if (collider == null)
                {
                    continue;
                }

                if (!value)
                {
                    collider.enabled = true;
                    collider.isTrigger = false;
                    continue;
                }

                collider.enabled = originalEnabledStates[index];
                collider.isTrigger = originalTriggerStates[index];
            }
        }

        private void ApplyVisual()
        {
            if (targetRenderer == null)
            {
                return;
            }

            ColorRuntimeService service = ColorRuntimeService.Existing;
            ColorCatalog catalog = service?.Catalog;
            if (catalog == null)
            {
                return;
            }

            ColorTypeDefinition definition = catalog.Find(baseColorTypeId);
            Material material = active
                ? definition?.targetMaterial
                : catalog.NeutralMaterial;
            if (material != null)
            {
                targetRenderer.sharedMaterial = material;
            }

            targetRenderer.enabled = true;
        }

        private void ApplyLayer()
        {
            ColorTypeDefinition definition =
                ColorRuntimeService.Existing?.Catalog?.Find(
                    baseColorTypeId);
            if (definition == null ||
                definition.unityLayer < 0 ||
                definition.unityLayer >= 32)
            {
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].gameObject.layer = definition.unityLayer;
            }
        }
    }
}
