using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("2026TapTap/Boss/Boss 血条")]
    public sealed class BossHealthBarUI : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField, Min(100f)] private float barWidth = 600f;
        [SerializeField, Min(8f)] private float barHeight = 28f;
        [SerializeField, Min(0f)] private float topOffset = 76f;
        [SerializeField, Min(1f)] private float dividerWidth = 4f;

        [Header("Boss 名字")]
        [SerializeField] private TMP_FontAsset uiSdfFont;
        [SerializeField, Min(8f)] private float nameFontSize = 34f;
        [SerializeField, Min(4f)] private float nameMinFontSize = 14f;
        [SerializeField, Min(0f)] private float nameMaxWidth = 260f;
        [SerializeField, Min(0f)] private float nameGap = 14f;
        [SerializeField] private Color nameColor = Color.white;
        [SerializeField, Min(0f)] private float nameOutlineWidth = .18f;
        [SerializeField] private Color nameOutlineColor = Color.black;

        [Header("外框")]
        [SerializeField, Min(2f)] private float frameThickness = 3f;
        [SerializeField, Min(0f)] private float frameAccentLength = 18f;
        [SerializeField] private Color frameColor =
            new Color32(82, 59, 66, 255);
        [SerializeField] private Color frameAccentColor =
            new Color32(246, 83, 50, 255);

        [Header("颜色")]
        [SerializeField] private Color healthColor =
            new Color32(178, 62, 62, 255);
        [SerializeField] private Color trailColor =
            Color.white;
        [SerializeField] private Color dividerColor =
            Color.black;

        [Header("动画")]
        [SerializeField, Min(0f)] private float initialFillDuration = .35f;
        [SerializeField, Min(0f)] private float healthLerpSpeed = 20f;
        [SerializeField, Min(0f)] private float trailLerpSpeed = 4.5f;

        private BossController controller;
        private RectTransform barRoot;
        private RectTransform nameRect;
        private TextMeshProUGUI nameText;
        private CanvasGroup hudCanvasGroup;
        private RectTransform healthFill;
        private RectTransform trailFill;
        private float fillAreaWidth;
        private float displayedHealth;
        private float displayedTrail;
        private float initialFillElapsed;
        private int segmentCount;
        private bool initialized;
        private bool initializing;
        private Tween hideTween;

        public bool IsInitialized => initialized;
        public bool IsHudVisible =>
            barRoot != null &&
            barRoot.gameObject.activeSelf;
        public int SegmentCount => segmentCount;
        public float DisplayedHealth => displayedHealth;
        public float DisplayedTrail => displayedTrail;

        private void Start()
        {
            controller = GetComponentInParent<BossController>();
            if (controller == null)
            {
                return;
            }

            controller.HealthSystemActivated -=
                HandleHealthSystemActivated;
            controller.HealthSystemActivated +=
                HandleHealthSystemActivated;
            if (controller.IsHealthSystemActive)
            {
                HandleHealthSystemActivated();
            }
        }

        private void OnDisable()
        {
            hideTween?.Kill();
            hideTween = null;
            if (controller != null)
            {
                controller.HealthSystemActivated -=
                    HandleHealthSystemActivated;
            }
        }

        private void Update()
        {
            if (!initialized ||
                controller == null ||
                healthFill == null ||
                trailFill == null)
            {
                return;
            }

            RefreshBossName();
            float maxHealth =
                Mathf.Max(.0001f, controller.MaxHealth);
            float target = controller.IsDead
                ? 0f
                : Mathf.Clamp01(
                    controller.CurrentHealth / maxHealth);
            if (initializing)
            {
                initialFillElapsed += Time.deltaTime;
                float duration =
                    Mathf.Max(.01f, initialFillDuration);
                float progress = Mathf.Clamp01(
                    initialFillElapsed / duration);
                float eased = Mathf.SmoothStep(
                    0f,
                    1f,
                    progress);
                displayedHealth = target * eased;
                displayedTrail = target * eased;
                if (progress >= 1f)
                {
                    initializing = false;
                }
            }
            else
            {
                displayedHealth = Mathf.Lerp(
                    displayedHealth,
                    target,
                    FrameRateIndependent(
                        healthLerpSpeed,
                        Time.deltaTime));
                displayedTrail = Mathf.Lerp(
                    displayedTrail,
                    target,
                    FrameRateIndependent(
                        trailLerpSpeed,
                        Time.deltaTime));
            }

            SetFill(healthFill, displayedHealth);
            SetFill(trailFill, displayedTrail);
        }

        private void HandleHealthSystemActivated()
        {
            if (controller == null)
            {
                controller = GetComponentInParent<BossController>();
            }
            if (controller == null)
            {
                return;
            }

            if (!initialized)
            {
                BuildBar();
            }

            hideTween?.Kill();
            hudCanvasGroup.alpha = 1f;
            displayedHealth = 0f;
            displayedTrail = 0f;
            initialFillElapsed = 0f;
            initializing = true;
            barRoot.gameObject.SetActive(true);
            SetFill(healthFill, 0f);
            SetFill(trailFill, 0f);
        }

        private void BuildBar()
        {
            segmentCount = Mathf.Max(
                1,
                Mathf.RoundToInt(
                    Mathf.Max(1f, controller.MaxHealth)));
            GameObject root = new GameObject(
                "Boss Health Bar",
                typeof(RectTransform));
            root.transform.SetParent(transform, false);
            barRoot = (RectTransform)root.transform;
            barRoot.anchorMin = new Vector2(.5f, 1f);
            barRoot.anchorMax = new Vector2(.5f, 1f);
            barRoot.pivot = new Vector2(.5f, 1f);
            barRoot.anchoredPosition =
                new Vector2(0f, -topOffset);
            barRoot.sizeDelta =
                new Vector2(barWidth, barHeight);
            hudCanvasGroup =
                barRoot.gameObject.AddComponent<CanvasGroup>();
            hudCanvasGroup.interactable = false;
            hudCanvasGroup.blocksRaycasts = false;
            BuildBossName(barRoot);

            float frame = Mathf.Clamp(
                frameThickness,
                2f,
                Mathf.Max(2f, Mathf.Min(barWidth, barHeight) * .25f));
            BuildFrame(barRoot, frame);

            RectTransform fillArea = CreateRect(
                "Fill Area",
                barRoot);
            fillArea.anchorMin = Vector2.zero;
            fillArea.anchorMax = Vector2.one;
            fillArea.offsetMin = new Vector2(frame, frame);
            fillArea.offsetMax = new Vector2(-frame, -frame);
            fillAreaWidth = Mathf.Max(
                1f,
                barWidth - frame * 2f);

            trailFill = CreateFill(
                "Trail Fill",
                fillArea,
                trailColor);
            healthFill = CreateFill(
                "Health Fill",
                fillArea,
                healthColor);

            for (int index = 1;
                 index < segmentCount;
                 index++)
            {
                RectTransform divider = CreateRect(
                    "Segment Divider",
                    barRoot);
                divider.anchorMin = new Vector2(0f, .5f);
                divider.anchorMax = new Vector2(0f, .5f);
                divider.pivot = new Vector2(.5f, .5f);
                divider.sizeDelta =
                    new Vector2(
                        dividerWidth,
                        Mathf.Max(1f, barHeight - frame * 2f));
                divider.anchoredPosition = new Vector2(
                    frame +
                    fillAreaWidth *
                    index /
                    segmentCount,
                    0f);
                Image image = divider.gameObject.AddComponent<Image>();
                image.type = Image.Type.Simple;
                image.color = dividerColor;
                image.raycastTarget = false;
            }

            initialized = true;
        }

        public void ShowHud()
        {
            if (controller == null)
            {
                controller =
                    GetComponentInParent<BossController>();
            }
            if (controller == null)
            {
                return;
            }

            if (!initialized)
            {
                BuildBar();
            }

            hideTween?.Kill();
            hudCanvasGroup.alpha = 1f;
            barRoot.gameObject.SetActive(true);
            RefreshBossName();
        }

        public void HideHud(
            float duration,
            bool immediate = false)
        {
            if (barRoot == null)
            {
                return;
            }

            hideTween?.Kill();
            if (immediate || duration <= 0f)
            {
                barRoot.gameObject.SetActive(false);
                if (hudCanvasGroup != null)
                {
                    hudCanvasGroup.alpha = 1f;
                }
                return;
            }

            hideTween = hudCanvasGroup
                .DOFade(0f, duration)
                .SetEase(Ease.InCubic)
                .OnComplete(() =>
                {
                    if (barRoot != null)
                    {
                        barRoot.gameObject.SetActive(false);
                    }
                    if (hudCanvasGroup != null)
                    {
                        hudCanvasGroup.alpha = 1f;
                    }
                });
        }

        private void BuildBossName(RectTransform parent)
        {
            GameObject nameObject = new GameObject(
                "Boss Name",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            nameObject.transform.SetParent(parent, false);
            nameRect = (RectTransform)nameObject.transform;
            nameRect.anchorMin = new Vector2(0f, .5f);
            nameRect.anchorMax = new Vector2(0f, .5f);
            nameRect.pivot = new Vector2(1f, .5f);
            nameRect.anchoredPosition =
                new Vector2(-nameGap, 0f);
            nameRect.sizeDelta =
                new Vector2(nameMaxWidth, barHeight);

            nameText =
                nameObject.GetComponent<TextMeshProUGUI>();
            if (uiSdfFont == null)
            {
                Debug.LogError(
                    "[BossHealthBarUI] 未配置 Cubic_11 SDF 字体，Boss 名字不会显示。",
                    this);
                nameObject.SetActive(false);
                return;
            }

            nameText.font = uiSdfFont;
            nameText.fontSize = nameFontSize;
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = Mathf.Min(
                nameMinFontSize,
                nameFontSize);
            nameText.fontSizeMax = nameFontSize;
            nameText.color = nameColor;
            nameText.outlineWidth = nameOutlineWidth;
            nameText.outlineColor = nameOutlineColor;
            nameText.alignment =
                TextAlignmentOptions.MidlineRight;
            nameText.textWrappingMode =
                TextWrappingModes.NoWrap;
            nameText.overflowMode =
                TextOverflowModes.Ellipsis;
            nameText.raycastTarget = false;
            RefreshBossName();
        }

        private void RefreshBossName()
        {
            if (nameText == null || controller == null)
            {
                return;
            }

            string nextName = controller.BossDisplayName;
            if (nameText.text != nextName)
            {
                nameText.text = nextName;
            }
        }

        private RectTransform CreateFill(
            string objectName,
            RectTransform parent,
            Color color)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 0f);
            Image image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private void BuildFrame(
            RectTransform parent,
            float thickness)
        {
            float inset = Mathf.Clamp(
                thickness - 1f,
                0f,
                thickness);
            float innerThickness =
                Mathf.Max(1f, thickness - inset);
            CreateFrameBorder(
                parent,
                "Frame Outer",
                0f,
                thickness,
                Color.black);
            CreateFrameBorder(
                parent,
                "Frame Body",
                inset,
                innerThickness,
                frameColor);

            if (frameAccentLength <= 0f)
            {
                return;
            }

            float accentLength = Mathf.Min(
                frameAccentLength,
                Mathf.Max(1f, barWidth - inset * 2f));
            RectTransform topLeft = CreateSolidRect(
                "Frame Accent Top Left",
                parent,
                frameAccentColor);
            topLeft.anchorMin = new Vector2(0f, 1f);
            topLeft.anchorMax = new Vector2(0f, 1f);
            topLeft.pivot = new Vector2(0f, 1f);
            topLeft.anchoredPosition =
                new Vector2(inset, -inset);
            topLeft.sizeDelta =
                new Vector2(accentLength, innerThickness);

            RectTransform bottomRight = CreateSolidRect(
                "Frame Accent Bottom Right",
                parent,
                frameAccentColor);
            bottomRight.anchorMin = new Vector2(1f, 0f);
            bottomRight.anchorMax = new Vector2(1f, 0f);
            bottomRight.pivot = new Vector2(1f, 0f);
            bottomRight.anchoredPosition =
                new Vector2(-inset, inset);
            bottomRight.sizeDelta =
                new Vector2(accentLength, innerThickness);
        }

        private static void CreateFrameBorder(
            Transform parent,
            string prefix,
            float inset,
            float thickness,
            Color color)
        {
            RectTransform top = CreateSolidRect(
                prefix + " Top",
                parent,
                color);
            top.anchorMin = new Vector2(0f, 1f);
            top.anchorMax = new Vector2(1f, 1f);
            top.pivot = new Vector2(.5f, 1f);
            top.anchoredPosition = new Vector2(0f, -inset);
            top.sizeDelta = new Vector2(
                inset * -2f,
                thickness);

            RectTransform bottom = CreateSolidRect(
                prefix + " Bottom",
                parent,
                color);
            bottom.anchorMin = new Vector2(0f, 0f);
            bottom.anchorMax = new Vector2(1f, 0f);
            bottom.pivot = new Vector2(.5f, 0f);
            bottom.anchoredPosition = new Vector2(0f, inset);
            bottom.sizeDelta = new Vector2(
                inset * -2f,
                thickness);

            RectTransform left = CreateSolidRect(
                prefix + " Left",
                parent,
                color);
            left.anchorMin = new Vector2(0f, 0f);
            left.anchorMax = new Vector2(0f, 1f);
            left.pivot = new Vector2(0f, .5f);
            left.anchoredPosition = new Vector2(inset, 0f);
            left.sizeDelta = new Vector2(
                thickness,
                inset * -2f);

            RectTransform right = CreateSolidRect(
                prefix + " Right",
                parent,
                color);
            right.anchorMin = new Vector2(1f, 0f);
            right.anchorMax = new Vector2(1f, 1f);
            right.pivot = new Vector2(1f, .5f);
            right.anchoredPosition = new Vector2(-inset, 0f);
            right.sizeDelta = new Vector2(
                thickness,
                inset * -2f);
        }

        private static RectTransform CreateSolidRect(
            string objectName,
            Transform parent,
            Color color)
        {
            RectTransform rect = CreateRect(objectName, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private static RectTransform CreateRect(
            string objectName,
            Transform parent)
        {
            GameObject go = new GameObject(
                objectName,
                typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private void SetFill(
            RectTransform target,
            float normalized)
        {
            if (target == null)
            {
                return;
            }

            target.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                fillAreaWidth *
                Mathf.Clamp01(normalized));
        }

        private static float FrameRateIndependent(
            float speed,
            float deltaTime)
        {
            return speed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-speed * deltaTime);
        }
    }
}
