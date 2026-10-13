using Project.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    public static class StartMenuTypography
    {
        private static TMP_FontAsset font;
        private static Font sourceFont;

        public static Font SourceFont => sourceFont;

        public static void Configure(
            TMP_FontAsset value,
            Font legacyValue)
        {
            font = value;
            sourceFont = legacyValue;
        }

        public static TextSdfMirror Apply(Text source)
        {
            if (source == null)
            {
                return null;
            }

            if (sourceFont != null)
            {
                source.font = sourceFont;
            }

            if (font == null)
            {
                return null;
            }

            TextSdfMirror existing =
                source.GetComponentInChildren<TextSdfMirror>(true);
            if (existing != null)
            {
                return existing;
            }

            return TextSdfMirror.Attach(
                source,
                font,
                font.material);
        }

        public static void ApplyTo(Transform root)
        {
            if (root == null || font == null)
            {
                return;
            }

            Text[] labels = root.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                Apply(labels[index]);
            }
        }
    }
}
