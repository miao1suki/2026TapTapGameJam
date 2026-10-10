using System;
using Project.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Project.StartMenu
{
    [DisallowMultipleComponent]
    public sealed class SettingsPanelController : MonoBehaviour
    {
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

        public event Action BackRequested;

        public void SetBackButtonVisible(bool value)
        {
            if (backButton != null)
            {
                backButton.gameObject.SetActive(value);
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

            soundTabButton = StartMenuUiFactory.CreateButton(
                "Sound",
                categoryList,
                "声音",
                new Vector2(190f, 48f),
                null);
            AddCategorySize(soundTabButton);
            keyBindingTabButton = StartMenuUiFactory.CreateButton(
                "Key Binding",
                categoryList,
                "按键映射",
                new Vector2(190f, 48f),
                null);
            AddCategorySize(keyBindingTabButton);
            creditsTabButton = StartMenuUiFactory.CreateButton(
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

            backButton = StartMenuUiFactory.CreateButton(
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

            BindButtons();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                RefreshSound();
                ShowPage(0);
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
                new Color(.07f, .1f, .13f, .9f));
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
                "当前只提供全局静音，后续 BGM 与 SFX 音量分类继续扩展。",
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
                new Color(.07f, .1f, .13f, .9f));
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
                new Color(.07f, .1f, .13f, .9f));
            StartMenuUiFactory.Stretch(page.rectTransform);

            Text title = StartMenuUiFactory.CreateText(
                "Title",
                page.transform,
                "制作人员",
                24,
                TextAnchor.UpperLeft,
                new Color(.92f, .98f, 1f, 1f));
            StartMenuUiFactory.SetTopLeft(
                title.rectTransform,
                new Vector2(24f, -22f),
                new Vector2(240f, 34f));

            Text body = StartMenuUiFactory.CreateText(
                "Body",
                page.transform,
                "制作人员名单待整理。\n\n当前页面用于确认栏目、排版和后续数据接入位置。",
                16,
                TextAnchor.UpperLeft,
                new Color(.76f, .86f, .92f, .96f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;
            StartMenuUiFactory.SetTopLeft(
                body.rectTransform,
                new Vector2(24f, -76f),
                new Vector2(760f, 210f));
            return page.gameObject;
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
