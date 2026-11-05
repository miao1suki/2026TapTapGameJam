using UnityEngine;
using System.Collections.Generic;

namespace Project.Planar
{
    public enum TriggerQuery { UseGlobal, Ignore, Collide }

    /// <summary>Allocation-free XY gameplay queries against Unity's native 2D world.
    /// World-space Vector3 inputs retain presentation depth; Z never selects a physics lane.
    /// Screen rays are intersected with Collider2D, while planar rays use Box2D raycasts.
    /// No PhysX component, simulation, query or fallback is used here.</summary>
    public static class PlanarPhysics
    {
        public const int AllLayers = Physics2D.AllLayers;
        public const int DefaultRaycastLayers = Physics2D.DefaultRaycastLayers;
        public static Vector2 gravity { get => Physics2D.gravity; set => Physics2D.gravity = value; }
        public static SimulationMode2D simulationMode { get => Physics2D.simulationMode; set => Physics2D.simulationMode = value; }
        public static float defaultContactOffset { get => Physics2D.defaultContactOffset; set => Physics2D.defaultContactOffset = value; }
        public static void SyncTransforms() => Physics2D.SyncTransforms();
        public static bool Simulate(float step) => Physics2D.Simulate(step);
        public static bool GetIgnoreCollision(Collider2D a, Collider2D b) => Physics2D.GetIgnoreCollision(a, b);
        public static void IgnoreCollision(Collider2D a, Collider2D b, bool ignore = true) => Physics2D.IgnoreCollision(a, b, ignore);
        public static bool GetIgnoreLayerCollision(int a, int b) => Physics2D.GetIgnoreLayerCollision(a, b);
        public static void IgnoreLayerCollision(int a, int b, bool ignore = true) => Physics2D.IgnoreLayerCollision(a, b, ignore);

        private static ContactFilter2D Filter(int mask, TriggerQuery trigger)
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(mask);
            filter.useTriggers = trigger == TriggerQuery.Collide || trigger == TriggerQuery.UseGlobal && Physics2D.queriesHitTriggers;
            filter.useDepth = false;
            return filter;
        }

        // Unity 6's native overlap path also consults queriesHitTriggers, even when a
        // ContactFilter2D explicitly enables triggers. Keep the native flag consistent
        // for this synchronous query and restore the caller's setting on every exit.
        private readonly struct TriggerScope : System.IDisposable
        {
            private readonly bool previous;
            public TriggerScope(TriggerQuery trigger)
            {
                previous = Physics2D.queriesHitTriggers;
                if (trigger != TriggerQuery.UseGlobal)
                    Physics2D.queriesHitTriggers = trigger == TriggerQuery.Collide;
            }
            public void Dispose() => Physics2D.queriesHitTriggers = previous;
        }

        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 half, Collider2D[] results,
            Quaternion rotation, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.OverlapBox(center, new Vector2(half.x * 2f, half.y * 2f), rotation.eulerAngles.z, Filter(mask, trigger), results);
        }
        public static Collider2D[] OverlapBox(Vector3 center, Vector3 half, Quaternion rotation, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            var results = new List<Collider2D>();
            Physics2D.OverlapBox(center, new Vector2(half.x * 2f, half.y * 2f), rotation.eulerAngles.z, Filter(mask, trigger), results);
            return results.ToArray();
        }
        public static int OverlapCircleNonAlloc(Vector3 center, float radius, Collider2D[] results,
            int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.OverlapCircle(center, radius, Filter(mask, trigger), results);
        }
        public static Collider2D[] OverlapCircle(Vector3 center, float radius, int mask = AllLayers)
            => Physics2D.OverlapCircleAll(center, radius, mask);
        private static Vector2 CapsuleSize(Vector3 a, Vector3 b, float radius)
            => new Vector2(radius * 2f, Vector2.Distance(a, b) + radius * 2f);
        private static float CapsuleAngle(Vector3 a, Vector3 b)
            => -Vector2.SignedAngle((Vector2)(a - b), Vector2.up);
        public static int OverlapCapsuleNonAlloc(Vector3 a, Vector3 b, float radius, Collider2D[] results,
            int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.OverlapCapsule((a + b) * .5f, CapsuleSize(a, b, radius), CapsuleDirection2D.Vertical,
                CapsuleAngle(a, b), Filter(mask, trigger), results);
        }
        public static int BoxCastNonAlloc(Vector3 center, Vector3 half, Vector3 direction, RaycastHit2D[] results,
            Quaternion rotation, float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.BoxCast(center, new Vector2(half.x * 2f, half.y * 2f), rotation.eulerAngles.z,
                direction, Filter(mask, trigger), results, distance);
        }
        public static int CapsuleCastNonAlloc(Vector3 a, Vector3 b, float radius, Vector3 direction, RaycastHit2D[] results,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.CapsuleCast((a + b) * .5f, CapsuleSize(a, b, radius), CapsuleDirection2D.Vertical,
                CapsuleAngle(a, b), direction, Filter(mask, trigger), results, distance);
        }
        public static int CircleCastNonAlloc(Vector3 center, float radius, Vector3 direction, RaycastHit2D[] results,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            using var scope = new TriggerScope(trigger);
            return Physics2D.CircleCast(center, radius, direction, Filter(mask, trigger), results, distance);
        }
        public static bool CircleCast(Vector3 center, float radius, Vector3 direction, out RaycastHit2D hit,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            var results = SingleHitBuffer;
            int count = CircleCastNonAlloc(center, radius, direction, results, distance, mask, trigger);
            hit = count > 0 ? results[0] : default;
            return count > 0;
        }
        // Single synchronous cast result only; no consumer retains this scratch buffer.
        private static readonly RaycastHit2D[] SingleHitBuffer = new RaycastHit2D[1];
        private static readonly List<RaycastHit2D> ScreenHits = new List<RaycastHit2D>(64);
        private static void ScreenRay(Ray ray, float distance, int mask, TriggerQuery trigger)
        {
            // GetRayIntersection has no ContactFilter2D overload. Scope its trigger flag to this
            // synchronous native query only, restore even on failure, and collect all hits before
            // truncating the caller's buffer so foreground triggers cannot hide a solid behind them.
            using var scope = new TriggerScope(trigger);
            ScreenHits.Clear();
            Physics2D.GetRayIntersection(ray, distance, ScreenHits, mask);
        }
        public static int RaycastNonAlloc(Vector3 origin, Vector3 direction, RaycastHit2D[] results,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
            => RaycastNonAlloc(new Ray(origin, direction), results, distance, mask, trigger);
        public static int RaycastNonAlloc(Ray ray, RaycastHit2D[] results,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            if (Mathf.Abs(ray.direction.z) > .999f)
            {
                // Native ray intersection uses presentation Z solely to order screen hits.
                ScreenRay(ray, distance, mask, trigger);
                int count = Mathf.Min(results.Length, ScreenHits.Count);
                for (int i = 0; i < count; i++) results[i] = ScreenHits[i];
                return count;
            }
            using var scope = new TriggerScope(trigger);
            return Physics2D.Raycast(ray.origin, ray.direction, Filter(mask, trigger), results, distance);
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, out RaycastHit2D hit,
            float distance = float.PositiveInfinity, int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
            => Raycast(new Ray(origin, direction), out hit, distance, mask, trigger);
        public static bool Raycast(Ray ray, out RaycastHit2D hit, float distance = float.PositiveInfinity,
            int mask = AllLayers, TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            int count = RaycastNonAlloc(ray, SingleHitBuffer, distance, mask, trigger);
            hit = count > 0 ? SingleHitBuffer[0] : default;
            return count > 0;
        }
        public static bool Raycast(Vector3 origin, Vector3 direction, float distance, int mask = AllLayers,
            TriggerQuery trigger = TriggerQuery.UseGlobal) => Raycast(origin, direction, out _, distance, mask, trigger);
        public static RaycastHit2D[] RaycastAll(Ray ray, float distance, int mask = AllLayers,
            TriggerQuery trigger = TriggerQuery.UseGlobal)
        {
            if (Mathf.Abs(ray.direction.z) > .999f)
            {
                ScreenRay(ray, distance, mask, trigger);
                return ScreenHits.ToArray();
            }
            var hits = new List<RaycastHit2D>();
            using var scope = new TriggerScope(trigger);
            Physics2D.Raycast(ray.origin, ray.direction, Filter(mask, trigger), hits, distance);
            return hits.ToArray();
        }

        public static bool ComputePenetration(Collider2D a, Vector3 positionA, Quaternion rotationA,
            Collider2D b, Vector3 positionB, Quaternion rotationB, out Vector3 direction, out float depth)
        {
            // Distance at explicit poses requires explicit bodies on BOTH shapes. Static level
            // geometry deliberately has none. Query the live shapes when possible, and use two
            // reusable shapes in an isolated Box2D scene for hypothetical placement checks.
            bool live = (Vector2)positionA == (Vector2)a.transform.position &&
                        (Vector2)positionB == (Vector2)b.transform.position &&
                        Mathf.Abs(Mathf.DeltaAngle(rotationA.eulerAngles.z, a.transform.eulerAngles.z)) < .001f &&
                        Mathf.Abs(Mathf.DeltaAngle(rotationB.eulerAngles.z, b.transform.eulerAngles.z)) < .001f;
            var separation = live ? a.Distance(b) : PlanarPoseQuery.Distance(a, positionA, rotationA, b, positionB, rotationB);
            direction = -separation.normal;
            depth = Mathf.Max(0f, -separation.distance);
            return separation.isValid && separation.isOverlapped;
        }
        public static bool IntersectsXY(Bounds a, Bounds b)
            => a.min.x <= b.max.x && a.max.x >= b.min.x && a.min.y <= b.max.y && a.max.y >= b.min.y;
        public static bool ContainsXY(Bounds bounds, Vector3 point)
            => point.x >= bounds.min.x && point.x <= bounds.max.x && point.y >= bounds.min.y && point.y <= bounds.max.y;
    }
}
