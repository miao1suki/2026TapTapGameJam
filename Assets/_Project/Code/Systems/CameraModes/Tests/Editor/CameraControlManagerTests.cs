using NUnit.Framework;
using UnityEngine;

namespace Project.CameraModes.Tests
{
    public sealed class CameraControlManagerTests
    {
        private sealed class FixedModeRequester : ICameraViewModeRequester
        {
            public string CameraModeRequesterName => "Test Mode Requester";
            public int CameraModeRequestPriority =>
                CameraControlPriorities.Cutscene;
        }

        private sealed class FixedSource : ICameraControlSource
        {
            public FixedSource(string name, Vector3 position, CameraProjectionMode projection)
            {
                CameraControlName = name;
                State = new CameraState
                {
                    position = position,
                    rotation = Quaternion.identity,
                    projection = projection,
                    orthographicSize = 5f,
                    fieldOfView = 50f,
                };
            }

            public string CameraControlName { get; }
            public CameraState State { get; set; }

            public bool TryGetCameraState(
                in CameraControlContext context,
                out CameraState state)
            {
                state = State;
                return true;
            }
        }

        private GameObject cameraObject;
        private Camera cameraComponent;
        private CameraControlManager manager;

        [SetUp]
        public void SetUp()
        {
            cameraObject = new GameObject("Managed Camera");
            cameraComponent = cameraObject.AddComponent<Camera>();
            manager = cameraObject.AddComponent<CameraControlManager>();
            manager.ConfigureOutput(cameraComponent);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void HigherPrioritySource_InterruptsFromCurrentVisualState()
        {
            FixedSource gameplay = new FixedSource(
                "Gameplay",
                new Vector3(0f, 0f, -10f),
                CameraProjectionMode.Orthographic);
            FixedSource cutscene = new FixedSource(
                "Cutscene",
                new Vector3(0f, 10f, -10f),
                CameraProjectionMode.Perspective);

            manager.RequestControl(gameplay);
            CameraControlHandle cutsceneHandle = manager.RequestControl(
                cutscene,
                CameraControlPriorities.Cutscene,
                CameraInterruptionPolicy.AllowHigherPriority,
                CameraTransition.Ease(1f));
            manager.Tick(0.25f);
            Vector3 interruptedPosition = cameraObject.transform.position;
            Matrix4x4 interruptedProjection = cameraComponent.projectionMatrix;

            manager.Retarget(cutsceneHandle, CameraTransition.Ease(1f));
            manager.Tick(0f);

            Assert.That(cameraObject.transform.position, Is.EqualTo(interruptedPosition));
            AssertMatricesEqual(interruptedProjection, cameraComponent.projectionMatrix, 0.00001f);
        }

        [Test]
        public void BlockAll_PreventsPriorityPreemptionUntilPolicyChanges()
        {
            FixedSource gameplay = new FixedSource(
                "Uninterruptible Gameplay",
                Vector3.left,
                CameraProjectionMode.Orthographic);
            FixedSource cutscene = new FixedSource(
                "Cutscene",
                Vector3.right,
                CameraProjectionMode.Perspective);
            CameraControlHandle gameplayHandle = manager.RequestControl(
                gameplay,
                CameraControlPriorities.Gameplay,
                CameraInterruptionPolicy.BlockAll);
            CameraControlHandle cutsceneHandle = manager.RequestControl(
                cutscene,
                CameraControlPriorities.Cutscene);

            Assert.That(gameplayHandle.HasControl, Is.True);
            Assert.That(cutsceneHandle.HasControl, Is.False);

            manager.SetInterruptionPolicy(
                gameplayHandle,
                CameraInterruptionPolicy.AllowHigherPriority);

            Assert.That(cutsceneHandle.HasControl, Is.True);
        }

        [Test]
        public void ReleasingCutscene_ReturnsControlToGameplaySource()
        {
            FixedSource gameplay = new FixedSource(
                "Gameplay",
                Vector3.left,
                CameraProjectionMode.Orthographic);
            FixedSource cutscene = new FixedSource(
                "Cutscene",
                Vector3.right,
                CameraProjectionMode.Perspective);
            CameraControlHandle gameplayHandle = manager.RequestControl(gameplay);
            CameraControlHandle cutsceneHandle = manager.RequestControl(
                cutscene,
                CameraControlPriorities.Cutscene);

            cutsceneHandle.Release(CameraTransition.Immediate);

            Assert.That(gameplayHandle.HasControl, Is.True);
            Assert.That(cameraObject.transform.position, Is.EqualTo(Vector3.left));
        }

        [Test]
        public void ForceTakeControl_AllowsExplicitEmergencyOverride()
        {
            FixedSource locked = new FixedSource(
                "Locked",
                Vector3.left,
                CameraProjectionMode.Orthographic);
            FixedSource emergency = new FixedSource(
                "Emergency",
                Vector3.right,
                CameraProjectionMode.Perspective);
            manager.RequestControl(
                locked,
                CameraControlPriorities.Cutscene,
                CameraInterruptionPolicy.BlockAll);
            CameraControlHandle emergencyHandle = manager.RequestControl(
                emergency,
                CameraControlPriorities.Gameplay);

            bool succeeded = manager.ForceTakeControl(
                emergencyHandle,
                CameraTransition.Immediate);

            Assert.That(succeeded, Is.True);
            Assert.That(emergencyHandle.HasControl, Is.True);
            Assert.That(cameraObject.transform.position, Is.EqualTo(Vector3.right));
        }

        [Test]
        public void ViewModeAuthority_OverridesActiveSourceProjection()
        {
            CameraModeController modeController =
                cameraObject.AddComponent<CameraModeController>();
            modeController.Configure(cameraComponent, null, true);
            modeController.SnapToMode(
                CameraViewMode.Perspective3D,
                false);
            manager.ConfigureOutput(cameraComponent);

            FixedSource source = new FixedSource(
                "Visual Only",
                new Vector3(0f, 4f, -8f),
                CameraProjectionMode.Perspective);
            manager.RequestControl(
                source,
                CameraControlPriorities.Cutscene,
                CameraInterruptionPolicy.AllowHigherPriority,
                CameraTransition.Immediate);
            modeController.RequestMode(
                new FixedModeRequester(),
                CameraViewMode.Side2D,
                true);

            manager.Tick(0f);

            Assert.That(cameraComponent.orthographic, Is.True);
        }

        private static void AssertMatricesEqual(Matrix4x4 expected, Matrix4x4 actual, float tolerance)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(tolerance));
                }
            }
        }
    }
}
