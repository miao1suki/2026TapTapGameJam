using Project.Planar;
using Project.BlockFeatures;
using Project.ColorBlocks;
using UnityEngine;

namespace Project.GreenHook
{
    /// <summary>Physics ownership for hook-movable mechanism bases, independent of their signals.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody2D))]
    public sealed class GreenHookBodyMotion : MonoBehaviour, IGreenHookMotion, IRoomColorResettable
    {
        private Rigidbody2D body;
        private BoxCollider2D pullShape;
        private MechanismBaseFeature mechanism;
        private bool active, wasKinematic, usedGravity;
        private RigidbodyConstraints2D savedConstraints;
        private float cellSize;
        private HorizontalPhysicsClearance horizontalClearance;
        public Vector3 GreenHookPosition => body != null ? body.WorldPosition() : transform.position;
        public bool IsHookMoving => active;
        public bool IsGreenHookPullActive => active;
        private void Awake() { body = GetComponent<Rigidbody2D>(); mechanism = GetComponent<MechanismBaseFeature>(); pullShape = GetComponentInChildren<BoxCollider2D>(); cellSize = Mathf.Max(.01f, GridCellSizeUtility.Resolve(this)); }
        public void BeginGreenHookPull()
        {
            if (active) return;
            if (body == null) body = GetComponent<Rigidbody2D>();
            if (pullShape == null) pullShape = GetComponentInChildren<BoxCollider2D>();
            VineGrowthLift.Cancel(body);
            wasKinematic = body.isKinematic; usedGravity = (body.gravityScale != 0f); savedConstraints = body.constraints;
            if (mechanism == null) mechanism = GetComponent<MechanismBaseFeature>();
            active = true; body.isKinematic = false;
            body.gravityScale = (mechanism != null ? mechanism.UsesGravity : usedGravity) ? 1f : 0f;
            if ((body.gravityScale != 0f)) horizontalClearance.Apply(pullShape, cellSize);
            SetGreenHookVelocity(Vector3.zero);
        }
        public void SetGreenHookVelocity(Vector3 velocity)
        {
            if (!active || body == null) return;
            // The hook command is cardinal, not the total velocity: horizontal pull
            // (and waiting for the player) must not cancel natural falling.
            body.gravityScale = (mechanism != null ? mechanism.UsesGravity : usedGravity) ? 1f : 0f;
            ApplyPullVelocity(body, velocity, pullShape);
        }
        internal static void ApplyPullVelocity(Rigidbody2D targetBody, Vector3 velocity, BoxCollider2D shape = null)
        {
            if ((targetBody.gravityScale != 0f) && shape != null)
                velocity = GreenHookPhysics.BrakeAcrossNarrowGap(targetBody, shape, velocity, Time.fixedDeltaTime);
            bool vertical = Mathf.Abs(velocity.y) > .00001f;
            bool horizontal = Mathf.Abs(velocity.x) > .00001f;
            targetBody.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.None |
                (!horizontal ? RigidbodyConstraints2D.FreezePositionX : RigidbodyConstraints2D.None) |
                (!(targetBody.gravityScale != 0f) && !vertical ? RigidbodyConstraints2D.FreezePositionY : RigidbodyConstraints2D.None);
            targetBody.linearVelocity = new Vector3(velocity.x,
                vertical ? velocity.y : (targetBody.gravityScale != 0f) ? ((Vector3)targetBody.linearVelocity).y : 0f, 0f);
        }
        public void EndGreenHookPull(bool snapToGrid, Vector3 separationDirection, float separationDistance)
        {
            if (!active || body == null) return;
            active = false; body.linearVelocity = Vector3.zero; body.angularVelocity = 0f;
            body.constraints = savedConstraints;
            body.gravityScale = (mechanism != null ? mechanism.UsesGravity : usedGravity) ? 1f : 0f;
            body.isKinematic = mechanism != null && !mechanism.UsesGravity || wasKinematic;
        }
        public void ResetForRoom() => EndGreenHookPull(false, Vector3.zero, 0f);
        private void OnDisable() => ResetForRoom();
    }
}
