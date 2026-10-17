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
        private MechanismBaseFeature mechanism;
        private bool active, wasKinematic, usedGravity;
        private RigidbodyConstraints savedConstraints;
        public Vector3 GreenHookPosition => body != null ? body.position : transform.position;
        public bool IsHookMoving => active;
        public bool IsGreenHookPullActive => active;
        private void Awake() { body = GetComponent<Rigidbody>(); mechanism = GetComponent<MechanismBaseFeature>(); }
        public void BeginGreenHookPull()
        {
            if (active) return;
            if (body == null) body = GetComponent<Rigidbody>();
            VineGrowthLift.Cancel(body);
            wasKinematic = body.isKinematic; usedGravity = body.useGravity; savedConstraints = body.constraints;
            if (mechanism == null) mechanism = GetComponent<MechanismBaseFeature>();
            active = true; body.isKinematic = false;
            body.useGravity = mechanism != null ? mechanism.UsesGravity : usedGravity;
            SetGreenHookVelocity(Vector3.zero);
        }
        public void SetGreenHookVelocity(Vector3 velocity)
        {
            if (!active || body == null) return;
            // The hook command is cardinal, not the total velocity: horizontal pull
            // (and waiting for the player) must not cancel natural falling.
            body.useGravity = mechanism != null ? mechanism.UsesGravity : usedGravity;
            ApplyPullVelocity(body, velocity);
        }
        internal static void ApplyPullVelocity(Rigidbody targetBody, Vector3 velocity)
        {
            bool vertical = Mathf.Abs(velocity.y) > .00001f;
            bool horizontal = Mathf.Abs(velocity.x) > .00001f;
            targetBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ |
                (!horizontal ? RigidbodyConstraints.FreezePositionX : RigidbodyConstraints.None) |
                (!targetBody.useGravity && !vertical ? RigidbodyConstraints.FreezePositionY : RigidbodyConstraints.None);
            targetBody.linearVelocity = new Vector3(velocity.x,
                vertical ? velocity.y : targetBody.useGravity ? targetBody.linearVelocity.y : 0f, 0f);
        }
        public void EndGreenHookPull(bool snapToGrid, Vector3 separationDirection, float separationDistance)
        {
            if (!active || body == null) return;
            active = false; body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
            body.constraints = savedConstraints;
            body.useGravity = mechanism != null ? mechanism.UsesGravity : usedGravity;
            body.isKinematic = mechanism != null && !mechanism.UsesGravity || wasKinematic;
        }
        public void ResetForRoom() => EndGreenHookPull(false, Vector3.zero, 0f);
        private void OnDisable() => ResetForRoom();
    }
}
