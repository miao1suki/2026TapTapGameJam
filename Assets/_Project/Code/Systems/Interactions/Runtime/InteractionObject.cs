using Project.ColorBlocks;
using Project.Player;
using UnityEngine;

namespace Project.Interactions
{
    /// <summary>
    /// 通用物体交互入口。物体颜色是属性，交互图属于这个物体定义。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionObject : MonoBehaviour,
        IInteractionObjectSource
    {
        [SerializeField] private InteractionObjectDefinition definition;
        [SerializeField] private string currentColorTypeId;
        private bool registered;

        public InteractionObjectDefinition Definition => definition;
        public GameObject InteractionGameObject => gameObject;
        public string BaseColorTypeId => definition != null
            ? definition.BaseColorTypeId
            : string.Empty;
        public string CurrentColorTypeId => string.IsNullOrWhiteSpace(
            currentColorTypeId)
            ? BaseColorTypeId
            : currentColorTypeId;
        public Component InteractionComponent => this;

        private void Awake()
        {
            if (string.IsNullOrWhiteSpace(currentColorTypeId))
            {
                currentColorTypeId = BaseColorTypeId;
            }
        }

        private void OnEnable()
        {
            if (definition != null)
            {
                InteractionManager.Register(this);
                registered = true;
            }
        }

        private void OnDisable()
        {
            if (!registered)
            {
                return;
            }

            InteractionManager.Unregister(this);
            registered = false;
        }

        private void OnTriggerEnter(Collider other)
        {
            GameObject otherObject = other != null ? other.gameObject : null;
            GameObject player = otherObject != null
                ? otherObject.GetComponentInParent<PlayerController>()?.gameObject
                : null;
            InteractionManager.Raise(
                this,
                player != null
                    ? InteractionNodeKind.PlayerEntered
                    : InteractionNodeKind.ObjectTouched,
                player,
                ResolveOther(otherObject));
        }

        private void OnTriggerExit(Collider other)
        {
            GameObject otherObject = other != null ? other.gameObject : null;
            GameObject player = otherObject != null
                ? otherObject.GetComponentInParent<PlayerController>()?.gameObject
                : null;
            if (player == null)
            {
                return;
            }

            InteractionManager.Raise(
                this,
                InteractionNodeKind.PlayerLeft,
                player,
                ResolveOther(otherObject));
        }

        private void OnTriggerStay(Collider other)
        {
            InteractionManager.Raise(
                this,
                InteractionNodeKind.ObjectStay,
                other != null
                    ? other.GetComponentInParent<PlayerController>()?.gameObject
                    : null,
                ResolveOther(other != null ? other.gameObject : null));
        }

        private void OnCollisionEnter(Collision collision)
        {
            GameObject otherObject = collision != null
                ? collision.gameObject
                : null;
            InteractionManager.Raise(
                this,
                otherObject != null &&
                otherObject.GetComponentInParent<PlayerController>() != null
                    ? InteractionNodeKind.PlayerEntered
                    : InteractionNodeKind.ObjectTouched,
                otherObject != null
                    ? otherObject.GetComponentInParent<PlayerController>()?.gameObject
                    : null,
                ResolveOther(otherObject));
        }

        private void OnCollisionExit(Collision collision)
        {
            GameObject otherObject = collision != null
                ? collision.gameObject
                : null;
            if (otherObject != null &&
                otherObject.GetComponentInParent<PlayerController>() != null)
            {
                InteractionManager.Raise(
                    this,
                    InteractionNodeKind.PlayerLeft,
                    otherObject.GetComponentInParent<PlayerController>()?.gameObject,
                    ResolveOther(otherObject));
            }
        }

        public bool SetCurrentColor(string colorTypeId)
        {
            string next = colorTypeId ?? string.Empty;
            if (CurrentColorTypeId == next)
            {
                return false;
            }

            currentColorTypeId = next;
            return true;
        }

        public void Configure(
            InteractionObjectDefinition valueDefinition,
            string valueCurrentColor = null)
        {
            definition = valueDefinition;
            currentColorTypeId = string.IsNullOrWhiteSpace(
                valueCurrentColor)
                ? BaseColorTypeId
                : valueCurrentColor;
            if (Application.isPlaying && isActiveAndEnabled)
            {
                InteractionManager.Register(this);
                registered = true;
            }
        }

        public static InteractionObject EnsureOn(
            GameObject target,
            InteractionObjectDefinition valueDefinition = null)
        {
            if (target == null)
            {
                return null;
            }

            InteractionObject source = target.GetComponent<InteractionObject>();
            if (source == null)
            {
                source = target.AddComponent<InteractionObject>();
            }

            if (valueDefinition != null)
            {
                source.Configure(valueDefinition);
            }

            return source;
        }

        private static IInteractionObjectSource ResolveOther(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            InteractionObject interactionObject =
                target.GetComponentInParent<InteractionObject>();
            if (interactionObject != null)
            {
                return interactionObject;
            }

            return target.GetComponentInParent<ColorBlock>();
        }
    }

    public interface IInteractionObjectSource
    {
        InteractionObjectDefinition Definition { get; }
        GameObject InteractionGameObject { get; }
        string BaseColorTypeId { get; }
        string CurrentColorTypeId { get; }
        Component InteractionComponent { get; }
        bool SetCurrentColor(string colorTypeId);
    }
}
