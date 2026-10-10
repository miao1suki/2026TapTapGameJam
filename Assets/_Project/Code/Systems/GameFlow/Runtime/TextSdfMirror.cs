using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class TextSdfMirror : MonoBehaviour
    {
        [SerializeField] private Text source;
        private TextMeshProUGUI target;

        public static TextSdfMirror Attach(
            Text sourceText,
            TMP_FontAsset font,
            Material material)
        {
            if (sourceText == null || font == null)
            {
                return null;
            }

            GameObject targetObject = new GameObject(
                "SDF Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(TextSdfMirror));
            RectTransform targetRect =
                targetObject.GetComponent<RectTransform>();
            targetRect.SetParent(sourceText.transform, false);
            targetRect.anchorMin = Vector2.zero;
            targetRect.anchorMax = Vector2.one;
            targetRect.offsetMin = Vector2.zero;
            targetRect.offsetMax = Vector2.zero;

            TextSdfMirror mirror =
                targetObject.GetComponent<TextSdfMirror>();
            mirror.Bind(sourceText, font, material);
            return mirror;
        }

        public void Bind(
            Text sourceText,
            TMP_FontAsset font,
            Material material)
        {
            source = sourceText;
            target = GetComponent<TextMeshProUGUI>();
            if (target == null)
            {
                target = gameObject.AddComponent<TextMeshProUGUI>();
            }

            target.font = font;
            if (material != null)
            {
                target.fontSharedMaterial = material;
            }

            target.raycastTarget = false;
            Outline legacyOutline = source.GetComponent<Outline>();
            if (legacyOutline != null)
            {
                Destroy(legacyOutline);
            }

            CopyText();
        }

        private void LateUpdate()
        {
            CopyText();
        }

        private void CopyText()
        {
            if (source == null || target == null)
            {
                return;
            }

            if (source.canvasRenderer != null)
            {
                source.canvasRenderer.SetAlpha(0f);
            }

            target.text = source.text;
            target.color = source.color;
            target.fontSize = Mathf.Max(1f, source.fontSize);
            target.fontStyle = ToTmpStyle(source.fontStyle);
            target.alignment = ToTmpAlignment(source.alignment);
            target.textWrappingMode =
                source.horizontalOverflow == HorizontalWrapMode.Wrap
                    ? TextWrappingModes.Normal
                    : TextWrappingModes.NoWrap;
            target.overflowMode = TextOverflowModes.Overflow;
            target.enableAutoSizing = source.resizeTextForBestFit;
            if (source.resizeTextForBestFit)
            {
                target.fontSizeMin = source.resizeTextMinSize;
                target.fontSizeMax = source.resizeTextMaxSize;
            }
        }

        private static FontStyles ToTmpStyle(FontStyle style)
        {
            switch (style)
            {
                case FontStyle.Bold:
                    return FontStyles.Bold;
                case FontStyle.Italic:
                    return FontStyles.Italic;
                case FontStyle.BoldAndItalic:
                    return FontStyles.Bold | FontStyles.Italic;
                default:
                    return FontStyles.Normal;
            }
        }

        private static TextAlignmentOptions ToTmpAlignment(
            TextAnchor anchor)
        {
            switch (anchor)
            {
                case TextAnchor.UpperLeft:
                    return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter:
                    return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight:
                    return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft:
                    return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight:
                    return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft:
                    return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter:
                    return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight:
                    return TextAlignmentOptions.BottomRight;
                default:
                    return TextAlignmentOptions.Center;
            }
        }
    }
}
