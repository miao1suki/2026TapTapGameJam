using System;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace TaSource.LinkEffect
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class LinkEffectDemoTarget : MonoBehaviour,
        IInteractionTarget, ILinkEffectPreviewTarget
    {
        [SerializeField] private string requiredColorId = "red";
        [SerializeField] private bool allowUnselectedForTest = true;
        [SerializeField] private Transform linkAnchor;

        public Transform LinkAnchor => linkAnchor != null ? linkAnchor : transform;
        public string RequiredColorId => requiredColorId;

        public bool CanLink(GameObject actor, string colorId)
        {
            return isActiveAndEnabled && actor != null &&
                   string.Equals(requiredColorId, colorId,
                       StringComparison.OrdinalIgnoreCase);
        }

        public bool CanInteract(GameObject actor)
        {
            if (!isActiveAndEnabled || actor == null) return false;
            PlayerColorWheel wheel = actor.GetComponent<PlayerColorWheel>();
            string selected = wheel != null ? wheel.SelectedColorId : string.Empty;
            if (string.IsNullOrWhiteSpace(selected) && allowUnselectedForTest)
                selected = requiredColorId;
            return CanLink(actor, selected);
        }

        public bool TryInteract(GameObject actor)
        {
            return CanInteract(actor);
        }
    }
}
