using Project.Items;
using UnityEngine;

namespace Project.GreenHook
{
    /// <summary>Queries the next physics step, never the visual rope or interpolated movement.</summary>
    internal static class GreenHookPhysics
    {
        private static readonly RaycastHit[][] buffers = CreateBuffers();
        private static readonly RaycastHit[] supportBuffer = new RaycastHit[32];
        private static RaycastHit[][] CreateBuffers()
        {
            var result = new RaycastHit[8][];
            for (int i = 0; i < result.Length; i++) result[i] = new RaycastHit[128];
            return result;
        }

        // An equal-width box can bridge an equal-width hole: one lip still
        // supports its trailing edge until the opposite lip supports its front.
        // At normal hook speed a physics step also skips the tiny open interval.
        // Brake only over a confirmed narrow, coplanar gap so gravity gets a
        // real opening. The movable body's small, stable horizontal collision
        // clearance below makes that opening real even for a one-cell hole.
        internal static Vector3 BrakeAcrossNarrowGap(Rigidbody body, BoxCollider shape, Vector3 velocity, float deltaTime)
        {
            if (!body.useGravity || !shape.enabled || shape.isTrigger || deltaTime <= 0f ||
                Mathf.Abs(velocity.x) < .00001f || Mathf.Abs(velocity.y) > .00001f) return velocity;
            Bounds bounds = shape.bounds;
            bounds.center += body.position - body.transform.position;
            float width = bounds.size.x;
            if (width <= .001f) return velocity;
            float skin = Mathf.Max(.001f, width * .01f);
            float probeHeight = skin * 4f;
            float probeDistance = skin * 12f;
            Vector3 nextFoot = new Vector3(bounds.center.x + velocity.x * deltaTime,
                bounds.min.y + probeHeight, bounds.center.z);
            if (TryTopSupport(body, shape, nextFoot, probeDistance, out _)) return velocity;
            float span = width + Mathf.Abs(velocity.x) * deltaTime * 2f + skin * 5f;
            if (!TryTopSupport(body, shape, nextFoot + Vector3.left * span, probeDistance, out RaycastHit left) ||
                !TryTopSupport(body, shape, nextFoot + Vector3.right * span, probeDistance, out RaycastHit right)) return velocity;
            if (!(left.collider is BoxCollider) || !(right.collider is BoxCollider) || left.collider == right.collider ||
                Mathf.Abs(left.point.y - right.point.y) > skin ||
                Mathf.Abs(left.point.y - left.collider.bounds.max.y) > skin ||
                Mathf.Abs(right.point.y - right.collider.bounds.max.y) > skin) return velocity;
            float leftEdge = left.collider.bounds.max.x;
            float rightEdge = right.collider.bounds.min.x;
            float gap = rightEdge - leftEdge;
            if (gap < width - skin * .1f || gap > width + skin * 30f ||
                nextFoot.x <= leftEdge || nextFoot.x >= rightEdge) return velocity;
            float toCenter = (leftEdge + rightEdge) * .5f - bounds.center.x;
            if (Mathf.Abs(toCenter) <= skin * .1f) velocity.x = 0f;
            else if (toCenter * velocity.x > 0f)
                velocity.x = Mathf.Sign(velocity.x) * Mathf.Min(Mathf.Abs(velocity.x), Mathf.Abs(toCenter) / deltaTime);
            return velocity;
        }

        private static bool TryTopSupport(Rigidbody body, Collider shape, Vector3 origin, float distance, out RaycastHit support)
        {
            support = default;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, supportBuffer, distance, ~0, QueryTriggerInteraction.Ignore);
            if (count == supportBuffer.Length) return false;
            float closest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = supportBuffer[i];
                Collider obstacle = hit.collider;
                if (obstacle == null || obstacle.attachedRigidbody == body || obstacle.transform.IsChildOf(body.transform) ||
                    hit.normal.y < .9f || Physics.GetIgnoreLayerCollision(shape.gameObject.layer, obstacle.gameObject.layer) ||
                    Physics.GetIgnoreCollision(shape, obstacle)) continue;
                if (hit.distance < closest) { closest = hit.distance; support = hit; }
            }
            return closest < float.MaxValue;
        }

        internal static float FreeDistance(Collider[] shapes, Transform owner, Transform other,
            Vector3 direction, float distance, float skin, bool pushCollectibles = false, int depth = 0)
            => Sweep(shapes, owner, other, direction, distance, skin, pushCollectibles, depth,
                Vector3.zero, out _);

        internal static Vector3 SlideDisplacement(Collider[] shapes, Transform owner, Vector3 movement, float skin)
        {
            Vector3 result = Vector3.zero;
            for (int iteration = 0; iteration < 3 && movement.sqrMagnitude > .0000001f; iteration++)
            {
                float distance = movement.magnitude;
                Vector3 direction = movement / distance;
                float free = Sweep(shapes, owner, null, direction, distance, skin, false, 0,
                    result, out Vector3 normal);
                result += direction * free;
                if (free >= distance - .00001f || normal.sqrMagnitude < .0001f) break;
                movement = Vector3.ProjectOnPlane(movement - direction * free, normal);
            }
            return result;
        }

        private static float Sweep(Collider[] shapes, Transform owner, Transform other,
            Vector3 direction, float distance, float skin, bool pushCollectibles, int depth,
            Vector3 originOffset, out Vector3 blockingNormal)
        {
            blockingNormal = Vector3.zero;
            if (distance <= 0f || direction.sqrMagnitude < .00001f) return 0f;
            if (depth >= buffers.Length) return 0f;
            var hits = buffers[depth];
            direction.Normalize();
            float free = distance;
            foreach (var shape in shapes)
            {
                if (shape == null || !shape.enabled || shape.isTrigger || !shape.gameObject.activeInHierarchy) continue;
                var bounds = shape.bounds;
                Vector3 half = bounds.extents - Vector3.one * skin;
                half = Vector3.Max(half, Vector3.one * .001f);
                int count = Physics.BoxCastNonAlloc(bounds.center + originOffset, half, direction, hits,
                    Quaternion.identity, distance + skin, ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) return 0f;
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i]; var obstacle = hit.collider;
                    if (obstacle == null || obstacle.transform.IsChildOf(owner) ||
                        (other != null && obstacle.transform.IsChildOf(other))) continue;
                    if (Physics.GetIgnoreLayerCollision(shape.gameObject.layer, obstacle.gameObject.layer) ||
                        Physics.GetIgnoreCollision(shape, obstacle)) continue;
                    // Parallel support contacts must not count as a wall in the pull direction.
                    if (Vector3.Dot(hit.normal, direction) >= -.01f) continue;
                    float contactShell = Mathf.Max(0f, shape.contactOffset) + Mathf.Max(0f, obstacle.contactOffset);
                    // One skin compensates for the inset cast volume; the other
                    // is the actual stopping margin. Without that margin a tiny
                    // positive command can be consumed by floor friction forever.
                    float available = Mathf.Max(0f, hit.distance - skin * 2f - contactShell);
                    if (pushCollectibles && obstacle.GetComponentInParent<CollectiblePickup>() is CollectiblePickup item && item.CanBePushed)
                    {
                        // A movable pickup is not a wall, but one pinned against a wall must not
                        // make the pulling mode alternate as the physics solver depenetrates it.
                        float pushedFree = FreeDistance(item.SolidColliders, item.transform, owner,
                            direction, distance, skin, true, depth + 1);
                        free = Mathf.Min(free, available + pushedFree);
                        continue;
                    }
                    // PhysX can keep resting bodies apart by their combined
                    // contact offsets. Treat that shell as blocked too: otherwise
                    // every step "can move" a few millimetres into the solver's
                    // separation, so target-priority never falls back to the player.
                    float allowed = available;
                    if (allowed < free)
                    {
                        free = allowed;
                        blockingNormal = hit.normal;
                    }
                }
            }
            return free;
        }
    }

    // Full-grid art/placement still uses the authored box. A movable box needs
    // a little physical clearance to enter an equally sized grid opening;
    // otherwise both floor lips can support it forever, even at zero speed.
    // Keep the resulting collider stable after releasing the hook: expanding
    // it inside the shaft would cause depenetration, re-bridging or launch.
    internal struct HorizontalPhysicsClearance
    {
        private BoxCollider shape;
        private Vector3 authoredSize;
        private int horizontalAxis;
        private float localInset;
        public Vector3 LogicalSize(BoxCollider candidate)
        {
            if (candidate == null) return Vector3.zero;
            Vector3 size = candidate.size;
            if (candidate == shape && localInset > 0f)
                size[horizontalAxis] = Mathf.Max(size[horizontalAxis], authoredSize[horizontalAxis]);
            return size;
        }

        public void Apply(BoxCollider candidate, float cellSize)
        {
            if (candidate == null || candidate.isTrigger) return;
            if (candidate != shape || localInset <= 0f)
            {
                Vector3 direction = candidate.transform.InverseTransformDirection(Vector3.right);
                int axis = Mathf.Abs(direction.y) > Mathf.Abs(direction.x) ? 1 : 0;
                if (Mathf.Abs(direction.z) > Mathf.Abs(direction[axis])) axis = 2;
                // Do not reshape tilted or irregular authored boxes.
                if (Mathf.Abs(direction[axis]) < .999f) return;
                float scale = Mathf.Abs(candidate.transform.lossyScale[axis]);
                if (scale < .00001f) return;
                shape = candidate;
                authoredSize = candidate.size;
                horizontalAxis = axis;
                localInset = Mathf.Min(.02f * cellSize / scale, authoredSize[axis] * .02f);
            }
            Vector3 size = candidate.size;
            float targetSize = Mathf.Max(.001f, authoredSize[horizontalAxis] - localInset * 2f);
            if (Mathf.Abs(size[horizontalAxis] - targetSize) > .00001f &&
                Mathf.Abs(size[horizontalAxis] - authoredSize[horizontalAxis]) > .00001f)
            {
                // Authored geometry can legitimately change on a room apply;
                // recognize the new full dimension, not a second skin pass.
                authoredSize = size;
                targetSize = Mathf.Max(.001f, authoredSize[horizontalAxis] - localInset * 2f);
            }
            // A room reset may restore authored geometry. Clamp to the same
            // target width rather than repeatedly subtracting from the box.
            size[horizontalAxis] = Mathf.Min(size[horizontalAxis], targetSize);
            if (candidate.size != size) candidate.size = size;
            candidate.contactOffset = Mathf.Min(candidate.contactOffset, .001f * cellSize);
        }
    }
}
