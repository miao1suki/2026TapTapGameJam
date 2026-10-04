using System.Collections.Generic;
using Project.InputAbstraction;
using Project.Player;
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
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();

        public string BaseColorTypeId => baseColorTypeId;
        public string CurrentColorTypeId => currentColorTypeId;
        public bool HasColor => !string.IsNullOrEmpty(currentColorTypeId);

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null) originalMaterial = targetRenderer.sharedMaterial;
            var definition = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (definition != null) ApplyLayer(definition.unityLayer);
        }

        private void OnEnable() => ColorWorldManager.Instance.Register(this);
        private void OnDisable()
        {
            contacts.Clear();
            if (ColorWorldManager.Existing != null) ColorWorldManager.Existing.Unregister(this);
        }

        private void OnTriggerEnter(Collider other) => EnterContact(other);
        private void OnTriggerExit(Collider other) => LeaveContact(other);
        private void OnCollisionEnter(Collision collision) => EnterContact(collision.collider);
        private void OnCollisionExit(Collision collision) => LeaveContact(collision.collider);

        private void EnterContact(Collider other)
        {
            if (other == null || !contacts.Add(other)) return;
            var owner = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (owner == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                ColorInteractionRunner.Run(owner, ColorGraphNodeKind.PlayerEntered, this, player.gameObject);
            else
            {
                var block = other.GetComponentInParent<ColorBlock>();
                if (block != null && block != this)
                    ColorInteractionRunner.Run(owner, ColorGraphNodeKind.ColorBlockTouched, this, other.gameObject, block);
            }
        }

        private void LeaveContact(Collider other)
        {
            if (other == null || !contacts.Remove(other)) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var owner = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (owner != null)
                ColorInteractionRunner.Run(owner, ColorGraphNodeKind.PlayerLeft, this, player.gameObject);
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
                ApplyLayer(activeLayer.unityLayer);
            var material = definition != null ? definition.targetMaterial : catalog != null ? catalog.NeutralMaterial : null;
            if (material == null) material = originalMaterial;
            if (material != null) targetRenderer.sharedMaterial = material;
        }

        private void ApplyLayer(int layer)
        {
            if (layer < 0 || layer > 31) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.gameObject.layer = layer;
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
