using System;
using DG.Tweening;
using Project.ColorBlocks;
using Project.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Development.PickWheel
{
    // 正式玩家轮盘外观；输入和选色仍由 PlayerColorWheel 负责。
    public sealed class PickWheelPrototype : MonoBehaviour, IPlayerColorWheelView
    {
        private const float Size = 420f;
        private static readonly string[] Ids = { "red", "green", "blue" };
        private static readonly float[] Angles = { 90f, 330f, 210f };
        [SerializeField] private Sprite wheelArtwork;
        [SerializeField] private TMP_FontAsset pixelFont;
        [SerializeField] private PlayerColorWheel playerWheel;

        private RectTransform root;
        private TMP_Text centerLabel;
        private readonly RectTransform[] sectors = new RectTransform[3];
        private readonly Image[] images = new Image[3];
        private readonly TMP_Text[] labels = new TMP_Text[3];
        private readonly Sprite[] artworkSectors = new Sprite[3];
        private readonly Sprite[] highlightedSectors = new Sprite[3];
        private readonly Vector2[] sectorCenters = new Vector2[3];
        private int hovered = -1;
        public string HighlightedColorId => hovered >= 0 ? Ids[hovered] : string.Empty;

        private void Awake()
        {
            if (wheelArtwork == null) wheelArtwork = Resources.Load<Sprite>("UI/轮盘");
            BuildView();
            Hide();
        }

        private void Start()
        {
            if (playerWheel == null)
                playerWheel = GetComponent<PlayerColorWheel>();
            if (playerWheel == null)
                playerWheel = ProjectDiscovery.FindFirst<PlayerColorWheel>();
            if (playerWheel == null)
            {
                Debug.LogError("[PickWheel] 玩家缺少颜色轮盘组件。", this);
                return;
            }
            playerWheel.SelectedColorChanged += OnSelected;
            playerWheel.BindExternalView(this);
            Refresh();
        }

        private void OnEnable()
        {
            ColorRuntimeService.Instance.ColorUnlocked += OnUnlocked;
            EventMgr.OnRoomColorReset += OnRoomReset;
            Refresh();
        }

        private void OnDisable()
        {
            if (ColorRuntimeService.Existing != null)
                ColorRuntimeService.Existing.ColorUnlocked -= OnUnlocked;
            EventMgr.OnRoomColorReset -= OnRoomReset;
            if (playerWheel != null)
            {
                playerWheel.SelectedColorChanged -= OnSelected;
                playerWheel.UnbindExternalView(this);
            }
            foreach (RectTransform sector in sectors)
                if (sector != null) sector.DOKill();
        }

        private void OnDestroy()
        {
            foreach (Sprite sprite in artworkSectors)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
            foreach (Sprite sprite in highlightedSectors)
            {
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        public void Show()
        {
            if (root != null) root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            if (root != null) root.gameObject.SetActive(false);
            SetHover(-1);
        }

        public void UpdatePointer(Vector2 screenPosition)
        {
            if (root == null) return;
            Vector2 delta = screenPosition -
                RectTransformUtility.WorldToScreenPoint(null, root.position);
            int next = -1;
            if (delta.sqrMagnitude > 34f * 34f)
            {
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                float nearest = 180f;
                for (int i = 0; i < 3; i++)
                {
                    float distance = Mathf.Abs(Mathf.DeltaAngle(angle, Angles[i]));
                    if (distance >= nearest) continue;
                    nearest = distance;
                    next = i;
                }
            }
            SetHover(next);
        }

        private void BuildView()
        {
            GameObject canvasObject = new GameObject("Pick Wheel Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2002;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            root = Rect("三色轮盘", canvasObject.transform, Size);
            if (wheelArtwork == null)
            {
                Debug.LogError("[PickWheel] 未指定轮盘原图。", this);
                return;
            }
            SplitArtwork();
            for (int i = 0; i < 3; i++)
            {
                Image image = NewImage(Ids[i] + " 原图扇区", root, artworkSectors[i], Size);
                image.raycastTarget = false;
                RectTransform rect = image.rectTransform;
                // 每一块以原图中自身可见像素的重心等比缩放。
                Vector2 pivot = sectorCenters[i];
                rect.pivot = pivot;
                rect.anchoredPosition = (pivot - new Vector2(.5f, .5f)) * Size;
                images[i] = image;
                sectors[i] = rect;
                TMP_Text label = NewLabel(Ids[i] + " 名称", rect, 72f);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = pivot;
                label.rectTransform.anchoredPosition = Vector2.zero;
                label.fontSize = 40f;
                labels[i] = label;
            }
            centerLabel = NewLabel("当前颜色", root, 156f);
            centerLabel.fontSize = 21f;
            centerLabel.color = new Color(.13f, .15f, .2f, 1f);
        }

        private static RectTransform Rect(string name, Transform parent, float size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform))
                .GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(size, size);
            return rect;
        }

        private static Image NewImage(string name, Transform parent, Sprite sprite, float size)
        {
            Image image = Rect(name, parent, size).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            return image;
        }

        private TMP_Text NewLabel(string name, Transform parent, float size)
        {
            TextMeshProUGUI label = Rect(name, parent, size)
                .gameObject.AddComponent<TextMeshProUGUI>();
            if (pixelFont != null) label.font = pixelFont;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            return label;
        }

        private void SplitArtwork()
        {
            Texture2D source = ReadArtwork(wheelArtwork.texture);
            int width = source.width;
            int height = source.height;
            Color32[] original = source.GetPixels32();
            Color32[][] normalPixels = new Color32[3][];
            Color32[][] whitePixels = new Color32[3][];
            float[] weight = new float[3];
            Vector2[] weightedCenter = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                normalPixels[i] = new Color32[original.Length];
                whitePixels[i] = new Color32[original.Length];
            }
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int pixel = y * width + x;
                Color32 color = original[pixel];
                if (color.a == 0) continue;
                float angle = Mathf.Atan2(y + .5f - height * .5f,
                    x + .5f - width * .5f) * Mathf.Rad2Deg;
                int sector = 0;
                float nearest = float.MaxValue;
                for (int i = 0; i < 3; i++)
                {
                    float distance = Mathf.Abs(Mathf.DeltaAngle(angle, Angles[i]));
                    if (distance >= nearest) continue;
                    nearest = distance;
                    sector = i;
                }
                normalPixels[sector][pixel] = color;
                whitePixels[sector][pixel] = new Color32(255, 255, 255, color.a);
                float alpha = color.a / 255f;
                weight[sector] += alpha;
                weightedCenter[sector] += new Vector2(x + .5f, y + .5f) * alpha;
            }
            for (int i = 0; i < 3; i++)
            {
                sectorCenters[i] = weight[i] > 0f
                    ? new Vector2(weightedCenter[i].x / (weight[i] * width),
                        weightedCenter[i].y / (weight[i] * height))
                    : new Vector2(.5f, .5f);
                artworkSectors[i] = MakeSprite(normalPixels[i], width, height);
                highlightedSectors[i] = MakeSprite(whitePixels[i], width, height);
            }
            Destroy(source);
        }

        private static Texture2D ReadArtwork(Texture texture)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(texture.width,
                texture.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(texture, temporary);
                RenderTexture.active = temporary;
                Texture2D copy = new Texture2D(texture.width, texture.height,
                    TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, texture.width, texture.height),
                    0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        private static Sprite MakeSprite(Color32[] pixels, int width, int height)
        {
            Texture2D texture = new Texture2D(width, height,
                TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(.5f, .5f), width);
        }

        private void SetHover(int next)
        {
            if (hovered == next) return;
            int previous = hovered;
            hovered = next;
            if (previous >= 0) SetSector(previous, false);
            if (hovered >= 0) SetSector(hovered, true);
        }

        private void SetSector(int index, bool active)
        {
            if (sectors[index] == null) return;
            sectors[index].DOKill();
            sectors[index].DOScale(active ? .84f : 1f, active ? .24f : .2f)
                .SetEase(active ? Ease.OutBack : Ease.OutCubic)
                .SetUpdate(true);
            // 原图黑色扇区与白色半透明扇区硬切；仅缩放交给 DOTween。
            images[index].sprite = active
                ? highlightedSectors[index] : artworkSectors[index];
            images[index].color = active
                ? new Color(1f, 1f, 1f, .58f) : Color.white;
            labels[index].color = active
                ? new Color(.16f, .18f, .23f, .8f) : Color.white;
        }

        private void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                if (images[i] == null) continue;
                bool unlocked = ColorRuntimeService.Instance.IsUnlocked(Ids[i]);
                ColorTypeDefinition definition = ColorRuntimeService.Instance.Catalog?.Find(Ids[i]);
                labels[i].text = unlocked
                    ? (definition?.displayName ?? Ids[i]) : "?";
            }
            if (centerLabel == null) return;
            string selected = playerWheel != null
                ? playerWheel.SelectedColorId : string.Empty;
            centerLabel.text = string.IsNullOrEmpty(selected)
                ? "选择颜色" : $"当前\n{selected}";
        }

        private void OnUnlocked(ColorTypeDefinition _) => Refresh();
        private void OnSelected(string _) => Refresh();
        private void OnRoomReset()
        {
            Refresh();
        }
    }
}
