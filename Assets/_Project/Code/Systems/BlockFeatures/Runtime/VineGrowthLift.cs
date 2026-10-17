using System.Collections.Generic;
using Project.ContactCubes;
using Project.Player;
using Project.Rooms;
using UnityEngine;

namespace Project.BlockFeatures
{
    /// <summary>A short exclusive physics lease; growth never writes a carried/hooked body's pose.</summary>
    public sealed class VineGrowthLift
    {
        private sealed class BodyState
        {
            public Rigidbody Body;
            public Collider[] Shapes;
            public Vector3 Start;
            public bool Gravity;
            public RigidbodyConstraints Constraints;
        }
        private static readonly Dictionary<Rigidbody, VineGrowthLift> owners = new();
        private readonly List<BodyState> bodies = new();
        private readonly List<PlayerController> riders = new();
        private readonly Collider[] hits = new Collider[128];
        private readonly ClimbableVineFeature vine;
        private float height, cell;
        private bool active;
        public bool HasBodies => bodies.Count > 0;
        public float Progress { get; private set; }
        public bool Active => active;
        public Collider Blocker { get; private set; }
        public static bool IsLifting(Rigidbody body) => body != null && owners.ContainsKey(body);
        public static void Cancel(Rigidbody body)
        {
            if (body != null && owners.TryGetValue(body, out var lift)) lift.End();
        }
        public VineGrowthLift(ClimbableVineFeature vine) => this.vine = vine;

        public bool Prepare(Vector3 center, Vector3 half, Quaternion rotation, float cellSize)
        {
            if (active) return false;
            bodies.Clear(); riders.Clear(); Blocker = null; Progress = 0;
            height = cell = cellSize;
            // Only bodies resting at the growing tip may be lifted, never a side obstruction.
            if (!Collect(center, half, rotation, center.y - half.y, true)) return false;
            for (int index = 0; index < bodies.Count; index++)
            {
                BodyState state = bodies[index];
                var member = state.Body.GetComponent<RoomResetObject>();
                if (member != null && member.Manager != null &&
                    !member.Manager.CanPlaceInAuthoredRegion(member, state.Body.position + Vector3.up * height)) return false;
                foreach (Collider shape in state.Shapes)
                {
                    if (!Solid(shape) || shape.attachedRigidbody != state.Body) continue;
                    Bounds b = shape.bounds;
                    Vector3 extent = Vector3.Max(b.extents - Vector3.one * cell * .02f, Vector3.one * .001f);
                    extent.y += height * .5f;
                    if (!Collect(b.center + Vector3.up * height * .5f, extent, Quaternion.identity, b.max.y, false)) return false;
                }
            }
            // A rider's head must have the same clearance as the load below it.
            for (int index = 0; index < riders.Count; index++)
            {
                Collider shape = riders[index].GetComponent<Collider>();
                Bounds b = shape.bounds;
                Vector3 halfRider = Vector3.Max(b.extents - Vector3.one * cell * .02f, Vector3.one * .001f);
                halfRider.y += height * .5f;
                if (!Collect(b.center + Vector3.up * height * .5f, halfRider, Quaternion.identity, b.max.y, false)) return false;
            }
            return true;
        }

        private bool Solid(Collider hit) => hit != null && hit.enabled && !hit.isTrigger && hit.gameObject.activeInHierarchy;
        private bool ContainsBody(Rigidbody body)
        {
            foreach (BodyState state in bodies) if (state.Body == body) return true;
            return false;
        }
        private bool Ignore(Collider hit)
        {
            if (!Solid(hit) || hit.transform.IsChildOf(vine.Root.transform) ||
                Physics.GetIgnoreLayerCollision(vine.gameObject.layer, hit.gameObject.layer)) return true;
            var segment = hit.GetComponentInParent<ClimbableVineFeature>();
            if (segment != null && segment.Root == vine.Root) return true;
            if (hit.GetComponentInParent<WaterVisualFeature>() != null ||
                hit.GetComponentInParent<InteractiveWater.InteractiveWater>() != null) return true;
            Collider own = vine.GetComponent<Collider>();
            return own != null && Physics.GetIgnoreCollision(own, hit);
        }
        private bool Collect(Vector3 center, Vector3 half, Quaternion rotation, float top, bool atTip)
        {
            int count = Physics.OverlapBoxNonAlloc(center, half, hits, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (Ignore(hit)) continue;
                Rigidbody body = hit.attachedRigidbody;
                if (ContainsBody(body)) continue;
                var player = hit.GetComponentInParent<PlayerController>();
                if (player != null && riders.Contains(player)) continue;
                if (Mathf.Abs(hit.bounds.min.y - top) > cell * .08f) { Blocker = hit; return false; }
                // A player directly on a vine is not a movable block. Only actual load riders are carried.
                if (!atTip && player != null && !player.IsDead && player.VerticalVelocity <= .1f)
                { riders.Add(player); continue; }
                if (player == null && body != null && !body.isKinematic && body.useGravity && !IsLifting(body) &&
                    ((body.constraints & RigidbodyConstraints.FreezePositionY) == 0 ||
                        body.GetComponent<ContactCubeMotion>() != null || body.GetComponent<MechanismBaseFeature>() != null))
                {
                    bodies.Add(new BodyState { Body = body, Shapes = body.GetComponentsInChildren<Collider>(), Start = body.position });
                    continue;
                }
                Blocker = hit; return false;
            }
            return true;
        }

        public void Begin()
        {
            active = true;
            foreach (BodyState state in bodies)
            {
                Rigidbody body = state.Body;
                state.Gravity = body.useGravity; state.Constraints = body.constraints; state.Start = body.position;
                owners.Add(body, this);
                body.constraints &= ~RigidbodyConstraints.FreezePositionY;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
        }

        public bool Advance(float distance)
        {
            if (!active) return false;
            distance = Mathf.Min(distance, height * (1f - Progress));
            for (int index = riders.Count-1; index >= 0; index--)
                if (!StillRiding(riders[index])) riders.RemoveAt(index);
            // Recheck moving/new obstacles each physics step; never crush a stack against a ceiling.
            foreach (BodyState state in bodies)
            {
                if (state.Body == null || !state.Body.gameObject.activeInHierarchy || state.Body.isKinematic) { End(); return false; }
                foreach (Collider shape in state.Shapes)
                {
                    if (!Solid(shape) || shape.attachedRigidbody != state.Body) continue;
                    if (!StepClear(shape, distance)) { End(); return false; }
                }
            }
            foreach (PlayerController rider in riders)
                if (rider != null && !rider.IsDead && !StepClear(rider.GetComponent<Collider>(), distance))
                { End(); return false; }
            Progress = Mathf.Min(1f, Progress + distance / height);
            foreach (BodyState state in bodies)
            {
                state.Body.linearVelocity = Vector3.zero;
                state.Body.MovePosition(state.Start + Vector3.up * (height * Progress));
            }
            foreach (PlayerController rider in riders)
                if (rider != null && !rider.IsDead) rider.QueuePlatformDelta(Vector3.up * distance);
            return true;
        }

        private bool StillRiding(PlayerController rider)
        {
            if (rider == null || rider.IsDead || rider.IsControlLocked || rider.VerticalVelocity > .1f) return false;
            Collider playerShape = rider.GetComponent<Collider>();
            if (!Solid(playerShape)) return false;
            Bounds player = playerShape.bounds;
            Vector3 offset = rider.Motor.position - rider.transform.position;
            Vector3 center = player.center + offset;
            float feet = player.min.y + offset.y, tolerance = cell * .12f;
            foreach (BodyState state in bodies)
            foreach (Collider shape in state.Shapes)
            {
                if (!Solid(shape) || shape.attachedRigidbody != state.Body || Physics.GetIgnoreCollision(shape, playerShape)) continue;
                Bounds support = shape.bounds;
                if (Mathf.Abs(feet-support.max.y) <= tolerance && center.x >= support.min.x-tolerance && center.x <= support.max.x+tolerance &&
                    center.z >= support.min.z-tolerance && center.z <= support.max.z+tolerance) return true;
            }
            return false;
        }

        private bool StepClear(Collider shape, float distance)
        {
            if (!Solid(shape)) return true;
            Bounds b = shape.bounds;
            Vector3 half = Vector3.Max(b.extents - Vector3.one * cell * .015f, Vector3.one * .001f);
            half.y += distance * .5f;
            int count = Physics.OverlapBoxNonAlloc(b.center + Vector3.up * distance * .5f, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider hit = hits[index];
                if (Ignore(hit) || ContainsBody(hit.attachedRigidbody) ||
                    riders.Contains(hit.GetComponentInParent<PlayerController>())) continue;
                Blocker = hit; return false;
            }
            return true;
        }

        public void End()
        {
            if (!active) return;
            active = false;
            foreach (BodyState state in bodies)
            {
                // Unity's destroyed-object null must still release the managed dictionary key.
                if (!ReferenceEquals(state.Body, null)) owners.Remove(state.Body);
                if (state.Body == null) continue;
                state.Body.constraints = state.Constraints; state.Body.useGravity = state.Gravity;
                if (!state.Body.isKinematic) state.Body.linearVelocity = Vector3.zero;
            }
        }
    }
}
