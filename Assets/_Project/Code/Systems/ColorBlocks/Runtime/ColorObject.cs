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
        [SerializeField, Tooltip("解锁后的正式外观材质。留空时隐藏识别模型，由功能组件提供水体等正式外观。不得指定编辑识别材质。")]
        private Material unlockedMaterial;
        private HSVColorFadeManager fadeManager;
        private BlockFeature[] visualFeatures;

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
            visualFeatures = GetComponents<BlockFeature>();
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

            fadeManager = HSVColorFadeManager.Instance;
            fadeManager.AppearanceChanged += OnAppearanceChanged;
            ColorRuntimeService.Instance.Register(this);
            ApplyLayer();
            registered = true;
        }

        private void OnDisable()
        {
            if (fadeManager != null) fadeManager.AppearanceChanged -= OnAppearanceChanged;
            fadeManager = null;
            if (!registered)
            {
                return;
            }

            ColorRuntimeService.Existing?.Unregister(this);
            registered = false;
        }

        private void OnAppearanceChanged(string colorId)
        {
            if (string.IsNullOrEmpty(colorId) || colorId == baseColorTypeId) ApplyVisual();
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
            // Features may have assigned their own material during activation.
            // The neutral/real handoff remains owned by this presentation bridge.
            ApplyVisual();
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
            // Authoring materials are never promoted into gameplay materials.
            if (!Application.isPlaying) return;
            if (targetRenderer == null)
            {
                return;
            }

            ColorRuntimeService service = ColorRuntimeService.Instance;
            ColorCatalog catalog = service?.Catalog;
            if (catalog == null)
            {
                return;
            }

            ColorTypeDefinition definition = catalog.Find(baseColorTypeId);
            bool showReal = active && HSVColorFadeManager.Instance.ShowsUnlockedAppearance(baseColorTypeId);
            Material material = showReal ? unlockedMaterial : catalog.NeutralMaterial;
            if (showReal && material == null && visualFeatures != null)
            {
                foreach (BlockFeature feature in visualFeatures)
                {
                    if (feature == null || !feature.enabled) continue;
                    material = feature.GetMaterialForRenderer(targetRenderer);
                    if (material != null) break;
                }
            }
            if (showReal && material == definition?.targetMaterial) material = null;
            if (material != null)
            {
                targetRenderer.sharedMaterial = material;
            }

            targetRenderer.enabled = material != null;
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
