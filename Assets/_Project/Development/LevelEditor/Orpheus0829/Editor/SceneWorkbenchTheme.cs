using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.LevelEditor.Editor
{
    // Claude's 墨蓝工坊 palette. Tool identity stays separate from gameplay colours.
    internal static class SceneWorkbenchTheme
    {
        internal static Color Hex(string value) { ColorUtility.TryParseHtmlString(value, out var color); return color; }
        private static Color Pair(string dark, string light) => Hex(EditorGUIUtility.isProSkin ? dark : light);
        internal static Color Background => Pair("#14171C", "#F3F4F7");
        internal static Color Surface => Pair("#1D2128", "#FFFFFF");
        internal static Color Well => Pair("#0E1014", "#E6E8ED");
        internal static Color Line => Pair("#2C323C", "#CBD0D9");
        internal static Color Text => Pair("#E6E9EF", "#1B1F27");
        internal static Color Muted => Pair("#8B93A1", "#5B6472");
        internal static Color Object => Pair("#F2765F", "#D4553E");
        internal static Color Art => Pair("#A98BFF", "#7B5CE0");
        internal static Color Room => Pair("#3FC9A5", "#1E9E7E");
        internal static Color Warning => Pair("#E8B341", "#B9811A");
        internal static Color Saved => Pair("#5FB878", "#2F8F4E");

        internal static void Border(VisualElement element, Color color, float width = 1)
        {
            element.style.borderTopColor = element.style.borderRightColor = element.style.borderBottomColor = element.style.borderLeftColor = color;
            element.style.borderTopWidth = element.style.borderRightWidth = element.style.borderBottomWidth = element.style.borderLeftWidth = width;
        }

        internal static void Tool(Button button, bool selected, Color identity)
        {
            button.style.backgroundColor = selected ? Color.Lerp(Surface, identity, .18f) : Surface;
            button.style.color = selected ? Text : Muted;
            Border(button, selected ? identity : Line);
        }

        internal static void Section(Foldout foldout, Color identity)
        {
            foldout.style.marginTop = 6;
            var toggle = foldout.Q<Toggle>();
            if (toggle == null) return;
            toggle.style.borderLeftWidth = 3;
            toggle.style.borderLeftColor = identity;
            toggle.style.paddingLeft = 5;
            toggle.style.minHeight = 26;
            toggle.style.backgroundColor = Surface;
            toggle.style.color = Text;
        }
    }
}
