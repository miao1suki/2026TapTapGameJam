using System;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    public static class StartMenuUiFactory
    {
        public static Font DefaultFont =>
            StartMenuTypography.SourceFont ??
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform CreateRect(
            string name,
            Transform parent)
        {
            GameObject gameObject =
                new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        public static Image CreateImage(
            string name,
            Transform parent,
            Color color)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(
            string name,
            Transform parent,
            string value,
            int fontSize,
            TextAnchor alignment,
            Color? color = null)
        {
            RectTransform rect = CreateRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color ?? Color.white;
            text.raycastTarget = false;
            StartMenuTypography.Apply(text);
            return text;
        }

        public static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Action onClick,
            Color? backgroundColor = null)
        {
            Image image = CreateImage(
                name,
                parent,
                backgroundColor ?? new Color(.18f, .42f, .68f, .96f));
            image.rectTransform.sizeDelta = size;
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.92f, .98f, 1f, 1f);
            colors.pressedColor = new Color(.76f, .88f, .98f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = .08f;
            button.colors = colors;

            Text text = CreateText(
                "Label",
                button.transform,
                label,
                17,
                TextAnchor.MiddleCenter,
                Color.white);
            Stretch(text.rectTransform, 8f, 4f);
            if (onClick != null)
            {
                button.onClick.AddListener(() => onClick());
            }

            return button;
        }

        public static Button CreateInnerButton(
            string name,
            Transform parent,
            string label,
            Vector2 size,
            Action onClick,
            Color? backgroundColor = null)
        {
            Button button = CreateButton(
                name,
                parent,
                label,
                size,
                onClick,
                backgroundColor);
            Image image = button.targetGraphic as Image;
            Sprite sprite = GameUiArtwork.Load()?.settingButton;
            if (image != null && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Sliced;
            }

            return button;
        }

        public static void ApplyInnerArtwork(Transform root)
        {
            if (root == null)
            {
                return;
            }

            GameUiArtwork artwork = GameUiArtwork.Load();
            if (artwork == null)
            {
                return;
            }

            Button[] buttons =
                root.GetComponentsInChildren<Button>(true);
            for (int index = 0; index < buttons.Length; index++)
            {
                Button button = buttons[index];
                if (button == null || button.name == "Close")
                {
                    continue;
                }

                Image image = button.targetGraphic as Image;
                if (image == null)
                {
                    image = button.GetComponent<Image>();
                }

                if (image != null && artwork.settingButton != null)
                {
                    image.sprite = artwork.settingButton;
                    image.type = Image.Type.Sliced;
                }
            }

            Slider[] sliders =
                root.GetComponentsInChildren<Slider>(true);
            for (int index = 0; index < sliders.Length; index++)
            {
                Slider slider = sliders[index];
                if (slider == null)
                {
                    continue;
                }

                Transform backgroundTransform =
                    slider.transform.Find("Background");
                Image background = backgroundTransform != null
                    ? backgroundTransform.GetComponent<Image>()
                    : null;
                if (background != null &&
                    artwork.settingSliderBackground != null)
                {
                    background.sprite = artwork.settingSliderBackground;
                    background.type = Image.Type.Sliced;
                }

                Image fill = slider.fillRect != null
                    ? slider.fillRect.GetComponent<Image>()
                    : null;
                if (fill != null && artwork.settingSliderFill != null)
                {
                    fill.sprite = artwork.settingSliderFill;
                    fill.type = Image.Type.Sliced;
                }

                Image handle = slider.handleRect != null
                    ? slider.handleRect.GetComponent<Image>()
                    : null;
                if (handle != null && artwork.settingSliderHandle != null)
                {
                    handle.sprite = artwork.settingSliderHandle;
                    handle.preserveAspect = true;
                }
            }

            Toggle[] toggles =
                root.GetComponentsInChildren<Toggle>(true);
            for (int index = 0; index < toggles.Length; index++)
            {
                Toggle toggle = toggles[index];
                Image background = toggle != null
                    ? toggle.targetGraphic as Image
                    : null;
                if (background != null &&
                    artwork.settingToggleBox != null)
                {
                    background.sprite = artwork.settingToggleBox;
                    background.type = Image.Type.Sliced;
                }
            }
        }

        public static Toggle CreateToggle(
            string name,
            Transform parent,
            string label,
            Action<bool> onChanged)
        {
            RectTransform root = CreateRect(name, parent);
            root.sizeDelta = new Vector2(280f, 36f);
            HorizontalLayoutGroup layout =
                root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = 10f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Image background = CreateImage(
                "Box",
                root,
                new Color(.12f, .22f, .31f, 1f));
            background.raycastTarget = true;
            background.rectTransform.sizeDelta = new Vector2(28f, 28f);
            Sprite toggleSprite =
                GameUiArtwork.Load()?.settingToggleBox;
            if (toggleSprite != null)
            {
                background.sprite = toggleSprite;
                background.type = Image.Type.Sliced;
            }
            LayoutElement boxElement =
                background.gameObject.AddComponent<LayoutElement>();
            boxElement.preferredWidth = 28f;
            boxElement.preferredHeight = 28f;

            Image checkmark = CreateImage(
                "Checkmark",
                background.transform,
                new Color(.31f, .84f, .57f, 1f));
            Sprite checkmarkSprite =
                GameUiArtwork.Load()?.achievementUnlockCheck;
            if (checkmarkSprite != null)
            {
                checkmark.sprite = checkmarkSprite;
                checkmark.color = Color.white;
                checkmark.preserveAspect = true;
                checkmark.raycastTarget = false;
                Stretch(checkmark.rectTransform, 1f, 1f);
            }
            else
            {
                Stretch(checkmark.rectTransform, 7f, 7f);
            }

            Text text = CreateText(
                "Label",
                root,
                label,
                16,
                TextAnchor.MiddleLeft,
                new Color(.9f, .96f, 1f, 1f));
            LayoutElement textElement =
                text.gameObject.AddComponent<LayoutElement>();
            textElement.preferredWidth = 230f;
            textElement.preferredHeight = 30f;

            Toggle toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.interactable = true;
            toggle.isOn = false;
            toggle.onValueChanged.AddListener(value => onChanged?.Invoke(value));
            return toggle;
        }

        public static Slider CreateSlider(
            string name,
            Transform parent,
            float minValue,
            float maxValue,
            float value,
            Action<float> onChanged)
        {
            RectTransform root = CreateRect(name, parent);
            root.sizeDelta = new Vector2(460f, 32f);

            Image background = CreateImage(
                "Background",
                root,
                new Color(.11f, .18f, .24f, 1f));
            Stretch(background.rectTransform, 8f, 11f);
            GameUiArtwork artwork = GameUiArtwork.Load();
            if (artwork?.settingSliderBackground != null)
            {
                background.sprite = artwork.settingSliderBackground;
                background.type = Image.Type.Sliced;
            }

            RectTransform fillArea = CreateRect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0f, .25f);
            fillArea.anchorMax = new Vector2(1f, .75f);
            fillArea.offsetMin = new Vector2(10f, 0f);
            fillArea.offsetMax = new Vector2(-10f, 0f);

            Image fill = CreateImage(
                "Fill",
                fillArea,
                new Color(.22f, .62f, .88f, 1f));
            Stretch(fill.rectTransform);
            if (artwork?.settingSliderFill != null)
            {
                fill.sprite = artwork.settingSliderFill;
                fill.type = Image.Type.Sliced;
            }

            Image handle = CreateImage(
                "Handle",
                root,
                new Color(.92f, .98f, 1f, 1f));
            handle.rectTransform.anchorMin = new Vector2(.5f, .5f);
            handle.rectTransform.anchorMax = new Vector2(.5f, .5f);
            handle.rectTransform.pivot = new Vector2(.5f, .5f);
            handle.rectTransform.sizeDelta = new Vector2(20f, 28f);
            if (artwork?.settingSliderHandle != null)
            {
                handle.sprite = artwork.settingSliderHandle;
                handle.preserveAspect = true;
            }

            Slider slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = minValue;
            slider.maxValue = maxValue;
            slider.SetValueWithoutNotify(value);
            if (onChanged != null)
            {
                slider.onValueChanged.AddListener(
                    changed => onChanged(changed));
            }

            return slider;
        }

        public static ScrollRect CreateVerticalScroll(
            string name,
            Transform parent,
            Color backgroundColor,
            out RectTransform content)
        {
            Image viewportImage = CreateImage(
                name,
                parent,
                backgroundColor);
            RectTransform viewport = viewportImage.rectTransform;
            viewportImage.gameObject.AddComponent<RectMask2D>();

            content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = viewportImage.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;
            return scroll;
        }

        public static void Stretch(
            RectTransform rect,
            float padding = 0f,
            float verticalPadding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, verticalPadding);
            rect.offsetMax = new Vector2(-padding, -verticalPadding);
        }

        public static void SetTopLeft(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static void SetCentered(
            RectTransform rect,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
