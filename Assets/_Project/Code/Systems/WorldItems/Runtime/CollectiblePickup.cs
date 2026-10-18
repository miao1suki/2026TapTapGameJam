using System;
using System.Collections.Generic;
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
        private readonly List<PlayerCollisionPair> playerCollisionPairs = new List<PlayerCollisionPair>();

        private struct PlayerCollisionPair
        {
            public Collider Item;
            public Collider Player;
        }

        protected override void OnEnable()
        {
            // Physics-only filtering belongs to the pickup, not to the player's
            // global layer: cubes and bases must still be able to push its body.
            PlayerController.PlayerActivated -= IgnorePlayerSolids;
            PlayerController.PlayerActivated += IgnorePlayerSolids;
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            PlayerController.PlayerActivated -= IgnorePlayerSolids;
            RestorePlayerSolids();
            base.OnDisable();
        }

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

        private void IgnorePlayerSolids(PlayerController player)
        {
            if (!isActiveAndEnabled || !CanCollect || player == null) return;
            EnsureState();
            // Called only on activation/reset or when a player is enabled, never
            // as a per-pickup Update/FixedUpdate or a whole-scene search.
            var playerShapes = player.GetComponentsInChildren<Collider>(true);
            foreach (var itemShape in shapes)
            {
                if (itemShape == null || itemShape.isTrigger || !itemShape.enabled ||
                    !itemShape.gameObject.activeInHierarchy) continue;
                foreach (var playerShape in playerShapes)
                {
                    if (playerShape == null || playerShape.isTrigger || !playerShape.enabled ||
                        !playerShape.gameObject.activeInHierarchy ||
                        playerShape.attachedRigidbody != player.Motor) continue;
                    bool owned = false;
                    for (int i = 0; i < playerCollisionPairs.Count; i++)
                        if (playerCollisionPairs[i].Item == itemShape &&
                            playerCollisionPairs[i].Player == playerShape)
                        { owned = true; break; }
                    // Do not claim or later restore an ignore pair supplied by
                    // another system. Owned pairs must be reapplied after either
                    // collider has been disabled during collection/respawn.
                    if (!owned && Physics.GetIgnoreCollision(itemShape, playerShape)) continue;
                    if (!owned) playerCollisionPairs.Add(new PlayerCollisionPair
                        { Item = itemShape, Player = playerShape });
                    Physics.IgnoreCollision(itemShape, playerShape, true);
                }
            }
        }

        private void RestorePlayerSolids()
        {
            foreach (var pair in playerCollisionPairs)
                if (pair.Item != null && pair.Player != null && pair.Item.enabled && pair.Player.enabled &&
                    pair.Item.gameObject.activeInHierarchy && pair.Player.gameObject.activeInHierarchy)
                    Physics.IgnoreCollision(pair.Item, pair.Player, false);
            playerCollisionPairs.Clear();
        }
        private void TryCollect(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            // A carried cube can temporarily be a descendant of the player,
            // but its independent body is not the collecting actor's body.
            if (player != null && other.attachedRigidbody == player.Motor && !player.IsDead)
                TryBeginCollection(player.gameObject);
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
            if (isActiveAndEnabled)
                foreach (var player in PlayerController.ActivePlayers) IgnorePlayerSolids(player);
        }
        public void ResetForRoom()
        {
            StopItemAnimations();
            // Re-enter the shared lifecycle even when reset interrupts a collection flight.
            ResetForReuse();
        }
    }
}
