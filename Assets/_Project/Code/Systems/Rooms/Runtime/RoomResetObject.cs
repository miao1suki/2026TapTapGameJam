using System.Collections.Generic;
using Project.BlockFeatures;
using Project.ColorBlocks;
using Project.Mechanisms;
using UnityEngine;

namespace Project.Rooms
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-1100)]
    public sealed class RoomResetObject : MonoBehaviour
    {
        [SerializeField] private string regionId;
        private RoomLifecycleManager manager;
        private Transform originalParent;
        private Vector3 originalPosition, originalScale;
        private Quaternion originalRotation;
        private bool originalActive, captured;
        private struct ChildPose
        {
            public Transform target;
            public Vector3 position, scale;
            public Quaternion rotation;
            public bool active;
        }
        private struct CollisionState { public Collider target; public bool enabled, trigger; }
        private readonly List<ChildPose> childPoses = new List<ChildPose>();
        private readonly List<CollisionState> collisionStates = new List<CollisionState>();
        private readonly List<GameObject> replacements = new List<GameObject>();
        private readonly List<MonoBehaviour> resetComponents = new List<MonoBehaviour>();
        private readonly List<MovingPlatformFeature> resetPlatforms = new List<MovingPlatformFeature>();
        private DoorController door;
        private DoorButton button;
        private DoorHint doorHint;
        private ButtonLockFeature colorButton;
        private ColorKeyPickup key;
        private Project.Items.CollectiblePickup collectible;
        private RespawnAnchorFeature anchor;
        private Project.Items.DropItemBase dropItem;
        private CarryableFeature carryable;
        private Project.TemporaryObjectHideState temporaryHide;
        private ColorObject colorObject;
        private BlockAbilityHost abilityHost;
        private BlockRuntime blockRuntime;
        private Project.LevelEditor.LevelEditorPlacedBlock placedBlock;
        public string RegionId => regionId;
        public RoomLifecycleManager Manager => manager;
        /// <summary>Invalidates work queued against the object's previous room state.</summary>
        public int ResetRevision { get; private set; }
        public Vector3 AuthoredWorldPosition =>
            originalParent != null
                ? originalParent.TransformPoint(originalPosition)
                : originalPosition;
        public bool Persistent
        {
            get
            {
                RefreshResetComponents();
                return door != null || button != null && button.Persistent || doorHint != null || colorButton != null && colorButton.Persistent ||
                    key != null && key.CollectionRequested ||
                    collectible != null && (collectible.IsCollected || collectible.CollectionRequested) || anchor != null;
            }
        }

        // One non-alloc component enumeration replaces repeated native lookups for
        // every persistence category on every static wall. Refresh at each reset
        // phase/property query so runtime-added/removed components remain visible.
        private void RefreshResetComponents()
        {
            GetComponents(resetComponents);
            door = null; button = null; doorHint = null; colorButton = null;
            key = null; collectible = null; anchor = null; dropItem = null;
            carryable = null; temporaryHide = null; colorObject = null;
            abilityHost = null; blockRuntime = null; placedBlock = null;
            foreach (MonoBehaviour component in resetComponents)
            {
                if (component is DoorController d) door = d;
                if (component is DoorButton b) button = b;
                if (component is DoorHint h) doorHint = h;
                if (component is ButtonLockFeature cb) colorButton = cb;
                if (component is ColorKeyPickup k) key = k;
                if (component is Project.Items.CollectiblePickup cp) collectible = cp;
                if (component is RespawnAnchorFeature a) anchor = a;
                if (component is Project.Items.DropItemBase item) dropItem = item;
                if (component is CarryableFeature c) carryable = c;
                if (component is Project.TemporaryObjectHideState hide) temporaryHide = hide;
                if (component is ColorObject co) colorObject = co;
                if (component is BlockAbilityHost host) abilityHost = host;
                if (component is BlockRuntime runtime) blockRuntime = runtime;
                if (component is Project.LevelEditor.LevelEditorPlacedBlock placed) placedBlock = placed;
            }
        }
        private void Awake() => Capture();
        private void Capture()
        {
            if (captured) return;
            captured = true;
            originalParent = transform.parent;
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;
            originalScale = transform.localScale;
            originalActive = gameObject.activeSelf;
            foreach (var child in GetComponentsInChildren<Transform>(true))
                if (child != transform) childPoses.Add(new ChildPose { target = child, position = child.localPosition,
                    rotation = child.localRotation, scale = child.localScale, active = child.gameObject.activeSelf });
            foreach (var collider in GetComponentsInChildren<Collider>(true))
                collisionStates.Add(new CollisionState { target = collider, enabled = collider.enabled, trigger = collider.isTrigger });
            RefreshResetComponents();
        }
        public void Bind(RoomLifecycleManager owner, string id) { Capture(); manager = owner; regionId = id; }
        public void SetRegion(string id) => regionId = id;

        public void RestorePose()
        {
            ResetRevision++;
            // Persistent means keep progress, not keep a temporary Boss visibility
            // lease. Completed/collecting pickups remain protected by their own
            // lifecycle, including disabled collection colliders.
            bool persistent = Persistent;
            if (persistent && dropItem != null) return;
            // Restore only the hide lease before skipping authored pose/state.
            var hidden = temporaryHide;
            bool wasHidden = hidden != null && hidden.IsHidden;
            hidden?.Restore();
            if (wasHidden) door?.RefreshBlockingState();
            if (persistent) return;
            Capture();
            dropItem?.CancelAttractionForRoomReset();
            carryable?.CancelCarry();
            foreach (var replacement in replacements) if (replacement != null) Destroy(replacement);
            replacements.Clear();
            bool geometryChanged = wasHidden || transform.parent != originalParent || transform.localPosition != originalPosition ||
                transform.localRotation != originalRotation || transform.localScale != originalScale;
            if (transform.parent != originalParent) transform.SetParent(originalParent, false);
            if (transform.localPosition != originalPosition) transform.localPosition = originalPosition;
            if (transform.localRotation != originalRotation) transform.localRotation = originalRotation;
            if (transform.localScale != originalScale) transform.localScale = originalScale;
            foreach (var pose in childPoses)
                if (pose.target != null)
                {
                    geometryChanged |= pose.target.localPosition != pose.position || pose.target.localRotation != pose.rotation ||
                        pose.target.localScale != pose.scale || pose.target.gameObject.activeSelf != pose.active;
                    if (pose.target.localPosition != pose.position) pose.target.localPosition = pose.position;
                    if (pose.target.localRotation != pose.rotation) pose.target.localRotation = pose.rotation;
                    if (pose.target.localScale != pose.scale) pose.target.localScale = pose.scale;
                    if (pose.target.gameObject.activeSelf != pose.active) pose.target.gameObject.SetActive(pose.active);
                }
            foreach (var state in collisionStates)
                if (state.target != null)
                {
                    geometryChanged |= state.target.enabled != state.enabled || state.target.isTrigger != state.trigger;
                    if (state.target.enabled != state.enabled) state.target.enabled = state.enabled;
                    if (state.target.isTrigger != state.trigger) state.target.isTrigger = state.trigger;
                }
            if (TryGetComponent(out Rigidbody body))
            {
                body.position = transform.position; body.rotation = transform.rotation;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            if (gameObject.activeSelf != originalActive) gameObject.SetActive(originalActive);
            if (geometryChanged) placedBlock?.NotifySolidGeometryChanged();
        }

        public void RestoreState()
        {
            if (Persistent) return;
            colorObject?.ResetForRoom();
            var host = abilityHost;
            if (host != null) host.ResetForRoom();
            // Color reactivation may add motion / carry helpers in OnAttach.
            // Preserve the previous post-host discovery boundary for reset dispatch,
            // without adding another native lookup to ordinary static walls.
            if (colorObject != null || host != null) GetComponents(resetComponents);
            for (int index = 0; index < resetComponents.Count; index++)
            {
                var behaviour = resetComponents[index];
                if (behaviour == null) continue;
                if (behaviour is IRoomColorResettable resettable && !(behaviour is ColorObject) &&
                    (!(behaviour is BlockFeature feature) || feature.IsAttached) &&
                    (host == null || !(behaviour is BlockFeature))) resettable.ResetForRoom();
            }
            // A platform can finish a route while detached/disabled. Host reset
            // already reset attached features; only repair the remaining ones.
            GetComponentsInChildren(true, resetPlatforms);
            foreach (MovingPlatformFeature platform in resetPlatforms)
                if (platform != null && !platform.IsAttached) platform.ResetForRoom();
            if (host == null) blockRuntime?.RefreshFeatureSet(true);
        }

        /// <summary>Keep authored objects alive; temporary reactions are disposed at room reset.</summary>
        public static bool HideForReaction(GameObject target, GameObject replacement = null)
        {
            var member = target.GetComponent<RoomResetObject>();
            if (member == null || member.manager == null) return false;
            if (replacement != null) member.replacements.Add(replacement);
            target.SetActive(false);
            return true;
        }
    }
}
