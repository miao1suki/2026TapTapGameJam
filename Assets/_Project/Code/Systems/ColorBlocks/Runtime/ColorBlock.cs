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
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();

        public string BaseColorTypeId => baseColorTypeId;
        public string CurrentColorTypeId => currentColorTypeId;
        public bool HasColor => !string.IsNullOrEmpty(currentColorTypeId);
        public bool IsActiveWater => currentColorTypeId == "blue";

        private void Awake()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            if (!Application.isPlaying) return;
            ApplyRuntimeVisual();
            var definition = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (definition != null) ApplyLayer(definition.unityLayer);
        }

        private void OnEnable()
        {
            if (Application.isPlaying) ColorWorldManager.Instance.Register(this);
#if UNITY_EDITOR
            else ApplyEditorPreviewMaterial();
#endif
        }
        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            foreach (Collider contact in contacts)
            {
                PlayerController player = contact != null ? contact.GetComponentInParent<PlayerController>() : null;
                if (player != null) player.ExitWater(this);
            }
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
            {
                if (IsActiveWater) player.EnterWater(this);
                ColorInteractionRunner.Run(owner, ColorGraphNodeKind.PlayerEntered, this, player.gameObject);
            }
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
            player.ExitWater(this);
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
            UpdateWaterContacts(previous);
            ApplyState();
            ColorWorldManager.Instance.NotifyBlockChanged(this, previous, typeId);
            return true;
        }

        internal void ResetToNeutral()
        {
            string previous = currentColorTypeId;
            currentColorTypeId = null;
            UpdateWaterContacts(previous);
            ApplyState();
            if (!string.IsNullOrEmpty(previous))
                ColorWorldManager.Instance.NotifyBlockChanged(this, previous, null);
        }

        private void ApplyState()
        {
            var catalog = ColorWorldManager.Instance.Catalog;
            var definition = catalog != null ? catalog.Find(currentColorTypeId) : null;
            var activeLayer = definition ?? (catalog != null ? catalog.Find(baseColorTypeId) : null);
            if (activeLayer != null && activeLayer.unityLayer >= 0 && activeLayer.unityLayer < 32)
                ApplyLayer(activeLayer.unityLayer);
            if (Application.isPlaying) ApplyRuntimeVisual();
            BoxCollider volume = GetComponent<BoxCollider>();
            if (volume != null) volume.isTrigger = IsActiveWater;
        }

        private void UpdateWaterContacts(string previous)
        {
            if ((previous == "blue") == IsActiveWater) return;
            foreach (Collider contact in contacts)
            {
                PlayerController player = contact != null ? contact.GetComponentInParent<PlayerController>() : null;
                if (player == null) continue;
                if (IsActiveWater) player.EnterWater(this);
                else player.ExitWater(this);
            }
        }

        private void ApplyLayer(int layer)
        {
            if (layer < 0 || layer > 31) return;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                renderer.gameObject.layer = layer;
        }

        private void ApplyRuntimeVisual()
        {
            if (targetRenderer == null) return;
            // The neutral material is the visible, locked-state placeholder.
            // Once colored, dedicated runtime art/effects replace this mesh.
            Material neutral = ColorWorldManager.Instance.Catalog?.NeutralMaterial;
            if (!HasColor && neutral != null)
                targetRenderer.sharedMaterial = neutral;
            targetRenderer.enabled = !HasColor && neutral != null;
        }

        public bool CanInteract(GameObject interactor) =>
            isActiveAndEnabled && ColorWorldManager.Instance.HasRecolorAbility &&
            ColorWorldManager.Instance.IsUnlocked(baseColorTypeId);

        // The graph is authoring data only. Mixing will be connected when rules are approved.
        public bool TryInteract(GameObject interactor) => false;

#if UNITY_EDITOR
        private void ApplyEditorPreviewMaterial()
        {
            if (targetRenderer == null) targetRenderer = GetComponentInChildren<Renderer>();
            ColorCatalog editorCatalog = Resources.Load<ColorCatalog>("ColorBlocks/ColorCatalog");
            ColorTypeDefinition definition = editorCatalog != null
                ? editorCatalog.Find(baseColorTypeId) : null;
            if (targetRenderer == null) return;
            targetRenderer.enabled = true;
            if (definition != null && definition.targetMaterial != null)
                targetRenderer.sharedMaterial = definition.targetMaterial;
        }

        public void EditorConfigure(string typeId, Renderer renderer)
        {
            baseColorTypeId = typeId;
            targetRenderer = renderer;
        }
#endif
    }
}
