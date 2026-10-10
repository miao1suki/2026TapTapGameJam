using System;
using System.Collections.Generic;
using DG.Tweening;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class EscMenuController : MonoBehaviour
    {
        [SerializeField] private CanvasGroup windowCanvas;
        [SerializeField] private RectTransform windowRoot;
        [SerializeField] private GameObject mainPage;
        [SerializeField] private GameObject settingsPage;
        [SerializeField] private GameObject savePage;
        [SerializeField] private GameObject collectionPage;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button collectionButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button saveCloseButton;
        [SerializeField] private Button collectionCloseButton;
        [SerializeField] private SettingsPanelController settingsPanel;
        [SerializeField] private SavePanelController savePanel;

        private readonly Dictionary<Button, Color> buttonColors =
            new Dictionary<Button, Color>();
        private readonly Dictionary<Button, Tween> buttonTweens =
            new Dictionary<Button, Tween>();
        private GameUiRouter router;
        private GameObject currentPage;
        private Tween rootTween;
        private Tween pageTween;
        private bool initialized;

        public bool IsSubPageOpen =>
            currentPage != null &&
            currentPage != mainPage;

        public void Configure(
            CanvasGroup valueWindowCanvas,
            RectTransform valueWindowRoot,
            GameObject valueMainPage,
            GameObject valueSettingsPage,
            GameObject valueSavePage,
            GameObject valueCollectionPage,
            Button valueContinueButton,
            Button valueSettingsButton,
            Button valueSaveButton,
            Button valueCollectionButton,
            Button valueQuitButton,
            Button valueSettingsCloseButton,
            Button valueSaveCloseButton,
            Button valueCollectionCloseButton,
            SettingsPanelController valueSettingsPanel,
            SavePanelController valueSavePanel)
        {
            windowCanvas = valueWindowCanvas;
            windowRoot = valueWindowRoot;
            mainPage = valueMainPage;
            settingsPage = valueSettingsPage;
            savePage = valueSavePage;
            collectionPage = valueCollectionPage;
            continueButton = valueContinueButton;
            settingsButton = valueSettingsButton;
            saveButton = valueSaveButton;
            collectionButton = valueCollectionButton;
            quitButton = valueQuitButton;
            settingsCloseButton = valueSettingsCloseButton;
            saveCloseButton = valueSaveCloseButton;
            collectionCloseButton = valueCollectionCloseButton;
            settingsPanel = valueSettingsPanel;
            savePanel = valueSavePanel;
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

            if (savePanel != null)
            {
                savePanel.BackRequested -= ReturnToMain;
                savePanel.LoadRequested -= LoadSlot;
                savePanel.SaveRequested -= SaveToSlot;
            }

            rootTween?.Kill();
            pageTween?.Kill();
        }

        public void PlayOpen()
        {
            EnsureInitialized();
            KillRootTween();
            ShowMainImmediate();
            windowRoot.localScale = Vector3.one * .86f;
            windowCanvas.alpha = 0f;
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(windowRoot
                .DOScale(Vector3.one, .24f)
                .SetEase(Ease.OutBack));
            sequence.Join(DOTween.To(
                    () => windowCanvas.alpha,
                    value => windowCanvas.alpha = value,
                    1f,
                    .18f)
                .SetEase(Ease.OutQuad));
            rootTween = sequence;
        }

        public void PlayClose(Action onComplete)
        {
            EnsureInitialized();
            KillPageTween();
            KillRootTween();
            windowRoot.localScale = Vector3.one;
            windowCanvas.alpha = 1f;
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(windowRoot
                .DOScale(Vector3.one * .88f, .16f)
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
            Button close = currentPage == settingsPage
                ? settingsCloseButton
                : currentPage == savePage
                    ? saveCloseButton
                    : currentPage == collectionPage
                        ? collectionCloseButton
                        : null;
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
            BindButtons();
            BindPanels();
            settingsPanel?.SetBackButtonVisible(false);
            savePanel?.SetBackButtonVisible(false);
            initialized = true;
        }

        private void BindButtons()
        {
            BindButton(continueButton, () => router?.ResumeGame());
            BindButton(settingsButton, () => OpenPage(settingsPage));
            BindButton(saveButton, () =>
            {
                savePanel?.Refresh();
                OpenPage(savePage);
            });
            BindButton(collectionButton, () => OpenPage(collectionPage));
            BindButton(quitButton, () => router?.OpenMainMenu());
            BindButton(settingsCloseButton, ReturnToMain);
            BindButton(saveCloseButton, ReturnToMain);
            BindButton(collectionCloseButton, ReturnToMain);

            AddButtonEffect(continueButton);
            AddButtonEffect(settingsButton);
            AddButtonEffect(saveButton);
            AddButtonEffect(collectionButton);
            AddButtonEffect(quitButton);
            AddButtonEffect(settingsCloseButton);
            AddButtonEffect(saveCloseButton);
            AddButtonEffect(collectionCloseButton);
        }

        private void BindPanels()
        {
            if (settingsPanel != null)
            {
                settingsPanel.BackRequested -= ReturnToMain;
                settingsPanel.BackRequested += ReturnToMain;
            }

            if (savePanel != null)
            {
                savePanel.BackRequested -= ReturnToMain;
                savePanel.BackRequested += ReturnToMain;
                savePanel.LoadRequested -= LoadSlot;
                savePanel.LoadRequested += LoadSlot;
                savePanel.SaveRequested -= SaveToSlot;
                savePanel.SaveRequested += SaveToSlot;
            }
        }

        private static void BindButton(Button button, Action action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void OpenPage(GameObject page)
        {
            ResetAllButtonVisuals();
            if (page == null || page == mainPage)
            {
                ShowMainImmediate();
                return;
            }

            EnsurePageState(page, false);
            currentPage = page;
            if (mainPage != null)
            {
                mainPage.SetActive(false);
            }

            RectTransform rect = page.GetComponent<RectTransform>();
            CanvasGroup canvas = EnsureCanvasGroup(page);
            page.SetActive(true);
            rect.anchoredPosition = new Vector2(1100f, 0f);
            canvas.alpha = 0f;
            KillPageTween();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(DOTween.To(
                    () => rect.anchoredPosition,
                    value => rect.anchoredPosition = value,
                    Vector2.zero,
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
            KillPageTween();
            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            Vector2 target = new Vector2(1100f, 0f);
            sequence.Join(DOTween.To(
                    () => rect.anchoredPosition,
                    value => rect.anchoredPosition = value,
                    target,
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
                ResetAllButtonVisuals();
                ShowMainImmediate();
            });
            pageTween = sequence;
        }

        private void ShowMainImmediate()
        {
            KillPageTween();
            ResetAllButtonVisuals();
            HidePage(settingsPage);
            HidePage(savePage);
            HidePage(collectionPage);
            currentPage = mainPage;
            if (mainPage != null)
            {
                mainPage.SetActive(true);
            }
        }

        private static void HidePage(GameObject page)
        {
            if (page != null)
            {
                page.SetActive(false);
            }
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

        private static void EnsurePageState(
            GameObject page,
            bool active)
        {
            if (page != null && page.activeSelf != active)
            {
                page.SetActive(active);
            }
        }

        private void LoadSlot(string slotId)
        {
            if (savePanel == null)
            {
                return;
            }

            string levelLabel = savePanel.LoadSlot(slotId);
            if (string.IsNullOrWhiteSpace(levelLabel))
            {
                return;
            }

            if (!TryMapLevel(levelLabel, out GameFlowSceneId sceneId))
            {
                savePanel.SetStatus(
                    $"已读取存档：{levelLabel}；当前原型未找到对应关卡。");
                return;
            }

            router?.SetPaused(false, false);
            GameFlowController.Instance?.RequestTransition(sceneId);
        }

        private void SaveToSlot(string slotId)
        {
            if (savePanel == null)
            {
                return;
            }

            GameFlowSceneId sceneId =
                GameFlowController.Instance?.ActiveSceneId ??
                GameFlowSceneId.Level01;
            string levelLabel = LevelLabel(sceneId);
            savePanel.SaveCurrentToSlot(
                slotId,
                levelLabel,
                "{}");
        }

        private static bool TryMapLevel(
            string label,
            out GameFlowSceneId sceneId)
        {
            if (label == "关卡 1")
            {
                sceneId = GameFlowSceneId.Level01;
                return true;
            }

            if (label == "关卡 2")
            {
                sceneId = GameFlowSceneId.Level02;
                return true;
            }

            if (label == "关卡 3")
            {
                sceneId = GameFlowSceneId.Level03;
                return true;
            }

            sceneId = default;
            return false;
        }

        private static string LevelLabel(GameFlowSceneId id)
        {
            switch (id)
            {
                case GameFlowSceneId.Level01:
                    return "关卡 1";
                case GameFlowSceneId.Level02:
                    return "关卡 2";
                case GameFlowSceneId.Level03:
                    return "关卡 3";
                default:
                    return "未开始";
            }
        }

        private void AddButtonEffect(Button button)
        {
            if (button == null || buttonColors.ContainsKey(button))
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                buttonColors[button] = image.color;
            }

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                trigger = button.gameObject.AddComponent<EventTrigger>();
            }

            AddTrigger(trigger, EventTriggerType.PointerEnter,
                _ => AnimateButton(
                    button,
                    1.055f,
                    GetHoverColor(button)));
            AddTrigger(trigger, EventTriggerType.PointerExit,
                _ => AnimateButton(
                    button,
                    1f,
                    GetBaseColor(button)));
            AddTrigger(trigger, EventTriggerType.PointerDown,
                _ => AnimateButton(
                    button,
                    1.085f,
                    GetPressedColor(button)));
            AddTrigger(trigger, EventTriggerType.PointerUp,
                _ => AnimateButton(
                    button,
                    1.055f,
                    GetHoverColor(button)));
        }

        private void ResetAllButtonVisuals()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                ResetButtonVisual(buttons[index]);
            }
        }

        private void ResetButtonVisual(Button button)
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

            buttonTweens.Remove(button);
            button.transform.localScale = Vector3.one;
            Image image = button.GetComponent<Image>();
            if (image != null &&
                buttonColors.TryGetValue(button, out Color baseColor))
            {
                image.color = baseColor;
            }
        }

        private Color GetBaseColor(Button button)
        {
            return buttonColors.TryGetValue(button, out Color value)
                ? value
                : Color.white;
        }

        private Color GetHoverColor(Button button)
        {
            return Color.Lerp(
                GetBaseColor(button),
                new Color(.82f, .90f, .98f, 1f),
                .42f);
        }

        private Color GetPressedColor(Button button)
        {
            return Color.Lerp(
                GetBaseColor(button),
                new Color(.68f, .82f, .95f, 1f),
                .52f);
        }

        private void AnimateButton(
            Button button,
            float scale,
            Color color)
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

            Sequence sequence = DOTween.Sequence();
            sequence.SetUpdate(true);
            sequence.Join(button.transform
                .DOScale(Vector3.one * scale, .1f)
                .SetEase(Ease.OutQuad));
            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                sequence.Join(DOTween.To(
                        () => image.color,
                        value => image.color = value,
                        color,
                        .1f)
                    .SetEase(Ease.OutQuad));
            }

            buttonTweens[button] = sequence;
        }

        private static void AddTrigger(
            EventTrigger trigger,
            EventTriggerType type,
            Action<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry
            {
                eventID = type
            };
            entry.callback.AddListener(
                data => callback?.Invoke(data));
            trigger.triggers.Add(entry);
        }

        private void KillRootTween()
        {
            if (rootTween != null)
            {
                rootTween.Kill();
                rootTween = null;
            }
        }

        private void KillPageTween()
        {
            if (pageTween != null)
            {
                pageTween.Kill();
                pageTween = null;
            }
        }
    }
}
