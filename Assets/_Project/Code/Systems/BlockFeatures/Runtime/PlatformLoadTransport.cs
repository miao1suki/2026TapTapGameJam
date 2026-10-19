using System.Collections.Generic;
using Project.ContactCubes;
using Project.Player;
using Project.Rooms;
using UnityEngine;

namespace Project.BlockFeatures
{
    // A physics-step contact set, not an authored attachment. Each load's own
    // motion component remains the only writer of its Rigidbody.
    internal sealed class PlatformLoadTransport
    {
        private struct Load
        {
            public Rigidbody Body;
            public ContactCubeMotion Cube;
            public MechanismBaseFeature Base;
        }
        private readonly List<Load> loads = new(16);
        private readonly List<Collider> shapes = new(8);
        private readonly HashSet<PlayerController> riders = new();
        private readonly Collider[] overlaps = new Collider[128];
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly MovingPlatformFeature platform;
        private float cell;
        private bool overflow;
        internal PlatformLoadTransport(MovingPlatformFeature owner) => platform = owner;
        internal int Count => loads.Count;
        internal void Clear() { loads.Clear(); riders.Clear(); overflow = false; }

        internal void Refresh(Collider[] surfaces, float cellSize)
        {
            Clear(); cell = cellSize;
            if (surfaces == null) return;
            foreach (Collider shape in surfaces) if (Solid(shape)) CollectTop(shape);
            // Include actual stacks, with a bounded local query for each load.
            for (int i = 0; i < loads.Count && i < 128; i++)
            {
                shapes.Clear(); loads[i].Body.GetComponentsInChildren(false, shapes);
                foreach (Collider shape in shapes)
                    if (Solid(shape) && shape.attachedRigidbody == loads[i].Body) CollectTop(shape);
            }
            if (loads.Count >= 128) overflow = true;
        }
        private void CollectTop(Collider support)
        {
            Bounds top = PhysicsBounds(support);
            float tolerance = cell * .12f;
            Vector3 half = new(Mathf.Max(.001f, top.extents.x - cell * .015f), tolerance,
                Mathf.Max(.001f, top.extents.z - cell * .015f));
            int count = Physics.OverlapBoxNonAlloc(new Vector3(top.center.x, top.max.y + tolerance * .5f, top.center.z),
                half, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) overflow = true;
            for (int i = 0; i < count; i++)
            {
                Collider shape = overlaps[i];
                if (!Solid(shape) || shape.transform.IsChildOf(platform.transform) ||
                    Physics.GetIgnoreLayerCollision(support.gameObject.layer, shape.gameObject.layer) ||
                    Physics.GetIgnoreCollision(support, shape)) continue;
                Bounds b = PhysicsBounds(shape);
                if (Mathf.Abs(b.min.y - top.max.y) > tolerance || b.max.x <= top.min.x + cell * .01f ||
                    b.min.x >= top.max.x - cell * .01f || b.max.z <= top.min.z || b.min.z >= top.max.z) continue;
                Rigidbody body = shape.attachedRigidbody;
                if (body == null || Contains(body)) continue;
                PlayerController player = body.GetComponent<PlayerController>();
                if (player != null)
                {
                    // Direct platform riders are already handled by the platform.
                    if (support.attachedRigidbody != null && Contains(support.attachedRigidbody) &&
                        !player.IsDead && !player.IsControlLocked && player.VerticalVelocity <= .1f) riders.Add(player);
                    continue;
                }
                var mount = body.GetComponent<PlatformMount>();
                if (mount != null && mount.IsMounted) continue;
                var cube = body.GetComponent<ContactCubeMotion>();
                var mechanism = body.GetComponent<MechanismBaseFeature>();
                if (cube != null ? !cube.CanRideMovingPlatform : mechanism == null || !mechanism.CanRideMovingPlatform) continue;
                if (!body.isKinematic && body.linearVelocity.y > .15f) continue;
                loads.Add(new Load { Body = body, Cube = cube, Base = mechanism });
            }
        }
        internal bool Contains(Rigidbody body)
        {
            if (body == null) return false;
            foreach (Load load in loads) if (load.Body == body) return true;
            return false;
        }
        private static bool Solid(Collider shape) => shape != null && shape.enabled && !shape.isTrigger && shape.gameObject.activeInHierarchy;
        private static Bounds PhysicsBounds(Collider shape)
        {
            Bounds result = shape.bounds;
            Rigidbody body = shape.attachedRigidbody;
            // Contact-cube presentation may interpolate a frame behind PhysX.
            if (body != null) result.center += body.position - body.transform.position;
            return result;
        }
        private bool Ignore(Collider own, Collider other)
        {
            if (!Solid(other) || other.transform.IsChildOf(platform.transform) || Contains(other.attachedRigidbody) ||
                riders.Contains(other.GetComponentInParent<PlayerController>()) ||
                Physics.GetIgnoreLayerCollision(own.gameObject.layer, other.gameObject.layer) || Physics.GetIgnoreCollision(own, other)) return true;
            return false;
        }
        internal bool CanMove(Vector3 delta)
        {
            if (overflow) return false;
            if (delta.sqrMagnitude < .00000001f) return true;
            foreach (Load load in loads)
            {
                if (load.Body == null || (load.Cube != null ? !load.Cube.CanRideMovingPlatform : !load.Base.CanRideMovingPlatform)) return false;
                if (load.Cube != null && !load.Cube.CanAcceptPlatformDelta(delta)) return false;
                if (load.Base != null && !load.Base.CanAcceptPlatformDelta(delta)) return false;
                var member = load.Body.GetComponent<RoomResetObject>();
                if (member != null && member.Manager != null && !member.Manager.CanPlaceInAuthoredRegion(member, load.Body.position + delta)) return false;
                shapes.Clear(); load.Body.GetComponentsInChildren(false, shapes);
                foreach (Collider shape in shapes)
                    if (Solid(shape) && shape.attachedRigidbody == load.Body && !ClearStep(shape, delta)) return false;
            }
            foreach (PlayerController rider in riders)
                if (rider != null && !ClearStep(rider.GetComponent<Collider>(), delta)) return false;
            return true;
        }
        private bool ClearStep(Collider shape, Vector3 delta)
        {
            if (!Solid(shape)) return true;
            Bounds b = PhysicsBounds(shape);
            Vector3 half = Vector3.Max(b.extents - Vector3.one * cell * .015f, Vector3.one * .001f);
            int count = Physics.BoxCastNonAlloc(b.center, half, delta.normalized, hits, Quaternion.identity,
                delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignore(shape, hits[i].collider)) return false;
            count = Physics.OverlapBoxNonAlloc(b.center + delta, half, overlaps, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++) if (!Ignore(shape, overlaps[i])) return false;
            return true;
        }
        internal void Apply(Vector3 delta)
        {
            foreach (Load load in loads)
                if (load.Cube != null) load.Cube.ApplyPlatformDelta(delta);
                else if (load.Base != null) load.Base.ApplyPlatformDelta(delta);
            foreach (PlayerController rider in riders) if (rider != null) rider.QueuePlatformDelta(delta);
        }
    }
}
