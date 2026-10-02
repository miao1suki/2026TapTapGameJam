using Project.InputAbstraction;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.GameFlow.Editor
{
    public static class PlayableGameUiBuilder
    {
        public static void RebuildScene()
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            BuildCurrentScene();
            EditorSceneManager.SaveScene(scene, GameFlowSceneScaffolder.UiPath);
        }

        public static void BuildCurrentScene()
        {
            GameObject rootObject = new GameObject("__System_UI");
            GameSystemSceneRoot root =
                rootObject.AddComponent<GameSystemSceneRoot>();
            root.Configure(GameSystemSceneKind.UI);

            GameObject eventSystemObject = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            eventSystemObject.transform.SetParent(root.transform, false);

            GameObject canvasObject = new GameObject(
                "GameUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(PlatformUILayoutController),
                typeof(GameUiRouter));
            canvasObject.transform.SetParent(root.transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Color accent = new Color(0.14f, 0.64f, 0.95f, 1f);
            Color accentHover = new Color(0.24f, 0.75f, 1f, 1f);

            GameObject mainMenu = CreatePanel(
                canvasObject.transform,
                "MainMenuScreen",
                new Color(0.025f, 0.045f, 0.075f, 1f),
                true);
            CreateText(
                mainMenu.transform,
                "Title",
                "2026TapTap",
                new Vector2(0f, 255f),
                new Vector2(1000f, 150f),
                82,
                font,
                new Color(0.78f, 0.93f, 1f));
            CreateText(
                mainMenu.transform,
                "Subtitle",
                "二维与三维之间的旅程",
                new Vector2(0f, 145f),
                new Vector2(900f, 70f),
                30,
                font,
                new Color(0.55f, 0.68f, 0.78f));
            Button start = CreateButton(
                mainMenu.transform,
                "StartGame",
                "开始游戏",
                new Vector2(0f, 15f),
                new Vector2(420f, 82f),
                font,
                accent,
                accentHover);
            Button menuQuit = CreateButton(
                mainMenu.transform,
                "QuitGame",
                "退出游戏",
                new Vector2(0f, -88f),
                new Vector2(420f, 70f),
                font,
                new Color(0.18f, 0.23f, 0.3f, 1f),
                new Color(0.28f, 0.34f, 0.43f, 1f));
            CreateText(
                mainMenu.transform,
                "Controls",
                "A / D 移动    Space 跳跃    ESC 暂停",
                new Vector2(0f, -260f),
                new Vector2(1200f, 70f),
                24,
                font,
                new Color(0.56f, 0.64f, 0.72f));

            GameObject gameplay = CreateScreenRoot(
                canvasObject.transform,
                "GameplayHud");
            Text gameplayLabel = CreateText(
                gameplay.transform,
                "LevelLabel",
                "关卡",
                new Vector2(-790f, 475f),
                new Vector2(280f, 64f),
                30,
                font,
                Color.white);
            gameplayLabel.alignment = TextAnchor.MiddleLeft;
            Text help = CreateText(
                gameplay.transform,
                "Help",
                "A/D 移动  ·  Space 跳跃  ·  ESC 暂停",
                new Vector2(-670f, -495f),
                new Vector2(760f, 50f),
                22,
                font,
                new Color(1f, 1f, 1f, 0.72f));
            help.alignment = TextAnchor.MiddleLeft;
            Text pauseHint = CreateText(
                gameplay.transform,
                "PauseHint",
                "ESC  暂停",
                new Vector2(805f, 475f),
                new Vector2(240f, 56f),
                24,
                font,
                new Color(1f, 1f, 1f, 0.82f));
            pauseHint.alignment = TextAnchor.MiddleRight;

            GameObject pause = CreatePanel(
                canvasObject.transform,
                "PauseScreen",
                new Color(0.015f, 0.025f, 0.04f, 0.72f),
                true);
            CreateText(
                pause.transform,
                "Title",
                "游戏暂停",
                new Vector2(0f, 190f),
                new Vector2(700f, 110f),
                60,
                font,
                Color.white);
            Button resume = CreateButton(
                pause.transform,
                "Resume",
                "继续游戏",
                new Vector2(0f, 65f),
                new Vector2(380f, 72f),
                font,
                accent,
                accentHover);
            Button reload = CreateButton(
                pause.transform,
                "Reload",
                "重新开始本关",
                new Vector2(0f, -25f),
                new Vector2(380f, 68f),
                font,
                new Color(0.18f, 0.27f, 0.36f, 1f),
                new Color(0.25f, 0.39f, 0.52f, 1f));
            Button menu = CreateButton(
                pause.transform,
                "MainMenu",
                "返回主菜单",
                new Vector2(0f, -112f),
                new Vector2(380f, 68f),
                font,
                new Color(0.18f, 0.23f, 0.3f, 1f),
                new Color(0.28f, 0.34f, 0.43f, 1f));

            GameObject ending = CreatePanel(
                canvasObject.transform,
                "EndingScreen",
                new Color(0.025f, 0.035f, 0.055f, 1f),
                true);
            CreateText(
                ending.transform,
                "Title",
                "旅程完成",
                new Vector2(0f, 165f),
                new Vector2(900f, 140f),
                72,
                font,
                new Color(1f, 0.84f, 0.36f));
            CreateText(
                ending.transform,
                "Message",
                "三个原型关卡已全部通过",
                new Vector2(0f, 65f),
                new Vector2(900f, 70f),
                30,
                font,
                new Color(0.72f, 0.78f, 0.86f));
            Button endingMenu = CreateButton(
                ending.transform,
                "ReturnToMenu",
                "返回主菜单",
                new Vector2(0f, -65f),
                new Vector2(380f, 72f),
                font,
                accent,
                accentHover);
            Button endingQuit = CreateButton(
                ending.transform,
                "QuitGame",
                "退出游戏",
                new Vector2(0f, -155f),
                new Vector2(380f, 66f),
                font,
                new Color(0.18f, 0.23f, 0.3f, 1f),
                new Color(0.28f, 0.34f, 0.43f, 1f));

            GameObject loading = CreateScreenRoot(
                canvasObject.transform,
                "LoadingScreen");
            GameObject loadingBadge = CreateBox(
                loading.transform,
                "LoadingBadge",
                new Vector2(775f, -470f),
                new Vector2(280f, 58f),
                new Color(0.03f, 0.055f, 0.08f, 0.88f));
            CreateText(
                loadingBadge.transform,
                "Label",
                "正在进入场景…",
                Vector2.zero,
                new Vector2(280f, 58f),
                22,
                font,
                new Color(0.72f, 0.9f, 1f));

            GameUiRouter router = canvasObject.GetComponent<GameUiRouter>();
            router.Configure(
                mainMenu,
                gameplay,
                pause,
                ending,
                loading,
                start,
                menuQuit,
                gameplayLabel,
                resume,
                reload,
                menu,
                endingMenu,
                endingQuit);

            mainMenu.SetActive(false);
            gameplay.SetActive(false);
            pause.SetActive(false);
            ending.SetActive(false);
            loading.SetActive(true);
        }

        private static GameObject CreateScreenRoot(Transform parent, string name)
        {
            GameObject root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            SetFullScreen(root.GetComponent<RectTransform>());
            return root;
        }

        private static GameObject CreatePanel(
            Transform parent,
            string name,
            Color color,
            bool raycastTarget)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CanvasGroup));
            panel.transform.SetParent(parent, false);
            SetFullScreen(panel.GetComponent<RectTransform>());
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return panel;
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject box = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            box.transform.SetParent(parent, false);
            RectTransform rect = box.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = box.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return box;
        }

        private static Text CreateText(
            Transform parent,
            string name,
            string value,
            Vector2 position,
            Vector2 size,
            int fontSize,
            Font font,
            Color color)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            Font font,
            Color normal,
            Color highlighted)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = normal;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.selectedColor = highlighted;
            colors.pressedColor = Color.Lerp(normal, Color.black, 0.25f);
            button.colors = colors;
            Text text = CreateText(
                buttonObject.transform,
                "Label",
                label,
                Vector2.zero,
                size,
                29,
                font,
                Color.white);
            SetFullScreen(text.rectTransform);
            return button;
        }

        private static void SetFullScreen(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
