using System.Collections.Generic;
using Project.BlockFeatures;
using Project.BlockFeatures.Vine;
using Project.InputAbstraction;
using Project.Interactions;
using Project.Player;
using UnityEngine;

namespace Project.ColorBlocks
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BlockAbilityHost))]
    public sealed class ColorBlock : MonoBehaviour, IInteractionTarget,
        IInteractionObjectSource
    {
        [SerializeField, Tooltip("设计时基础颜色；运行时只读，不由染色行为改写。")]
        private string baseColorTypeId = "red";
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Tooltip("可选的物体级交互定义；颜色只作为本物体属性。")]
        private InteractionObjectDefinition interactionDefinition;
        private string currentColorTypeId;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private readonly HashSet<Collider> topContacts =
            new HashSet<Collider>();

        public string BaseColorTypeId => baseColorTypeId;
        public string CurrentColorTypeId => currentColorTypeId;
        public InteractionObjectDefinition Definition => interactionDefinition;
        public GameObject InteractionGameObject => gameObject;
        public Component InteractionComponent => this;
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
            var definition = ColorRuntimeService.Instance.Catalog?.Find(baseColorTypeId);
            if (definition != null) ApplyLayer(definition.unityLayer);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ColorRuntimeService.Instance.Register(this);
                if (interactionDefinition != null)
                    InteractionManager.Register(this);
            }
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
            if (ColorRuntimeService.Existing != null) ColorRuntimeService.Existing.Unregister(this);
            if (InteractionManager.Existing != null)
                InteractionManager.Unregister(this);
        }

        private void OnTriggerEnter(Collider other) => EnterContact(other);
        private void OnTriggerExit(Collider other) => LeaveContact(other);
        private void OnTriggerStay(Collider other) => StayContact(other);
        private void OnCollisionEnter(Collision collision) => EnterContact(collision.collider);
        private void OnCollisionExit(Collision collision) => LeaveContact(collision.collider);
        private void OnCollisionStay(Collision collision) => StayContact(collision.collider);

        private void EnterContact(Collider other)
        {
            if (other == null || !contacts.Add(other)) return;
            bool isTopContact = IsTopContact(other);
            if (isTopContact)
            {
                topContacts.Add(other);
            }
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                if (interactionDefinition == null)
                {
                    OnInteractionPlayerEntered(player.gameObject);
                    return;
                }

                RaiseInteraction(
                    InteractionNodeKind.PlayerEntered,
                    player.gameObject,
                    null);
            }
            else
            {
                var block = other.GetComponentInParent<ColorBlock>();
                IInteractionObjectSource interactionOther = block != null
                    ? (IInteractionObjectSource)block
                    : other.GetComponentInParent<InteractionObject>();
                if (interactionOther != null &&
                    interactionOther.InteractionComponent != this)
                {
                    RaiseInteraction(
                        InteractionNodeKind.ObjectTouched,
                        null,
                        interactionOther);
                }
            }
        }

        private void LeaveContact(Collider other)
        {
            if (other == null || !contacts.Remove(other)) return;
            topContacts.Remove(other);
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            if (interactionDefinition == null)
            {
                OnInteractionPlayerLeft(player.gameObject);
                return;
            }

            RaiseInteraction(
                InteractionNodeKind.PlayerLeft,
                player.gameObject,
                null);
        }

        private void StayContact(Collider other)
        {
            if (other == null || !contacts.Contains(other)) return;
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                RaiseInteraction(
                    InteractionNodeKind.ObjectStay,
                    player.gameObject,
                    null);
                return;
            }

            ColorBlock block = other.GetComponentInParent<ColorBlock>();
            IInteractionObjectSource interactionOther = block != null
                ? (IInteractionObjectSource)block
                : other.GetComponentInParent<InteractionObject>();
            if (interactionOther != null &&
                interactionOther.InteractionComponent != this)
                RaiseInteraction(
                    InteractionNodeKind.ObjectStay,
                    null,
                    interactionOther);
        }

        private void RaiseInteraction(
            InteractionNodeKind trigger,
            GameObject actor,
            IInteractionObjectSource other)
        {
            if (interactionDefinition == null) return;
            InteractionManager.Raise(this, trigger, actor, other);
        }

        /// <summary>
        /// 由物体交互图的“调用方法”节点驱动玩家进入效果。
        /// </summary>
        public void OnInteractionPlayerEntered(GameObject actor)
        {
            PlayerController player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            if (player == null)
            {
                return;
            }

            foreach (Collider contact in contacts)
            {
                if (contact == null ||
                    contact.GetComponentInParent<PlayerController>() != player ||
                    topContacts.Contains(contact))
                {
                    continue;
                }

                GetComponent<IBlockClimbSource>()?.EnterActor(
                    player.gameObject);
            }

            if (!IsActiveWater)
            {
                return;
            }

            IBlockWaterSource waterSource = GetComponent<IBlockWaterSource>();
            if (waterSource != null)
            {
                waterSource.EnterActor(player.gameObject);
            }
            else
            {
                player.EnterWater(this);
            }
        }

        /// <summary>
        /// 由物体交互图的“调用方法”节点驱动玩家离开效果。
        /// </summary>
        public void OnInteractionPlayerLeft(GameObject actor)
        {
            PlayerController player = actor != null
                ? actor.GetComponentInParent<PlayerController>()
                : null;
            if (player == null)
            {
                return;
            }

            GetComponent<IBlockClimbSource>()?.ExitActor(player.gameObject);
            IBlockWaterSource waterSource = GetComponent<IBlockWaterSource>();
            if (waterSource != null)
            {
                waterSource.ExitActor(player.gameObject);
            }
            else
            {
                player.ExitWater(this);
            }
        }

        public void OnInteractionObjectTouched(GameObject other)
        {
            // 具体物体碰触的效果由交互图后续节点决定；保留公开入口供策划连线。
        }

        public void OnInteractionObjectStay(GameObject other)
        {
            // 停留行为同样由交互图后续节点决定。
        }

        public void OnInteractionPlayerStay(GameObject actor)
        {
            // 玩家停留行为由交互图后续节点决定。
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
            ColorRuntimeService.Instance.NotifyBlockChanged(this, previous, typeId);
            return true;
        }

        public bool SetCurrentColor(string typeId)
        {
            return ApplyCurrentColor(typeId);
        }

        internal void ResetToNeutral()
        {
            string previous = currentColorTypeId;
            currentColorTypeId = null;
            UpdateWaterContacts(previous);
            ApplyState();
            if (!string.IsNullOrEmpty(previous))
                ColorRuntimeService.Instance.NotifyBlockChanged(this, previous, null);
        }

        private void ApplyState()
        {
            var catalog = ColorRuntimeService.Instance.Catalog;
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
            Material neutral = ColorRuntimeService.Instance.Catalog?.NeutralMaterial;
            if (!HasColor && neutral != null)
                targetRenderer.sharedMaterial = neutral;
            targetRenderer.enabled = !HasColor && neutral != null;
        }

        public bool CanInteract(GameObject interactor) =>
            isActiveAndEnabled && ColorRuntimeService.Instance.HasRecolorAbility &&
            ColorRuntimeService.Instance.IsUnlocked(baseColorTypeId);

        public bool CanUseColorWheel(GameObject interactor)
        {
            if (!CanInteract(interactor)) return false;
            return !IsPlayerStandingOnTop(interactor);
        }

        public bool TryOpenColorWheel(GameObject interactor)
        {
            if (!CanUseColorWheel(interactor)) return false;
            Collider collider = GetComponent<Collider>();
            BlockSprayEmitter emitter = GetComponent<BlockSprayEmitter>();
            if (collider != null && emitter != null && interactor != null)
            {
                emitter.SetInteractionPoint(
                    collider.ClosestPoint(interactor.transform.position));
            }
            return InteractionManager.Trigger(
                gameObject,
                InteractionNodeKind.Manual,
                interactor,
                null);
        }

        public bool TryInteract(GameObject interactor)
        {
            return TryOpenColorWheel(interactor);
        }

        private bool IsPlayerStandingOnTop(GameObject interactor)
        {
            if (interactor == null) return false;
            Collider blockCollider = GetComponent<Collider>();
            Collider actorCollider = interactor.GetComponentInParent<Collider>();
            if (blockCollider == null || actorCollider == null ||
                blockCollider.isTrigger || actorCollider.isTrigger)
            {
                return false;
            }

            Bounds block = blockCollider.bounds;
            Bounds actor = actorCollider.bounds;
            return actor.min.y >= block.max.y - .2f &&
                   actor.center.x >= block.min.x && actor.center.x <= block.max.x &&
                   actor.center.z >= block.min.z && actor.center.z <= block.max.z;
        }

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

        public void EditorConfigureInteraction(
            InteractionObjectDefinition definition)
        {
            interactionDefinition = definition;
            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.BaseColorTypeId))
            {
                baseColorTypeId = definition.BaseColorTypeId;
            }
        }
#endif
    }
}
