using System.Collections.Generic;
using Project.Achievements;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class AchievementCollectionPresenter : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField, Min(72f)] private float rowHeight = 192f;
        [SerializeField, Min(0f)] private float rowSpacing = 10f;
        [SerializeField, Min(0f)] private float columnSpacing = 12f;
        [Tooltip("新增成就优先从左到右填满当前行")]
        [SerializeField] private bool fillRowsFirst = true;
        [SerializeField, Min(0f)] private float horizontalPadding = 36f;
        [SerializeField, Min(0f)] private float bottomPadding = 32f;
        [SerializeField, Min(0f)] private float topPadding = 88f;

        [Header("Visual Assets")]
        [Tooltip("每个成就块的材质")]
        [SerializeField] private Material rowMaterial;
        [Tooltip("成就达成后贴在块右侧的图片")]
        [SerializeField] private Sprite completedSprite;
        [Tooltip("成就达成后覆盖整块的灰色遮罩")]
        [SerializeField] private Color completedOverlayColor =
            new Color(.32f, .34f, .36f, .58f);

        [Header("Colors")]
        [SerializeField] private Color rowColor = Color.white;
        [SerializeField] private Color titleColor = Color.black;
        [SerializeField] private Color descriptionColor =
            new Color(.55f, .58f, .6f, 1f);
        [SerializeField] private Color requirementColor = Color.black;
        [SerializeField] private Color completionColor =
            new Color(.65f, 1f, .78f, 1f);

        [Header("Typography")]
        [SerializeField, Min(1f)] private int titleFontSize = 36;
        [SerializeField, Min(1f)] private int descriptionFontSize = 16;

        private sealed class RowView
        {
            public AchievementSO Achievement;
            public GameObject Root;
            public GameObject Overlay;
            public GameObject Completion;
            public Toggle Toggle;
            public Text Requirement;
        }

        private readonly List<RowView> rows =
            new List<RowView>();
        private readonly Dictionary<int, RowView> rowsById =
            new Dictionary<int, RowView>();
        private AchievementManager achievementManager;
        private ScrollRect scrollRect;
        private RectTransform content;
        private GridLayoutGroup gridLayout;
        private Text emptyLabel;
        private int lastRefreshFrame = -1;

        private void OnEnable()
        {
            achievementManager = AchievementManager.Instance;
            achievementManager.AchievementUnlocked -=
                HandleAchievementUnlocked;
            achievementManager.AchievementUnlocked +=
                HandleAchievementUnlocked;
            achievementManager.AchievementProgressChanged -=
                HandleAchievementProgressChanged;
            achievementManager.AchievementProgressChanged +=
                HandleAchievementProgressChanged;
            Refresh();
        }

        private void OnDisable()
        {
            if (achievementManager != null)
            {
                achievementManager.AchievementUnlocked -=
                    HandleAchievementUnlocked;
                achievementManager.AchievementProgressChanged -=
                    HandleAchievementProgressChanged;
            }
        }

        public void Refresh()
        {
            if (lastRefreshFrame == Time.frameCount)
            {
                return;
            }

            lastRefreshFrame = Time.frameCount;
            EnsureLayout();
            ClearRows();
            IReadOnlyList<AchievementSO> achievements =
                achievementManager != null
                    ? achievementManager.GetAllAchievements()
                    : null;
            if (achievements == null || achievements.Count == 0)
            {
                ShowEmpty("暂无成就数据");
                RefreshTypography();
                return;
            }

            int created = 0;
            for (int index = 0; index < achievements.Count; index++)
            {
                AchievementSO achievement = achievements[index];
                if (achievement == null)
                {
                    continue;
                }

                bool unlocked = achievementManager.IsUnlocked(
                    achievement.AchievementId);
                rows.Add(CreateRow(achievement, unlocked));
                created++;
            }

            if (created == 0)
            {
                ShowEmpty("暂无成就数据");
            }
            else if (emptyLabel != null)
            {
                emptyLabel.gameObject.SetActive(false);
            }

            UpdateGridSize();
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            RefreshTypography();
        }

        private void LateUpdate()
        {
            UpdateGridSize();
        }

        private void EnsureLayout()
        {
            if (scrollRect == null)
            {
                Transform oldPlaceholder = transform.Find("Placeholder");
                if (oldPlaceholder != null)
                {
                    oldPlaceholder.gameObject.SetActive(false);
                }

                GameObject scrollObject = CreateRectObject(
                    "Achievement List",
                    transform);
                RectTransform scrollTransform =
                    scrollObject.GetComponent<RectTransform>();
                scrollTransform.anchorMin = Vector2.zero;
                scrollTransform.anchorMax = Vector2.one;
                scrollTransform.offsetMin = new Vector2(
                    horizontalPadding,
                    bottomPadding);
                scrollTransform.offsetMax = new Vector2(
                    -horizontalPadding,
                    -topPadding);

                ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
                scroll.horizontal = false;
                scroll.vertical = true;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 28f;

                GameObject viewportObject = CreateRectObject(
                    "Viewport",
                    scrollObject.transform);
                RectTransform viewport =
                    viewportObject.GetComponent<RectTransform>();
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = Vector2.zero;
                viewport.offsetMax = Vector2.zero;
                viewportObject.AddComponent<RectMask2D>();

                GameObject contentObject = CreateRectObject(
                    "Content",
                    viewportObject.transform);
                content = contentObject.GetComponent<RectTransform>();
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = new Vector2(1f, 1f);
                content.pivot = new Vector2(.5f, 1f);
                content.anchoredPosition = Vector2.zero;
                content.sizeDelta = Vector2.zero;

                gridLayout =
                    contentObject.AddComponent<GridLayoutGroup>();
                gridLayout.constraint =
                    GridLayoutGroup.Constraint.FixedColumnCount;
                gridLayout.constraintCount = 2;
                gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
                gridLayout.startAxis = fillRowsFirst
                    ? GridLayoutGroup.Axis.Horizontal
                    : GridLayoutGroup.Axis.Vertical;
                gridLayout.childAlignment = TextAnchor.UpperLeft;
                gridLayout.spacing = new Vector2(
                    columnSpacing,
                    rowSpacing);
                gridLayout.cellSize = new Vector2(
                    100f,
                    rowHeight);
                ContentSizeFitter fitter =
                    contentObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;

                scroll.viewport = viewport;
                scroll.content = content;
                scrollRect = scroll;
            }

            if (emptyLabel == null)
            {
                emptyLabel = CreateText(
                    "Empty",
                    transform,
                    22,
                    TextAnchor.MiddleCenter,
                    descriptionColor);
                RectTransform rect = emptyLabel.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(40f, 40f);
                rect.offsetMax = new Vector2(-40f, -80f);
                emptyLabel.gameObject.SetActive(false);
            }
        }

        private void UpdateGridSize()
        {
            if (gridLayout == null ||
                content == null ||
                scrollRect == null ||
                scrollRect.viewport == null)
            {
                return;
            }

            float width = content.rect.width;
            if (width <= 1f)
            {
                width = scrollRect.viewport.rect.width;
            }

            if (width <= 1f)
            {
                return;
            }

            float cellWidth = (width - columnSpacing) * .5f;
            gridLayout.constraint =
                GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;
            gridLayout.startAxis = fillRowsFirst
                ? GridLayoutGroup.Axis.Horizontal
                : GridLayoutGroup.Axis.Vertical;
            if (Mathf.Abs(gridLayout.cellSize.x - cellWidth) < .5f)
            {
                return;
            }

            gridLayout.cellSize = new Vector2(
                cellWidth,
                rowHeight);
            LayoutRebuilder.MarkLayoutForRebuild(content);
        }

        private RowView CreateRow(
            AchievementSO achievement,
            bool unlocked)
        {
            GameObject rowObject = CreateRectObject(
                achievement.AchievementId + " Row",
                content);
            RectTransform row =
                rowObject.GetComponent<RectTransform>();
            Image background = rowObject.AddComponent<Image>();
            background.color = rowColor;
            if (rowMaterial != null)
            {
                background.material = rowMaterial;
            }

            LayoutElement element = rowObject.AddComponent<LayoutElement>();
            element.minHeight = rowHeight;
            element.preferredHeight = rowHeight;
            element.flexibleHeight = 0f;

            Text title = CreateText(
                "Title",
                rowObject.transform,
                titleFontSize,
                TextAnchor.UpperLeft,
                titleColor);
            title.fontStyle = FontStyle.Bold;
            title.text = achievement.DisplayName;
            SetRect(
                title.rectTransform,
                new Vector2(18f, -8f),
                new Vector2(-76f, 46f),
                new Vector2(0f, 1f));

            Text description = CreateText(
                "Description",
                rowObject.transform,
                descriptionFontSize,
                TextAnchor.UpperLeft,
                descriptionColor);
            description.text = string.IsNullOrWhiteSpace(
                    achievement.Description)
                ? "暂无简介"
                : achievement.Description;
            SetRect(
                description.rectTransform,
                new Vector2(18f, -60f),
                new Vector2(-76f, 26f),
                new Vector2(0f, 1f));

            Text requirement = CreateText(
                "Requirement",
                rowObject.transform,
                titleFontSize,
                TextAnchor.UpperLeft,
                requirementColor);
            requirement.text = BuildRequirement(achievement);
            SetRect(
                requirement.rectTransform,
                new Vector2(18f, -92f),
                new Vector2(-76f, 92f),
                new Vector2(0f, 1f));

            Toggle toggle = CreateCompletion(
                rowObject.transform,
                unlocked,
                out GameObject overlay,
                out GameObject completion);
            RowView rowView = new RowView
            {
                Achievement = achievement,
                Root = rowObject,
                Overlay = overlay,
                Completion = completion,
                Toggle = toggle,
                Requirement = requirement,
            };
            rowsById[achievement.AchievementId] = rowView;
            return rowView;
        }

        private Toggle CreateCompletion(
            Transform parent,
            bool unlocked,
            out GameObject overlay,
            out GameObject completion)
        {
            GameObject overlayObject = CreateRectObject(
                "Completed Overlay",
                parent);
            RectTransform overlayRect =
                overlayObject.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = overlayObject.AddComponent<Image>();
            overlayImage.color = completedOverlayColor;
            overlayImage.raycastTarget = false;
            overlayObject.SetActive(unlocked);
            overlay = overlayObject;

            GameObject completionObject = CreateRectObject(
                "Completion",
                parent);
            RectTransform completionRect =
                completionObject.GetComponent<RectTransform>();
            completionRect.anchorMin = completionRect.anchorMax =
                new Vector2(1f, .5f);
            completionRect.pivot = new Vector2(1f, .5f);
            completionRect.anchoredPosition =
                new Vector2(-20f, 0f);
            completionRect.sizeDelta = new Vector2(48f, 48f);

            Image completionImage =
                completionObject.AddComponent<Image>();
            completionImage.sprite = completedSprite;
            completionImage.color = completionColor;
            completionImage.preserveAspect = true;
            completionImage.raycastTarget = false;
            Toggle toggle =
                completionObject.AddComponent<Toggle>();
            toggle.targetGraphic = completionImage;
            toggle.interactable = false;
            toggle.transition = Selectable.Transition.None;
            toggle.isOn = unlocked;

            Text fallbackCheck = CreateText(
                "Fallback Check",
                completionObject.transform,
                26,
                TextAnchor.MiddleCenter,
                completionColor);
            fallbackCheck.text = "\u221A";
            fallbackCheck.rectTransform.anchorMin = Vector2.zero;
            fallbackCheck.rectTransform.anchorMax = Vector2.one;
            fallbackCheck.rectTransform.offsetMin = Vector2.zero;
            fallbackCheck.rectTransform.offsetMax = Vector2.zero;
            fallbackCheck.gameObject.SetActive(
                unlocked && completedSprite == null);
            toggle.graphic = fallbackCheck;
            completionObject.SetActive(unlocked);
            completion = completionObject;
            return toggle;
        }

        private string BuildRequirement(AchievementSO achievement)
        {
            if (achievement == null)
            {
                return "暂无成就要求";
            }

            if (achievementManager.IsLocked(
                    achievement.AchievementId))
            {
                return "已锁定";
            }

            if (achievementManager.IsUnlocked(
                    achievement.AchievementId))
            {
                return "已完成";
            }

            string current = achievementManager
                .GetCurrentRequirementText(
                    achievement.AchievementId);
            return string.IsNullOrWhiteSpace(current)
                ? "暂无成就要求"
                : current;
        }

        private void HandleAchievementUnlocked(
            AchievementSO achievement)
        {
            RefreshRowViews();
        }

        private void HandleAchievementProgressChanged(
            AchievementSO achievement)
        {
            RefreshRowViews();
        }

        private void RefreshRowViews()
        {
            foreach (KeyValuePair<int, RowView> pair in rowsById)
            {
                RowView row = pair.Value;
                if (row == null || row.Root == null)
                {
                    continue;
                }

                bool unlocked = achievementManager.IsUnlocked(
                    pair.Key);
                row.Overlay.SetActive(unlocked);
                row.Completion.SetActive(unlocked);
                row.Toggle.isOn = unlocked;
                row.Requirement.text = BuildRequirement(
                    row.Achievement);
            }
        }

        private void ClearRows()
        {
            for (int index = 0; index < rows.Count; index++)
            {
                if (rows[index]?.Root != null)
                {
                    Destroy(rows[index].Root);
                }
            }

            rows.Clear();
            rowsById.Clear();
        }

        private void ShowEmpty(string message)
        {
            emptyLabel.text = message;
            emptyLabel.gameObject.SetActive(true);
        }

        private void RefreshTypography()
        {
            EscMenuController controller =
                GetComponentInParent<EscMenuController>();
            controller?.RefreshTypography();
        }

        private static GameObject CreateRectObject(
            string name,
            Transform parent)
        {
            GameObject value = new GameObject(
                name,
                typeof(RectTransform));
            value.transform.SetParent(parent, false);
            return value;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            int fontSize,
            TextAnchor alignment,
            Color color)
        {
            GameObject value = CreateRectObject(name, parent);
            Text text = value.AddComponent<Text>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 sizeDelta,
            Vector2 pivot)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
