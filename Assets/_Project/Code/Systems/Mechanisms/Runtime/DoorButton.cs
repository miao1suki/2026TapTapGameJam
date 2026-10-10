using Project.BlockFeatures;
using Project.Player;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum DoorButtonState { Unpressed, Pressed }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DoorButton : MonoBehaviour
    {
        [SerializeField, InspectorName("所属门")] private DoorController owner;
        [BlockParameter(Label = "脚部容差（格）", Group = "踩踏", Order = 0)]
        [SerializeField, Min(0f)] private float footTolerance = .15f;
        [SerializeField, InspectorName("按钮模型")] private Renderer visual;
        private BoxCollider pressTrigger;
        public DoorController Owner => owner;
        public DoorButtonState State { get; private set; }
        public bool IsPressed => State == DoorButtonState.Pressed;
        private void Awake()
        {
            pressTrigger = GetComponent<BoxCollider>();
            pressTrigger.isTrigger = true;
            // Applies to existing Scene instances as well as newly placed prefabs.
            pressTrigger.center = new Vector3(0f, -.35f, 0f);
            pressTrigger.size = new Vector3(.6f, .3f, .6f);
            ResetButton();
        }
        public void SetOwner(DoorController value) => owner = value;
        private void OnDestroy() { if (owner != null) owner.ForgetButton(this); }
        private void OnTriggerEnter(Collider other) => TryPressFromContact(other);
        private void OnTriggerStay(Collider other) => TryPressFromContact(other);

        private void TryPressFromContact(Collider other)
        {
            if (IsPressed || other == null || other.isTrigger || pressTrigger == null) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            var feetCollider = player.GetComponent<CapsuleCollider>();
            if (feetCollider == null || !feetCollider.enabled) return;
            float feetY = feetCollider.bounds.min.y;
            Bounds area = pressTrigger.bounds;
            float tolerance = footTolerance * GridCellSizeUtility.Resolve(this);
            if (feetY < area.min.y - tolerance || feetY > area.max.y + tolerance) return;
            TryPress(player.gameObject);
        }

        public bool TryPress(GameObject actor)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsPressed || owner == null ||
                actor == null || actor.GetComponentInParent<PlayerController>() == null) return false;
            State = DoorButtonState.Pressed;
            ApplyAppearance();
            owner.NotifyButtonPressed(actor);
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
