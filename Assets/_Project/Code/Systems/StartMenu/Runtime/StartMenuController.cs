using System;
using System.Collections;
using DG.Tweening;
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
        [SerializeField] private RectTransform menuContentRoot;
        [SerializeField] private CanvasGroup menuContentCanvasGroup;
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
        [SerializeField, Min(0f)] private float panelBackdropSlideDuration = .28f;
        private bool buttonsBound;
        private bool backgroundScrollEnabled = true;
        private static bool externalBackgroundScrollPaused;
        private int lastScreenWidth;
        private int lastScreenHeight;
        private Image worldMenuLogo;
        private Sprite[] logoFrames;
        private float logoFrameSeconds = .08f;
        private int lastLogoFrame = -1;
        private RectTransform panelBackdrop;
        private Tween panelBackdropTween;
        private bool pendingShowCredits;

        public event Action StartGameRequested;
        public event Action ExitGameRequested;

        public Canvas Canvas => canvas;
        public RawImage Background => background;
        public Transform StartButtonTransform =>
            startButton != null ? startButton.transform : null;
        public RectTransform MenuContentRoot => EnsureMenuContentRoot();
        public bool IsPresented => canvas != null && canvas.enabled;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetExternalBackgroundScrollPause()
        {
            externalBackgroundScrollPaused = false;
        }

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
            EnsureMenuContentRoot();
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
            EnsureMenuContentRoot();
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

            panelBackdropTween?.Kill(false);
        }

        private void Update()
        {
            if (!IsPresented) return;
            if (settingsPanel != null &&
                (settingsPanel.activeSelf ||
                 (panelBackdrop != null &&
                  panelBackdrop.gameObject.activeSelf)) &&
                (Project.InputAbstraction.GameInput
                     .WasTriggeredThisFrame(
                         Project.InputAbstraction.InputActionId.Cancel) ||
                 Project.InputAbstraction.GameInput
                     .WasTriggeredThisFrame(
                         Project.InputAbstraction.InputActionId.Pause)))
            {
                if (settingsPanel.activeSelf)
                {
                    settingsController?.TryTriggerBackButton();
                }
                else
                {
                    HidePanelBackdropImmediate();
                    SetOnly(mainPanel);
                }

                return;
            }

            Project.InputAbstraction.DesktopCursorPolicy.Apply(true);
            if (worldMenuLogo != null && logoFrames != null && logoFrames.Length > 0)
            {
                int frame = (int)(Time.unscaledTime / logoFrameSeconds) % logoFrames.Length;
                if (frame != lastLogoFrame) { lastLogoFrame = frame; worldMenuLogo.sprite = logoFrames[frame]; }
            }

            if (backgroundScrollEnabled &&
                !externalBackgroundScrollPaused &&
                background != null &&
                background.texture != null)
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

        private void OnApplicationFocus(bool focused)
        {
            if (IsPresented) Project.InputAbstraction.DesktopCursorPolicy.Apply(true, focused);
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
            if (panelBackdrop != null &&
                panelBackdrop.gameObject.activeSelf)
            {
                HidePanelBackdrop();
                return;
            }

            SetOnly(mainPanel);
        }

        public void SetPresented(bool value, bool interactive = true)
        {
            if (canvas != null) canvas.enabled = value;
            CanvasGroup group = EnsureMenuContentCanvasGroup(EnsureMenuContentRoot());
            group.interactable = value && interactive;
            group.blocksRaycasts = value;
        }

        public void SetFusionContent(float visible, float slideDistance = -180f)
        {
            RectTransform root = EnsureMenuContentRoot();
            CanvasGroup group = EnsureMenuContentCanvasGroup(root);
            group.alpha = Mathf.Clamp01(visible);
            root.anchoredPosition = new Vector2(Mathf.Lerp(slideDistance, 0f, visible), 0f);
        }

        public void SetBackgroundScrollEnabled(bool value)
        {
            backgroundScrollEnabled = value;
        }

        public static void SetExternalBackgroundScrollPaused(bool value)
        {
            externalBackgroundScrollPaused = value;
        }

        public void SetBackgroundVisible(bool value)
        {
            if (background != null)
            {
                background.gameObject.SetActive(value);
            }
        }

        public void SyncBackgroundState(RawImage source)
        {
            if (source == null || background == null)
            {
                return;
            }

            background.texture = source.texture;
            background.color = source.color;
            background.uvRect = source.uvRect;
            backgroundOffset = source.uvRect.x;
        }

        public IEnumerator SlideMenuContentOutAsync(
            float distance,
            float duration,
            Ease ease)
        {
            RectTransform root = EnsureMenuContentRoot();
            CanvasGroup group = EnsureMenuContentCanvasGroup(root);
            root.gameObject.SetActive(true);
            Vector2 start = root.anchoredPosition;
            Tween move = DOTween.To(
                    () => root.anchoredPosition.x,
                    value => root.anchoredPosition = new Vector2(
                        value,
                        root.anchoredPosition.y),
                    start.x + distance,
                    Mathf.Max(.01f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            Tween fade = DOTween.To(
                    () => group.alpha,
                    value => group.alpha = value,
                    0f,
                    Mathf.Max(.01f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(move)
                .Join(fade);
            yield return sequence.WaitForCompletion();
        }

        public IEnumerator SlideMenuContentInAsync(
            float distance,
            float duration,
            Ease ease)
        {
            RectTransform root = EnsureMenuContentRoot();
            CanvasGroup group = EnsureMenuContentCanvasGroup(root);
            root.gameObject.SetActive(true);
            root.anchoredPosition = new Vector2(distance, 0f);
            group.alpha = 0f;
            Tween move = DOTween.To(
                    () => root.anchoredPosition.x,
                    value => root.anchoredPosition = new Vector2(
                        value,
                        root.anchoredPosition.y),
                    0f,
                    Mathf.Max(.01f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            Tween fade = DOTween.To(
                    () => group.alpha,
                    value => group.alpha = value,
                    1f,
                    Mathf.Max(.01f, duration))
                .SetEase(ease)
                .SetUpdate(true);
            Sequence sequence = DOTween.Sequence()
                .SetUpdate(true)
                .Join(move)
                .Join(fade);
            yield return sequence.WaitForCompletion();
        }

        public void ApplyArtwork()
        {
            var art = GameUiArtwork.Load();
            if (art == null) return;
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
            ApplyWorldMenuArtwork(art);
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

        private void ApplyWorldMenuArtwork(GameUiArtwork art)
        {
            if (art.worldMenuButtons == null || art.worldMenuButtons.Length != 4) return;
            // The supplied black reference is a layout reference, never a runtime backdrop.
            SetBackgroundVisible(false);
            SetBackgroundScrollEnabled(false);
            Button[] ordered = { startButton, creditsButton, settingsButton, quitButton };
            for (int index = 0; index < ordered.Length; index++)
            {
                Button button = ordered[index];
                Sprite sprite = art.worldMenuButtons[index];
                if (button == null || sprite == null) continue;
                ApplyButtonArtwork(button, sprite, Vector2.zero);
                StartMenuUiFactory.SetTopLeft((RectTransform)button.transform,
                    new Vector2(68f, -270f - index * 104f), new Vector2(368f, 96f));
                // Keep a generous fixed hit area while aligning the actual art, not its
                // transparent source canvas. One precomputed union offset serves both states.
                Image hit = button.GetComponent<Image>();
                hit.color = Color.clear;
                Transform existingVisual = button.transform.Find("World Menu Artwork");
                Image visual = existingVisual != null ? existingVisual.GetComponent<Image>() :
                    StartMenuUiFactory.CreateImage("World Menu Artwork", button.transform, Color.white);
                visual.sprite = sprite;
                visual.preserveAspect = true;
                visual.raycastTarget = false;
                float left = art.worldMenuButtonLeftPadding != null && index < art.worldMenuButtonLeftPadding.Length
                    ? art.worldMenuButtonLeftPadding[index] : 0f;
                StartMenuUiFactory.SetTopLeft(visual.rectTransform, new Vector2(-left * 4f, 0f), sprite.rect.size * 4f);
                button.targetGraphic = visual;
                button.transition = Selectable.Transition.SpriteSwap;
                Sprite selected = art.worldMenuSelectedButtons != null && index < art.worldMenuSelectedButtons.Length
                    ? art.worldMenuSelectedButtons[index] : sprite;
                SpriteState states = button.spriteState;
                states.highlightedSprite = states.pressedSprite = states.selectedSprite = selected;
                states.disabledSprite = sprite;
                button.spriteState = states;
                var navigation = button.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = ordered[(index + ordered.Length - 1) % ordered.Length];
                navigation.selectOnDown = ordered[(index + 1) % ordered.Length];
                button.navigation = navigation;
            }
            if (mainPanel != null && art.worldMenuLogoFrames != null && art.worldMenuLogoFrames.Length > 0)
            {
                Transform existing = mainPanel.transform.Find("Animated Logo");
                worldMenuLogo = existing != null ? existing.GetComponent<Image>() :
                    StartMenuUiFactory.CreateImage("Animated Logo", mainPanel.transform, Color.white);
                worldMenuLogo.raycastTarget = false;
                worldMenuLogo.preserveAspect = true;
                StartMenuUiFactory.SetTopLeft(worldMenuLogo.rectTransform, new Vector2(24f, 0f), new Vector2(552f, 268f));
                logoFrames = art.worldMenuLogoFrames;
                logoFrameSeconds = Mathf.Max(.02f, art.worldMenuLogoFrameSeconds);
                worldMenuLogo.sprite = logoFrames[0];
                lastLogoFrame = -1;
            }
        }

        private void ShowCredits()
        {
            AudioCuePlayer.PlayUiConfirm();
            OpenSettingsPanel(true);
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
            AudioCuePlayer.PlayUiConfirm();
            if (StartGameRequested != null)
            {
                StartGameRequested.Invoke();
                return;
            }

            ShowStartChoice();
        }

        public void ShowStartChoice()
        {
            HidePanelBackdropImmediate();
            SetOnly(startChoicePanel);
        }

        public void ShowSettings()
        {
            AudioCuePlayer.PlayUiConfirm();
            OpenSettingsPanel(false);
        }

        public void ShowSave()
        {
            AudioCuePlayer.PlayUiConfirm();
            HidePanelBackdropImmediate();
            SetOnly(savePanel);
            saveController?.Refresh();
        }

        public void ContinueLastGame()
        {
            AudioCuePlayer.PlayUiConfirm();
            SaveSlotInfo last = SaveGameService.GetMostRecentSlot();
            if (statusText == null) return;
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
            AudioCuePlayer.PlayUiConfirm();
            Action exitRequested = ExitGameRequested;
            exitRequested?.Invoke();
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
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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

        private RectTransform EnsureMenuContentRoot()
        {
            if (menuContentRoot != null)
            {
                EnsurePanelBackdrop();
                return menuContentRoot;
            }

            Transform parent = canvas != null ? canvas.transform : transform;
            if (parent != null)
            {
                menuContentRoot = parent.Find("Safe Content")
                    as RectTransform;
                if (menuContentRoot != null)
                {
                    EnsurePanelBackdrop();
                    return menuContentRoot;
                }
            }

            GameObject rootObject = new GameObject(
                "StartMenuContent",
                typeof(RectTransform),
                typeof(CanvasGroup));
            rootObject.transform.SetParent(parent, false);
            menuContentRoot =
                rootObject.GetComponent<RectTransform>();
            StartMenuUiFactory.Stretch(menuContentRoot);
            menuContentCanvasGroup =
                rootObject.GetComponent<CanvasGroup>();
            menuContentRoot.SetAsLastSibling();

            GameObject[] panels =
            {
                mainPanel,
                startChoicePanel,
                settingsPanel,
                savePanel
            };
            for (int index = 0; index < panels.Length; index++)
            {
                if (panels[index] != null)
                {
                    panels[index].transform.SetParent(
                        menuContentRoot,
                        false);
                }
            }

            EnsurePanelBackdrop();
            return menuContentRoot;
        }

        private void OpenSettingsPanel(bool showCredits)
        {
            if (settingsPanel == null)
            {
                return;
            }

            EnsurePanelBackdrop();
            pendingShowCredits = showCredits;
            panelBackdropTween?.Kill(false);
            panelBackdrop.gameObject.SetActive(true);
            int settingsIndex = settingsPanel.transform.GetSiblingIndex();
            panelBackdrop.SetSiblingIndex(settingsIndex);

            float width = Mathf.Max(1f, menuContentRoot.rect.width);
            panelBackdrop.anchoredPosition =
                new Vector2(width, 0f);
            panelBackdropTween = DOTween.To(
                    () => panelBackdrop.anchoredPosition.x,
                    value => panelBackdrop.anchoredPosition =
                        new Vector2(value, 0f),
                    0f,
                    panelBackdropSlideDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    panelBackdropTween = null;
                    SetOnly(settingsPanel);
                    settingsPanel.transform.SetAsLastSibling();
                    ApplyTypography();
                    settingsController?.FocusBackButton();
                    if (pendingShowCredits)
                    {
                        pendingShowCredits = false;
                        settingsController?.ShowCredits();
                    }
                });
        }

        private void HidePanelBackdrop()
        {
            if (panelBackdrop == null)
            {
                SetOnly(mainPanel);
                return;
            }

            pendingShowCredits = false;
            panelBackdropTween?.Kill(false);
            SetOnly(mainPanel);
            float width = Mathf.Max(1f, menuContentRoot.rect.width);
            panelBackdropTween = DOTween.To(
                    () => panelBackdrop.anchoredPosition.x,
                    value => panelBackdrop.anchoredPosition =
                        new Vector2(value, 0f),
                    width,
                    panelBackdropSlideDuration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    panelBackdropTween = null;
                    panelBackdrop.gameObject.SetActive(false);
                    panelBackdrop.anchoredPosition = Vector2.zero;
                });
        }

        private void HidePanelBackdropImmediate()
        {
            pendingShowCredits = false;
            panelBackdropTween?.Kill(false);
            panelBackdropTween = null;
            if (panelBackdrop != null)
            {
                panelBackdrop.anchoredPosition = Vector2.zero;
                panelBackdrop.gameObject.SetActive(false);
            }
        }

        private void EnsurePanelBackdrop()
        {
            if (menuContentRoot == null)
            {
                return;
            }

            bool created = false;
            if (panelBackdrop == null)
            {
                Transform existing =
                    menuContentRoot.Find("Panel Backdrop");
                panelBackdrop = existing as RectTransform;
            }

            if (panelBackdrop == null)
            {
                GameObject root = new GameObject(
                    "Panel Backdrop",
                    typeof(RectTransform),
                    typeof(Image));
                root.transform.SetParent(menuContentRoot, false);
                panelBackdrop =
                    root.GetComponent<RectTransform>();
                StartMenuUiFactory.Stretch(panelBackdrop);
                Image underlay = root.GetComponent<Image>();
                underlay.color =
                    new Color(.035f, .07f, .085f, 1f);
                underlay.raycastTarget = true;

                GameObject art = new GameObject(
                    "Background Art",
                    typeof(RectTransform),
                    typeof(Image));
                art.transform.SetParent(root.transform, false);
                RectTransform artRect =
                    art.GetComponent<RectTransform>();
                StartMenuUiFactory.Stretch(artRect);
                Image image = art.GetComponent<Image>();
                image.sprite =
                    GameUiArtwork.Load()?.startMenuPanelBackground;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                image.raycastTarget = false;
                created = true;
            }

            if (created)
            {
                panelBackdrop.gameObject.SetActive(false);
            }
        }

        private CanvasGroup EnsureMenuContentCanvasGroup(
            RectTransform root)
        {
            if (menuContentCanvasGroup == null ||
                menuContentCanvasGroup.gameObject != root.gameObject)
            {
                menuContentCanvasGroup =
                    root.GetComponent<CanvasGroup>();
                if (menuContentCanvasGroup == null)
                {
                    menuContentCanvasGroup =
                        root.gameObject.AddComponent<CanvasGroup>();
                }
            }

            return menuContentCanvasGroup;
        }

        private void BindSubPanels()
        {
            settingsController?.SetOpenLayoutEnabled(true);
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
