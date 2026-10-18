using System;
using System.Collections.Generic;
using Project.Player;
using UnityEngine;

namespace Project.BlockFeatures
{
    // A solid masks its actual volume, not its cell or the entire lava object.
    // A small, genuinely supporting top-contact layer also tolerates solver settling.
    // Grid solids can be subtracted exactly; all remaining volumes use PhysX shape queries.
    internal sealed class LavaContactOcclusion
    {
        private readonly List<Collider> lavaVolumes = new List<Collider>();
        private readonly List<Collider> playerVolumes = new List<Collider>();
        private readonly List<Collider> blockers = new List<Collider>();
        private readonly List<Bounds> exposed = new List<Bounds>(16);
        private Collider[] queryResults = new Collider[32];
        private LavaHazardFeature owner;
        private PlayerController cachedPlayer;

        public void Bind(LavaHazardFeature feature)
        {
            owner = feature;
            feature.GetComponentsInChildren(true, lavaVolumes);
            for (int i = lavaVolumes.Count - 1; i >= 0; i--)
                if (lavaVolumes[i].GetComponentInParent<LavaHazardFeature>() != feature)
                    lavaVolumes.RemoveAt(i);
            cachedPlayer = null;
            playerVolumes.Clear();
        }

        public bool HasExposedContact(PlayerController player, float cellWorldSize)
        {
            if (cachedPlayer != player)
            {
                cachedPlayer = player;
                player.GetComponentsInChildren(true, playerVolumes);
            }

            float tolerance = Mathf.Max(.00001f, cellWorldSize * .0001f);
            foreach (Collider actor in playerVolumes)
            {
                if (actor == null || !actor.enabled || actor.isTrigger ||
                    !actor.gameObject.activeInHierarchy || actor.attachedRigidbody != player.Motor)
                    continue;

                CollectBlockers(actor, cellWorldSize, tolerance);
                foreach (Collider lava in lavaVolumes)
                {
                    if (lava == null || !lava.enabled || !lava.isTrigger ||
                        !lava.gameObject.activeInHierarchy || !lava.bounds.Intersects(actor.bounds))
                        continue;

                    if (lava is BoxCollider box)
                    {
                        if (HasExposedBoxContact(box, actor, cellWorldSize, tolerance)) return true;
                    }
                    else if (Physics.ComputePenetration(lava, lava.transform.position, lava.transform.rotation,
                                 actor, actor.transform.position, actor.transform.rotation, out _, out _))
                    {
                        // Authored lava uses boxes (including sparse merged boxes). Do not turn
                        // a future arbitrary trigger into whole-volume immunity from a nearby block.
                        return true;
                    }
                }
            }
            return false;
        }

        private void CollectBlockers(Collider actor, float cellWorldSize, float tolerance)
        {
            blockers.Clear();
            Bounds area = actor.bounds;
            // A resting capsule may sit just above its support by the PhysX contact shell.
            // Include that supporting face in the broad phase, then verify its exact bounds.
            Vector3 min = area.min;
            min.y -= Mathf.Max(tolerance, cellWorldSize * .03f);
            area.SetMinMax(min, area.max);
            int count = Query(area.center, area.extents + Vector3.one * tolerance, Quaternion.identity, Physics.AllLayers);
            for (int i = 0; i < count; i++)
            {
                Collider solid = queryResults[i];
                if (solid == actor || solid.transform.IsChildOf(cachedPlayer.transform) ||
                    solid.transform.IsChildOf(owner.transform) ||
                    Physics.GetIgnoreLayerCollision(actor.gameObject.layer, solid.gameObject.layer) ||
                    Physics.GetIgnoreCollision(actor, solid)) continue;
                blockers.Add(solid);
            }
        }

        private bool HasExposedBoxContact(BoxCollider lava, Collider actor, float cellWorldSize, float tolerance)
        {
            Quaternion rotation = lava.transform.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 origin = lava.transform.TransformPoint(lava.center);
            Vector3 half = Vector3.Scale(lava.size * .5f, Abs(lava.transform.lossyScale));
            Bounds actorInLava = TransformBounds(actor.bounds, inverse, origin);
            Vector3 min = Vector3.Max(-half, actorInLava.min);
            Vector3 max = Vector3.Min(half, actorInLava.max);
            exposed.Clear();
            AddRegion(min, max, tolerance);
            if (exposed.Count == 0) return false;

            foreach (Collider solid in blockers)
            {
                if (!(solid is BoxCollider box) || !TryAlignedBox(box, inverse, origin, out Bounds cut)) continue;
                AccommodateSettledTopContact(ref cut, actorInLava, half, inverse, actor, solid,
                    cellWorldSize, tolerance);
                // Only numerical contact skin is tolerated; exposed neighbouring lava remains lethal.
                cut.Expand(tolerance * 2f);
                for (int i = exposed.Count - 1; i >= 0; i--)
                {
                    Bounds region = exposed[i];
                    if (!region.Intersects(cut)) continue;
                    exposed.RemoveAt(i);
                    Subtract(region, cut, tolerance);
                }
                if (exposed.Count == 0) return false;
            }

            foreach (Bounds region in exposed)
            {
                if (CoveredByConvexSolid(region, rotation, origin, tolerance)) continue;
                int count = Query(origin + rotation * region.center, region.extents, rotation,
                    1 << actor.gameObject.layer);
                for (int i = 0; i < count; i++)
                    if (queryResults[i] == actor) return true;
            }
            return false;
        }

        private static void AccommodateSettledTopContact(ref Bounds solid, Bounds actor, Vector3 lavaHalf,
            Quaternion inverse, Collider actorCollider, Collider solidCollider, float cellWorldSize,
            float tolerance)
        {
            // PhysX can leave a resting dynamic box a little below the grid-aligned lava top.
            // Subtracting only that box would leave a wafer of lava over the whole supporting
            // face, burning a player who is actually standing on it. Do not enlarge its sides:
            // a narrow block, an exposed seam, and lava beside it must remain dangerous.
            Vector3 up = inverse * Vector3.up;
            int axis = Mathf.Abs(up.x) > Mathf.Abs(up.y) ? 0 : 1;
            if (Mathf.Abs(up.z) > Mathf.Abs(up[axis])) axis = 2;
            if (Mathf.Abs(up[axis]) < .99999f) return;
            float sign = Mathf.Sign(up[axis]);
            float top = sign > 0 ? solid.max[axis] : -solid.min[axis];
            float foot = sign > 0 ? actor.min[axis] : -actor.max[axis];
            float settlingSkin = Mathf.Max(tolerance, cellWorldSize * .03f);
            float remainingLava = lavaHalf[axis] - top;
            if (remainingLava <= tolerance || remainingLava > settlingSkin + tolerance) return;

            float contactSkin = Mathf.Min(settlingSkin,
                Mathf.Max(tolerance, cellWorldSize * .01f + actorCollider.contactOffset + solidCollider.contactOffset));
            if (Mathf.Abs(foot - top) > contactSkin + tolerance) return;
            Vector3 supportPoint = actor.center;
            for (int index = 0; index < 3; index++)
                if (index != axis && (supportPoint[index] < solid.min[index] - tolerance ||
                                      supportPoint[index] > solid.max[index] + tolerance)) return;

            Vector3 min = solid.min, max = solid.max;
            if (sign > 0) max[axis] = lavaHalf[axis];
            else min[axis] = -lavaHalf[axis];
            solid.SetMinMax(min, max);
        }

        private void Subtract(Bounds region, Bounds cut, float tolerance)
        {
            Vector3 lo = Vector3.Max(region.min, cut.min), hi = Vector3.Min(region.max, cut.max);
            Vector3 a = region.min, b = region.max;
            AddRegion(a, new Vector3(lo.x, b.y, b.z), tolerance);
            AddRegion(new Vector3(hi.x, a.y, a.z), b, tolerance);
            AddRegion(new Vector3(lo.x, a.y, a.z), new Vector3(hi.x, lo.y, b.z), tolerance);
            AddRegion(new Vector3(lo.x, hi.y, a.z), new Vector3(hi.x, b.y, b.z), tolerance);
            AddRegion(new Vector3(lo.x, lo.y, a.z), new Vector3(hi.x, hi.y, lo.z), tolerance);
            AddRegion(new Vector3(lo.x, lo.y, hi.z), new Vector3(hi.x, hi.y, b.z), tolerance);
        }

        private void AddRegion(Vector3 min, Vector3 max, float tolerance)
        {
            Vector3 size = max - min;
            if (size.x > tolerance && size.y > tolerance && size.z > tolerance)
                exposed.Add(new Bounds((min + max) * .5f, size));
        }

        private bool CoveredByConvexSolid(Bounds region, Quaternion rotation, Vector3 origin, float tolerance)
        {
            foreach (Collider solid in blockers)
            {
                if (!(solid is BoxCollider) && !(solid is SphereCollider) && !(solid is CapsuleCollider) &&
                    !(solid is MeshCollider mesh && mesh.convex)) continue;
                bool covers = true;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 point = origin + rotation * Corner(region, i);
                    if ((solid.ClosestPoint(point) - point).sqrMagnitude <= tolerance * tolerance) continue;
                    covers = false;
                    break;
                }
                if (covers) return true;
            }
            return false;
        }

        private int Query(Vector3 center, Vector3 half, Quaternion rotation, int layerMask)
        {
            while (true)
            {
                int count = Physics.OverlapBoxNonAlloc(center, half, queryResults, rotation,
                    layerMask, QueryTriggerInteraction.Ignore);
                if (count < queryResults.Length) return count;
                Array.Resize(ref queryResults, queryResults.Length * 2);
            }
        }

        private static bool TryAlignedBox(BoxCollider solid, Quaternion inverse, Vector3 origin, out Bounds bounds)
        {
            Quaternion relative = inverse * solid.transform.rotation;
            Vector3 x = Abs(relative * Vector3.right), y = Abs(relative * Vector3.up), z = Abs(relative * Vector3.forward);
            if (Mathf.Max(x.x, Mathf.Max(x.y, x.z)) < .99999f ||
                Mathf.Max(y.x, Mathf.Max(y.y, y.z)) < .99999f ||
                Mathf.Max(z.x, Mathf.Max(z.y, z.z)) < .99999f)
            {
                bounds = default;
                return false;
            }
            Vector3 half = Vector3.Scale(solid.size * .5f, Abs(solid.transform.lossyScale));
            bounds = new Bounds(inverse * (solid.transform.TransformPoint(solid.center) - origin),
                (x * half.x + y * half.y + z * half.z) * 2f);
            return true;
        }

        private static Bounds TransformBounds(Bounds bounds, Quaternion inverse, Vector3 origin)
        {
            Bounds result = new Bounds(inverse * (Corner(bounds, 0) - origin), Vector3.zero);
            for (int i = 1; i < 8; i++) result.Encapsulate(inverse * (Corner(bounds, i) - origin));
            return result;
        }

        private static Vector3 Corner(Bounds bounds, int index) => new Vector3(
            (index & 1) == 0 ? bounds.min.x : bounds.max.x,
            (index & 2) == 0 ? bounds.min.y : bounds.max.y,
            (index & 4) == 0 ? bounds.min.z : bounds.max.z);

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
