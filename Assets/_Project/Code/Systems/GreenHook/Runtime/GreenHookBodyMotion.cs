using Project.BlockFeatures;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.GreenHook
{
    /// <summary>Physics ownership for hook-movable mechanism bases, independent of their signals.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class GreenHookBodyMotion : MonoBehaviour, IGreenHookMotion, IRoomColorResettable
    {
        private Rigidbody body;
        private bool active, wasKinematic, usedGravity;
        private RigidbodyConstraints savedConstraints;
        public Vector3 GreenHookPosition => body != null ? body.position : transform.position;
        public bool IsHookMoving => active;
        private void Awake() => body = GetComponent<Rigidbody>();
        public void BeginGreenHookPull()
        {
            if (active) return;
            if (body == null) body = GetComponent<Rigidbody>();
            wasKinematic = body.isKinematic; usedGravity = body.useGravity; savedConstraints = body.constraints;
            active = true; body.isKinematic = false; body.useGravity = false;
            SetGreenHookVelocity(Vector3.zero);
        }
        public void SetGreenHookVelocity(Vector3 velocity)
        {
            if (!active || body == null) return;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ |
                (velocity.sqrMagnitude < .000001f ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            body.linearVelocity = new Vector3(velocity.x, velocity.y, 0f);
        }
        public void EndGreenHookPull(bool snapToGrid, Vector3 separationDirection, float separationDistance)
        {
            if (!active || body == null) return;
            active = false; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.constraints = savedConstraints; body.useGravity = usedGravity; body.isKinematic = wasKinematic;
        }
        public void ResetForRoom() => EndGreenHookPull(false, Vector3.zero, 0f);
        private void OnDisable() => ResetForRoom();
    }
}
