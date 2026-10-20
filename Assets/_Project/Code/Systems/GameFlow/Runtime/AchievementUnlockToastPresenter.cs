using System.Collections.Generic;
using DG.Tweening;
using Project.Achievements;
using Project.StartMenu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    [AddComponentMenu("2026TapTap/UI/Achievement Unlock Toast")]
    public sealed class AchievementUnlockToastPresenter :
        MonoBehaviour,
        IAchievementUnlockReceiver
    {
        [Header("内容")]
        [SerializeField] private string unlockTitle = "达成成就！";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Color titleColor =
            new Color(.27f, .29f, .31f, 1f);
        [SerializeField] private Color achievementNameColor = Color.black;
        [SerializeField, Min(1f)] private float titleFontSize = 28f;
        [SerializeField, Min(1f)] private float achievementNameFontSize = 44f;
        [SerializeField, Min(1f)] private float minimumNameFontSize = 28f;
        [SerializeField] private bool boldTitle;
        [SerializeField] private bool boldAchievementName = true;

        [Header("成就块")]
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private Material blockMaterial;
        [SerializeField] private Color blockColor = Color.white;
        [SerializeField] private Sprite checkSprite;
        [SerializeField] private Color checkColor = Color.white;
        [SerializeField, Min(1f)] private float checkSize = 52f;
        [SerializeField, Min(0f)] private float checkGap = 14f;
        [SerializeField] private float checkVerticalOffset;
        [SerializeField] private Vector2 blockSize = new Vector2(560f, 160f);
        [SerializeField] private Vector2 bottomLeftOffset =
            new Vector2(48f, 56f);
        [SerializeField] private Vector2 contentPadding =
            new Vector2(42f, 22f);
        [SerializeField, Min(0f)] private float titleHeight = 36f;
        [SerializeField, Min(0f)] private float titleNameSpacing = 6f;

        [Header("入退场")]
        [SerializeField, Min(0f)] private float offscreenExtra = 80f;
        [SerializeField, Min(0f)] private float enterDuration = .42f;
        [SerializeField, Min(0f)] private float stayDuration = 3.2f;
        [SerializeField, Min(0f)] private float exitDuration = .3f;
        [SerializeField] private Ease enterEase = Ease.OutCubic;
        [SerializeField] private Ease exitEase = Ease.InCubic;
        [SerializeField] private bool fadeWithSlide = true;

        private readonly Queue<string> pendingNames =
            new Queue<string>();
        private AchievementManager achievementManager;
        private GameObject toastCanvasObject;
        private RectTransform toastRect;
        private CanvasGroup toastGroup;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI achievementNameText;
        private Sequence sequence;
        private bool isShowing;

        private void OnEnable()
        {
            achievementManager = AchievementManager.Instance;
            achievementManager.RegisterReceiver(this);
        }

        private void OnDisable()
        {
            if (achievementManager != null)
            {
                achievementManager.UnregisterReceiver(this);
            }

            StopAndClear();
        }

        public void OnAchievementUnlocked(string displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return;
            }

            Debug.Log(
                "[AchievementToast] 收到成就提示：" +
                displayName.Trim(),
                this);
            pendingNames.Enqueue(displayName.Trim());
            if (!isShowing)
            {
                ShowNext();
            }
        }

        private void ShowNext()
        {
            if (isShowing ||
                pendingNames.Count == 0 ||
                !isActiveAndEnabled)
            {
                return;
            }

            EnsureView();
            isShowing = true;
            string displayName = pendingNames.Dequeue();
            titleText.text = unlockTitle;
            achievementNameText.text = displayName;
            toastRect.gameObject.SetActive(true);

            float hiddenX = -blockSize.x - offscreenExtra;
            toastRect.anchoredPosition =
                new Vector2(hiddenX, bottomLeftOffset.y);
            toastGroup.alpha = fadeWithSlide ? 0f : 1f;

            sequence?.Kill(false);
            sequence = DOTween.Sequence()
                .SetUpdate(true);
            sequence.Append(
                CreateHorizontalMoveTween(
                        bottomLeftOffset.x,
                        enterDuration)
                    .SetEase(enterEase));
            if (fadeWithSlide)
            {
                sequence.Join(
                    DOTween.To(
                            () => toastGroup.alpha,
                            value => toastGroup.alpha = value,
                            1f,
                            enterDuration)
                        .SetUpdate(true));
            }

            sequence
                .AppendInterval(stayDuration)
                .Append(
                    CreateHorizontalMoveTween(
                            hiddenX,
                            exitDuration)
                        .SetEase(exitEase));
            if (fadeWithSlide)
            {
                sequence.Join(
                    DOTween.To(
                            () => toastGroup.alpha,
                            value => toastGroup.alpha = value,
                            0f,
                            exitDuration)
                        .SetUpdate(true));
            }

            sequence.OnComplete(() =>
            {
                sequence = null;
                isShowing = false;
                toastRect.gameObject.SetActive(false);
                ShowNext();
            });
        }

        private Tween CreateHorizontalMoveTween(
            float targetX,
            float duration)
        {
            float y = toastRect.anchoredPosition.y;
            return DOTween.To(
                    () => toastRect.anchoredPosition.x,
                    value => toastRect.anchoredPosition =
                        new Vector2(value, y),
                    targetX,
                    duration)
                .SetUpdate(true);
        }

        private void EnsureView()
        {
            if (toastRect != null)
            {
                return;
            }

            if (blockSprite == null)
            {
                blockSprite =
                    GameUiArtwork.Load()?.achievementUnlockBlock;
            }

            if (checkSprite == null)
            {
                checkSprite =
                    GameUiArtwork.Load()?.achievementUnlockCheck;
            }

            toastCanvasObject = new GameObject(
                "Achievement Unlock Toast Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            toastCanvasObject.transform.SetParent(null, false);
            toastCanvasObject.layer = gameObject.layer;

            Canvas canvas =
                toastCanvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingLayerID =
                ResolveHighestSortingLayerId();
            canvas.sortingOrder = short.MaxValue;
            Canvas parentCanvas = GetComponent<Canvas>();
            if (parentCanvas != null)
            {
                canvas.additionalShaderChannels =
                    parentCanvas.additionalShaderChannels;
            }

            CanvasScaler scaler =
                toastCanvasObject.GetComponent<CanvasScaler>();
            CanvasScaler parentScaler =
                GetComponent<CanvasScaler>();
            if (parentScaler != null)
            {
                scaler.uiScaleMode =
                    parentScaler.uiScaleMode;
                scaler.referenceResolution =
                    parentScaler.referenceResolution;
                scaler.screenMatchMode =
                    parentScaler.screenMatchMode;
                scaler.matchWidthOrHeight =
                    parentScaler.matchWidthOrHeight;
            }
            else
            {
                scaler.uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution =
                    new Vector2(1920f, 1080f);
                scaler.screenMatchMode =
                    CanvasScaler.ScreenMatchMode.Expand;
            }

            GameObject toastObject = new GameObject(
                "Achievement Unlock Toast",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));
            // The root ScreenSpaceOverlay canvas stays above ending black
            // covers; the child RectTransform keeps the toast itself at the
            // configured bottom-left size instead of inheriting full-screen
            // canvas dimensions.
            toastObject.transform.SetParent(
                toastCanvasObject.transform,
                false);
            toastObject.layer = gameObject.layer;

            toastRect = toastObject.GetComponent<RectTransform>();
            toastRect.anchorMin = Vector2.zero;
            toastRect.anchorMax = Vector2.zero;
            toastRect.pivot = Vector2.zero;
            toastRect.anchoredPosition = bottomLeftOffset;
            toastRect.sizeDelta = blockSize;

            toastGroup = toastObject.GetComponent<CanvasGroup>();
            toastGroup.interactable = false;
            toastGroup.blocksRaycasts = false;

            Image background = toastObject.GetComponent<Image>();
            background.sprite = blockSprite;
            background.type = Image.Type.Sliced;
            background.color = blockColor;
            background.material = blockMaterial;
            background.raycastTarget = false;

            float textLeftPadding = contentPadding.x;
            if (checkSprite != null)
            {
                GameObject checkObject = new GameObject(
                    "Unlock Check",
                    typeof(RectTransform),
                    typeof(Image));
                checkObject.transform.SetParent(
                    toastObject.transform,
                    false);
                RectTransform checkRect =
                    checkObject.GetComponent<RectTransform>();
                checkRect.anchorMin = checkRect.anchorMax =
                    new Vector2(0f, .5f);
                checkRect.pivot = new Vector2(0f, .5f);
                checkRect.anchoredPosition = new Vector2(
                    contentPadding.x,
                    checkVerticalOffset);
                checkRect.sizeDelta =
                    new Vector2(checkSize, checkSize);

                Image checkImage = checkObject.GetComponent<Image>();
                checkImage.sprite = checkSprite;
                checkImage.color = checkColor;
                checkImage.preserveAspect = true;
                checkImage.raycastTarget = false;
                textLeftPadding += checkSize + checkGap;
            }

            TMP_FontAsset resolvedFont = font != null
                ? font
                : TMP_Settings.defaultFontAsset;
            titleText = CreateText(
                "Unlock Title",
                toastObject.transform,
                titleFontSize,
                boldTitle,
                titleColor,
                resolvedFont);
            RectTransform titleRect = titleText.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(.5f, 1f);
            titleRect.anchoredPosition = new Vector2(
                (textLeftPadding - contentPadding.x) * .5f,
                -contentPadding.y);
            titleRect.sizeDelta = new Vector2(
                -(textLeftPadding + contentPadding.x),
                titleHeight);

            achievementNameText = CreateText(
                "Achievement Name",
                toastObject.transform,
                achievementNameFontSize,
                boldAchievementName,
                achievementNameColor,
                resolvedFont);
            achievementNameText.enableAutoSizing = true;
            achievementNameText.fontSizeMin =
                Mathf.Min(minimumNameFontSize, achievementNameFontSize);
            achievementNameText.fontSizeMax =
                achievementNameFontSize;
            achievementNameText.textWrappingMode =
                TextWrappingModes.Normal;
            achievementNameText.overflowMode =
                TextOverflowModes.Ellipsis;
            RectTransform nameRect =
                achievementNameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(.5f, 0f);
            nameRect.offsetMin = new Vector2(
                textLeftPadding,
                contentPadding.y);
            nameRect.offsetMax = new Vector2(
                -contentPadding.x,
                -(contentPadding.y +
                  titleHeight +
                  titleNameSpacing));

            toastObject.SetActive(false);
        }

        private static int ResolveHighestSortingLayerId()
        {
            SortingLayer[] layers = SortingLayer.layers;
            int highestId = 0;
            int highestValue = int.MinValue;
            foreach (SortingLayer layer in layers)
            {
                if (layer.value <= highestValue)
                {
                    continue;
                }

                highestValue = layer.value;
                highestId = layer.id;
            }
            return highestId;
        }

        private static TextMeshProUGUI CreateText(
            string objectName,
            Transform parent,
            float fontSize,
            bool bold,
            Color color,
            TMP_FontAsset fontAsset)
        {
            GameObject textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text =
                textObject.GetComponent<TextMeshProUGUI>();
            text.font = fontAsset;
            text.fontSize = fontSize;
            text.fontStyle = bold
                ? FontStyles.Bold
                : FontStyles.Normal;
            text.color = color;
            text.alignment = TextAlignmentOptions.Left;
            text.raycastTarget = false;
            return text;
        }

        private void StopAndClear()
        {
            sequence?.Kill(false);
            sequence = null;
            isShowing = false;
            pendingNames.Clear();
            if (toastRect != null)
            {
                toastRect.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (toastCanvasObject != null)
            {
                Destroy(toastCanvasObject);
            }
            toastCanvasObject = null;
            toastRect = null;
        }
    }
}
