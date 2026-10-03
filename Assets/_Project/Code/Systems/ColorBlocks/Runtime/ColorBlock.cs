using Project.InputAbstraction;
using UnityEngine;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    public sealed class ColorBlock : MonoBehaviour, IInteractionTarget
    {
        [SerializeField, Tooltip("设计时基础颜色；运行时只读，不由染色行为改写。")]
        private string baseColorTypeId = "red";
        [SerializeField] private Renderer targetRenderer;
        private string currentColorTypeId;
        private Material originalMaterial;

        public string BaseColorTypeId => baseColorTypeId;
        public string CurrentColorTypeId => currentColorTypeId;
        public bool HasColor => !string.IsNullOrEmpty(currentColorTypeId);

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null) originalMaterial = targetRenderer.sharedMaterial;
            var definition = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (definition != null && definition.unityLayer >= 0 && targetRenderer != null)
                targetRenderer.gameObject.layer = definition.unityLayer;
        }

        private void OnEnable() => ColorWorldManager.Instance.Register(this);
        private void OnDisable()
        {
            if (ColorWorldManager.Existing != null) ColorWorldManager.Existing.Unregister(this);
        }

        internal void OnBaseColorUnlocked(string typeId)
        {
            if (typeId == baseColorTypeId && !HasColor) ApplyCurrentColor(typeId);
        }

        internal bool ApplyCurrentColor(string typeId)
        {
            if (currentColorTypeId == typeId) return false;
            string previous = currentColorTypeId;
            currentColorTypeId = typeId;
            ApplyMaterial();
            ColorWorldManager.Instance.NotifyBlockChanged(this, previous, typeId);
            return true;
        }

        internal void ResetToNeutral()
        {
            string previous = currentColorTypeId;
            currentColorTypeId = null;
            ApplyMaterial();
            if (!string.IsNullOrEmpty(previous))
                ColorWorldManager.Instance.NotifyBlockChanged(this, previous, null);
        }

        private void ApplyMaterial()
        {
            if (targetRenderer == null) return;
            var catalog = ColorWorldManager.Instance.Catalog;
            var definition = catalog != null ? catalog.Find(currentColorTypeId) : null;
            var activeLayer = definition ?? (catalog != null ? catalog.Find(baseColorTypeId) : null);
            if (activeLayer != null && activeLayer.unityLayer >= 0 && activeLayer.unityLayer < 32)
                targetRenderer.gameObject.layer = activeLayer.unityLayer;
            var material = definition != null ? definition.targetMaterial : catalog != null ? catalog.NeutralMaterial : null;
            if (material == null) material = originalMaterial;
            if (material != null) targetRenderer.sharedMaterial = material;
        }

        public bool CanInteract(GameObject interactor) =>
            isActiveAndEnabled && ColorWorldManager.Instance.HasRecolorAbility &&
            ColorWorldManager.Instance.IsUnlocked(baseColorTypeId);

        // The graph is authoring data only. Mixing will be connected when rules are approved.
        public bool TryInteract(GameObject interactor) => false;

#if UNITY_EDITOR
        public void EditorConfigure(string typeId, Renderer renderer)
        {
            baseColorTypeId = typeId;
            targetRenderer = renderer;
        }
#endif
    }
}
