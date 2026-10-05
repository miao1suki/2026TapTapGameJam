using System.Collections.Generic;
using Project.BlockFeatures;
using Project.BlockFeatures.Vine;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockAbilityHost))]
    public sealed class ColorBlock : MonoBehaviour, IInteractionTarget
    {
        [SerializeField, Tooltip("设计时基础颜色；运行时只读，不由染色行为改写。")]
        private string baseColorTypeId = "red";
        [SerializeField] private Renderer targetRenderer;
        private string currentColorTypeId;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private readonly HashSet<Collider> topContacts =
            new HashSet<Collider>();

        public string BaseColorTypeId => baseColorTypeId;
        public string CurrentColorTypeId => currentColorTypeId;
        public bool HasColor => !string.IsNullOrEmpty(currentColorTypeId);
        public bool IsActiveWater
        {
            get
            {
                IBlockWaterSource waterSource =
                    GetComponent<IBlockWaterSource>();
                return waterSource != null
                    ? waterSource.IsWaterEnabled
                    : currentColorTypeId == "blue";
            }
        }

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
                if (player == null) continue;
                GetComponent<IBlockClimbSource>()?.ExitActor(
                    player.gameObject);
                IBlockWaterSource waterSource =
                    GetComponent<IBlockWaterSource>();
                if (waterSource != null)
                {
                    waterSource.ExitActor(player.gameObject);
                }
                else
                {
                    player.ExitWater(this);
                }
            }
            contacts.Clear();
            topContacts.Clear();
            if (ColorWorldManager.Existing != null) ColorWorldManager.Existing.Unregister(this);
        }

        private void OnTriggerEnter(Collider other) => EnterContact(other);
        private void OnTriggerExit(Collider other) => LeaveContact(other);
        private void OnCollisionEnter(Collision collision) => EnterContact(collision.collider);
        private void OnCollisionExit(Collision collision) => LeaveContact(collision.collider);

        private void EnterContact(Collider other)
        {
            if (other == null || !contacts.Add(other)) return;
            bool isTopContact = IsTopContact(other);
            if (isTopContact)
            {
                topContacts.Add(other);
            }
            var owner = ColorWorldManager.Instance.Catalog?.Find(baseColorTypeId);
            if (owner == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (!isTopContact)
                {
                    GetComponent<IBlockClimbSource>()?.EnterActor(
                        player.gameObject);
                }
                if (IsActiveWater)
                {
                    IBlockWaterSource waterSource =
                        GetComponent<IBlockWaterSource>();
                    if (waterSource != null)
                    {
                        waterSource.EnterActor(player.gameObject);
                    }
                    else
                    {
                        player.EnterWater(this);
                    }
                }
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
            topContacts.Remove(other);
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            GetComponent<IBlockClimbSource>()?.ExitActor(
                player.gameObject);
            IBlockWaterSource waterSource =
                GetComponent<IBlockWaterSource>();
            if (waterSource != null)
            {
                waterSource.ExitActor(player.gameObject);
            }
            else
            {
                player.ExitWater(this);
            }
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
            RefreshWaterContacts();
        }

        internal void RefreshWaterContacts()
        {
            foreach (Collider contact in contacts)
            {
                PlayerController player = contact != null ? contact.GetComponentInParent<PlayerController>() : null;
                if (player == null) continue;
                IBlockWaterSource waterSource =
                    GetComponent<IBlockWaterSource>();
                if (waterSource != null)
                {
                    if (IsActiveWater)
                    {
                        waterSource.EnterActor(player.gameObject);
                    }
                    else
                    {
                        waterSource.ExitActor(player.gameObject);
                    }
                }
                else if (IsActiveWater)
                {
                    player.EnterWater(this);
                }
                else
                {
                    player.ExitWater(this);
                }
            }
        }

        internal void RefreshClimbContacts()
        {
            IBlockClimbSource climbSource =
                GetComponent<IBlockClimbSource>();
            foreach (Collider contact in contacts)
            {
                PlayerController player = contact != null
                    ? contact.GetComponentInParent<PlayerController>()
                    : null;
                if (player == null ||
                    topContacts.Contains(contact))
                {
                    continue;
                }

                if (climbSource != null &&
                    climbSource.IsClimbEnabled)
                {
                    climbSource.EnterActor(player.gameObject);
                }
                else
                {
                    climbSource?.ExitActor(player.gameObject);
                }
            }
        }

        private bool IsTopContact(Collider other)
        {
            Collider selfCollider = GetComponent<Collider>();
            if (selfCollider == null ||
                other == null ||
                selfCollider.isTrigger ||
                other.isTrigger)
            {
                return false;
            }

            Bounds selfBounds = selfCollider.bounds;
            Bounds otherBounds = other.bounds;
            float threshold = Mathf.Max(
                .04f,
                Mathf.Min(
                    selfBounds.size.x,
                    selfBounds.size.z) * .08f);
            return otherBounds.min.y >=
                   selfBounds.max.y - threshold;
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
