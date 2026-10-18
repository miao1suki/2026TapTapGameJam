using System;
using Project.ColorBlocks;
using Project.Player;
using Project.Rooms;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Items
{
    /// <summary>A neutral, pushable world pickup. Rewards are supplied by the collection listener.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class CollectiblePickup : DropItemBase, IRoomColorResettable
    {
        [SerializeField, InspectorName("收集品编号")] private string itemId = "collectible";
        [SerializeField, Min(1), InspectorName("数量")] private int amount = 1;
        [SerializeField, InspectorName("收集完成")] private UnityEvent onCollected = new UnityEvent();
        public static event Action<CollectiblePickup, GameObject> Collected;
        public string ItemId => itemId;
        public int Amount => amount;
        public bool CanBePushed => CanCollect && body != null && !body.isKinematic;
        public Collider[] SolidColliders { get { EnsureState(); return shapes; } }
        private Rigidbody body;
        private Collider[] shapes;
        private bool[] shapeEnabled;
        private bool originalKinematic;

        private void EnsureState()
        {
            if (body != null) return;
            body = GetComponent<Rigidbody>();
            originalKinematic = body.isKinematic;
            shapes = GetComponentsInChildren<Collider>(true);
            shapeEnabled = new bool[shapes.Length];
            for (int i = 0; i < shapes.Length; i++) shapeEnabled[i] = shapes[i].enabled;
        }
        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);
        private void OnCollisionEnter(Collision collision) => TryCollect(collision.collider);
        private void TryCollect(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null && !player.IsDead) TryBeginCollection(player.gameObject);
        }
        protected override void OnCollectionStarted()
        {
            EnsureState(); StopItemAnimations();
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.isKinematic = true;
            foreach (var shape in shapes) if (shape != null) shape.enabled = false;
        }
        protected override void OnCollectionFinished()
        {
            onCollected.Invoke(); Collected?.Invoke(this, CollectionActor);
        }
        protected override void RecycleItem()
        {
            if (!RoomResetObject.HideForReaction(gameObject)) gameObject.SetActive(false);
        }
        protected override void ResetItemState()
        {
            base.ResetItemState(); EnsureState();
            // Includes already-authored instances with the old serialized flag.
            // Stay dynamic so cubes/bases can push it in either grid direction.
            body.useGravity = false; body.isKinematic = originalKinematic;
            for (int i = 0; i < shapes.Length; i++) if (shapes[i] != null) shapes[i].enabled = shapeEnabled[i];
        }
        public void ResetForRoom()
        {
            StopItemAnimations();
            // Re-enter the shared lifecycle even when reset interrupts a collection flight.
            ResetForReuse();
        }
    }
}
