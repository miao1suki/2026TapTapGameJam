using Project.BlockFeatures;
using TMPro;
using UnityEngine;

namespace Project.Subtitles
{
    public enum DisplayBlockAnchor
    {
        [InspectorName("中间")] Center = 0,
        [InspectorName("上方")] Top = 1,
        [InspectorName("下方")] Bottom = 2,
        [InspectorName("左侧")] Left = 3,
        [InspectorName("右侧")] Right = 4,
        [InspectorName("左上")] TopLeft = 5,
        [InspectorName("右上")] TopRight = 6,
        [InspectorName("左下")] BottomLeft = 7,
        [InspectorName("右下")] BottomRight = 8
    }

    public enum DisplayBlockTextAlignment
    {
        [InspectorName("居中")] Center = 0,
        [InspectorName("靠左")] Left = 1,
        [InspectorName("靠右")] Right = 2
    }

    public enum DisplayBlockLayerOrder
    {
        [InspectorName("玩家上方")] AbovePlayer = 0,
        [InspectorName("玩家下方")] BelowPlayer = 1
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class DisplayBlockFeature : MonoBehaviour
    {
        private const float RuntimeFontSize = 100f;

        [SerializeField, TextArea(1, 4)]
        private string text = "Display Block";
        [SerializeField] private TMP_FontAsset font;
        [SerializeField, Min(.01f)]
        private float fontSizeRatio = .4f;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private DisplayBlockAnchor anchor =
            DisplayBlockAnchor.Center;
        [SerializeField] private DisplayBlockTextAlignment textAlignment =
            DisplayBlockTextAlignment.Center;
        [SerializeField] private Vector2 offset;
        [SerializeField] private DisplayBlockLayerOrder layerOrder =
            DisplayBlockLayerOrder.AbovePlayer;
        [SerializeField] private string playerTag = "Player";
        [SerializeField] private bool showOnAwake = true;
        [SerializeField] private bool hideColliderOnAwake = true;
        [SerializeField] private bool hideVisualOnAwake = true;

        private Collider[] colliders;
        private Renderer sourceRenderer;
        private Bounds localBounds;
        private TextMeshPro textObject;
        private Renderer textRenderer;

        private void Awake()
        {
            CacheSceneReferences();
            if (hideColliderOnAwake)
            {
                SetCollidersEnabled(false);
            }

            if (hideVisualOnAwake)
            {
                SetSourceVisible(false);
            }

            if (showOnAwake)
            {
                Show();
            }
        }

        private void OnValidate()
        {
            if (!Application.isPlaying || textObject == null)
            {
                return;
            }

            RefreshText();
        }

        private void OnDestroy()
        {
            if (textObject != null)
            {
                Destroy(textObject.gameObject);
                textObject = null;
                textRenderer = null;
            }
        }

        public void Show()
        {
            EnsureTextObject();
            if (textObject == null)
            {
                return;
            }

            textObject.gameObject.SetActive(true);
            RefreshText();
        }

        public void Hide()
        {
            if (textObject != null)
            {
                textObject.gameObject.SetActive(false);
            }
        }

        private void CacheSceneReferences()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            sourceRenderer = GetComponent<Renderer>();
            if (sourceRenderer == null)
            {
                Renderer[] renderers =
                    GetComponentsInChildren<Renderer>(true);
                sourceRenderer = renderers.Length > 0
                    ? renderers[0]
                    : null;
            }

            localBounds = sourceRenderer != null
                ? sourceRenderer.localBounds
                : new Bounds(
                    Vector3.zero,
                    Vector3.one *
                    GridCellSizeUtility.Resolve(this));
        }

        private void EnsureTextObject()
        {
            if (textObject != null)
            {
                return;
            }

            GameObject textGo = new GameObject(
                "Display Text",
                typeof(TextMeshPro));
            textGo.transform.SetParent(transform, false);
            textObject = textGo.GetComponent<TextMeshPro>();
            textObject.font = font != null
                ? font
                : TMP_Settings.defaultFontAsset;
            textObject.textWrappingMode = TextWrappingModes.NoWrap;
            textObject.overflowMode = TextOverflowModes.Overflow;
            textRenderer = textObject.renderer;
        }

        private void RefreshText()
        {
            if (textObject == null)
            {
                return;
            }

            textObject.font = font != null
                ? font
                : TMP_Settings.defaultFontAsset;
            textObject.fontSize = RuntimeFontSize;
            textObject.color = color;
            textObject.alignment = GetAlignment();
            textObject.text = text ?? string.Empty;
            textObject.transform.localPosition =
                GetAnchorPosition() + (Vector3)offset;
            textObject.ForceMeshUpdate();
            float preferredHeight =
                textObject.preferredHeight;
            float targetHeight = Mathf.Max(
                .0001f,
                localBounds.size.y *
                Mathf.Max(.01f, fontSizeRatio));
            float scale = preferredHeight > .0001f
                ? targetHeight / preferredHeight
                : targetHeight / RuntimeFontSize;
            textObject.transform.localScale =
                Vector3.one * Mathf.Max(.0001f, scale);
            ApplyLayerOrder();
        }

        private Vector3 GetAnchorPosition()
        {
            Vector3 extents = localBounds.extents;
            switch (anchor)
            {
                case DisplayBlockAnchor.Top:
                    return new Vector3(0f, extents.y, 0f);
                case DisplayBlockAnchor.Bottom:
                    return new Vector3(0f, -extents.y, 0f);
                case DisplayBlockAnchor.Left:
                    return new Vector3(-extents.x, 0f, 0f);
                case DisplayBlockAnchor.Right:
                    return new Vector3(extents.x, 0f, 0f);
                case DisplayBlockAnchor.TopLeft:
                    return new Vector3(-extents.x, extents.y, 0f);
                case DisplayBlockAnchor.TopRight:
                    return new Vector3(extents.x, extents.y, 0f);
                case DisplayBlockAnchor.BottomLeft:
                    return new Vector3(-extents.x, -extents.y, 0f);
                case DisplayBlockAnchor.BottomRight:
                    return new Vector3(extents.x, -extents.y, 0f);
                default:
                    return Vector3.zero;
            }
        }

        private TextAlignmentOptions GetAlignment()
        {
            bool top = anchor == DisplayBlockAnchor.Top ||
                       anchor == DisplayBlockAnchor.TopLeft ||
                       anchor == DisplayBlockAnchor.TopRight;
            bool bottom = anchor == DisplayBlockAnchor.Bottom ||
                          anchor == DisplayBlockAnchor.BottomLeft ||
                          anchor == DisplayBlockAnchor.BottomRight;
            if (top)
            {
                return textAlignment == DisplayBlockTextAlignment.Left
                    ? TextAlignmentOptions.TopLeft
                    : textAlignment == DisplayBlockTextAlignment.Right
                        ? TextAlignmentOptions.TopRight
                        : TextAlignmentOptions.Top;
            }

            if (bottom)
            {
                return textAlignment == DisplayBlockTextAlignment.Left
                    ? TextAlignmentOptions.BottomLeft
                    : textAlignment == DisplayBlockTextAlignment.Right
                        ? TextAlignmentOptions.BottomRight
                        : TextAlignmentOptions.Bottom;
            }

            return textAlignment == DisplayBlockTextAlignment.Left
                ? TextAlignmentOptions.Left
                : textAlignment == DisplayBlockTextAlignment.Right
                    ? TextAlignmentOptions.Right
                    : TextAlignmentOptions.Center;
        }

        private void ApplyLayerOrder()
        {
            if (textRenderer == null)
            {
                return;
            }

            Renderer reference = FindPlayerRenderer();
            if (reference == null)
            {
                reference = sourceRenderer;
            }

            if (reference == null)
            {
                return;
            }

            textRenderer.sortingLayerID = reference.sortingLayerID;
            textRenderer.sortingOrder = reference.sortingOrder +
                (layerOrder == DisplayBlockLayerOrder.AbovePlayer
                    ? 1
                    : -1);
        }

        private Renderer FindPlayerRenderer()
        {
            GameObject player = string.IsNullOrWhiteSpace(playerTag)
                ? null
                : GameObject.FindGameObjectWithTag(playerTag);
            return player != null
                ? player.GetComponentInChildren<Renderer>()
                : null;
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = enabled;
                }
            }
        }

        private void SetSourceVisible(bool visible)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null &&
                    renderers[index] != textRenderer)
                {
                    renderers[index].enabled = visible;
                }
            }
        }
    }
}
