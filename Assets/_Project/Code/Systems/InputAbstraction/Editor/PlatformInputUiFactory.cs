using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.InputAbstraction.Editor
{
    internal static class PlatformInputUiFactory
    {
        private static readonly Color JoystickBackground = new Color(1f, 1f, 1f, 0.22f);
        private static readonly Color JoystickHandle = new Color(1f, 1f, 1f, 0.6f);
        private static readonly Color ButtonColor = new Color(0.12f, 0.18f, 0.28f, 0.72f);

        [MenuItem("GameObject/2026TapTap/Input/创建双平台输入 UI", false, 10)]
        private static void CreatePlatformInputUi()
        {
            GameObject canvasObject = new GameObject(
                "PlatformInputUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlatformUILayoutController));
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create platform input UI");

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform desktopRoot = CreateStretchRoot("DesktopUI", canvasObject.transform);
            RectTransform mobileRoot = CreateStretchRoot("MobileUI", canvasObject.transform);

            CreateJoystick(
                "MoveJoystick",
                mobileRoot,
                InputActionId.Move,
                new Vector2(0f, 0f),
                new Vector2(210f, 210f),
                new Vector2(170f, 170f));
            CreateJoystick(
                "LookJoystick",
                mobileRoot,
                InputActionId.Look,
                new Vector2(1f, 0f),
                new Vector2(-230f, 210f),
                new Vector2(150f, 150f));
            CreateButton(
                "JumpButton",
                "跳跃",
                mobileRoot,
                InputActionId.Jump,
                new Vector2(1f, 0f),
                new Vector2(-105f, 330f));
            CreateButton(
                "InteractButton",
                "互动",
                mobileRoot,
                InputActionId.Interact,
                new Vector2(1f, 0f),
                new Vector2(-300f, 350f));

            PlatformUILayoutController controller =
                canvasObject.GetComponent<PlatformUILayoutController>();
            controller.SetVisibilityGroups(
                new[] { desktopRoot.gameObject },
                new[] { mobileRoot.gameObject });
            controller.ReplaceLayoutTargets(
                canvasObject.GetComponentsInChildren<RectTransform>(true));
            controller.CaptureLayout(InputPlatformMode.Desktop);
            controller.CaptureLayout(InputPlatformMode.Mobile);
            controller.ApplyLayout(InputBuildTargetUtility.Current);

            EnsureEventSystem();
            Selection.activeGameObject = canvasObject;
            EditorSceneManager.MarkSceneDirty(canvasObject.scene);
        }

        private static RectTransform CreateStretchRoot(string name, Transform parent)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(value, $"Create {name}");
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void CreateJoystick(
            string name,
            RectTransform parent,
            InputActionId action,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            GameObject backgroundObject = CreateImage(name, parent, JoystickBackground);
            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            SetRect(background, anchor, position, size);

            GameObject handleObject = CreateImage("Handle", background, JoystickHandle);
            RectTransform handle = handleObject.GetComponent<RectTransform>();
            SetRect(handle, new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.42f);
            handleObject.GetComponent<Image>().raycastTarget = false;

            VirtualJoystick joystick = backgroundObject.AddComponent<VirtualJoystick>();
            joystick.Configure(background, handle, action);
        }

        private static void CreateButton(
            string name,
            string label,
            RectTransform parent,
            InputActionId action,
            Vector2 anchor,
            Vector2 position)
        {
            GameObject buttonObject = CreateImage(name, parent, ButtonColor);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            SetRect(rect, anchor, position, new Vector2(132f, 132f));
            buttonObject.AddComponent<VirtualInputButton>().Configure(action);

            GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 28;
            text.color = Color.white;
            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static GameObject CreateImage(string name, Transform parent, Color color)
        {
            GameObject value = new GameObject(name, typeof(RectTransform), typeof(Image));
            value.transform.SetParent(parent, false);
            Image image = value.GetComponent<Image>();
            image.color = color;
            return value;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>(
                FindObjectsInactive.Include);
            if (eventSystem == null)
            {
                GameObject eventSystemObject = new GameObject(
                    "EventSystem",
                    typeof(EventSystem));
                Undo.RegisterCreatedObjectUndo(eventSystemObject, "Create input event system");
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            InputSystemUIInputModule inputModule =
                eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = Undo.AddComponent<InputSystemUIInputModule>(eventSystem.gameObject);
                inputModule.AssignDefaultActions();
            }

            BaseInputModule[] inputModules = eventSystem.GetComponents<BaseInputModule>();
            for (int index = 0; index < inputModules.Length; index++)
            {
                BaseInputModule other = inputModules[index];
                if (other != inputModule && other.enabled)
                {
                    Undo.RecordObject(other, "Disable legacy input module");
                    other.enabled = false;
                    EditorUtility.SetDirty(other);
                }
            }
        }
    }
}
