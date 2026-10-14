using System;
using Project.Audio;
using Project.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class StartMenuController : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        [SerializeField] private RawImage background;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private GameObject startChoicePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject savePanel;
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button creditsButton;
        [SerializeField, Min(0f), InspectorName("背景横移速度（每秒屏宽）")] private float backgroundScrollSpeed = .025f;
        private float backgroundOffset;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button selectSaveButton;
        [SerializeField] private Button startBackButton;
        [SerializeField] private SettingsPanelController settingsController;
        [SerializeField] private SavePanelController saveController;
        [SerializeField] private Text statusText;
        [Header("Typography")]
        [Tooltip("Cubic_11 SDF字体资产")]
        [SerializeField] private TMP_FontAsset uiSdfFont;
        [Tooltip("Cubic_11 TTF源字体，供运行时生成文本和编辑器布局使用")]
        [SerializeField] private Font uiFont;
        private bool buttonsBound;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public event Action StartGameRequested;

        public Canvas Canvas => canvas;
        public RawImage Background => background;

        public void BuildView()
        {
            if (canvas == null)
            {
                canvas = GetComponentInChildren<Canvas>(true);
            }

            if (canvas == null)
            {
                canvas = BuildCanvas();
            }

            if (background == null)
            {
                background = BuildBackground(canvas.transform);
            }

            if (mainPanel == null)
            {
                mainPanel = BuildMainPanel(canvas.transform).gameObject;
            }

            if (startChoicePanel == null)
            {
                startChoicePanel = BuildStartChoicePanel(canvas.transform).gameObject;
            }

            if (settingsPanel == null)
            {
                settingsPanel = BuildSettingsPanel(canvas.transform).gameObject;
            }

            if (savePanel == null)
            {
                savePanel = BuildSavePanel(canvas.transform).gameObject;
            }

            ApplyTypography();
            ApplyArtwork();
            ShowMain();
        }

        private void Awake()
        {
            if (canvas == null)
            {
                Debug.LogError(
                    "[StartMenu] 场景缺少 Canvas。请先运行 " +
                    "Tools/2026TapTap/UI/生成开始菜单原型。",
                    this);
                enabled = false;
                return;
            }

            ApplyAdaptiveCanvasScale();
            BindButtons();
            BindSubPanels();
            ApplyTypography();
            ApplyArtwork();
            if (statusText != null)
            {
                statusText.text =
                    "音量设置已生效；按键映射与存档仍为界面预览，尚未接入实际控制逻辑。";
            }
        }

        private void OnEnable()
        {
            AudioCuePlayer.Play(AudioCueIds.MusicMenuOpening);
        }

        private void OnDestroy()
        {
            if (settingsController != null)
            {
                settingsController.BackRequested -= ShowMain;
            }

            if (saveController != null)
            {
                saveController.BackRequested -= ShowStartChoice;
            }
        }

        private void Update()
        {
            if (background != null && background.texture != null)
            {
                backgroundOffset = Mathf.Repeat(backgroundOffset - Time.unscaledDeltaTime * backgroundScrollSpeed, 1f);
                background.uvRect = new Rect(backgroundOffset, 0f, 1f, 1f);
            }
            if (canvas != null &&
                (lastScreenWidth != Screen.width ||
                 lastScreenHeight != Screen.height))
            {
                ApplyAdaptiveCanvasScale();
            }
        }

        private void ApplyAdaptiveCanvasScale()
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            ConfigureCanvasScaler(canvas);
        }

        private void ApplyTypography()
        {
            StartMenuTypography.Configure(uiSdfFont, uiFont);
            StartMenuTypography.ApplyTo(
                canvas != null ? canvas.transform : transform);
        }

        public void ShowMain()
        {
            SetOnly(mainPanel);
        }

        public void ApplyArtwork()
        {
            var art = GameUiArtwork.Load();
            if (art == null) return;
            if (background != null && art.menuBackground != null)
            {
                background.texture = art.menuBackground;
                background.color = Color.white;
            }
            if (mainPanel != null)
            {
                var panel = mainPanel.GetComponent<Image>();
                if (panel != null) panel.color = Color.clear;
                foreach (string name in new[] { "Title Placeholder", "Subtitle", "Status" })
                {
                    var child = mainPanel.transform.Find(name);
                    if (child != null) child.gameObject.SetActive(false);
                }
                var menu = mainPanel.transform.Find("Main Buttons") as RectTransform;
                if (menu != null)
                {
                    StartMenuUiFactory.Stretch(menu);
                    var layout = menu.GetComponent<VerticalLayoutGroup>();
                    if (layout != null)
                    {
                        layout.enabled = false;
                    }
                    if (creditsButton == null)
                    {
                        var existing = menu.Find("Credits");
                        creditsButton = existing != null ? existing.GetComponent<Button>() :
                            StartMenuUiFactory.CreateButton("Credits", menu, "制作人员", new Vector2(448,180), null);
                    }
                }
            }
            ApplyButtonArtwork(startButton, art.startButton, new Vector2(6,21));
            ApplyButtonArtwork(creditsButton, art.creditsButton, new Vector2(5,77));
            ApplyButtonArtwork(settingsButton, art.settingsButton, new Vector2(4,125));
            ApplyButtonArtwork(quitButton, art.quitButton, new Vector2(72,125));
            if (Application.isPlaying && creditsButton != null)
            {
                creditsButton.onClick.RemoveListener(ShowCredits);
                creditsButton.onClick.AddListener(ShowCredits);
            }
            ApplyPanelArtwork(settingsPanel, art.panelBackground);
            ApplyPanelArtwork(savePanel, art.panelBackground);
            ApplyPanelArtwork(startChoicePanel, art.panelBackground);
            if (settingsPanel != null)
            {
                foreach (Transform child in settingsPanel.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Hint" || child.name == "Status") child.gameObject.SetActive(false);
            }
        }

        private void ShowCredits()
        {
            ShowSettings();
            settingsController?.ShowCredits();
        }

        private static void ApplyButtonArtwork(Button button, Sprite sprite, Vector2 topLeft)
        {
            if (button == null || sprite == null) return;
            var image = button.GetComponent<Image>();
            if (image == null) return;
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            StartMenuUiFactory.SetTopLeft(image.rectTransform,
                new Vector2(topLeft.x * 4, -topLeft.y * 4), sprite.rect.size * 4);
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
                label.gameObject.SetActive(false);
            var layout = button.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.ignoreLayout = true;
            }
        }

        private static void ApplyPanelArtwork(GameObject panel, Sprite sprite)
        {
            if (panel == null || sprite == null) return;
            var image = panel.GetComponent<Image>();
            if (image == null) return;
            image.sprite = null;
            image.color = Color.clear;
        }

        private void RequestStartGame()
        {
            if (StartGameRequested != null)
            {
                StartGameRequested.Invoke();
                return;
            }

            ShowStartChoice();
        }

        public void ShowStartChoice()
        {
            SetOnly(startChoicePanel);
        }

        public void ShowSettings()
        {
            SetOnly(settingsPanel);
        }

        public void ShowSave()
        {
            SetOnly(savePanel);
            saveController?.Refresh();
        }

        public void ContinueLastGame()
        {
            SaveSlotInfo last = SaveGameService.GetMostRecentSlot();
            statusText.text = last == null
                ? "没有可继续的存档。"
                : $"将读取存档：{last.displayName}；进入游戏的逻辑暂未接线。";
        }

        public void SelectSave()
        {
            ShowSave();
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        private Canvas BuildCanvas()
        {
            GameObject canvasObject =
                new GameObject(
                    "Start Menu Canvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas result = canvasObject.GetComponent<Canvas>();
            result.renderMode = RenderMode.ScreenSpaceOverlay;
            result.sortingOrder = 100;
            ConfigureCanvasScaler(result);
            return result;
        }

        public static void ConfigureCanvasScaler(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                return;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f,720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
        }

        private static RawImage BuildBackground(Transform parent)
        {
            RectTransform rect = StartMenuUiFactory.CreateRect("Background", parent);
            StartMenuUiFactory.Stretch(rect);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.color = new Color(.08f, .12f, .16f, 1f);
            image.raycastTarget = false;
            return image;
        }

        private RectTransform BuildMainPanel(Transform parent)
        {
            Image panel = StartMenuUiFactory.CreateImage(
                "Main Panel",
                parent,
                new Color(.03f, .05f, .07f, .42f));
            StartMenuUiFactory.Stretch(panel.rectTransform);

            Text title = StartMenuUiFactory.CreateText(
                "Title Placeholder",
                panel.transform,
                "形之彩",
                62,
                TextAnchor.MiddleLeft,
                new Color(.94f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(96f, -92f),
                new Vector2(760f, 96f));

            Text subtitle = StartMenuUiFactory.CreateText(
                "Subtitle",
                panel.transform,
                "史莱姆合成 / 治愈风原型",
                18,
                TextAnchor.MiddleLeft,
                new Color(.58f, .76f, .86f, .94f));
            StartMenuUiFactory.SetTopLeft(
                subtitle.rectTransform,
                new Vector2(100f, -184f),
                new Vector2(500f, 32f));

            RectTransform menu = StartMenuUiFactory.CreateRect(
                "Main Buttons",
                panel.transform);
            VerticalLayoutGroup layout =
                menu.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                menu,
                new Vector2(100f, -286f),
                new Vector2(250f, 190f));

            startButton = CreateMenuButton(menu, "开始游戏");
            settingsButton = CreateMenuButton(menu, "设置");
            quitButton = CreateMenuButton(menu, "退出游戏");

            statusText = StartMenuUiFactory.CreateText(
                "Status",
                panel.transform,
                "音量设置已生效；按键映射与存档仍为界面预览，尚未接入实际控制逻辑。",
                14,
                TextAnchor.MiddleLeft,
                new Color(.7f, .82f, .9f, .92f));
            StartMenuUiFactory.SetTopLeft(
                statusText.rectTransform,
                new Vector2(100f, -494f),
                new Vector2(780f, 28f));
            return panel.rectTransform;
        }

        private RectTransform BuildStartChoicePanel(Transform parent)
        {
            Image panel = StartMenuUiFactory.CreateImage(
                "Start Choice Panel",
                parent,
                new Color(.025f, .04f, .055f, .84f));
            StartMenuUiFactory.Stretch(panel.rectTransform);

            Text title = StartMenuUiFactory.CreateText(
                "Title",
                panel.transform,
                "开始游戏",
                34,
                TextAnchor.MiddleLeft,
                new Color(.94f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(88f, -82f),
                new Vector2(300f, 52f));

            RectTransform menu = StartMenuUiFactory.CreateRect(
                "Choice Buttons",
                panel.transform);
            VerticalLayoutGroup layout =
                menu.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                menu,
                new Vector2(92f, -194f),
                new Vector2(300f, 190f));

            continueButton = CreateMenuButton(menu, "继续上次游戏");
            selectSaveButton = CreateMenuButton(menu, "选择存档");
            startBackButton = CreateMenuButton(
                menu,
                "返回",
                new Color(.19f, .27f, .34f, .96f));
            return panel.rectTransform;
        }

        private RectTransform BuildSettingsPanel(Transform parent)
        {
            Image panel = StartMenuUiFactory.CreateImage(
                "Settings Panel",
                parent,
                new Color(.025f, .04f, .055f, .84f));
            StartMenuUiFactory.Stretch(panel.rectTransform);
            settingsController =
                panel.gameObject.AddComponent<SettingsPanelController>();
            settingsController.BuildStructure();
            return panel.rectTransform;
        }

        private RectTransform BuildSavePanel(Transform parent)
        {
            Image panel = StartMenuUiFactory.CreateImage(
                "Save Panel",
                parent,
                new Color(.025f, .04f, .055f, .84f));
            StartMenuUiFactory.Stretch(panel.rectTransform);
            saveController =
                panel.gameObject.AddComponent<SavePanelController>();
            saveController.BuildStructure();
            return panel.rectTransform;
        }

        private static Button CreateMenuButton(
            Transform parent,
            string label,
            Color? color = null)
        {
            Button button = StartMenuUiFactory.CreateButton(
                label,
                parent,
                label,
                new Vector2(250f, 48f),
                null,
                color ?? new Color(.17f, .45f, .7f, .96f));
            LayoutElement element =
                button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 250f;
            element.preferredHeight = 48f;
            return button;
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            startButton?.onClick.AddListener(RequestStartGame);
            settingsButton?.onClick.AddListener(ShowSettings);
            quitButton?.onClick.AddListener(QuitGame);
            continueButton?.onClick.AddListener(ContinueLastGame);
            selectSaveButton?.onClick.AddListener(SelectSave);
            startBackButton?.onClick.AddListener(ShowMain);
            buttonsBound = true;
        }

        private void BindSubPanels()
        {
            if (settingsController != null)
            {
                settingsController.BackRequested -= ShowMain;
                settingsController.BackRequested += ShowMain;
            }

            if (saveController != null)
            {
                saveController.BackRequested -= ShowStartChoice;
                saveController.BackRequested += ShowStartChoice;
            }
        }

        private void SetOnly(GameObject target)
        {
            SetActive(mainPanel, target == mainPanel);
            SetActive(startChoicePanel, target == startChoicePanel);
            SetActive(settingsPanel, target == settingsPanel);
            SetActive(savePanel, target == savePanel);
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target != null && target.activeSelf != value)
            {
                target.SetActive(value);
            }
        }
    }
}
