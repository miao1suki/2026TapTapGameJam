using System;
using Project.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class SettingsPanelController : MonoBehaviour
    {
        private const string CreditsText =
            "队长-程序/TA    白喵喵\n" +
            "副队长-主策划    溺水的咸鱼君\n" +
            "策划    R\n" +
            "策划    <段>\n" +
            "程序    臾日不再\n" +
            "美术    艾尔四次方\n" +
            "美术    阿良\n" +
            "TA    Irotin\n" +
            "音乐    BK2049";

        [SerializeField] private Button soundTabButton;
        [SerializeField] private Button keyBindingTabButton;
        [SerializeField] private Button creditsTabButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject soundPage;
        [SerializeField] private GameObject keyBindingPage;
        [SerializeField] private GameObject creditsPage;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider effectsVolumeSlider;
        [SerializeField] private Text masterVolumeText;
        [SerializeField] private Text musicVolumeText;
        [SerializeField] private Text effectsVolumeText;
        [SerializeField] private Text statusText;
        [SerializeField] private RectTransform categoryList;
        [SerializeField] private RectTransform contentRoot;
        private bool buttonsBound;
        private bool openLayoutEnabled;

        public event Action BackRequested;
        public static event Action<TMPro.TMP_FontAsset> TouchLayoutRequested;

        public void SetBackButtonVisible(bool value)
        {
            if (backButton != null)
            {
                backButton.gameObject.SetActive(value);
            }
        }

        public void SetOpenLayoutEnabled(bool value)
        {
            openLayoutEnabled = value;
            ApplyOpenLayout();
        }

        public bool TryTriggerBackButton()
        {
            KeyBindingPanelController keyBinding =
                GetComponentInChildren<KeyBindingPanelController>(true);
            if (keyBinding != null && keyBinding.IsRebinding)
            {
                return false;
            }

            if (backButton == null || !backButton.interactable)
            {
                return false;
            }

            backButton.onClick.Invoke();
            return true;
        }

        public void FocusBackButton()
        {
            if (backButton != null &&
                backButton.gameObject.activeInHierarchy &&
                EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(
                    backButton.gameObject);
            }
        }

        public void BuildStructure()
        {
            if (soundPage != null)
            {
                return;
            }

            Image root = gameObject.GetComponent<Image>();
            if (root == null)
            {
                root = gameObject.AddComponent<Image>();
            }

            root.color = new Color(.04f, .06f, .08f, .95f);

            Text title = StartMenuUiFactory.CreateText(
                "Title",
                transform,
                "设置",
                31,
                TextAnchor.MiddleLeft,
                new Color(.92f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(52f, -38f),
                new Vector2(300f, 48f));

            categoryList = StartMenuUiFactory.CreateRect(
                "Category List",
                transform);
            VerticalLayoutGroup categories =
                categoryList.gameObject.AddComponent<VerticalLayoutGroup>();
            categories.spacing = 8f;
            categories.childAlignment = TextAnchor.UpperLeft;
            categories.childControlWidth = true;
            categories.childControlHeight = true;
            categories.childForceExpandWidth = true;
            categories.childForceExpandHeight = false;
            StartMenuUiFactory.SetTopLeft(
                categoryList,
                new Vector2(52f, -118f),
                new Vector2(190f, 390f));

            soundTabButton = StartMenuUiFactory.CreateInnerButton(
                "Sound",
                categoryList,
                "声音",
                new Vector2(190f, 48f),
                null);
            AddCategorySize(soundTabButton);
            keyBindingTabButton = StartMenuUiFactory.CreateInnerButton(
                "Key Binding",
                categoryList,
                "按键映射",
                new Vector2(190f, 48f),
                null);
            AddCategorySize(keyBindingTabButton);
            creditsTabButton = StartMenuUiFactory.CreateInnerButton(
                "Credits",
                categoryList,
                "制作人员",
                new Vector2(190f, 48f),
                null);
            AddCategorySize(creditsTabButton);

            contentRoot = StartMenuUiFactory.CreateRect(
                "Content",
                transform);
            StartMenuUiFactory.SetTopLeft(
                contentRoot,
                new Vector2(276f, -118f),
                new Vector2(930f, 470f));

            soundPage = BuildSoundPage(contentRoot);
            keyBindingPage = BuildKeyBindingPage(contentRoot);
            creditsPage = BuildCreditsPage(contentRoot);

            statusText = StartMenuUiFactory.CreateText(
                "Status",
                transform,
                "当前为原型设置页，正式视觉后续调整。",
                13,
                TextAnchor.MiddleLeft,
                new Color(.62f, .74f, .82f, 1f));
            StartMenuUiFactory.SetTopLeft(
                statusText.rectTransform,
                new Vector2(276f, -600f),
                new Vector2(930f, 24f));

            backButton = StartMenuUiFactory.CreateInnerButton(
                "Back",
                transform,
                "返回",
                new Vector2(190f, 44f),
                null,
                new Color(.22f, .29f, .37f, .96f));
            StartMenuUiFactory.SetTopLeft(
                backButton.GetComponent<RectTransform>(),
                new Vector2(52f, -606f),
                new Vector2(190f, 44f));

            ShowPage(0);
        }

        private void Awake()
        {
            if (soundPage == null)
            {
                BuildStructure();
            }

            StyleCreditsPage();
            EnsurePrivacyButton();
            EnsureTouchLayoutButton();
            BindButtons();
            StyleBackButtonAsClose();
            ApplyTransparentPageBackgrounds();
            HideSmallHints();
            StartMenuUiFactory.ApplyInnerArtwork(transform);
            ApplyOpenLayout();
        }

        private void EnsureTouchLayoutButton()
        {
            if (categoryList == null ||
                Project.InputAbstraction.GameInput.PlatformMode != Project.InputAbstraction.InputPlatformMode.Mobile) return;
            Button layout = categoryList.Find("Touch Layout")?.GetComponent<Button>();
            if (layout == null)
            {
                layout = StartMenuUiFactory.CreateInnerButton("Touch Layout", categoryList, "触控布局",
                    new Vector2(190f, 48f), null);
                AddCategorySize(layout);
            }
            // Runtime listeners are not retained by a prefab or a Play-mode script reload.
            // A persistent menu can already have the view, but still need its action rebound.
            layout.onClick.RemoveListener(OpenTouchLayout);
            layout.onClick.AddListener(OpenTouchLayout);
        }

        private void OpenTouchLayout()
        {
            var font = ResolveFont();
            if (font != null) TouchLayoutRequested?.Invoke(font);
            else Debug.LogWarning("触控布局缺少项目字体配置。", this);
        }

        private TMPro.TMP_FontAsset ResolveFont()
        {
            var font = StartMenuTypography.SdfFont;
            if (font == null)
                foreach (var text in GetComponentsInChildren<TMPro.TMP_Text>(true))
                    if (text.font != null && text.font.name == "Cubic_11 SDF") { font = text.font; break; }
            return font;
        }

        private void OpenPrivacyPolicy() => Project.Privacy.AndroidPrivacyPolicy.ShowPolicy(ResolveFont());

        private void EnsurePrivacyButton()
        {
            if (categoryList == null || categoryList.Find("Privacy Policy") != null) return;
            Button privacy = StartMenuUiFactory.CreateInnerButton("Privacy Policy", categoryList, "隐私政策",
                new Vector2(190f, 48f), OpenPrivacyPolicy);
            AddCategorySize(privacy);
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsureTouchLayoutButton();
                var label = keyBindingTabButton != null ? keyBindingTabButton.GetComponentInChildren<Text>(true) : null;
                if (label != null) label.text = Project.InputAbstraction.GameInput.PlatformMode ==
                    Project.InputAbstraction.InputPlatformMode.Mobile ? "手柄按键" : "按键映射";
                RefreshSound();
                ShowPage(0);
                StyleBackButtonAsClose();
                ApplyTransparentPageBackgrounds();
                HideSmallHints();
                StartMenuUiFactory.ApplyInnerArtwork(transform);
                ApplyOpenLayout();
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (openLayoutEnabled)
            {
                ApplyOpenLayout();
            }
        }

        private void ApplyOpenLayout()
        {
            if (!openLayoutEnabled ||
                categoryList == null ||
                contentRoot == null)
            {
                return;
            }

            RectTransform root = transform as RectTransform;
            if (root == null ||
                root.rect.width <= 1f ||
                root.rect.height <= 1f)
            {
                return;
            }

            float widthScale = root.rect.width / 1360f;
            float heightScale = root.rect.height / 720f;
            float scale = Mathf.Clamp(
                Mathf.Min(widthScale, heightScale) * 1.15f,
                .85f,
                1.65f);
            float left = Mathf.Max(52f, root.rect.width * .04f);

            categoryList.anchorMin = new Vector2(0f, 1f);
            categoryList.anchorMax = new Vector2(0f, 1f);
            categoryList.pivot = new Vector2(0f, 1f);
            categoryList.anchoredPosition =
                new Vector2(left, -118f);
            categoryList.localScale = Vector3.one * scale;

            RectTransform title =
                transform.Find("Title") as RectTransform;
            if (title != null)
            {
                title.anchorMin = new Vector2(0f, 1f);
                title.anchorMax = new Vector2(0f, 1f);
                title.pivot = new Vector2(0f, 1f);
                title.anchoredPosition =
                    new Vector2(left, -38f);
                title.localScale = Vector3.one * scale;
            }

            float categoryRight =
                left + categoryList.rect.width * scale;
            float contentX =
                categoryRight + 36f * scale;
            float availableWidth = Mathf.Max(
                320f,
                root.rect.width - contentX -
                Mathf.Max(32f, root.rect.width * .03f));
            float contentWidth = Mathf.Max(
                1f,
                contentRoot.rect.width);
            float contentScale = Mathf.Min(
                scale,
                availableWidth / contentWidth);

            contentRoot.anchorMin = new Vector2(0f, 1f);
            contentRoot.anchorMax = new Vector2(0f, 1f);
            contentRoot.pivot = new Vector2(0f, 1f);
            contentRoot.anchoredPosition =
                new Vector2(contentX, -118f);
            contentRoot.localScale =
                Vector3.one * Mathf.Max(.75f, contentScale);
        }

        private void ApplyTransparentPageBackgrounds()
        {
            MakePageTransparent(soundPage);
            MakePageTransparent(keyBindingPage);
            MakePageTransparent(creditsPage);
        }

        private static void MakePageTransparent(GameObject page)
        {
            if (page == null)
            {
                return;
            }

            Image image = page.GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.color = Color.clear;
            image.raycastTarget = false;
        }

        private void StyleBackButtonAsClose()
        {
            if (backButton == null)
            {
                return;
            }

            backButton.gameObject.name = "Close";
            RectTransform rect =
                backButton.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-26f, -26f);
            rect.sizeDelta = new Vector2(48f, 48f);
            rect.localScale = Vector3.one;
            Project.InputAbstraction.MobileOverlaySafeArea.ProtectCloseButton(backButton);

            Image image = backButton.GetComponent<Image>();
            Sprite closeSprite =
                GameUiArtwork.Load()?.settingCloseButton;
            if (image != null)
            {
                if (closeSprite != null)
                {
                    image.sprite = closeSprite;
                    image.type = Image.Type.Simple;
                }
                image.color = Color.white;
                image.preserveAspect = true;
            }

            ColorBlock colors = backButton.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor =
                new Color(.94f, .96f, .96f, 1f);
            colors.pressedColor =
                new Color(.72f, .72f, .72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            backButton.colors = colors;

            Text[] labels =
                backButton.GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                labels[index].gameObject.SetActive(false);
            }
        }

        private void HideSmallHints()
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }

            Text[] labels = GetComponentsInChildren<Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                Text label = labels[index];
                if (label != null &&
                    (label.name == "Hint" || label.name == "Status"))
                {
                    label.gameObject.SetActive(false);
                }
            }
        }

        private void BindButtons()
        {
            if (buttonsBound)
            {
                return;
            }

            soundTabButton?.onClick.AddListener(() => ShowPage(0));
            keyBindingTabButton?.onClick.AddListener(() => ShowPage(1));
            creditsTabButton?.onClick.AddListener(() => ShowPage(2));
            backButton?.onClick.AddListener(() => BackRequested?.Invoke());
            masterVolumeSlider?.onValueChanged.AddListener(
                SetMasterVolume);
            musicVolumeSlider?.onValueChanged.AddListener(
                SetMusicVolume);
            effectsVolumeSlider?.onValueChanged.AddListener(
                SetEffectsVolume);
            buttonsBound = true;
        }

        private void RefreshSound()
        {
            if (masterVolumeSlider == null)
            {
                return;
            }

            AudioManager manager = AudioManager.EnsureInstance();
            masterVolumeSlider.SetValueWithoutNotify(
                manager.MasterVolume);
            musicVolumeSlider.SetValueWithoutNotify(
                manager.MusicVolume);
            effectsVolumeSlider.SetValueWithoutNotify(
                manager.EffectsVolume);
            UpdateVolumeTexts(manager);
            statusText.text =
                "实际音乐 = 总音量 × 背景音乐；实际音效 = 总音量 × 音效。";
        }

        private void SetMasterVolume(float value)
        {
            AudioManager manager = AudioManager.EnsureInstance();
            manager.SetMasterVolume(value);
            UpdateVolumeTexts(manager);
        }

        private void SetMusicVolume(float value)
        {
            AudioManager manager = AudioManager.EnsureInstance();
            manager.SetMusicVolume(value);
            UpdateVolumeTexts(manager);
        }

        private void SetEffectsVolume(float value)
        {
            AudioManager manager = AudioManager.EnsureInstance();
            manager.SetEffectsVolume(value);
            UpdateVolumeTexts(manager);
        }

        private void UpdateVolumeTexts(AudioManager manager)
        {
            if (manager == null)
            {
                return;
            }

            masterVolumeText.text = VolumePercent(manager.MasterVolume);
            musicVolumeText.text = VolumePercent(manager.MusicVolume);
            effectsVolumeText.text = VolumePercent(manager.EffectsVolume);
        }

        public void ShowCredits() => ShowPage(2);

        private void ShowPage(int index)
        {
            if (soundPage == null || keyBindingPage == null || creditsPage == null)
            {
                return;
            }

            soundPage.SetActive(index == 0);
            keyBindingPage.SetActive(index == 1);
            creditsPage.SetActive(index == 2);
            SetTabColor(soundTabButton, index == 0);
            SetTabColor(keyBindingTabButton, index == 1);
            SetTabColor(creditsTabButton, index == 2);
        }

        private GameObject BuildSoundPage(Transform parent)
        {
            Image page = StartMenuUiFactory.CreateImage(
                "Sound Page",
                parent,
                Color.clear);
            StartMenuUiFactory.Stretch(page.rectTransform);

            Text heading = StartMenuUiFactory.CreateText(
                "Heading",
                page.transform,
                "声音",
                22,
                TextAnchor.MiddleLeft,
                new Color(.9f, .97f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                heading.rectTransform,
                new Vector2(22f, -20f),
                new Vector2(200f, 30f));

            Text hint = StartMenuUiFactory.CreateText(
                "Hint",
                page.transform,
                "总音量同时影响音乐与音效；背景音乐和音效分开控制，修改立即生效。",
                14,
                TextAnchor.MiddleLeft,
                new Color(.64f, .75f, .82f, 1f));
            StartMenuUiFactory.SetTopLeft(
                hint.rectTransform,
                new Vector2(24f, -62f),
                new Vector2(720f, 28f));

            CreateVolumeRow(
                page.transform,
                "总音量",
                -112f,
                out masterVolumeSlider,
                out masterVolumeText);
            CreateVolumeRow(
                page.transform,
                "背景音乐",
                -176f,
                out musicVolumeSlider,
                out musicVolumeText);
            CreateVolumeRow(
                page.transform,
                "音效",
                -240f,
                out effectsVolumeSlider,
                out effectsVolumeText);
            return page.gameObject;
        }

        private static void CreateVolumeRow(
            Transform parent,
            string label,
            float y,
            out Slider slider,
            out Text valueText)
        {
            Text name = StartMenuUiFactory.CreateText(
                "Label",
                parent,
                label,
                16,
                TextAnchor.MiddleLeft,
                new Color(.88f, .95f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                name.rectTransform,
                new Vector2(24f, y),
                new Vector2(140f, 30f));

            slider = StartMenuUiFactory.CreateSlider(
                "Slider",
                parent,
                0f,
                1f,
                1f,
                null);
            StartMenuUiFactory.SetTopLeft(
                slider.GetComponent<RectTransform>(),
                new Vector2(176f, y),
                new Vector2(520f, 30f));

            valueText = StartMenuUiFactory.CreateText(
                "Value",
                parent,
                "100%",
                15,
                TextAnchor.MiddleRight,
                new Color(.7f, .84f, .92f, 1f));
            StartMenuUiFactory.SetTopLeft(
                valueText.rectTransform,
                new Vector2(706f, y),
                new Vector2(80f, 30f));
        }

        private static string VolumePercent(float value)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private GameObject BuildKeyBindingPage(Transform parent)
        {
            Image page = StartMenuUiFactory.CreateImage(
                "Key Binding Page",
                parent,
                Color.clear);
            StartMenuUiFactory.Stretch(page.rectTransform);
            KeyBindingPanelController panel =
                page.gameObject.AddComponent<KeyBindingPanelController>();
            panel.BuildStructure();
            return page.gameObject;
        }

        private GameObject BuildCreditsPage(Transform parent)
        {
            Image page = StartMenuUiFactory.CreateImage(
                "Credits Page",
                parent,
                Color.clear);
            StartMenuUiFactory.Stretch(page.rectTransform);

            Text title = StartMenuUiFactory.CreateText(
                "Title",
                page.transform,
                "制作人员",
                30,
                TextAnchor.UpperLeft,
                Color.white);
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(24f, -22f),
                new Vector2(240f, 34f));

            Text body = StartMenuUiFactory.CreateText(
                "Body",
                page.transform,
                CreditsText,
                26,
                TextAnchor.UpperLeft,
                Color.white);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            StartMenuUiFactory.SetTopLeft(
                body.rectTransform,
                new Vector2(24f, -76f),
                new Vector2(820f, 340f));
            ApplyCreditsOutline(title);
            ApplyCreditsOutline(body);
            return page.gameObject;
        }

        private void StyleCreditsPage()
        {
            if (creditsPage == null)
            {
                return;
            }

            Text title = creditsPage.transform
                .Find("Title")
                ?.GetComponent<Text>();
            Text body = creditsPage.transform
                .Find("Body")
                ?.GetComponent<Text>();
            if (title != null)
            {
                title.fontSize = 30;
                title.color = Color.white;
                ApplyCreditsOutline(title);
            }

            if (body != null)
            {
                body.fontSize = 26;
                body.color = Color.white;
                body.text = CreditsText;
                body.rectTransform.sizeDelta = new Vector2(820f, 340f);
                ApplyCreditsOutline(body);
            }
        }

        private static void ApplyCreditsOutline(Text text)
        {
            if (text == null)
            {
                return;
            }

            Outline outline = text.GetComponent<Outline>();
            if (outline == null)
            {
                outline = text.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            outline.useGraphicAlpha = true;
        }

        private static void SetTabColor(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            Image image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = selected
                    ? new Color(.22f, .55f, .82f, .98f)
                    : new Color(.16f, .22f, .29f, .94f);
            }
        }

        private static void AddCategorySize(Button button)
        {
            LayoutElement element =
                button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = 190f;
            element.preferredHeight = 48f;
        }
    }
}
