using System;
using Project.BlockFeatures;
using Project.Player;
using Project.ContactCubes;
using Project.ColorBlocks;
using Project.Audio;
using UnityEngine;

namespace Project.Mechanisms
{
    public enum DoorButtonState { Unpressed, Pressed }
    public enum DoorButtonMode
    {
        [InspectorName("一次按下保持")] Latched,
        [InspectorName("持续压住")] Held
    }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class DoorButton : MonoBehaviour,
        IRoomColorResettable,
        IMechanismSignalSource
    {
        [SerializeField, InspectorName("所属门")] private DoorController owner;
        [BlockParameter(Label = "触发模式", Group = "踩踏", Order = -1)]
        [SerializeField, InspectorName("触发模式")] private DoorButtonMode mode;
        [BlockParameter(Label = "脚部容差（格）", Group = "踩踏", Order = 0)]
        [SerializeField, Min(0f)] private float footTolerance = .15f;
        [SerializeField, InspectorName("按钮模型")] private Renderer visual;
        private BoxCollider pressTrigger;
        private Collider[] contacts = new Collider[16];
        private MaterialPropertyBlock properties;
        public DoorController Owner => owner;
        public DoorButtonMode Mode => mode;
        public bool Persistent => mode == DoorButtonMode.Latched;
        public DoorButtonState State { get; private set; }
        public bool IsPressed => State == DoorButtonState.Pressed;
        public bool IsSignalActive => IsPressed && isActiveAndEnabled;
        public event Action<bool> SignalChanged;
        public int BindingIndex
        {
            get
            {
                if (owner != null) for (int index = 0; index < owner.Buttons.Count; index++)
                    if (owner.Buttons[index] == this) return index;
                return 0;
            }
        }
        private void Awake()
        {
            pressTrigger = GetComponent<BoxCollider>();
            pressTrigger.isTrigger = true;
            // Applies to existing Scene instances as well as newly placed prefabs.
            pressTrigger.center = new Vector3(0f, -.35f, 0f);
            pressTrigger.size = new Vector3(.6f, .3f, .6f);
            ResetButton();
        }
        public void SetOwner(DoorController value)
        {
            if (owner == value) return;
            var previous = owner;
            owner = value;
            if (previous != null) previous.ForgetButton(this);
        }
        private void OnDestroy() { if (owner != null) owner.ForgetButton(this); }
        private void FixedUpdate()
        {
            if (Persistent && IsPressed) return;
            if (pressTrigger == null || !pressTrigger.enabled) { if (!Persistent) SetPressed(false, null); return; }
            GameObject actor = FindPressureActor(pressTrigger, footTolerance * GridCellSizeUtility.Resolve(this), ref contacts);
            if (actor != null) TryPress(actor);
            else if (!Persistent) SetPressed(false,null);
        }

        internal static GameObject FindPressureActor(BoxCollider pressTrigger, float tolerance, ref Collider[] contacts)
        {
            Transform frame = pressTrigger.transform;
            Vector3 scale = frame.lossyScale;
            Vector3 half = Vector3.Scale(pressTrigger.size * .5f, new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            int count;
            do
            {
                count = Physics.OverlapBoxNonAlloc(frame.TransformPoint(pressTrigger.center),half,contacts,frame.rotation,~0,QueryTriggerInteraction.Ignore);
                if (count < contacts.Length) break;
                System.Array.Resize(ref contacts,contacts.Length * 2);
            } while (contacts.Length <= 4096);
            for (int i = 0; i < count; i++)
                if (TryGetPressureActor(contacts[i], pressTrigger, tolerance, out var actor)) return actor;
            return null;
        }

        private static bool TryGetPressureActor(Collider other, BoxCollider pressTrigger, float tolerance, out GameObject actor)
        {
            actor = null;
            if (other == null || other.isTrigger || !other.enabled) return false;
            var player = other.GetComponentInParent<PlayerController>();
            var cube = other.GetComponentInParent<ContactCube>();
            Collider feetCollider = null;
            if (player != null && player.isActiveAndEnabled && !player.IsDead) { feetCollider = player.GetComponent<CapsuleCollider>(); actor = player.gameObject; }
            else if (cube != null && cube.isActiveAndEnabled) { feetCollider = other; actor = cube.gameObject; }
            if (actor == null || feetCollider == null || !feetCollider.enabled) { actor = null; return false; }
            float feetY = feetCollider.bounds.min.y;
            Bounds area = pressTrigger.bounds;
            if (feetY < area.min.y - tolerance || feetY > area.max.y + tolerance) { actor = null; return false; }
            return true;
        }

        public bool TryPress(GameObject actor)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || IsPressed || owner == null ||
                actor == null || (actor.GetComponentInParent<PlayerController>() == null && actor.GetComponentInParent<ContactCube>() == null)) return false;
            SetPressed(true, actor);
            return true;
        }
        private void SetPressed(bool value, GameObject actor)
        {
            if (IsPressed == value) return;
            State = value ? DoorButtonState.Pressed : DoorButtonState.Unpressed;
            if (Application.isPlaying) AudioCuePlayer.Play(value ? AudioCueIds.ButtonPress : AudioCueIds.ButtonRelease);
            ApplyAppearance();
            SignalChanged?.Invoke(IsSignalActive);
            if (owner != null) owner.NotifyButtonStateChanged(this, actor);
        }
        public void ResetButton() { SetPressed(false,null); ApplyAppearance(); if (owner != null) owner.RefreshHint(); }
        public void ResetForRoom() { if (!Persistent) ResetButton(); }
        private void OnDisable() { if (!Persistent) ResetButton(); }
        private void ApplyAppearance()
        {
            if (visual == null) visual = GetComponentInChildren<Renderer>();
            if (visual == null) return;
            properties ??= new MaterialPropertyBlock();
            visual.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", IsPressed ? new Color(.3f, .85f, .5f) : new Color(.85f, .7f, .3f));
            visual.SetPropertyBlock(properties);
        }
    }
}
