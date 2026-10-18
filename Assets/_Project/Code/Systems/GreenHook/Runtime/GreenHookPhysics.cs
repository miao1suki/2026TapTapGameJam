using Project.Items;
using UnityEngine;

namespace Project.GreenHook
{
    /// <summary>Queries the next physics step, never the visual rope or interpolated movement.</summary>
    internal static class GreenHookPhysics
    {
        private static readonly RaycastHit[][] buffers = CreateBuffers();
        private static RaycastHit[][] CreateBuffers()
        {
            var result = new RaycastHit[8][];
            for (int i = 0; i < result.Length; i++) result[i] = new RaycastHit[128];
            return result;
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
                    if (pushCollectibles && obstacle.GetComponentInParent<CollectiblePickup>() is CollectiblePickup item && item.CanBePushed)
                    {
                        // A movable pickup is not a wall, but one pinned against a wall must not
                        // make the pulling mode alternate as the physics solver depenetrates it.
                        float pushedFree = FreeDistance(item.SolidColliders, item.transform, owner,
                            direction, distance, skin, true, depth + 1);
                        free = Mathf.Min(free, Mathf.Max(0f, hit.distance - skin) + pushedFree);
                        continue;
                    }
                    float allowed = Mathf.Max(0f, hit.distance - skin);
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
}
