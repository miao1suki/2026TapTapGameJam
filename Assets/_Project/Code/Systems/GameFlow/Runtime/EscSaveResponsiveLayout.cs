using UnityEngine;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class EscSaveResponsiveLayout : MonoBehaviour
    {
        [Header("Responsive Reference")]
        [SerializeField, Min(1f)] private float referenceWidth = 1360f;
        [SerializeField, Min(1f)] private float referenceHeight = 900f;
        [SerializeField, Min(.1f)] private float scaleBoost = 1.15f;
        [SerializeField, Min(.5f)] private float minScale = .85f;
        [SerializeField, Min(.5f)] private float maxScale = 1.65f;

        private RectTransform root;
        private RectTransform title;
        private RectTransform status;
        private RectTransform slotList;
        private RectTransform createSlot;
        private RectTransform back;
        private RectTransform close;
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

            title = FindChild("Title");
            status = FindChild("Status");
            slotList = FindChild("Save Slots");
            createSlot = FindChild("Create Empty Save");
            back = FindChild("Back");
            close = FindChild("Close");
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
                root.rect.width <= 1f ||
                root.rect.height <= 1f)
            {
                return;
            }

            applying = true;
            float scale = Mathf.Clamp(
                Mathf.Min(
                    root.rect.width / referenceWidth,
                    root.rect.height / referenceHeight) *
                scaleBoost,
                minScale,
                maxScale);

            SetTopLeft(
                title,
                new Vector2(54f, -42f) * scale,
                scale);
            SetTopLeft(
                status,
                new Vector2(56f, -96f) * scale,
                scale);
            SetTopLeft(
                slotList,
                new Vector2(48f, -142f) * scale,
                scale);
            SetBottom(
                createSlot,
                new Vector2(48f, 24f) * scale,
                scale);
            SetBottom(
                back,
                new Vector2(246f, 24f) * scale,
                scale);
            SetTopRight(
                close,
                new Vector2(-26f, -26f) * scale,
                scale);
            applying = false;
        }

        private static void SetTopLeft(
            RectTransform target,
            Vector2 position,
            float scale)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = new Vector2(0f, 1f);
            target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = position;
            target.localScale = Vector3.one * scale;
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

        private static void SetTopRight(
            RectTransform target,
            Vector2 position,
            float scale)
        {
            if (target == null)
            {
                return;
            }

            target.anchorMin = Vector2.one;
            target.anchorMax = Vector2.one;
            target.pivot = Vector2.one;
            target.anchoredPosition = position;
            target.localScale = Vector3.one * scale;
        }
    }
}
