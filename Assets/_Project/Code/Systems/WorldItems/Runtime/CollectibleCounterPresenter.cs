using Project.GameFlow;
using Project.StartMenu;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Items
{
    [DisallowMultipleComponent]
    public sealed class CollectibleCounterPresenter :
        MonoBehaviour
    {
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private Color textColor =
            new Color(.92f, .98f, 1f, 1f);
        [SerializeField, Min(1f)] private int fontSize = 30;
        [SerializeField, Min(0f)] private float leftInset = 100f;
        [SerializeField, Min(0f)] private float rightInset = 36f;
        [SerializeField, Min(1f)] private float rowHeight = 96f;
        [SerializeField, Min(1f)] private float iconSize = 96f;

        private GameObject rowRoot;
        private Image icon;
        private Text countLabel;

        private void OnEnable()
        {
            CollectibleProgressService.Changed -=
                HandleProgressChanged;
            CollectibleProgressService.Changed +=
                HandleProgressChanged;
            EnsureView();
            Refresh();
        }

        private void OnDisable()
        {
            CollectibleProgressService.Changed -=
                HandleProgressChanged;
        }

        public static void EnsureForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (GameObject root in
                     scene.GetRootGameObjects())
            {
                AchievementCollectionPresenter[] presenters =
                    root.GetComponentsInChildren<
                        AchievementCollectionPresenter>(true);
                for (int index = 0;
                     index < presenters.Length;
                     index++)
                {
                    AchievementCollectionPresenter presenter =
                        presenters[index];
                    if (presenter == null ||
                        presenter.GetComponent<
                            CollectibleCounterPresenter>() != null)
                    {
                        continue;
                    }

                    presenter.gameObject.AddComponent<
                        CollectibleCounterPresenter>();
                }
            }
        }

        private void EnsureView()
        {
            if (rowRoot != null)
            {
                return;
            }

            rowRoot = new GameObject(
                "Collectible Total",
                typeof(RectTransform),
                typeof(Image));
            RectTransform row =
                rowRoot.GetComponent<RectTransform>();
            row.SetParent(transform, false);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(.5f, 1f);
            row.anchoredPosition =
                new Vector2(
                    (leftInset - rightInset) * .5f,
                    -98f);
            row.sizeDelta =
                new Vector2(
                    -(leftInset + rightInset),
                    rowHeight);

            Image rowHitArea = rowRoot.GetComponent<Image>();
            rowHitArea.color = Color.clear;
            rowHitArea.raycastTarget = false;

            GameObject iconObject = new GameObject(
                "Collectible Icon",
                typeof(RectTransform),
                typeof(Image));
            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(row, false);
            iconRect.anchorMin = new Vector2(0f, .5f);
            iconRect.anchorMax = new Vector2(0f, .5f);
            iconRect.pivot = new Vector2(0f, .5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta =
                new Vector2(iconSize, iconSize);
            icon = iconObject.GetComponent<Image>();
            icon.sprite = ResolveIcon();
            icon.color = Color.white;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject labelObject = new GameObject(
                "Collectible Count",
                typeof(RectTransform),
                typeof(Text));
            RectTransform labelRect =
                labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(row, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin =
                new Vector2(iconSize + 14f, 0f);
            labelRect.offsetMax = Vector2.zero;
            countLabel = labelObject.GetComponent<Text>();
            countLabel.color = textColor;
            countLabel.fontSize = fontSize;
            countLabel.alignment = TextAnchor.MiddleLeft;
            countLabel.horizontalOverflow =
                HorizontalWrapMode.Overflow;
            countLabel.verticalOverflow =
                VerticalWrapMode.Truncate;
            countLabel.raycastTarget = false;
            RefreshTypography();
        }

        private Sprite ResolveIcon()
        {
            if (iconSprite != null)
            {
                return iconSprite;
            }

            GameUiArtwork artwork = GameUiArtwork.Load();
            if (artwork?.collectibleIcon != null)
            {
                return artwork.collectibleIcon;
            }

            Sprite[] icons = artwork?.pauseIcons;
            return icons != null && icons.Length > 3
                ? icons[3]
                : null;
        }

        private void HandleProgressChanged()
        {
            EnsureView();
            Refresh();
        }

        private void Refresh()
        {
            if (countLabel == null)
            {
                return;
            }

            CollectibleProgressSnapshot progress =
                CollectibleProgressService.GetWorldProgress();
            countLabel.text =
                $" : {progress.Collected}/{progress.Total}";
        }

        private void RefreshTypography()
        {
            EscMenuController menu =
                GetComponentInParent<EscMenuController>();
            menu?.RefreshTypography();
        }
    }
}
