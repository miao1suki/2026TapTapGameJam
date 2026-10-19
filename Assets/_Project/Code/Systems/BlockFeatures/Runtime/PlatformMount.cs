using Project.ColorBlocks;
using Project.ContactCubes;
using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>Authored platform-relative pose; carrying detaches until the room is restored.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    [AddComponentMenu("2026TapTap/机关/平台附着物")]
    public sealed class PlatformMount : MonoBehaviour, IRoomColorResettable
    {
        [SerializeField] private MovingPlatformFeature platform;
        [SerializeField] private Vector3 localAnchor;
        private ContactCubeMotion cubeMotion;
        private bool detached;
        public MovingPlatformFeature Platform => platform;
        public bool IsMounted => !detached && isActiveAndEnabled && platform != null && platform.gameObject.activeInHierarchy;
        public Vector3 TargetPosition => platform != null ? platform.transform.TransformPoint(localAnchor) : transform.position;
        public bool IsDetached => detached;

        public void Bind(MovingPlatformFeature owner)
        {
            if (owner != null && (owner.transform == transform || owner.transform.IsChildOf(transform)))
                throw new System.ArgumentException("平台附着不能绑定自身或子层级。");
            platform = owner;
            localAnchor = owner != null ? owner.transform.InverseTransformPoint(transform.position) : Vector3.zero;
            detached = false;
        }
        private void Awake() => cubeMotion = GetComponent<ContactCubeMotion>();
        private void FixedUpdate()
        {
            // Run after platform motion (including existing importer order overrides).
            // A cube's own motion component remains its only Rigidbody writer.
            if (!IsMounted) return;
            if (cubeMotion != null) cubeMotion.ApplyPlatformMount();
            else transform.position = TargetPosition;
        }
        public void DetachForCarry() => detached = true;
        public void ResetForRoom() => detached = false;
    }
}
