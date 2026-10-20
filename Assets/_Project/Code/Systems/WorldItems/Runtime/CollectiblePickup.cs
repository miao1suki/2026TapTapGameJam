using System;
using System.Collections.Generic;
using System.Globalization;
using Project.ColorBlocks;
using Project.LevelEditor;
using Project.Player;
using Project.Pickups;
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
        [SerializeField, InspectorName("仅演出，不计入收集进度")] private bool excludedFromProgress;
        [SerializeField, InspectorName("收集完成")] private UnityEvent onCollected = new UnityEvent();
        public static event Action<CollectiblePickup, GameObject> Collected;
        public string ItemId => itemId;
        public int Amount => amount;
        public bool ExcludedFromProgress => excludedFromProgress;
        /// <summary>Settled gameplay progress; room reset/reuse must not clear it.</summary>
        public bool IsCollected { get; private set; }
        public bool CanBePushed => !IsCollected && CanCollect && body != null && !body.isKinematic;
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
            if (CollectibleProgressService.Register(this) && !IsCollected)
                RestoreCollectedState();
            if (IsCollected)
            {
                // An authored object or its parent may be enabled again. Do
                // not run the shared reuse path and recreate a settled reward.
                RecycleItem();
                return;
            }
            // Physics-only filtering belongs to the pickup, not to the player's
            // global layer: cubes and bases must still be able to push its body.
            PlayerController.PlayerActivated -= IgnorePlayerSolids;
            PlayerController.PlayerActivated += IgnorePlayerSolids;
            GameplayPresentationGate.GameplayRestored -= RefreshPlayerSolids;
            GameplayPresentationGate.GameplayRestored += RefreshPlayerSolids;
            base.OnEnable();
        }

        private void Start()
        {
            if (CollectibleProgressService.Register(this) && !IsCollected) RestoreCollectedState();
        }

        /// <summary>Call before enabling a cosmetic/scripted clone.</summary>
        public void SetExcludedFromProgress(bool value)
        {
            excludedFromProgress = value;
            if (value) CollectibleProgressService.Exclude(this);
            else CollectibleProgressService.Register(this);
        }

        protected override void OnDisable()
        {
            PlayerController.PlayerActivated -= IgnorePlayerSolids;
            GameplayPresentationGate.GameplayRestored -= RefreshPlayerSolids;
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
            // Presentation blocks collecting, not the persistent physical pass-through rule.
            if (!isActiveAndEnabled || IsCollected || CollectionRequested || player == null) return;
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

        private void RefreshPlayerSolids()
        {
            if (isActiveAndEnabled)
                foreach (var player in PlayerController.ActivePlayers) IgnorePlayerSolids(player);
        }
        private void TryCollect(Collider other)
        {
            if (IsCollected || other == null) return;
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
            // The updated 2D artwork is a sibling of the legacy sphere visual.
            // Move the now-suspended root so both old and new layouts share the
            // existing animation lifecycle, without a second cosmetic Tween.
            GetComponent<PickupFlightAnimator>()?.BindVisual(transform);
        }
        protected override void OnCollectionFinished()
        {
            if (IsCollected) return;
            IsCollected = true;
            CollectibleProgressService.RecordCollected(this);
            onCollected.Invoke(); Collected?.Invoke(this, CollectionActor);
        }
        protected override void RecycleItem()
        {
            if (!RoomResetObject.HideForReaction(gameObject)) gameObject.SetActive(false);
        }
        protected override void ResetItemState()
        {
            base.ResetItemState(); EnsureState();
            if (IsCollected)
            {
                SuspendCollectedBody();
                return;
            }
            // Includes already-authored instances with the old serialized flag.
            // Stay dynamic so cubes/bases can push it in either grid direction.
            body.useGravity = false; body.isKinematic = originalKinematic;
            for (int i = 0; i < shapes.Length; i++) if (shapes[i] != null) shapes[i].enabled = shapeEnabled[i];
            RefreshPlayerSolids();
        }
        public void ResetForRoom()
        {
            // Contact has already entered the shared collection lifecycle.
            // Keep its flight/callback alive across R, death, or leaving the
            // region; otherwise a reset between contact and completion loses
            // the reward, or recreates it for a second collection.
            if (IsCollected || CollectionRequested) return;
            StopItemAnimations();
            ResetForReuse();
        }

        /// <summary>
        /// Stable author identity for a future save owner. ItemId identifies a
        /// reward kind, not a world instance. No runtime instance ID, position,
        /// or random GUID is substituted when author metadata is unavailable.
        /// </summary>
        public bool TryGetStableInstanceId(out string instanceId)
        {
            instanceId = null;
            var placed = GetComponent<LevelEditorPlacedBlock>();
            string scenePath = gameObject.scene.path;
            if (placed == null || string.IsNullOrEmpty(placed.PlanningBoxId) || string.IsNullOrEmpty(scenePath))
                return false;
            Vector2Int cell = placed.PlanningLocalCell;
            scenePath = scenePath.Replace('\\', '/');
            string box = placed.PlanningBoxId;
            // Length-prefix author strings so delimiters in an asset name or
            // future planning ID cannot alias another valid key.
            instanceId = string.Format(CultureInfo.InvariantCulture, "collectible:{0}:{1}:{2}:{3}:{4}:{5}",
                scenePath.Length, scenePath, box.Length, box, cell.x, cell.y);
            return true;
        }

        /// <summary>
        /// Apply an already-settled record without awarding it again. This does
        /// not read/write disk or own a global/session save collection.
        /// </summary>
        public void RestoreCollectedState()
        {
            IsCollected = true;
            // Loading progress is not a fresh achievement/pickup notification.
            CollectibleProgressService.Register(this);
            StopItemAnimations();
            GetComponent<IDropItemCollectionReset>()?.ResetCollectionAnimation();
            EnsureState();
            SuspendCollectedBody();
            RecycleItem();
        }

        public void ResetPersistentCollectionForNewRun()
        {
            PreparePersistentCollectionForNewRun();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        // The service prepares EVERY item before enabling any of them. OnEnable
        // can notify UI/save listeners which query the entire scene immediately.
        internal void PreparePersistentCollectionForNewRun()
        {
            EnsureState();
            IsCollected = false;
            ResetForReuse();
        }

        private void SuspendCollectedBody()
        {
            if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            body.useGravity = false;
            body.isKinematic = true;
            foreach (var shape in shapes) if (shape != null) shape.enabled = false;
        }
    }
}
