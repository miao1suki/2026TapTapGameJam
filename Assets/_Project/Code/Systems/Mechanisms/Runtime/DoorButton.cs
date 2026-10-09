using Project.BlockFeatures;
using Project.InputAbstraction;
using Project.Player;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum DoorButtonState { Unpressed, Pressed }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DoorButton : MonoBehaviour, IInteractionTarget
    {
        [SerializeField, InspectorName("所属门")] private DoorController owner;
        [BlockParameter(Label = "交互距离（格）", Group = "交互", Order = 0)]
        [SerializeField, Min(.1f)] private float interactionRadius = 1.2f;
        [SerializeField, InspectorName("按钮模型")] private Renderer visual;
        public DoorController Owner => owner;
        public DoorButtonState State { get; private set; }
        public bool IsPressed => State == DoorButtonState.Pressed;
        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            ResetButton();
        }
        public void SetOwner(DoorController value) => owner = value;
        private void OnDestroy() { if (owner != null) owner.ForgetButton(this); }
        public bool CanInteract(GameObject interactor) => isActiveAndEnabled && !IsPressed && owner != null &&
            interactor != null && interactor.GetComponentInParent<PlayerController>() != null &&
            Vector3.Distance(interactor.transform.position, transform.position) <= interactionRadius * GridCellSizeUtility.Resolve(this);
        public bool TryInteract(GameObject interactor)
        {
            if (!CanInteract(interactor)) return false;
            State = DoorButtonState.Pressed;
            ApplyAppearance();
            owner.NotifyButtonPressed(interactor);
            return true;
        }
        public void ResetButton() { State = DoorButtonState.Unpressed; ApplyAppearance(); }
        private void ApplyAppearance()
        {
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            if (visual == null) return;
            var properties = new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", IsPressed ? new Color(.3f, .85f, .5f) : new Color(.85f, .7f, .3f));
            visual.SetPropertyBlock(properties);
        }
    }
}
