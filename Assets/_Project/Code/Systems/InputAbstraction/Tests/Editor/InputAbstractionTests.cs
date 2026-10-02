using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Project.InputAbstraction.Tests
{
    public sealed class InputAbstractionTests
    {
        [TearDown]
        public void TearDown()
        {
            VirtualInputState.Reset();
            if (InputService.Instance != null)
            {
                Object.DestroyImmediate(InputService.Instance.gameObject);
            }
        }

        [Test]
        public void ExternalSource_IsVisibleThroughStableFacade()
        {
            FakeInputSource fake = new FakeInputSource
            {
                Pressed = true,
                Move = new Vector2(0.25f, 0.75f),
                DeviceMode = InputDeviceMode.Touch,
            };
            GameInput.UseExternalSource(fake);

            Assert.That(GameInput.IsPressed(InputActionId.Jump), Is.True);
            Assert.That(GameInput.ReadVector2(InputActionId.Move), Is.EqualTo(fake.Move));
            Assert.That(GameInput.ActiveDeviceMode, Is.EqualTo(InputDeviceMode.Touch));

            GameInput.ClearExternalSource(fake);
        }

        [Test]
        public void DefaultActionIds_AreIndependentOfDeviceBindings()
        {
            Assert.That(InputActionId.Move.ToString(), Is.EqualTo("Move"));
            Assert.That(InputActionId.CameraModeSwitch.ToString(), Is.EqualTo("CameraModeSwitch"));
            Assert.That(InputActionId.PointerPrimary.ToString(), Is.EqualTo("PointerPrimary"));
        }

        [TestCase(RuntimePlatform.WindowsPlayer, InputPlatformMode.Desktop)]
        [TestCase(RuntimePlatform.OSXPlayer, InputPlatformMode.Desktop)]
        [TestCase(RuntimePlatform.Android, InputPlatformMode.Mobile)]
        [TestCase(RuntimePlatform.IPhonePlayer, InputPlatformMode.Mobile)]
        public void RuntimePlatform_ResolvesExpectedInputMode(
            RuntimePlatform platform,
            InputPlatformMode expected)
        {
            Assert.That(InputPlatformResolver.FromRuntimePlatform(platform), Is.EqualTo(expected));
        }

        [Test]
        public void MobileComposition_UsesVirtualInputWithoutRemovingGamepad()
        {
            FakeInputSource hardware = new FakeInputSource
            {
                Move = new Vector2(0.2f, 0f),
                DeviceMode = InputDeviceMode.Gamepad,
                GamepadAvailable = true,
            };
            VirtualInputState.SetVector(123, InputActionId.Move, new Vector2(0.8f, 0.3f));
            CompositeInputSource composite =
                new CompositeInputSource(hardware, new VirtualInputSource());

            Assert.That(
                composite.ReadVector2(InputActionId.Move),
                Is.EqualTo(new Vector2(0.8f, 0.3f)));
            Assert.That(composite.HasGamepad, Is.True);
            Assert.That(composite.HasTouch, Is.True);
            Assert.That(composite.ActiveDeviceMode, Is.EqualTo(InputDeviceMode.Touch));
        }

        [Test]
        public void PlatformLayout_RestoresSeparateDesktopAndMobilePositions()
        {
            GameObject root = new GameObject("LayoutRoot", typeof(PlatformUILayoutController));
            GameObject child = new GameObject("LayoutChild", typeof(RectTransform));
            child.transform.SetParent(root.transform, false);
            RectTransform rect = child.GetComponent<RectTransform>();
            PlatformUILayoutController controller =
                root.GetComponent<PlatformUILayoutController>();
            controller.ReplaceLayoutTargets(new[] { rect });

            rect.anchoredPosition = new Vector2(100f, 20f);
            controller.CaptureLayout(InputPlatformMode.Desktop);
            rect.anchoredPosition = new Vector2(-40f, 300f);
            controller.CaptureLayout(InputPlatformMode.Mobile);

            controller.ApplyLayout(InputPlatformMode.Desktop);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(100f, 20f)));
            controller.ApplyLayout(InputPlatformMode.Mobile);
            Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-40f, 300f)));

            Object.DestroyImmediate(root);
        }

        [Test]
        public void SetupMenu_CreatesCompletePlatformInputUi()
        {
            EventSystem existingEventSystem = Object.FindFirstObjectByType<EventSystem>();
            GameObject createdLegacyEventSystem = null;
            if (existingEventSystem == null)
            {
                createdLegacyEventSystem = new GameObject(
                    "LegacyEventSystem",
                    typeof(EventSystem),
                    typeof(StandaloneInputModule));
                existingEventSystem = createdLegacyEventSystem.GetComponent<EventSystem>();
            }

            Assert.That(
                EditorApplication.ExecuteMenuItem(
                    "GameObject/2026TapTap/Input/创建双平台输入 UI"),
                Is.True);

            PlatformUILayoutController controller =
                Object.FindFirstObjectByType<PlatformUILayoutController>(FindObjectsInactive.Include);
            Assert.That(controller, Is.Not.Null);
            Assert.That(
                controller.GetComponentsInChildren<VirtualJoystick>(true).Length,
                Is.EqualTo(2));
            Assert.That(
                controller.GetComponentsInChildren<VirtualInputButton>(true).Length,
                Is.EqualTo(2));
            Assert.That(Object.FindFirstObjectByType<InputSystemUIInputModule>(), Is.Not.Null);
            StandaloneInputModule legacyModule =
                existingEventSystem.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Assert.That(legacyModule.enabled, Is.False);
            }

            Object.DestroyImmediate(controller.gameObject);
            if (createdLegacyEventSystem != null)
            {
                Object.DestroyImmediate(createdLegacyEventSystem);
            }
        }

        private sealed class FakeInputSource : IInputSource
        {
            public bool Pressed;
            public Vector2 Move;
            public InputDeviceMode DeviceMode;
            public bool GamepadAvailable;

            public InputDeviceMode ActiveDeviceMode => DeviceMode;
            public bool IsActionPressed(InputActionId action) => Pressed;
            public bool WasActionPressedThisFrame(InputActionId action) => Pressed;
            public bool WasActionTriggeredThisFrame(InputActionId action) => Pressed;
            public bool WasActionReleasedThisFrame(InputActionId action) => false;
            public InputActionTrigger GetActionTrigger(InputActionId action) =>
                InputActionTrigger.Press;
            public float ReadAxis(InputActionId action) => Move.x;
            public Vector2 ReadVector2(InputActionId action) => Move;
            public Vector2 PointerPosition => Vector2.zero;
            public Vector2 PointerDelta => Vector2.zero;
            public bool HasKeyboard => false;
            public bool HasMouse => false;
            public bool HasGamepad => GamepadAvailable;
            public bool HasTouch => true;
        }
    }
}
