using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class EscSettingsResponsiveLayout : MonoBehaviour
    {
        [Header("Responsive Reference")]
        [SerializeField, Min(1f)] private float referenceWidth = 1360f;
        [SerializeField, Min(1f)] private float referenceHeight = 720f;
        [SerializeField, Min(.1f)] private float scaleBoost = 1.15f;
        [SerializeField, Min(.5f)] private float minScale = .85f;
        [SerializeField, Min(.5f)] private float maxScale = 1.65f;
        [SerializeField, Min(0f)] private float contentGap = 36f;

        private RectTransform root;
        private RectTransform categoryList;
        private RectTransform contentRoot;
        private RectTransform title;
        private RectTransform status;
        private RectTransform back;
        private bool applying;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            ApplyLayout();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (isActiveAndEnabled)
            {
                ApplyLayout();
            }
        }

        public void RefreshLayout()
        {
            CacheReferences();
            ApplyLayout();
        }

        private void CacheReferences()
        {
            if (root == null)
            {
                root = transform as RectTransform;
            }

            if (root == null)
            {
                return;
            }

            categoryList = FindChild("Category List");
            contentRoot = FindChild("Content");
            title = FindChild("Title");
            status = FindChild("Status");
            back = FindChild("Back");
        }

        private RectTransform FindChild(string childName)
        {
            Transform child = transform.Find(childName);
            return child as RectTransform;
        }

        private void ApplyLayout()
        {
            if (applying ||
                root == null ||
                contentRoot == null ||
                root.rect.width <= 1f ||
                root.rect.height <= 1f)
            {
                return;
            }

            applying = true;
            float widthScale = root.rect.width / referenceWidth;
            float heightScale = root.rect.height / referenceHeight;
            float scale = Mathf.Clamp(
                Mathf.Min(widthScale, heightScale) * scaleBoost,
                minScale,
                maxScale);

            SetScale(categoryList, scale);
            float categoryRight = categoryList != null
                ? categoryList.anchoredPosition.x +
                  categoryList.rect.width * scale
                : 52f;
            float contentX = categoryRight + contentGap * scale;
            Vector2 contentPosition = contentRoot.anchoredPosition;
            contentPosition.x = contentX;
            contentRoot.anchoredPosition = contentPosition;
            float contentScale = scale;
            if (contentRoot.rect.width > 1f)
            {
                float availableWidth =
                    root.rect.width - contentX - 24f;
                contentScale = Mathf.Min(
                    scale,
                    Mathf.Max(
                        minScale,
                        availableWidth / contentRoot.rect.width));
            }

            SetScale(contentRoot, contentScale);
            SetScale(title, scale);
            SetBottom(status, new Vector2(contentX, 30f), scale);
            SetBottom(back, new Vector2(52f, 24f), scale);
            applying = false;
        }

        private static void SetScale(
            RectTransform target,
            float scale)
        {
            if (target != null)
            {
                target.localScale = Vector3.one * scale;
            }
        }

        private static void SetBottom(
            RectTransform target,
            Vector2 position,
            float scale)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.zero;
            target.pivot = Vector2.zero;
            target.anchoredPosition = position;
            target.localScale = Vector3.one * scale;
        }
    }
}
