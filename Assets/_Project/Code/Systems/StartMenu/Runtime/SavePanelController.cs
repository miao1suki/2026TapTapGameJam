using System;
using Project.Saving;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class SavePanelController : MonoBehaviour
    {
        private const string PlaceholderObjectName =
            "Pending Development Content";
        private const string DeleteButtonObjectName =
            "Delete Current Save";

        [SerializeField] private TMP_FontAsset uiSdfFont;
        [SerializeField] private string placeholderText =
            "待开发内容，请关注后续消息";
        [SerializeField, Min(1f)] private float placeholderFontSize =
            72f;
        [SerializeField] private Color placeholderColor =
            Color.white;
        [SerializeField] private Color outlineColor =
            Color.black;
        [SerializeField, Min(0f)] private float outlineWidth =
            .2f;
        [Header("Current run deletion")]
        [SerializeField] private Color deleteButtonColor =
            new Color(.72f, .16f, .16f, 1f);
        [SerializeField] private Color deleteButtonTextColor =
            Color.white;
        [SerializeField] private Color deleteButtonOutlineColor =
            Color.black;
        [SerializeField, Min(1f)] private float deleteButtonFontSize =
            30f;
        [SerializeField] private Vector2 deleteButtonSize =
            new Vector2(380f, 76f);
        [SerializeField, Min(0f)] private float deleteButtonGap = 28f;

        private TextMeshProUGUI placeholder;
        private RectTransform placeholderRect;
        private Button deleteButton;
        private RectTransform deleteButtonRect;
        private TextMeshProUGUI deleteButtonLabel;
        private bool allowCurrentRunDeletion;

        public static event Func<bool> DeleteCurrentSaveRequested;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetDeleteRequest()
        {
            DeleteCurrentSaveRequested = null;
        }

        public void BuildStructure()
        {
            EnsurePlaceholder();
        }

        public void Refresh()
        {
            EnsurePlaceholder();
        }

        public void ConfigureCurrentRunDeletion(bool enabled)
        {
            allowCurrentRunDeletion = enabled;
            EnsurePlaceholder();
        }

        private void Awake()
        {
            EnsurePlaceholder();
        }

        private void OnEnable()
        {
            EnsurePlaceholder();
        }

        private void EnsurePlaceholder()
        {
            HideLegacyContent();
            if (placeholder == null)
            {
                BuildPlaceholder();
            }

            if (placeholder != null)
            {
                placeholder.gameObject.SetActive(true);
            }

            EnsureDeleteButton();
            ApplyResponsiveLayout();
        }

        private void HideLegacyContent()
        {
            for (int index = transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name == "Close" ||
                    child.name == PlaceholderObjectName ||
                    child.name == DeleteButtonObjectName)
                {
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private void BuildPlaceholder()
        {
            GameObject textObject = new GameObject(
                PlaceholderObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform rect =
                (RectTransform)textObject.transform;
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(1120f, 220f);
            placeholderRect = rect;

            placeholder =
                textObject.GetComponent<TextMeshProUGUI>();
            placeholder.font = uiSdfFont;
            placeholder.text = placeholderText +
                "\n<size=" +
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        placeholderFontSize * .55f)) +
                ">已自动存档</size>";
            placeholder.richText = true;
            placeholder.fontSize = Mathf.Max(
                1f,
                placeholderFontSize);
            placeholder.enableAutoSizing = true;
            placeholder.fontSizeMin = Mathf.Min(
                24f,
                placeholder.fontSize);
            placeholder.fontSizeMax = placeholder.fontSize;
            placeholder.color = placeholderColor;
            placeholder.outlineColor = outlineColor;
            placeholder.outlineWidth = Mathf.Max(
                0f,
                outlineWidth);
            placeholder.alignment =
                TextAlignmentOptions.Center;
            placeholder.textWrappingMode =
                TextWrappingModes.Normal;
            placeholder.overflowMode =
                TextOverflowModes.Ellipsis;
            placeholder.raycastTarget = false;
            placeholder.lineSpacing = 8f;

            if (uiSdfFont == null)
            {
                Debug.LogError(
                    "[SavePanelController] 未配置 Cubic_11 SDF 字体。",
                    this);
            }
        }

        private void EnsureDeleteButton()
        {
            if (!allowCurrentRunDeletion)
            {
                if (deleteButton != null)
                    deleteButton.gameObject.SetActive(false);
                return;
            }

            if (deleteButton == null)
                BuildDeleteButton();

            bool available = RunSaveService.HasActiveRun;
            deleteButton.gameObject.SetActive(available);
            deleteButton.interactable = available;
        }

        private void BuildDeleteButton()
        {
            GameObject buttonObject = new GameObject(
                DeleteButtonObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            deleteButtonRect =
                (RectTransform)buttonObject.transform;
            deleteButtonRect.anchorMin =
                deleteButtonRect.anchorMax =
                    new Vector2(.5f, .5f);
            deleteButtonRect.pivot = new Vector2(.5f, .5f);

            Image background =
                buttonObject.GetComponent<Image>();
            GameUiArtwork artwork = GameUiArtwork.Load();
            if (artwork != null &&
                artwork.settingButton != null)
            {
                background.sprite = artwork.settingButton;
                background.type = Image.Type.Sliced;
            }

            background.color = deleteButtonColor;
            deleteButton = buttonObject.GetComponent<Button>();
            deleteButton.targetGraphic = background;
            deleteButton.navigation =
                new Navigation { mode = Navigation.Mode.None };
            ColorBlock colors = deleteButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(1f, .9f, .9f, 1f);
            colors.pressedColor =
                new Color(.82f, .68f, .68f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            deleteButton.colors = colors;

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(
                buttonObject.transform,
                false);
            RectTransform labelRect =
                (RectTransform)labelObject.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin =
                new Vector2(12f, 6f);
            labelRect.offsetMax =
                new Vector2(-12f, -6f);
            deleteButtonLabel =
                labelObject.GetComponent<TextMeshProUGUI>();
            deleteButtonLabel.font = uiSdfFont;
            deleteButtonLabel.text = "删除当前存档";
            deleteButtonLabel.fontSize =
                Mathf.Max(1f, deleteButtonFontSize);
            deleteButtonLabel.enableAutoSizing = true;
            deleteButtonLabel.fontSizeMin = 18f;
            deleteButtonLabel.fontSizeMax =
                Mathf.Max(18f, deleteButtonFontSize);
            deleteButtonLabel.color = deleteButtonTextColor;
            deleteButtonLabel.outlineColor =
                deleteButtonOutlineColor;
            deleteButtonLabel.outlineWidth = .15f;
            deleteButtonLabel.alignment =
                TextAlignmentOptions.Center;
            deleteButtonLabel.textWrappingMode =
                TextWrappingModes.NoWrap;
            deleteButtonLabel.overflowMode =
                TextOverflowModes.Ellipsis;
            deleteButtonLabel.raycastTarget = false;
            deleteButton.onClick.AddListener(
                RequestDeleteCurrentSave);
        }

        private void RequestDeleteCurrentSave()
        {
            bool accepted =
                DeleteCurrentSaveRequested?.Invoke() == true;
            if (accepted)
                deleteButton.interactable = false;
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
                ApplyResponsiveLayout();
        }

        private void ApplyResponsiveLayout()
        {
            if (placeholderRect == null)
                return;

            RectTransform root = transform as RectTransform;
            if (root == null ||
                root.rect.width <= 1f ||
                root.rect.height <= 1f)
            {
                return;
            }

            float contentWidth = Mathf.Max(
                1f,
                Mathf.Min(
                    1120f,
                    root.rect.width - 120f));
            float placeholderHeight = Mathf.Max(
                48f,
                Mathf.Min(
                    190f,
                    root.rect.height * .42f));
            float buttonHeight = Mathf.Max(
                40f,
                Mathf.Min(
                    deleteButtonSize.y,
                    root.rect.height * .2f));
            float buttonWidth = Mathf.Max(
                1f,
                Mathf.Min(
                    deleteButtonSize.x,
                    contentWidth * .62f));
            bool hasDeleteButton =
                deleteButton != null &&
                deleteButton.gameObject.activeSelf;
            float gap = hasDeleteButton
                ? Mathf.Min(
                    deleteButtonGap,
                    root.rect.height * .08f)
                : 0f;
            float totalHeight = placeholderHeight +
                (hasDeleteButton
                    ? gap + buttonHeight
                    : 0f);
            float top = totalHeight * .5f;
            placeholderRect.sizeDelta =
                new Vector2(contentWidth, placeholderHeight);
            placeholderRect.anchoredPosition =
                new Vector2(
                    0f,
                    top - placeholderHeight * .5f);

            if (deleteButtonRect == null ||
                !hasDeleteButton)
                return;

            deleteButtonRect.sizeDelta =
                new Vector2(buttonWidth, buttonHeight);
            deleteButtonRect.anchoredPosition =
                new Vector2(
                    0f,
                    top -
                    placeholderHeight -
                    gap -
                    buttonHeight * .5f);
            if (deleteButtonLabel != null)
            {
                deleteButtonLabel.fontSizeMax =
                    Mathf.Max(
                        18f,
                        Mathf.Min(
                            deleteButtonFontSize,
                            buttonHeight * .46f));
            }
        }
    }
}
