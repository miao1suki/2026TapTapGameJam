using System;
using System.Collections.Generic;
using DG.Tweening;
using Project.Audio;
using Project.InputAbstraction;
using Project.StartMenu;
using Project.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Project.GameFlow
{
    public enum EscWheelAction
    {
        Continue = 0,
        Settings = 1,
        Save = 2,
        Collection = 3,
        Quit = 4,
        Credits = 5,
    }

    [Serializable]
    public sealed class EscWheelOption
    {
        [Tooltip("轮盘和子页面显示的名称")]
        public string label;
        [Tooltip("这一页选定的主题颜色，用于扇区高亮和颜料过渡")]
        [FormerlySerializedAs("color")]
        public Color pageColor;
        [Tooltip("单个扇区的专用材质；留空使用轮盘扇区默认材质")]
        public Material sectorMaterial;
        public EscWheelAction action;
        [Tooltip("这个选项打开的子页面")]
        public GameObject panel;
    }

    [DisallowMultipleComponent]
    public sealed class EscMenuController : MonoBehaviour
    {
        [Header("Pages")]
        [SerializeField] private CanvasGroup windowCanvas;
        [SerializeField] private RectTransform windowRoot;
        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private GameObject savePage;
        [SerializeField] private GameObject collectionPage;
        [SerializeField] private Graphic pauseBackdrop;
        [SerializeField] private Vector2 pageOpenOffset =
            new Vector2(360f, 0f);

        [Header("Wheel")]
        [SerializeField] private CanvasGroup wheelCanvas;
        [SerializeField] private RectTransform wheelPivot;
        [SerializeField] private RectTransform wheelDecoration;
        [Tooltip("整个轮盘装饰层的材质")]
        [SerializeField] private Material wheelMaterial;
        [Tooltip("所有扇区共用的默认材质")]
        [SerializeField] private Material wheelSectorMaterial;
        [SerializeField] private Button[] wheelButtons;
        [SerializeField] private List<EscWheelOption> options =
            new List<EscWheelOption>();
        [Header("Typography")]
        [Tooltip("Cubic_11 SDF字体资产")]
        [SerializeField] private TMP_FontAsset uiSdfFont;
        [SerializeField, Min(1f)] private float wheelLabelFontSize = 32f;
        [Tooltip("SDF字形笔画加粗量；0为字体默认粗细")]
        [SerializeField, Range(-.5f, .5f)] private float textStrokeThickness = .15f;
        [SerializeField] private Color textOutlineColor = Color.black;
        [SerializeField, Min(0f)] private float textOutlineDistance = 1.5f;
        [SerializeField, Min(.1f)] private float spinDuration = .65f;
        [SerializeField, Min(.05f)] private float snapDuration = .6f;
        [SerializeField, Min(.05f)] private float scrollStepInterval = .22f;
        [SerializeField, Range(-180f, 180f)] private float selectedAngle;
        [SerializeField, Range(.5f, 1.5f)] private float selectedScale = 1.08f;
        [SerializeField, Range(.5f, 1.5f)] private float unselectedScale = .94f;
        [SerializeField, Range(.1f, 1f)] private float dimAlpha = .5f;
        [SerializeField, Range(.5f, 1f)] private float dimScale = .88f;
        [SerializeField] private Vector2 occupyOffset = new Vector2(-100f, 0f);

        [Header("Existing business panels")]
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private SavePanelController savePanel;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button saveCloseButton;
        [SerializeField] private Button collectionCloseButton;

        private readonly Dictionary<Button, Color> buttonColors =
            new Dictionary<Button, Color>();
        private readonly Dictionary<Button, Tween> buttonTweens =
            new Dictionary<Button, Tween>();
        private GameUiRouter router;
        private GameObject currentPage;
        private Tween rootTween;
        private Tween pageTween;
        private Tween wheelTween;
        private Tween visualTween;
        private float[] visualAmounts;
        private float[] visualStartAmounts;
        private float[] visualTargetAmounts;
        private Color backdropColor = Color.white;
        private Material sdfOutlineMaterial;
        private float nextTypographyScanTime;
        private AchievementCollectionPresenter collectionPresenter;
        private EscSettingsResponsiveLayout settingsLayout;
        private EscSaveResponsiveLayout saveLayout;
        private int currentIndex;
        private float wheelAngle;
        private float scrollReadyTime;
        private bool moveLatched;
        private bool wheelInputReady;
        private bool initialized;
        private bool isOpen;

        public bool IsSubPageOpen =>
            currentPage != null &&
            currentPage != mainPage;

        public void ConfigureWheel(
            CanvasGroup valueWindowCanvas,
            RectTransform valueWindowRoot,
            GameObject valueMainPage,
            GameObject valueSettingsPage,
            GameObject valueSavePage,
            GameObject valueCollectionPage,
            CanvasGroup valueWheelCanvas,
            RectTransform valueWheelPivot,
            RectTransform valueWheelDecoration,
            Button[] valueWheelButtons,
            List<EscWheelOption> valueOptions,
            SettingsPanelController valueSettingsPanel,
            SavePanelController valueSavePanel,
            Button valueSettingsCloseButton,
            Button valueSaveCloseButton,
            Button valueCollectionCloseButton)
        {
            windowCanvas = valueWindowCanvas;
            windowRoot = valueWindowRoot;
            mainPage = valueMainPage;
            settingsPage = valueSettingsPage;
            savePage = valueSavePage;
            collectionPage = valueCollectionPage;
            wheelCanvas = valueWheelCanvas;
            wheelPivot = valueWheelPivot;
            wheelDecoration = valueWheelDecoration;
            wheelButtons = valueWheelButtons;
            options = valueOptions ?? new List<EscWheelOption>();
            settingsPanel = valueSettingsPanel;
            savePanel = valueSavePanel;
            settingsCloseButton = valueSettingsCloseButton;
            saveCloseButton = valueSaveCloseButton;
            collectionCloseButton = valueCollectionCloseButton;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (settingsPanel != null)
            {
                settingsPanel.BackRequested -= ReturnToMain;
            }

            UnhookButtonEffects();
            rootTween?.Kill();
            pageTween?.Kill();
            wheelTween?.Kill();
            visualTween?.Kill();
            if (sdfOutlineMaterial != null)
            {
                Destroy(sdfOutlineMaterial);
            }
        }

        private void Update()
        {
            if (MobileControlLayout.IsEditing || Project.Privacy.AndroidPrivacyPolicy.IsShowing) return;
            KeepWheelLabelsUpright();
            if (!isOpen || IsSubPageOpen || options.Count == 0)
            {
                return;
            }

            if (!wheelInputReady)
            {
                return;
            }

            float vertical = GameInput.ReadVector2(InputActionId.Move).y;
            float uiVertical = GameInput.ReadVector2(InputActionId.Navigate).y;
            if (Mathf.Abs(uiVertical) > Mathf.Abs(vertical)) vertical = uiVertical;
            if (Mathf.Abs(vertical) > .55f)
            {
                if (!moveLatched)
                {
                    moveLatched = true;
                    MoveSelection(vertical > 0f ? 1 : -1);
                }
            }
            else if (Mathf.Abs(vertical) < .25f)
            {
                moveLatched = false;
            }

            if (Mouse.current != null &&
                Time.unscaledTime >= scrollReadyTime)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > .1f)
                {
                    MoveSelection(scroll > 0f ? 1 : -1);
                    scrollReadyTime =
                        Time.unscaledTime + scrollStepInterval;
                }
            }

            bool confirmPressed =
                GameInput.WasTriggeredThisFrame(InputActionId.Jump) ||
                GameInput.WasTriggeredThisFrame(InputActionId.Submit);
            if (!confirmPressed && Mouse.current != null)
            {
                confirmPressed = (UnityEngine.EventSystems.EventSystem.current == null ||
                    !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) &&
                    (
                    Mouse.current.leftButton.wasPressedThisFrame ||
                    Mouse.current.rightButton.wasPressedThisFrame);
            }

            if (confirmPressed)
            {
                ConfirmCurrentOption();
            }
        }

        private void LateUpdate()
        {
            if (!isOpen ||
                Time.unscaledTime < nextTypographyScanTime)
            {
                return;
            }

            ApplyTypography();
            nextTypographyScanTime = Time.unscaledTime + .5f;
        }

        public void PlayOpen()
        {
            EnsureInitialized();
            isOpen = true;
            nextTypographyScanTime = 0f;
            moveLatched = false;
            wheelInputReady = false;
            scrollReadyTime = Time.unscaledTime + scrollStepInterval;
            rootTween?.Kill();
            wheelTween?.Kill();
            visualTween?.Kill();
            ShowMainImmediate();
            windowRoot.localScale = Vector3.one * .9f;
            windowCanvas.alpha = 0f;
            wheelCanvas.alpha = 1f;
            wheelPivot.localScale = Vector3.one;
            wheelPivot.anchoredPosition = Vector2.zero;
            wheelDecoration.localEulerAngles = Vector3.zero;
            wheelPivot.localEulerAngles = Vector3.zero;
            wheelAngle = 0f;
            SetSelection(0, false);
            wheelPivot.localEulerAngles = new Vector3(
                0f,
                0f,
                selectedAngle - 180f);
            wheelAngle = selectedAngle - 180f;

            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(windowRoot
                .DOScale(Vector3.one, .2f)
                .SetEase(Ease.OutCubic));
            sequence.Join(DOTween.To(
                    () => windowCanvas.alpha,
                    value => windowCanvas.alpha = value,
                    1f,
                    .18f)
                .SetEase(Ease.OutQuad));
            sequence.Join(wheelPivot
                .DORotate(
                    new Vector3(0f, 0f, selectedAngle),
                    spinDuration,
                    RotateMode.Fast)
                .SetEase(Ease.OutQuart)
                .OnUpdate(KeepWheelLabelsUpright));
            sequence.OnComplete(() =>
            {
                wheelAngle = selectedAngle;
                wheelInputReady = true;
            });
            rootTween = sequence;
        }

        public void PlayClose(Action onComplete)
        {
            EnsureInitialized();
            isOpen = false;
            wheelInputReady = false;
            rootTween?.Kill();
            wheelTween?.Kill();
            visualTween?.Kill();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(windowRoot
                .DOScale(Vector3.one * .9f, .14f)
                .SetEase(Ease.InQuad));
            sequence.Join(DOTween.To(
                    () => windowCanvas.alpha,
                    value => windowCanvas.alpha = value,
                    0f,
                    .14f)
                .SetEase(Ease.InQuad));
            sequence.OnComplete(() => onComplete?.Invoke());
            rootTween = sequence;
        }

        public bool TryTriggerCurrentCloseButton()
        {
            EnsureInitialized();
            Button close = CloseButtonForPage(currentPage);
            if (close == null || !close.interactable)
            {
                return false;
            }

            close.onClick.Invoke();
            return true;
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            router = GetComponentInParent<GameUiRouter>();
            if (pauseBackdrop == null)
            {
                pauseBackdrop = GetComponent<Graphic>();
            }

            if (pauseBackdrop != null)
            {
                backdropColor = pauseBackdrop.color;
            }

            ApplyTypography();
            EnsureCollectionPresenter();
            EnsureSettingsLayout();
            EnsureSaveLayout();
            BindCloseButtons();
            BindBusinessPanels();
            EnsureSectorGraphics();
            RegisterWheelButtons();
            initialized = true;
        }

        private void BindCloseButtons()
        {
            BindButton(settingsCloseButton, ReturnToMain);
            BindButton(saveCloseButton, ReturnToMain);
            BindButton(collectionCloseButton, ReturnToMain);
        }

        private void RegisterWheelButtons()
        {
            if (wheelButtons == null)
            {
                return;
            }

            for (int index = 0; index < wheelButtons.Length; index++)
            {
                if (wheelButtons[index] == null)
                {
                    continue;
                }

                int selectedIndex = index;
                wheelButtons[index].onClick.RemoveAllListeners();
                wheelButtons[index].interactable = true;
                wheelButtons[index].onClick.AddListener(() =>
                {
                    if (!isOpen || IsSubPageOpen || !wheelInputReady) return;
                    SetSelection(selectedIndex, false);
                    ConfirmCurrentOption();
                });
            }
            if (mainPage != null)
            {
                var gesture = mainPage.GetComponent<EscWheelTouchNavigation>();
                if (gesture == null) gesture = mainPage.AddComponent<EscWheelTouchNavigation>();
                gesture.Configure(this);
            }
        }

        public void NavigateWheel(int direction)
        {
            if (!isOpen || IsSubPageOpen || !wheelInputReady || direction == 0) return;
            MoveSelection(direction > 0 ? 1 : -1);
        }

        private void EnsureSectorGraphics()
        {
            if (wheelButtons == null)
            {
                return;
            }

            var artwork = GameUiArtwork.Load();
            EnsureCreditsOption(artwork);
            if (artwork != null && wheelPivot != null && wheelPivot.parent is RectTransform viewport &&
                Mathf.Approximately(viewport.anchorMin.x, viewport.anchorMax.x))
            {
                float labelExtent = artwork.pauseSectorSpacing + 350f * artwork.pauseSectorPixelScale / 8f + 110f;
                viewport.sizeDelta = new Vector2(Mathf.Max(viewport.sizeDelta.x, labelExtent * selectedScale + 32f), viewport.sizeDelta.y);
            }
            if (artwork != null &&
                (artwork.settingsPageBackground != null ||
                 artwork.panelBackground != null))
            {
                foreach (GameObject page in new[] { settingsPage, savePage, collectionPage })
                {
                    var pageImage = page != null ? page.GetComponent<Image>() : null;
                    if (pageImage == null) continue;
                    pageImage.sprite =
                        artwork.settingsPageBackground != null
                            ? artwork.settingsPageBackground
                            : artwork.panelBackground;
                    pageImage.type = Image.Type.Simple;
                    pageImage.color = Color.white;
                }
            }
            Graphic decorationGraphic = wheelDecoration != null
                ? wheelDecoration.GetComponent<Graphic>()
                : null;
            if (decorationGraphic != null && wheelMaterial != null)
            {
                decorationGraphic.material = wheelMaterial;
            }
            if (decorationGraphic != null && artwork != null) decorationGraphic.enabled = false;

            float angleStep = artwork != null ? artwork.pauseSectorAngle : options.Count > 0
                ? 360f / options.Count
                : 72f;
            for (int index = 0; index < wheelButtons.Length; index++)
            {
                Button button = wheelButtons[index];
                if (button == null)
                {
                    continue;
                }

                if (artwork != null && index < options.Count)
                {
                    int action = (int)options[index].action;
                    if (artwork.pauseIcons != null && action < artwork.pauseIcons.Length && artwork.pauseIcons[action] != null)
                    {
                        ConfigureArtworkButton(button, artwork.pauseIcons[action], index * angleStep, options[index].label);
                        continue;
                    }
                }

                Image oldImage = button.GetComponent<Image>();
                if (oldImage != null)
                {
                    DestroyImmediate(oldImage);
                }

                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax =
                    new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(900f, 900f);

                float centerAngle = index * angleStep;
                EscWheelSectorGraphic sector =
                    button.GetComponent<EscWheelSectorGraphic>();
                if (sector == null)
                {
                    sector = button.gameObject.AddComponent<
                        EscWheelSectorGraphic>();
                }

                Color color = index < options.Count
                    ? options[index].pageColor
                    : Color.white;
                sector.Configure(
                    centerAngle - angleStep * .44f,
                    centerAngle + angleStep * .44f,
                    135f,
                    360f);
                sector.color = color;
                sector.raycastTarget = true;
                button.targetGraphic = sector;
                button.transition = Selectable.Transition.None;
                Material optionMaterial = index < options.Count
                    ? options[index].sectorMaterial
                    : null;
                if (optionMaterial != null)
                {
                    sector.material = optionMaterial;
                }
                else if (wheelSectorMaterial != null)
                {
                    sector.material = wheelSectorMaterial;
                }

                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    if (index < options.Count)
                    {
                        label.text = options[index].label;
                    }

                    float radians = centerAngle * Mathf.Deg2Rad;
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = labelRect.anchorMax =
                        new Vector2(.5f, .5f);
                    labelRect.pivot = new Vector2(.5f, .5f);
                    labelRect.anchoredPosition =
                        new Vector2(
                            Mathf.Cos(radians),
                            Mathf.Sin(radians)) * 245f;
                    label.fontSize = Mathf.RoundToInt(
                        wheelLabelFontSize);
                    labelRect.sizeDelta = new Vector2(300f, 74f);
                    label.transform.rotation = Quaternion.identity;
                }
            }
        }

        private void EnsureCreditsOption(GameUiArtwork artwork)
        {
            if (artwork == null || artwork.pauseIcons == null || artwork.pauseIcons.Length < 6 ||
                settingsPage == null || wheelButtons == null || wheelButtons.Length == 0) return;
            foreach (var option in options)
                if (option.action == EscWheelAction.Credits) return;
            var template = wheelButtons[wheelButtons.Length - 1];
            if (template == null) return;
            var button = Instantiate(template, template.transform.parent);
            button.name = "Credits Sector";
            options.Add(new EscWheelOption
            {
                label = "制作名单",
                action = EscWheelAction.Credits,
                pageColor = new Color(.15f, .65f, .8f),
                panel = settingsPage,
            });
            Array.Resize(ref wheelButtons, wheelButtons.Length + 1);
            wheelButtons[wheelButtons.Length - 1] = button;
        }

        private void ConfigureArtworkButton(Button button, Sprite sprite, float angle, string title)
        {
            var oldSector = button.GetComponent<EscWheelSectorGraphic>();
            if (oldSector != null) DestroyImmediate(oldSector);
            var oldIcon = button.transform.Find("Menu Icon");
            if (oldIcon != null) DestroyImmediate(oldIcon.gameObject);
            var image = button.GetComponent<Image>();
            if (image == null) image = button.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.material = null;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = true;
            image.alphaHitTestMinimumThreshold = sprite.texture.isReadable ? .1f : 0f;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            var rect = image.rectTransform;
            var artwork = GameUiArtwork.Load();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = artwork.pauseSectorPivot;
            float radians = angle * Mathf.Deg2Rad;
            rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * artwork.pauseSectorSpacing;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            rect.sizeDelta = sprite.rect.size * artwork.pauseSectorPixelScale;
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = title;
                label.fontSize = Mathf.RoundToInt(wheelLabelFontSize);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = rect.pivot;
                label.rectTransform.pivot = new Vector2(.5f, .5f);
                label.rectTransform.anchoredPosition = new Vector2(350f * artwork.pauseSectorPixelScale / 8f, 0f);
                label.rectTransform.sizeDelta = new Vector2(220f, 60f);
                label.transform.rotation = Quaternion.identity;
            }
        }

        private void ApplyTypography()
        {
            if (windowRoot == null || uiSdfFont == null)
            {
                return;
            }

            Material material = EnsureSdfOutlineMaterial();
            Text[] labels = windowRoot.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                Text label = labels[index];
                if (label == null)
                {
                    continue;
                }

                TextSdfMirror existing =
                    label.GetComponentInChildren<TextSdfMirror>(true);
                if (existing != null)
                {
                    existing.ApplyFont(uiSdfFont, material);
                    continue;
                }

                TextSdfMirror.Attach(label, uiSdfFont, material);
            }
        }

        public void RefreshTypography()
        {
            ApplyTypography();
        }

        private void EnsureCollectionPresenter()
        {
            if (collectionPage == null)
            {
                return;
            }

            collectionPresenter =
                collectionPage.GetComponent<
                    AchievementCollectionPresenter>();
            if (collectionPresenter == null)
            {
                collectionPresenter =
                    collectionPage.AddComponent<
                        AchievementCollectionPresenter>();
            }
        }

        private void EnsureSettingsLayout()
        {
            if (settingsPage == null)
            {
                return;
            }

            settingsLayout =
                settingsPage.GetComponent<
                    EscSettingsResponsiveLayout>();
            if (settingsLayout == null)
            {
                settingsLayout =
                    settingsPage.AddComponent<
                        EscSettingsResponsiveLayout>();
            }
        }

        private void EnsureSaveLayout()
        {
            if (savePage == null)
            {
                return;
            }

            saveLayout =
                savePage.GetComponent<EscSaveResponsiveLayout>();
            if (saveLayout == null)
            {
                saveLayout =
                    savePage.AddComponent<EscSaveResponsiveLayout>();
            }
        }

        private Material EnsureSdfOutlineMaterial()
        {
            if (sdfOutlineMaterial != null)
            {
                return sdfOutlineMaterial;
            }

            sdfOutlineMaterial = new Material(uiSdfFont.material);
            sdfOutlineMaterial.name = uiSdfFont.name + " Outline";
            sdfOutlineMaterial.SetColor(
                ShaderUtilities.ID_OutlineColor,
                textOutlineColor);
            sdfOutlineMaterial.SetFloat(
                ShaderUtilities.ID_OutlineWidth,
                Mathf.Clamp(textOutlineDistance * .1f, 0f, 1f));
            sdfOutlineMaterial.SetFloat(
                ShaderUtilities.ID_FaceDilate,
                textStrokeThickness);
            sdfOutlineMaterial.SetFloat(
                ShaderUtilities.ID_OutlineSoftness,
                0f);
            sdfOutlineMaterial.EnableKeyword("OUTLINE_ON");
            return sdfOutlineMaterial;
        }

        private void BindBusinessPanels()
        {
            if (settingsPanel != null)
            {
                settingsPanel.BackRequested -= ReturnToMain;
                settingsPanel.BackRequested += ReturnToMain;
                settingsPanel.SetBackButtonVisible(false);
            }

        }

        private void MoveSelection(int delta)
        {
            if (options.Count == 0)
            {
                return;
            }

            int next = currentIndex + delta;
            if (next < 0)
            {
                next = options.Count - 1;
            }
            else if (next >= options.Count)
            {
                next = 0;
            }

            SetSelection(next, true, delta);
        }

        private void SetSelection(
            int index,
            bool animate,
            int stepDirection = 0)
        {
            if (options.Count == 0)
            {
                return;
            }

            currentIndex = Mathf.Clamp(index, 0, options.Count - 1);
            var artwork = GameUiArtwork.Load();
            float angleStep = artwork != null ? artwork.pauseSectorAngle : 360f / options.Count;
            wheelAngle = animate && stepDirection != 0
                ? wheelAngle - stepDirection * angleStep
                : selectedAngle - currentIndex * angleStep;
            if (animate)
            {
                EnsureWheelVisualAmounts();
                int count = visualAmounts.Length;
                for (int visualIndex = 0;
                     visualIndex < count;
                     visualIndex++)
                {
                    visualStartAmounts[visualIndex] =
                        visualAmounts[visualIndex];
                    visualTargetAmounts[visualIndex] =
                        visualIndex == currentIndex ? 1f : 0f;
                }

                wheelTween?.Kill();
                visualTween?.Kill();
                visualTween = DOTween.To(
                        () => 0f,
                        progress =>
                        {
                            for (int visualIndex = 0;
                                 visualIndex < count;
                                 visualIndex++)
                            {
                                visualAmounts[visualIndex] =
                                    Mathf.LerpUnclamped(
                                        visualStartAmounts[visualIndex],
                                        visualTargetAmounts[visualIndex],
                                        progress);
                            }

                            ApplyWheelVisualStates();
                        },
                        1f,
                        snapDuration)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .OnComplete(RefreshWheelVisuals);
                wheelTween = wheelPivot
                    .DORotate(
                        new Vector3(0f, 0f, wheelAngle),
                        snapDuration,
                        RotateMode.Fast)
                    .SetEase(Ease.OutCubic)
                    .SetUpdate(true)
                    .OnUpdate(KeepWheelLabelsUpright);
            }
            else
            {
                wheelPivot.localEulerAngles =
                    new Vector3(0f, 0f, wheelAngle);
                RefreshWheelVisuals();
                KeepWheelLabelsUpright();
            }
        }

        private void RefreshWheelVisuals()
        {
            if (wheelButtons == null)
            {
                return;
            }

            EnsureWheelVisualAmounts();
            for (int visualIndex = 0;
                 visualIndex < visualAmounts.Length;
                 visualIndex++)
            {
                visualAmounts[visualIndex] =
                    visualIndex == currentIndex ? 1f : 0f;
            }

            ApplyWheelVisualStates();
        }

        private void EnsureWheelVisualAmounts()
        {
            int count = wheelButtons?.Length ?? 0;
            if (visualAmounts == null ||
                visualAmounts.Length != count)
            {
                visualAmounts = new float[count];
                visualStartAmounts = new float[count];
                visualTargetAmounts = new float[count];
            }
        }

        private void ApplyWheelVisualStates()
        {
            if (wheelButtons == null || visualAmounts == null)
            {
                return;
            }

            int count = Mathf.Min(
                wheelButtons.Length,
                visualAmounts.Length);
            for (int buttonIndex = 0;
                 buttonIndex < count;
                 buttonIndex++)
            {
                Button button = wheelButtons[buttonIndex];
                if (button == null)
                {
                    continue;
                }

                float selectedAmount =
                    Mathf.Clamp01(visualAmounts[buttonIndex]);
                Color baseColor = buttonIndex < options.Count
                    ? options[buttonIndex].pageColor
                    : Color.white;
                if (button.targetGraphic is Image) baseColor = Color.white;
                Graphic graphic = button.targetGraphic;
                if (graphic != null)
                {
                    graphic.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
                }

                float scale = Mathf.Lerp(
                        unselectedScale,
                        selectedScale,
                        selectedAmount);
                button.transform.localScale = Vector3.one * scale;
                if (button.targetGraphic is Image && buttonIndex < options.Count)
                {
                    // Scale radial spacing with the artwork, retaining wheel-centred protrusion.
                    var artwork = GameUiArtwork.Load();
                    float angle = buttonIndex * artwork.pauseSectorAngle * Mathf.Deg2Rad;
                    ((RectTransform)button.transform).anchoredPosition =
                        new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (artwork.pauseSectorSpacing * scale);
                }
            }
        }

        private void ConfirmCurrentOption()
        {
            if (currentIndex < 0 || currentIndex >= options.Count)
            {
                return;
            }

            EscWheelOption option = options[currentIndex];
            AudioCuePlayer.PlayUiConfirm();
            switch (option.action)
            {
                case EscWheelAction.Continue:
                    router?.ResumeGame();
                    break;
                case EscWheelAction.Quit:
                    router?.OpenMainMenu();
                    break;
                case EscWheelAction.Credits:
                    PlayWheelOccupy();
                    OpenPage(settingsPage, option.pageColor);
                    settingsPanel?.ShowCredits();
                    break;
                default:
                    PlayWheelOccupy();
                    OpenPage(option.panel, option.pageColor);
                    break;
            }
        }

        private void PlayWheelOccupy()
        {
            wheelTween?.Kill();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(DOTween.To(
                    () => wheelCanvas.alpha,
                    value => wheelCanvas.alpha = value,
                    dimAlpha,
                    .2f)
                .SetEase(Ease.OutQuad));
            sequence.Join(DOTween.To(
                    () => wheelPivot.anchoredPosition,
                    value => wheelPivot.anchoredPosition = value,
                    occupyOffset,
                    .22f)
                .SetEase(Ease.OutCubic));
            sequence.Join(wheelPivot
                .DOScale(dimScale, .22f)
                .SetEase(Ease.OutCubic));
            wheelTween = sequence;
        }

        private void RestoreWheel()
        {
            wheelTween?.Kill();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(DOTween.To(
                    () => wheelCanvas.alpha,
                    value => wheelCanvas.alpha = value,
                    1f,
                    .2f)
                .SetEase(Ease.OutQuad));
            sequence.Join(DOTween.To(
                    () => wheelPivot.anchoredPosition,
                    value => wheelPivot.anchoredPosition = value,
                    Vector2.zero,
                    .22f)
                .SetEase(Ease.OutCubic));
            sequence.Join(wheelPivot
                .DOScale(Vector3.one, .22f)
                .SetEase(Ease.OutCubic));
            wheelTween = sequence;
        }

        private void OpenPage(GameObject page, Color color)
        {
            if (page == null || page == mainPage)
            {
                return;
            }

            currentPage = page;
            RectTransform rect = page.GetComponent<RectTransform>();
            CanvasGroup canvas = EnsureCanvasGroup(page);
            PreparePageRect(rect);
            SetPageSlide(rect, 1100f);
            page.SetActive(true);
            if (page == collectionPage)
            {
                collectionPresenter?.Refresh();
            }
            else if (page == settingsPage)
            {
                settingsLayout?.RefreshLayout();
            }
            else if (page == savePage)
            {
                saveLayout?.RefreshLayout();
            }

            ApplyTypography();
            canvas.alpha = 0f;
            SetBackdropVisible(false);

            pageTween?.Kill();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(DOTween.To(
                    () => rect.offsetMax.x,
                    value => SetPageSlide(rect, value),
                    0f,
                    .3f)
                .SetEase(Ease.OutCubic));
            sequence.Join(DOTween.To(
                    () => canvas.alpha,
                    value => canvas.alpha = value,
                    1f,
                    .2f)
                .SetEase(Ease.OutQuad));
            pageTween = sequence;
        }

        private void ReturnToMain()
        {
            if (!IsSubPageOpen)
            {
                ShowMainImmediate();
                return;
            }

            GameObject closing = currentPage;
            RectTransform rect = closing.GetComponent<RectTransform>();
            CanvasGroup canvas = EnsureCanvasGroup(closing);
            pageTween?.Kill();
            SetBackdropVisible(true);
            RestoreWheel();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(DOTween.To(
                    () => rect.offsetMax.x,
                    value => SetPageSlide(rect, value),
                    1100f,
                    .22f)
                .SetEase(Ease.InCubic));
            sequence.Join(DOTween.To(
                    () => canvas.alpha,
                    value => canvas.alpha = value,
                    0f,
                    .16f)
                .SetEase(Ease.InQuad));
            sequence.OnComplete(() =>
            {
                closing.SetActive(false);
                currentPage = null;
                ShowMainImmediate();
            });
            pageTween = sequence;
        }

        private void ShowMainImmediate()
        {
            pageTween?.Kill();
            HidePage(settingsPage);
            HidePage(savePage);
            HidePage(collectionPage);
            SetBackdropVisible(true);
            currentPage = mainPage;
            if (mainPage != null)
            {
                mainPage.SetActive(true);
            }
        }

        private Button CloseButtonForPage(GameObject page)
        {
            return page == settingsPage
                ? settingsCloseButton
                : page == savePage
                    ? saveCloseButton
                    : page == collectionPage
                        ? collectionCloseButton
                        : null;
        }

        private static CanvasGroup EnsureCanvasGroup(GameObject target)
        {
            CanvasGroup canvas = target.GetComponent<CanvasGroup>();
            if (canvas == null)
            {
                canvas = target.AddComponent<CanvasGroup>();
            }

            return canvas;
        }

        private static void HidePage(GameObject page)
        {
            if (page != null)
            {
                page.SetActive(false);
            }
        }

        private void PreparePageRect(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
        }

        private void SetPageSlide(RectTransform rect, float slide)
        {
            rect.offsetMin = new Vector2(
                pageOpenOffset.x + slide,
                0f);
            rect.offsetMax = new Vector2(slide, 0f);
        }

        private void SetBackdropVisible(bool visible)
        {
            if (pauseBackdrop == null)
            {
                return;
            }

            Color color = backdropColor;
            color.a = visible ? backdropColor.a : 0f;
            pauseBackdrop.color = color;
        }

        private void BindButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                AudioCuePlayer.PlayUiConfirm();
                action?.Invoke();
            });
        }

        private void KeepWheelLabelsUpright()
        {
            if (wheelButtons == null)
            {
                return;
            }

            for (int index = 0; index < wheelButtons.Length; index++)
            {
                Button button = wheelButtons[index];
                if (button == null)
                {
                    continue;
                }

                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.transform.rotation = Quaternion.identity;
                }
                var icon = button.transform.Find("Menu Icon");
                if (icon != null) icon.rotation = Quaternion.identity;
            }
        }

        private void AddButtonEffect(Button button)
        {
            if (button == null || buttonColors.ContainsKey(button))
            {
                return;
            }

            Graphic graphic = button.targetGraphic;
            if (graphic == null)
            {
                return;
            }

            buttonColors[button] = graphic.color;
            UnityEngine.EventSystems.EventTrigger trigger =
                button.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<
                    UnityEngine.EventSystems.EventTrigger>();
            }

            AddTrigger(
                trigger,
                UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                () => AnimateButton(button, true));
            AddTrigger(
                trigger,
                UnityEngine.EventSystems.EventTriggerType.PointerExit,
                () => AnimateButton(button, false));
        }

        private void AddTrigger(
            UnityEngine.EventSystems.EventTrigger trigger,
            UnityEngine.EventSystems.EventTriggerType type,
            Action action)
        {
            UnityEngine.EventSystems.EventTrigger.Entry entry =
                new UnityEngine.EventSystems.EventTrigger.Entry
                {
                    eventID = type,
                };
            entry.callback.AddListener(_ => action?.Invoke());
            trigger.triggers.Add(entry);
        }

        private void AnimateButton(Button button, bool highlighted)
        {
            if (button == null)
            {
                return;
            }

            if (buttonTweens.TryGetValue(button, out Tween active) &&
                active != null)
            {
                active.Kill();
            }

            Color baseColor = buttonColors.TryGetValue(
                button,
                out Color stored)
                ? stored
                : Color.white;
            Color target = highlighted
                ? Color.Lerp(baseColor, new Color(.82f, .9f, .98f), .42f)
                : baseColor;
            Tween tween = DOTween.Sequence()
                .Join(button.transform
                    .DOScale(
                        Vector3.one * (highlighted ? 1.06f : 1f),
                        .1f)
                    .SetEase(Ease.OutQuad))
                .Join(DOTween.To(
                        () => button.targetGraphic.color,
                        value => button.targetGraphic.color = value,
                        target,
                        .1f)
                    .SetEase(Ease.OutQuad))
                .SetUpdate(true);
            buttonTweens[button] = tween;
        }

        private void UnhookButtonEffects()
        {
            foreach (Tween tween in buttonTweens.Values)
            {
                tween?.Kill();
            }

            buttonTweens.Clear();
            buttonColors.Clear();
        }
    }
}
