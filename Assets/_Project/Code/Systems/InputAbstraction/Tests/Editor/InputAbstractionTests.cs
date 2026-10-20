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
                Is.EqualTo(4));
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

        [Test]
        public void DirectionButtons_CombineAxesAndCancelOpposites()
        {
            VirtualInputState.SetVector(1, InputActionId.Move, Vector2.right);
            VirtualInputState.SetVector(2, InputActionId.Move, Vector2.up);
            Assert.That(VirtualInputState.ReadVector(InputActionId.Move), Is.EqualTo(new Vector2(1, 1).normalized));
            VirtualInputState.SetVector(3, InputActionId.Move, Vector2.left);
            Assert.That(VirtualInputState.ReadVector(InputActionId.Move), Is.EqualTo(Vector2.up));
            VirtualInputState.ReleaseOwner(2);
            Assert.That(VirtualInputState.ReadVector(InputActionId.Move), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void TouchDirection_ReleaseOnlyMatchingPointer()
        {
            var obj = new GameObject("Direction", typeof(RectTransform), typeof(VirtualDirectionButton));
            var direction = obj.GetComponent<VirtualDirectionButton>();
            direction.Configure(Vector2.up);
            direction.OnPointerDown(new PointerEventData(null) { pointerId = 3 });
            direction.OnPointerUp(new PointerEventData(null) { pointerId = 4 });
            Assert.That(VirtualInputState.ReadVector(InputActionId.Move), Is.EqualTo(Vector2.up));
            direction.OnPointerUp(new PointerEventData(null) { pointerId = 3 });
            Assert.That(VirtualInputState.ReadVector(InputActionId.Move), Is.EqualTo(Vector2.zero));
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void ThreeFingersAndTwoJumpButtons_HaveIndependentOwnership()
        {
            var left = new GameObject("Left", typeof(VirtualDirectionButton));
            var run = new GameObject("Run", typeof(VirtualInputButton));
            var jump1 = new GameObject("Jump1", typeof(VirtualInputButton));
            var jump2 = new GameObject("Jump2", typeof(VirtualInputButton));
            try
            {
                var direction = left.GetComponent<VirtualDirectionButton>(); direction.Configure(Vector2.left);
                var sprint = run.GetComponent<VirtualInputButton>(); sprint.Configure(InputActionId.Sprint);
                var a = jump1.GetComponent<VirtualInputButton>(); var b = jump2.GetComponent<VirtualInputButton>();
                var e1 = new PointerEventData(null) { pointerId = 1 };
                var e2 = new PointerEventData(null) { pointerId = 2 };
                var e3 = new PointerEventData(null) { pointerId = 3 };
                direction.OnPointerDown(e1); sprint.OnPointerDown(e2); a.OnPointerDown(e3);
                direction.OnPointerExit(e1); sprint.OnPointerExit(e2); a.OnPointerExit(e3);
                Assert.AreEqual(Vector2.left, VirtualInputState.ReadVector(InputActionId.Move));
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Jump));
                a.OnPointerUp(e2); Assert.IsTrue(a.IsPressed);
                b.OnPointerDown(new PointerEventData(null) { pointerId = 4 });
                a.OnPointerUp(e3); Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Jump));
                b.OnPointerUp(new PointerEventData(null) { pointerId = 4 }); Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Jump));
                Assert.AreEqual(Vector2.left, VirtualInputState.ReadVector(InputActionId.Move));
                direction.OnPointerUp(e1); sprint.OnPointerUp(e2);
                Assert.AreEqual(Vector2.zero, VirtualInputState.ReadVector(InputActionId.Move));
                Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Sprint));
            }
            finally { Object.DestroyImmediate(left); Object.DestroyImmediate(run); Object.DestroyImmediate(jump1); Object.DestroyImmediate(jump2); }
        }

        [Test]
        public void SafeArea_NormalizesInsetAnchorsWithoutLeavingOffsets()
        {
            var obj = new GameObject("Safe", typeof(RectTransform), typeof(ScreenSafeArea));
            obj.GetComponent<ScreenSafeArea>().Apply(new Rect(80, 20, 2200, 1020), new Vector2(2400, 1080));
            var rect = (RectTransform)obj.transform;
            Assert.That(rect.anchorMin.x, Is.EqualTo(80f / 2400).Within(.00001f));
            Assert.That(rect.anchorMax.y, Is.EqualTo(1040f / 1080).Within(.00001f));
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero));
            Object.DestroyImmediate(obj);
        }

        [Test]
        public void RunToggle_PersistsAfterReleaseAndSecondTapTurnsOff()
        {
            var obj = new GameObject("ToggleRun", typeof(VirtualInputButton));
            try
            {
                var button = obj.GetComponent<VirtualInputButton>();
                button.Configure(InputActionId.Sprint); button.ConfigureToggle();
                var finger = new PointerEventData(null) { pointerId = 1 };
                button.OnPointerDown(finger); button.OnPointerUp(finger);
                Assert.IsFalse(button.IsPressed); Assert.IsTrue(button.ToggleOn);
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
                typeof(VirtualInputButton).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(button, null);
                Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Sprint));
                typeof(VirtualInputButton).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(button, null);
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
                button.OnPointerDown(finger); button.OnPointerUp(finger);
                Assert.IsFalse(button.ToggleOn); Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Sprint));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void DefaultRun_FirstWalkTapDisablesSprint_SecondTapRestoresIt()
        {
            var obj = new GameObject("DefaultRun", typeof(VirtualInputButton));
            try
            {
                var button = obj.GetComponent<VirtualInputButton>();
                button.Configure(InputActionId.Sprint); button.ConfigureToggle(true);
                Assert.IsTrue(button.ToggleOn);
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
                var finger = new PointerEventData(null) { pointerId = 1 };
                button.OnPointerDown(finger); button.OnPointerUp(finger);
                Assert.IsFalse(button.ToggleOn);
                Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Sprint));
                button.OnPointerDown(finger); button.OnPointerUp(finger);
                Assert.IsTrue(button.ToggleOn);
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                var focus = typeof(VirtualInputButton).GetMethod("OnApplicationFocus", flags);
                focus.Invoke(button, new object[] { false });
                Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Sprint));
                focus.Invoke(button, new object[] { true });
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Sprint));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void HeldLeftJump_RepeatsUntilItsOwnFingerReleases()
        {
            var left = new GameObject("LeftJump", typeof(VirtualInputButton));
            var right = new GameObject("RightJump", typeof(VirtualInputButton));
            try
            {
                var a = left.GetComponent<VirtualInputButton>(); a.ConfigureRepeatJump();
                var b = right.GetComponent<VirtualInputButton>();
                var first = new PointerEventData(null) { pointerId = 1 };
                var second = new PointerEventData(null) { pointerId = 2 };
                a.OnPointerDown(first); b.OnPointerDown(second);
                Assert.IsTrue(GameInput.IsTouchJumpRepeating);
                a.OnPointerUp(second); Assert.IsTrue(GameInput.IsTouchJumpRepeating);
                a.OnPointerUp(first); Assert.IsFalse(GameInput.IsTouchJumpRepeating);
                Assert.IsTrue(VirtualInputState.IsPressed(InputActionId.Jump));
                b.OnPointerUp(second); Assert.IsFalse(VirtualInputState.IsPressed(InputActionId.Jump));
            }
            finally { Object.DestroyImmediate(left); Object.DestroyImmediate(right); }
        }

        [Test]
        public void TouchFeedback_DragsArtworkOnlyAndIgnoresOtherFinger()
        {
            var obj = new GameObject("Touch", typeof(RectTransform), typeof(VirtualDirectionButton), typeof(TouchPressFeedback));
            try
            {
                var rect = (RectTransform)obj.transform; rect.sizeDelta = new Vector2(124, 124);
                var feedback = obj.GetComponent<TouchPressFeedback>();
                var direction = obj.GetComponent<VirtualDirectionButton>(); direction.Configure(Vector2.right);
                var finger = new PointerEventData(null) { pointerId = 3, position = Vector2.zero };
                direction.OnPointerDown(finger); feedback.OnPointerDown(finger);
                finger.position = Vector2.one * 200; feedback.OnDrag(finger);
                Assert.AreEqual(Vector2.zero, rect.anchoredPosition);
                var other = new PointerEventData(null) { pointerId = 7 };
                direction.OnPointerUp(other); feedback.OnPointerUp(other);
                Assert.IsTrue(direction.IsPressed);
                Assert.Less(obj.transform.Find("Press Feedback").localScale.x, 1f);
                direction.OnPointerUp(finger); feedback.OnPointerUp(finger);
                Assert.IsFalse(direction.IsPressed);
                Assert.AreEqual(Vector3.one, obj.transform.Find("Press Feedback").localScale);
                Assert.AreEqual(Vector2.zero, rect.anchoredPosition);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void TouchMargin_ExtendsInitialHitOutsideVisibleRectangle()
        {
            var obj = new GameObject("HitArea", typeof(RectTransform));
            try
            {
                var rect = (RectTransform)obj.transform; rect.sizeDelta = Vector2.one * 124;
                var point = new Vector2(72, 0);
                Assert.IsFalse(RectTransformUtility.RectangleContainsScreenPoint(rect, point, null));
                Assert.IsTrue(RectTransformUtility.RectangleContainsScreenPoint(rect, point, null, Vector4.one * -14));
                Assert.IsFalse(RectTransformUtility.RectangleContainsScreenPoint(rect, new Vector2(80, 0), null, Vector4.one * -14));
            }
            finally { Object.DestroyImmediate(obj); }
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
