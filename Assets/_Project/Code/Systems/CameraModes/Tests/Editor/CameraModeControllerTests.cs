using NUnit.Framework;
using UnityEngine;

namespace Project.CameraModes.Tests
{
    public sealed class CameraModeControllerTests
    {
        private sealed class Requester : ICameraViewModeRequester
        {
            public string CameraModeRequesterName => "Native 2D camera test";
            public int CameraModeRequestPriority { get; set; }
        }
        private GameObject owner, actor;
        private Camera camera;
        private CameraModeController controller;
        [SetUp] public void SetUp()
        {
            owner = new GameObject("Planar camera test");
            camera = owner.AddComponent<Camera>();
            controller = owner.AddComponent<CameraModeController>();
            actor = new GameObject("Focus");
            actor.transform.position = new Vector3(4f, 2f, 3f);
            controller.Configure(camera, actor.transform, true);
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(owner); Object.DestroyImmediate(actor); }
        [Test] public void Gameplay_UsesOrthographicXYAndFixedPresentationDepth()
        {
            Assert.That(camera.orthographic, Is.True);
            Assert.That(owner.transform.position.x, Is.EqualTo(4f).Within(.001f));
            Assert.That(owner.transform.position.z, Is.EqualTo(controller.PresentationDepth).Within(.001f));
            Assert.That(Quaternion.Angle(owner.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
            actor.transform.position += Vector3.forward * 20f;
            controller.Tick(.1f);
            Assert.That(owner.transform.position.z, Is.EqualTo(controller.PresentationDepth).Within(.001f));
        }
        [Test] public void OldSerializedModeValue_CannotRestorePerspective()
        {
            controller.SnapToMode((CameraViewMode)1);
            Assert.That(controller.CurrentMode, Is.EqualTo(CameraViewMode.Side2D));
            Assert.That(camera.orthographic, Is.True);
        }
        [Test] public void RollRequest_RotatesScreenWithoutChangingTheGameplayPlane()
        {
            var handle = controller.RequestMode(new Requester(), CameraViewMode.Side2D, 90f, true);
            Assert.That(Vector3.Dot(owner.transform.forward, Vector3.forward), Is.GreaterThan(.99999f));
            Assert.That(Quaternion.Angle(owner.transform.rotation, Quaternion.Euler(0f, 0f, 90f)), Is.LessThan(.001f));
            Assert.That(owner.transform.position.x, Is.EqualTo(4f).Within(.001f));
            handle.Release(true);
            Assert.That(Quaternion.Angle(owner.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
        }
        [Test] public void RollRequests_RespectPriorityAndRestoreLowerLease()
        {
            var low = controller.RequestMode(new Requester { CameraModeRequestPriority = 1 }, CameraViewMode.Side2D, 45f, true);
            var high = controller.RequestMode(new Requester { CameraModeRequestPriority = 2 }, CameraViewMode.Side2D, 90f, true);
            Assert.That(controller.Side2DYaw, Is.EqualTo(90f).Within(.001f));
            high.Release(true);
            Assert.That(controller.Side2DYaw, Is.EqualTo(45f).Within(.001f));
            low.Release(true);
            Assert.That(controller.Side2DYaw, Is.EqualTo(0f).Within(.001f));
        }
        [Test] public void InterruptedRoll_RemainsOrthographicThroughout()
        {
            var first = controller.RequestMode(new Requester(), CameraViewMode.Side2D, 90f, false);
            controller.Tick(.3f);
            Assert.That(controller.Side2DYaw, Is.InRange(.01f, 89.99f));
            float interrupted = controller.Side2DYaw;
            var second = controller.RequestMode(new Requester { CameraModeRequestPriority = 1 }, CameraViewMode.Side2D, -45f, false);
            controller.Tick(0f);
            Assert.That(controller.Side2DYaw, Is.EqualTo(interrupted).Within(.001f));
            for (int i = 0; i < 20; i++)
            {
                controller.Tick(.1f);
                Assert.That(camera.orthographic, Is.True);
                Assert.That(Vector3.Dot(owner.transform.forward, Vector3.forward), Is.GreaterThan(.99999f));
            }
            Assert.That(controller.Side2DYaw, Is.EqualTo(-45f).Within(.001f));
            second.Release(true); first.Release(true);
        }
        [Test] public void ReleasedHandle_CannotAffectANewRequest()
        {
            var first = controller.RequestMode(new Requester(), CameraViewMode.Side2D, 30f, true);
            first.Release(true);
            var second = controller.RequestMode(new Requester(), CameraViewMode.Side2D, 60f, true);
            first.Release(true);
            Assert.That(first.IsValid, Is.False);
            Assert.That(second.IsValid, Is.True);
            Assert.That(controller.Side2DYaw, Is.EqualTo(60f).Within(.001f));
            second.Release(true);
        }
    }
}