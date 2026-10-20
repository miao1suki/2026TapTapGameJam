using TMPro;
using UnityEngine;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class SavePanelController : MonoBehaviour
    {
        private const string PlaceholderObjectName =
            "Pending Development Content";

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

        private TextMeshProUGUI placeholder;

        public void BuildStructure()
        {
            EnsurePlaceholder();
        }

        public void Refresh()
        {
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
        }

        private void HideLegacyContent()
        {
            for (int index = transform.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = transform.GetChild(index);
                if (child.name == "Close" ||
                    child.name == PlaceholderObjectName)
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

            placeholder =
                textObject.GetComponent<TextMeshProUGUI>();
            placeholder.font = uiSdfFont;
            placeholder.text = placeholderText;
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
                TextWrappingModes.NoWrap;
            placeholder.overflowMode =
                TextOverflowModes.Overflow;
            placeholder.raycastTarget = false;

            if (uiSdfFont == null)
            {
                Debug.LogError(
                    "[SavePanelController] 未配置 Cubic_11 SDF 字体。",
                    this);
            }
        }
    }
}
