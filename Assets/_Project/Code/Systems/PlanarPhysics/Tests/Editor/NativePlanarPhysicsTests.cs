using NUnit.Framework;
using UnityEngine;

namespace Project.Planar.Tests
{
    public sealed class NativePlanarPhysicsTests
    {
        private SimulationMode2D mode;
        private Vector2 gravity;
        private GameObject root;
        [SetUp] public void SetUp()
        {
            mode = Physics2D.simulationMode;
            gravity = Physics2D.gravity;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0f, -9.81f);
            root = new GameObject("Native 2D verification");
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root);
            Physics2D.simulationMode = mode;
            Physics2D.gravity = gravity;
        }
        private BoxCollider2D Box(Vector3 position, Vector2 size, bool trigger = false)
        {
            var go = new GameObject("2D shape");
            go.transform.SetParent(root.transform);
            go.transform.position = position;
            var shape = go.AddComponent<BoxCollider2D>();
            shape.size = size;
            shape.isTrigger = trigger;
            return shape;
        }
        [Test] public void FallingBody_CollidesAcrossPresentationDepth()
        {
            Box(new Vector3(10000f, 0f, 7f), new Vector2(10f, 1f));
            var actor = Box(new Vector3(10000f, 4f, -3f), Vector2.one);
            var body = actor.gameObject.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
            Physics2D.SyncTransforms();
            for (int i = 0; i < 200; i++) Physics2D.Simulate(.02f);
            Assert.That(body.position.y, Is.EqualTo(1f).Within(.04f));
            Assert.That(body.linearVelocity.y, Is.EqualTo(0f).Within(.02f));
            Assert.That(actor.transform.position.z, Is.EqualTo(-3f));
            Assert.That(body.WorldPosition().z, Is.EqualTo(-3f));
        }
        [Test] public void OverlapFilter_DoesNotMutateGlobalTriggerPolicy()
        {
            var solid = Box(new Vector3(11000f, 0f, 0f), Vector2.one);
            var trigger = Box(new Vector3(11000f, 0f, -2f), Vector2.one, true);
            var results = new Collider2D[8];
            Physics2D.SyncTransforms();
            bool policy = Physics2D.queriesHitTriggers;
            int count = PlanarPhysics.OverlapBoxNonAlloc(solid.transform.position, Vector3.one, results, Quaternion.identity, ~0, TriggerQuery.Ignore);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(results[0], Is.SameAs(solid));
            count = PlanarPhysics.OverlapBoxNonAlloc(solid.transform.position, Vector3.one, results, Quaternion.identity, ~0, TriggerQuery.Collide);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(Physics2D.queriesHitTriggers, Is.EqualTo(policy));
        }
        [Test] public void HypotheticalSeparation_DoesNotMoveAuthoredObjects()
        {
            var a = Box(new Vector3(12000f, 0f, 0f), Vector2.one);
            var b = Box(new Vector3(12002f, 0f, 1f), Vector2.one);
            Physics2D.SyncTransforms();
            Vector3 before = a.transform.position;
            Assert.That(PlanarPhysics.ComputePenetration(a, before + Vector3.right * 1.5f, Quaternion.identity,
                b, b.transform.position, Quaternion.identity, out _, out float depth), Is.True);
            Assert.That(depth, Is.GreaterThan(.45f));
            Assert.That(a.transform.position, Is.EqualTo(before));
        }
        [Test] public void ScreenPicking_ExplicitTriggerFilterCannotHideSolidBehindForegroundTrigger()
        {
            var solid = Box(new Vector3(14000f, 0f, 2f), Vector2.one);
            Box(new Vector3(14000f, 0f, 0f), Vector2.one, true);
            Physics2D.SyncTransforms();
            bool policy = Physics2D.queriesHitTriggers;
            try
            {
                Physics2D.queriesHitTriggers = false;
                var ray = new Ray(new Vector3(14000f, 0f, -10f), Vector3.forward);
                Assert.That(PlanarPhysics.Raycast(ray, out var hit, 20f, ~0, TriggerQuery.Ignore), Is.True);
                Assert.That(hit.collider, Is.SameAs(solid));
                Assert.That(PlanarPhysics.RaycastAll(ray, 20f, ~0, TriggerQuery.Collide).Length, Is.EqualTo(2));
                Assert.That(Physics2D.queriesHitTriggers, Is.False);
                Assert.That(PlanarPhysics.OverlapBox(solid.transform.position, Vector3.one, Quaternion.identity, ~0, TriggerQuery.Collide).Length, Is.EqualTo(2));
                Assert.That(Physics2D.queriesHitTriggers, Is.False);
            }
            finally { Physics2D.queriesHitTriggers = policy; }
        }
        [Test] public void BoxSweep_ReportsWallAndIgnoresVisualDepth()
        {
            var wall = Box(new Vector3(13002f, 0f, 4f), Vector2.one);
            Physics2D.SyncTransforms();
            var hits = new RaycastHit2D[8];
            int count = PlanarPhysics.BoxCastNonAlloc(new Vector3(13000f, 0f, -4f), Vector3.one * .5f,
                Vector3.right, hits, Quaternion.identity, 4f, ~0, TriggerQuery.Ignore);
            Assert.That(count, Is.GreaterThan(0));
            Assert.That(hits[0].collider, Is.SameAs(wall));
            Assert.That(hits[0].distance, Is.EqualTo(1f).Within(.03f));
        }
    }
}
