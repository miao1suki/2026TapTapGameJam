using Project.Planar;
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
            public Rigidbody2D Body;
            public Collider2D[] Shapes;
            public Vector3 Start;
            public bool Gravity;
            public RigidbodyConstraints2D Constraints;
        }
        private static readonly Dictionary<Rigidbody2D, VineGrowthLift> owners = new();
        private readonly List<BodyState> bodies = new();
        private readonly List<PlayerController> riders = new();
        private readonly HashSet<PlayerController> tipRiders = new();
        private readonly Collider2D[] hits = new Collider2D[128];
        private readonly ClimbableVineFeature vine;
        private float height, cell;
        private Bounds tipSupport;
        private bool active;
        public bool HasBodies => bodies.Count > 0 || riders.Count > 0;
        public float Progress { get; private set; }
        public bool Active => active;
        public Collider2D Blocker { get; private set; }
        public static bool IsLifting(Rigidbody2D body) => body != null && owners.ContainsKey(body);
        public static void Cancel(Rigidbody2D body)
        {
            if (body != null && owners.TryGetValue(body, out var lift)) lift.End();
        }
        public VineGrowthLift(ClimbableVineFeature vine) => this.vine = vine;

        public bool Prepare(Vector3 center, Vector3 half, Quaternion rotation, float cellSize)
        {
            if (active) return false;
            bodies.Clear(); riders.Clear(); tipRiders.Clear(); Blocker = null; Progress = 0;
            height = cell = cellSize;
            tipSupport = new Bounds(center - Vector3.up * half.y, new Vector3(half.x * 2f, 0f, 0f));
            // Only bodies resting at the growing tip may be lifted, never a side obstruction.
            if (!Collect(center, half, rotation, center.y - half.y, true)) return false;
            for (int index = 0; index < bodies.Count; index++)
            {
                BodyState state = bodies[index];
                var member = state.Body.GetComponent<RoomResetObject>();
                if (member != null && member.Manager != null &&
                    !member.Manager.CanPlaceInAuthoredRegion(member, state.Body.WorldPosition() + Vector3.up * height)) return false;
                foreach (Collider2D shape in state.Shapes)
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
                Collider2D shape = riders[index].GetComponent<Collider2D>();
                Bounds b = shape.bounds;
                Vector3 halfRider = Vector3.Max(b.extents - Vector3.one * cell * .02f, Vector3.one * .001f);
                halfRider.y += height * .5f;
                if (!Collect(b.center + Vector3.up * height * .5f, halfRider, Quaternion.identity, b.max.y, false)) return false;
            }
            return true;
        }

        private bool Solid(Collider2D hit) => hit != null && hit.enabled && !hit.isTrigger && hit.gameObject.activeInHierarchy;
        private bool ContainsBody(Rigidbody2D body)
        {
            foreach (BodyState state in bodies) if (state.Body == body) return true;
            return false;
        }
        private bool Ignore(Collider2D hit)
        {
            if (!Solid(hit) || hit.transform.IsChildOf(vine.Root.transform) ||
                PlanarPhysics.GetIgnoreLayerCollision(vine.gameObject.layer, hit.gameObject.layer)) return true;
            var segment = hit.GetComponentInParent<ClimbableVineFeature>();
            if (segment != null && segment.Root == vine.Root) return true;
            if (hit.GetComponentInParent<WaterVisualFeature>() != null ||
                hit.GetComponentInParent<InteractiveWater.InteractiveWater>() != null) return true;
            Collider2D own = vine.GetComponent<Collider2D>();
            return own != null && PlanarPhysics.GetIgnoreCollision(own, hit);
        }
        private bool Collect(Vector3 center, Vector3 half, Quaternion rotation, float top, bool atTip)
        {
            int count = PlanarPhysics.OverlapBoxNonAlloc(center, half, hits, rotation, ~0, TriggerQuery.Ignore);
            if (count == hits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider2D hit = hits[index];
                if (Ignore(hit)) continue;
                Rigidbody2D body = hit.attachedRigidbody;
                if (ContainsBody(body)) continue;
                var player = hit.GetComponentInParent<PlayerController>();
                if (player != null && riders.Contains(player)) continue;
                if (Mathf.Abs(hit.bounds.min.y - top) > cell * .08f) { Blocker = hit; return false; }
                // Direct tip riders use the same clearance/lease checks as riders on a load.
                if (player != null && !player.IsControlLocked && !player.IsGreenHookActive && player.VerticalVelocity <= .1f)
                { riders.Add(player); if (atTip) tipRiders.Add(player); continue; }
                if (player == null && body != null && !body.isKinematic && (body.gravityScale != 0f) && !IsLifting(body) &&
                    ((body.constraints & RigidbodyConstraints2D.FreezePositionY) == 0 ||
                        body.GetComponent<ContactCubeMotion>() != null || body.GetComponent<MechanismBaseFeature>() != null))
                {
                    bodies.Add(new BodyState { Body = body, Shapes = body.GetComponentsInChildren<Collider2D>(), Start = body.WorldPosition() });
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
                Rigidbody2D body = state.Body;
                state.Gravity = (body.gravityScale != 0f); state.Constraints = body.constraints; state.Start = body.WorldPosition();
                owners.Add(body, this);
                body.constraints &= ~RigidbodyConstraints2D.FreezePositionY;
                body.gravityScale = 0f;
                body.linearVelocity = Vector3.zero; body.angularVelocity = 0f;
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
                foreach (Collider2D shape in state.Shapes)
                {
                    if (!Solid(shape) || shape.attachedRigidbody != state.Body) continue;
                    if (!StepClear(shape, distance)) { End(); return false; }
                }
            }
            foreach (PlayerController rider in riders)
                if (rider != null && !rider.IsDead && !StepClear(rider.GetComponent<Collider2D>(), distance))
                { End(); return false; }
            float previousTip = tipSupport.center.y + height * Progress;
            Progress = Mathf.Min(1f, Progress + distance / height);
            foreach (BodyState state in bodies)
            {
                state.Body.linearVelocity = Vector3.zero;
                state.Body.MovePosition(state.Start + Vector3.up * (height * Progress));
            }
            foreach (PlayerController rider in riders)
                if (rider != null && !rider.IsDead)
                {
                    if (tipRiders.Contains(rider))
                    {
                        float feet = rider.GetComponent<Collider2D>().bounds.min.y + rider.Motor.WorldPosition().y - rider.transform.position.y;
                        float correction = Mathf.Max(0f, previousTip - feet);
                        rider.QueueVineGrowthSupport(Vector3.up * (distance + correction));
                    }
                    else rider.QueuePlatformDelta(Vector3.up * distance);
                }
            return true;
        }

        private bool StillRiding(PlayerController rider)
        {
            if (rider == null || rider.IsDead || rider.IsControlLocked || rider.VerticalVelocity > .1f) return false;
            Collider2D playerShape = rider.GetComponent<Collider2D>();
            if (!Solid(playerShape)) return false;
            Bounds player = playerShape.bounds;
            Vector3 offset = rider.Motor.WorldPosition() - rider.transform.position;
            Vector3 center = player.center + offset;
            float feet = player.min.y + offset.y, tolerance = cell * .12f;
            if (tipRiders.Contains(rider))
                return Mathf.Abs(feet - tipSupport.center.y - height * Progress) <= tolerance &&
                    player.max.x + offset.x > tipSupport.min.x + cell * .02f &&
                    player.min.x + offset.x < tipSupport.max.x - cell * .02f;
            foreach (BodyState state in bodies)
            foreach (Collider2D shape in state.Shapes)
            {
                if (!Solid(shape) || shape.attachedRigidbody != state.Body || PlanarPhysics.GetIgnoreCollision(shape, playerShape)) continue;
                Bounds support = shape.bounds;
                if (Mathf.Abs(feet-support.max.y) <= tolerance && center.x >= support.min.x-tolerance && center.x <= support.max.x+tolerance) return true;
            }
            return false;
        }

        private bool StepClear(Collider2D shape, float distance)
        {
            if (!Solid(shape)) return true;
            Bounds b = shape.bounds;
            Vector3 half = Vector3.Max(b.extents - Vector3.one * cell * .015f, Vector3.one * .001f);
            half.y += distance * .5f;
            int count = PlanarPhysics.OverlapBoxNonAlloc(b.center + Vector3.up * distance * .5f, half, hits, Quaternion.identity, ~0, TriggerQuery.Ignore);
            if (count == hits.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider2D hit = hits[index];
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
                state.Body.constraints = state.Constraints; state.Body.gravityScale = (state.Gravity) ? 1f : 0f;
                if (!state.Body.isKinematic) state.Body.linearVelocity = Vector3.zero;
            }
        }
    }
}
