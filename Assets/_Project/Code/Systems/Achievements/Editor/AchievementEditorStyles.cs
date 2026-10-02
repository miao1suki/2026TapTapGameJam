using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Project.Achievements.Editor
{
    internal static class AchievementEditorStyles
    {
        private sealed class ButtonVisualState
        {
            internal Color BaseColor;
            internal Color HoverColor;
            internal Color PressedColor;
        }

        internal static void ApplyAll(
            VisualElement root,
            params string[] primaryTexts)
        {
            if (root == null)
            {
                return;
            }

            foreach (VisualElement child in root.Children())
            {
                if (child is Button button)
                {
                    Apply(
                        button,
                        Array.IndexOf(
                            primaryTexts,
                            button.text) >= 0);
                }

                if (child.childCount > 0)
                {
                    ApplyAll(
                        child,
                        primaryTexts);
                }
            }
        }

        internal static void Apply(
            Button button,
            bool primary)
        {
            bool proSkin = EditorGUIUtility.isProSkin;
            ButtonVisualState state =
                button.userData as ButtonVisualState;
            if (state == null)
            {
                state = new ButtonVisualState();
                button.userData = state;
                button.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (button.enabledSelf)
                    {
                        button.style.backgroundColor =
                            state.HoverColor;
                    }
                });
                button.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    button.style.backgroundColor =
                        state.BaseColor;
                });
                button.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (button.enabledSelf)
                    {
                        button.style.backgroundColor =
                            state.PressedColor;
                    }
                });
                button.RegisterCallback<PointerUpEvent>(_ =>
                {
                    if (button.enabledSelf)
                    {
                        button.style.backgroundColor =
                            state.HoverColor;
                    }
                });
            }

            button.style.height = primary ? 30f : 25f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 2f;
            button.style.marginTop = 2f;
            button.style.marginBottom = 2f;
            button.style.borderTopLeftRadius = 5f;
            button.style.borderTopRightRadius = 5f;
            button.style.borderBottomLeftRadius = 5f;
            button.style.borderBottomRightRadius = 5f;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.unityFontStyleAndWeight =
                primary
                    ? FontStyle.Bold
                    : FontStyle.Normal;

            if (!primary)
            {
                state.BaseColor =
                    proSkin
                        ? new Color(0.12f, 0.17f, 0.23f, 1f)
                        : new Color(0.84f, 0.88f, 0.92f, 1f);
                state.HoverColor =
                    proSkin
                        ? new Color(0.17f, 0.24f, 0.32f, 1f)
                        : new Color(0.9f, 0.93f, 0.96f, 1f);
                state.PressedColor =
                    proSkin
                        ? new Color(0.24f, 0.34f, 0.45f, 1f)
                        : new Color(0.72f, 0.8f, 0.88f, 1f);
                button.style.backgroundColor =
                    state.BaseColor;
                button.style.borderLeftColor =
                    proSkin
                        ? new Color(0.22f, 0.36f, 0.5f, 1f)
                        : new Color(0.58f, 0.68f, 0.78f, 1f);
                button.style.borderRightColor =
                    button.style.borderLeftColor;
                button.style.borderTopColor =
                    button.style.borderLeftColor;
                button.style.borderBottomColor =
                    button.style.borderLeftColor;
                button.style.color =
                    proSkin
                        ? new Color(0.86f, 0.92f, 1f, 1f)
                        : new Color(0.08f, 0.14f, 0.2f, 1f);
                return;
            }

            state.BaseColor =
                new Color(0.16f, 0.5f, 0.86f, 1f);
            state.HoverColor =
                new Color(0.2f, 0.58f, 0.95f, 1f);
            state.PressedColor =
                new Color(0.28f, 0.68f, 1f, 1f);
            button.style.backgroundColor =
                state.BaseColor;
            button.style.borderLeftColor =
                new Color(0.25f, 0.62f, 0.98f, 1f);
            button.style.borderRightColor =
                button.style.borderLeftColor;
            button.style.borderTopColor =
                button.style.borderLeftColor;
            button.style.borderBottomColor =
                button.style.borderLeftColor;
            button.style.color = Color.white;
        }
    }
}
