using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    internal static class LevelEditorTypography
    {
        private static Font simplifiedChineseFont;
        private static bool resolved;

        internal static void Apply(VisualElement root)
        {
            if (!resolved)
            {
                resolved = true;
                // Prefer SC fonts rather than the Editor's locale-dependent CJK fallback.
                string[] installed = Font.GetOSInstalledFontNames();
                foreach (string candidate in new[]
                         { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Noto Sans SC" })
                {
                    if (!System.Array.Exists(installed, name => name == candidate)) continue;
                    simplifiedChineseFont = Font.CreateDynamicFontFromOSFont(candidate, 14);
                    simplifiedChineseFont.hideFlags = HideFlags.HideAndDontSave;
                    break;
                }
            }

            if (simplifiedChineseFont != null)
                root.style.unityFontDefinition = FontDefinition.FromFont(simplifiedChineseFont);
        }
    }
}
