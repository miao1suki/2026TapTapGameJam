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
        public string RegionId => regionId;
        public RoomLifecycleManager Manager => manager;
        public Vector3 AuthoredWorldPosition =>
            originalParent != null
                ? originalParent.TransformPoint(originalPosition)
                : originalPosition;
        public bool Persistent => GetComponent<DoorController>() != null || (GetComponent<DoorButton>() is DoorButton button && button.Persistent) ||
                                  GetComponent<DoorHint>() != null || GetComponent<ButtonLockFeature>() != null ||
                                  (GetComponent<ColorKeyPickup>() is ColorKeyPickup key && key.CollectionRequested) ||
                                  GetComponent<RespawnAnchorFeature>() != null;
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
        }
        public void Bind(RoomLifecycleManager owner, string id) { Capture(); manager = owner; regionId = id; }
        public void SetRegion(string id) => regionId = id;

        public void RestorePose()
        {
            if (Persistent) return;
            Capture();
            GetComponent<Project.TemporaryObjectHideState>()?.Restore();
            GetComponent<Project.Items.DropItemBase>()?.CancelAttractionForRoomReset();
            GetComponent<CarryableFeature>()?.CancelCarry();
            foreach (var replacement in replacements) if (replacement != null) Destroy(replacement);
            replacements.Clear();
            transform.SetParent(originalParent, false);
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            foreach (var pose in childPoses)
                if (pose.target != null)
                {
                    pose.target.localPosition = pose.position; pose.target.localRotation = pose.rotation;
                    pose.target.localScale = pose.scale; pose.target.gameObject.SetActive(pose.active);
                }
            foreach (var state in collisionStates)
                if (state.target != null) { state.target.enabled = state.enabled; state.target.isTrigger = state.trigger; }
            if (TryGetComponent(out Rigidbody body))
            {
                body.position = transform.position; body.rotation = transform.rotation;
                if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; }
            }
            gameObject.SetActive(originalActive);
        }

        public void RestoreState()
        {
            if (Persistent) return;
            GetComponent<ColorObject>()?.ResetForRoom();
            var host = GetComponent<BlockAbilityHost>();
            if (host != null) host.ResetForRoom();
            foreach (var behaviour in GetComponents<MonoBehaviour>())
                if (behaviour is IRoomColorResettable resettable && !(behaviour is ColorObject) &&
                    (!(behaviour is BlockFeature feature) || feature.IsAttached) &&
                    (host == null || !(behaviour is BlockFeature))) resettable.ResetForRoom();
            // A platform can finish a route while detached/disabled. Host reset
            // only visits attached features, so always restore authored motion.
            foreach (MovingPlatformFeature platform in
                     GetComponentsInChildren<MovingPlatformFeature>(
                         true))
            {
                platform.ResetForRoom();
            }
            if (host == null) GetComponent<BlockRuntime>()?.RefreshFeatureSet(true);
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
