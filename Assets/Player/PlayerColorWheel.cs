using System;
using System.Collections.Generic;
using Project.ColorBlocks;
using Project.InputAbstraction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Project.Player
{
    /// <summary>
    /// 玩家颜色选择能力。它只保存玩家当前选中的颜色并提供轮盘 UI；
    /// 物体的实际效果由目标组件直接执行。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInteractionSensor))]
    public sealed class PlayerColorWheel : MonoBehaviour
    {
        private const int WheelSortingOrder = 2001;
        private const int CircleTextureSize = 64;
        private const float WheelOuterRadius = 92f;
        private const float WheelSelectionDeadZone = 24f;

        [SerializeField, Min(.1f)] private float rangeLineWidth = .035f;
        [SerializeField, Min(8)] private int rangeDashCount = 32;
        [SerializeField] private Color rangeColor =
            new Color(.55f, .8f, 1f, .45f);
        [SerializeField, Min(100f)] private float wheelSize = 280f;
        [SerializeField, Min(32f)] private float wheelButtonSize = 72f;

        private PlayerInteractionSensor sensor;
        private GameObject rangeRoot;
        private Material rangeMaterial;
        private readonly List<LineRenderer> rangeDashes =
            new List<LineRenderer>();
        private GameObject canvasObject;
        private GameObject eventSystemObject;
        private RectTransform wheelRoot;
        private Text wheelLabel;
        private string selectedColorId = string.Empty;
        private string highlightedColorId = string.Empty;
        private readonly List<string> wheelColorIds = new List<string>();
        private readonly List<Image> wheelColorImages = new List<Image>();
        private readonly List<Color> wheelBaseColors = new List<Color>();
        private float rangeRadius = -1f;
        private bool wheelOpen;
        private Vector2 pointerPosition;

        private static Sprite circleSprite;
        private static Sprite ringSprite;

        public string SelectedColorId => selectedColorId;
        public bool HasSelectedColor => !string.IsNullOrWhiteSpace(selectedColorId);
        public bool IsWheelOpen => wheelOpen;

        public event Action<string> SelectedColorChanged;

        private void Awake()
        {
            sensor = GetComponent<PlayerInteractionSensor>();
            CreateRangeIndicator();
            Debug.Log("[PlayerColorWheel] 当前颜色能力：无", this);
        }

        private void OnEnable()
        {
            EventMgr.OnRoomColorReset += ClearSelectedColor;
        }

        private void OnDisable()
        {
            EventMgr.OnRoomColorReset -= ClearSelectedColor;
        }

        public void Tick(Vector2 currentPointerPosition)
        {
            pointerPosition = currentPointerPosition;
            RefreshRangeIndicator();
            if (wheelOpen)
            {
                UpdateWheelSelection(pointerPosition);
            }
        }

        public void UpdateInput(
            bool holdMode,
            bool pressedThisFrame,
            bool releasedThisFrame,
            bool triggeredThisFrame,
            Vector2 currentPointerPosition)
        {
            Tick(currentPointerPosition);

            if (holdMode)
            {
                if (pressedThisFrame)
                {
                    OpenWheel();
                }

                if (releasedThisFrame)
                {
                    CloseWheel();
                }

                return;
            }

            if (pressedThisFrame ||
                triggeredThisFrame)
            {
                ToggleWheel();
            }
        }

        public void ToggleWheel()
        {
            if (wheelOpen)
            {
                CloseWheel();
            }
            else
            {
                OpenWheel();
            }
        }

        public bool IsSelected(string colorId)
        {
            return !string.IsNullOrWhiteSpace(colorId) &&
                   string.Equals(
                       selectedColorId,
                       colorId,
                       StringComparison.OrdinalIgnoreCase);
        }

        public bool TrySelectColor(string colorId)
        {
            if (string.IsNullOrWhiteSpace(colorId) ||
                !ColorRuntimeService.Instance.IsUnlocked(colorId))
            {
                return false;
            }

            SetSelectedColor(colorId);
            return true;
        }

        private void SetSelectedColor(string colorId)
        {
            if (string.Equals(selectedColorId, colorId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            selectedColorId = colorId ?? string.Empty;
            SelectedColorChanged?.Invoke(selectedColorId);
            Debug.Log(
                $"[PlayerColorWheel] 颜色能力切换成功：{selectedColorId}",
                this);
        }

        public void ClearSelectedColor()
        {
            if (string.IsNullOrEmpty(selectedColorId))
            {
                return;
            }

            selectedColorId = string.Empty;
            SelectedColorChanged?.Invoke(selectedColorId);
        }

        private void OpenWheel()
        {
            if (wheelOpen)
            {
                return;
            }

            highlightedColorId = string.Empty;
            EnsureCanvas();
            RebuildWheel();
            wheelOpen = true;
            wheelRoot.gameObject.SetActive(true);
            UpdateWheelPosition();
            UpdateWheelSelection(pointerPosition);
        }

        public void CloseWheel()
        {
            wheelOpen = false;
            if (wheelRoot != null)
            {
                wheelRoot.gameObject.SetActive(false);
            }
        }

        public void CommitWheelSelection()
        {
            if (!wheelOpen)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(highlightedColorId))
            {
                TrySelectColor(highlightedColorId);
            }

            CloseWheel();
        }

        public void TryUseSelectedAbility()
        {
            if (EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null ||
                sensor == null ||
                !sensor.TryGetScreenComponent<IColorApplicationTarget>(
                    camera,
                    pointerPosition,
                    out IColorApplicationTarget colorTarget))
            {
                Debug.Log(
                    "[PlayerColorWheel] 左键交互失败：没有点击到可接收颜色的物体。",
                    this);
                return;
            }

            Component targetComponent = colorTarget as Component;
            if (!sensor.IsWithinRange(targetComponent))
            {
                Debug.Log(
                    "[PlayerColorWheel] 左键交互失败：点击目标超出交互圈。",
                    this);
                return;
            }

            string targetName = targetComponent != null
                ? targetComponent.gameObject.name
                : colorTarget.GetType().Name;
            if (!HasSelectedColor)
            {
                Debug.Log(
                    "[PlayerColorWheel] 左键交互失败：尚未选择颜色。",
                    targetComponent != null
                        ? targetComponent.gameObject
                        : gameObject);
                return;
            }

            bool success = colorTarget.ApplyColor(
                selectedColorId,
                gameObject);

            Debug.Log(
                success
                    ? $"[PlayerColorWheel] 使用能力“{(HasSelectedColor ? selectedColorId : "无")}”成功与物体交互：{targetName}"
                    : $"[PlayerColorWheel] 使用能力“{(HasSelectedColor ? selectedColorId : "无")}”与物体交互失败：{targetName}",
                targetComponent != null ? targetComponent.gameObject : gameObject);
        }

        private void EnsureCanvas()
        {
            if (canvasObject != null)
            {
                return;
            }

            canvasObject = new GameObject(
                "Player Color Wheel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            if (EventSystem.current == null)
            {
                eventSystemObject = new GameObject(
                    "Color Wheel EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
            }
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = WheelSortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            wheelRoot = new GameObject(
                "Color Wheel",
                typeof(RectTransform)).GetComponent<RectTransform>();
            wheelRoot.SetParent(canvas.transform, false);
            wheelRoot.anchorMin = new Vector2(.5f, .5f);
            wheelRoot.anchorMax = new Vector2(.5f, .5f);
            wheelRoot.pivot = new Vector2(.5f, .5f);
            wheelRoot.sizeDelta = new Vector2(wheelSize, wheelSize);
            wheelRoot.gameObject.SetActive(false);
        }

        private void RebuildWheel()
        {
            for (int index = wheelRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(wheelRoot.GetChild(index).gameObject);
            }

            Image backdrop = CreateImage(
                "Wheel Backdrop",
                wheelRoot,
                GetRingSprite(),
                new Color(.03f, .06f, .1f, .96f),
                new Vector2(wheelSize - 18f, wheelSize - 18f));
            backdrop.raycastTarget = false;

            wheelLabel = CreateText(
                "Wheel Center Label",
                wheelRoot,
                HasSelectedColor
                    ? $"当前：{selectedColorId}\n选择颜色"
                    : "选择颜色",
                18,
                Color.white,
                new Vector2(120f, 58f));
            wheelLabel.rectTransform.anchoredPosition = Vector2.zero;

            Button cancel = CreateButton(
                "Cancel",
                wheelRoot,
                GetCircleSprite(),
                new Color(.15f, .18f, .22f, .95f),
                new Vector2(64f, 64f),
                CloseWheel);
            cancel.GetComponent<RectTransform>().anchoredPosition =
                Vector2.zero;
            Text cancelLabel = CreateText(
                "Cancel Label",
                cancel.transform,
                "取消",
                14,
                Color.white,
                new Vector2(64f, 64f));
            cancelLabel.rectTransform.anchoredPosition = Vector2.zero;

            List<ColorTypeDefinition> colors = GetUnlockedColors();
            wheelColorIds.Clear();
            wheelColorImages.Clear();
            wheelBaseColors.Clear();
            for (int index = 0; index < colors.Count; index++)
            {
                ColorTypeDefinition definition = colors[index];
                if (definition == null || string.IsNullOrWhiteSpace(definition.id))
                {
                    continue;
                }

                string colorId = definition.id;
                float angle = index * Mathf.PI * 2f / colors.Count -
                    Mathf.PI * .5f;
                Vector2 position = new Vector2(
                    Mathf.Cos(angle),
                    Mathf.Sin(angle)) * WheelOuterRadius;
                Button button = CreateButton(
                    definition.displayName,
                    wheelRoot,
                    GetCircleSprite(),
                    definition.swatch,
                    new Vector2(wheelButtonSize, wheelButtonSize),
                    () => TrySelectColor(colorId));
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchoredPosition = position;
                Image buttonImage = button.GetComponent<Image>();
                buttonImage.raycastTarget = false;
                wheelColorIds.Add(colorId);
                wheelColorImages.Add(buttonImage);
                wheelBaseColors.Add(definition.swatch);

                Text label = CreateText(
                    "Label",
                    button.transform,
                    string.IsNullOrWhiteSpace(definition.displayName)
                        ? colorId
                        : definition.displayName,
                    13,
                    Color.white,
                    new Vector2(wheelButtonSize + 28f, 26f));
                label.rectTransform.anchoredPosition =
                    new Vector2(0f, -wheelButtonSize * .65f);
            }

            if (colors.Count == 0)
            {
                wheelLabel.text = "尚未获得颜色";
            }
        }

        private void UpdateWheelSelection(Vector2 currentPointerPosition)
        {
            if (!wheelOpen || wheelColorIds.Count == 0 || wheelRoot == null)
            {
                return;
            }

            Vector2 center = RectTransformUtility.WorldToScreenPoint(null,
                wheelRoot.position);
            Vector2 delta = currentPointerPosition - center;
            if (delta.sqrMagnitude >= WheelSelectionDeadZone * WheelSelectionDeadZone)
            {
                float angle = Mathf.Atan2(delta.y, delta.x) + Mathf.PI * .5f;
                if (angle < 0f) angle += Mathf.PI * 2f;
                int index = Mathf.Clamp(
                    Mathf.RoundToInt(angle / (Mathf.PI * 2f / wheelColorIds.Count)) % wheelColorIds.Count,
                    0,
                    wheelColorIds.Count - 1);
                highlightedColorId = wheelColorIds[index];
            }

            for (int index = 0; index < wheelColorImages.Count; index++)
            {
                bool highlighted = string.Equals(
                    wheelColorIds[index], highlightedColorId,
                    StringComparison.OrdinalIgnoreCase);
                wheelColorImages[index].color = highlighted
                    ? Color.Lerp(wheelBaseColors[index], Color.white, .45f)
                    : wheelBaseColors[index];
            }

            if (wheelLabel != null)
            {
                wheelLabel.text = string.IsNullOrWhiteSpace(highlightedColorId)
                    ? (HasSelectedColor ? $"当前：{selectedColorId}" : "选择颜色")
                    : $"高亮：{highlightedColorId}";
            }
        }

        private List<ColorTypeDefinition> GetUnlockedColors()
        {
            var result = new List<ColorTypeDefinition>();
            ColorCatalog catalog = ColorRuntimeService.Instance.Catalog;
            if (catalog == null)
            {
                return result;
            }

            for (int index = 0; index < catalog.Colors.Count; index++)
            {
                ColorTypeDefinition definition = catalog.Colors[index];
                if (definition != null &&
                    ColorRuntimeService.Instance.IsUnlocked(definition.id))
                {
                    result.Add(definition);
                }
            }

            return result;
        }

        private void UpdateWheelPosition()
        {
            if (wheelRoot == null)
            {
                return;
            }

            Camera camera = Camera.main;
            Vector3 screen = camera != null
                ? camera.WorldToScreenPoint(transform.position + Vector3.up * 1.1f)
                : new Vector3(Screen.width * .5f, Screen.height * .5f, 0f);
            float half = wheelSize * .5f + 8f;
            screen.x = Mathf.Clamp(screen.x, half, Screen.width - half);
            screen.y = Mathf.Clamp(screen.y, half, Screen.height - half);
            wheelRoot.position = screen;
        }

        private void CreateRangeIndicator()
        {
            rangeRoot = new GameObject("Interaction Range");
            rangeRoot.transform.SetParent(transform, false);
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                rangeMaterial = new Material(shader)
                {
                    name = "Interaction Range Dashed Material"
                };
            }

            int count = Mathf.Max(8, rangeDashCount);
            for (int index = 0; index < count; index++)
            {
                GameObject dashObject = new GameObject(
                    $"Range Dash {index:00}");
                dashObject.transform.SetParent(rangeRoot.transform, false);
                LineRenderer line = dashObject.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.positionCount = 2;
                line.startWidth = rangeLineWidth;
                line.endWidth = rangeLineWidth;
                line.numCapVertices = 2;
                line.startColor = rangeColor;
                line.endColor = rangeColor;
                line.sortingOrder = 1000;
                line.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                if (rangeMaterial != null)
                {
                    line.sharedMaterial = rangeMaterial;
                }
                rangeDashes.Add(line);
            }
        }

        private void RefreshRangeIndicator()
        {
            if (sensor == null || rangeDashes.Count == 0)
            {
                return;
            }

            float radius = Mathf.Max(.1f, sensor.ScanRadius);
            if (Mathf.Abs(radius - rangeRadius) < .001f)
            {
                return;
            }

            rangeRadius = radius;
            int count = rangeDashes.Count;
            float step = Mathf.PI * 2f / count;
            for (int index = 0; index < count; index++)
            {
                float begin = index * step;
                float end = begin + step * .62f;
                rangeDashes[index].SetPosition(
                    0,
                    new Vector3(
                        Mathf.Cos(begin) * radius,
                        Mathf.Sin(begin) * radius,
                        0f));
                rangeDashes[index].SetPosition(
                    1,
                    new Vector3(
                        Mathf.Cos(end) * radius,
                        Mathf.Sin(end) * radius,
                        0f));
            }
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 size,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(action);
            return button;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Sprite sprite,
            Color color,
            Vector2 size)
        {
            GameObject imageObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            imageObject.transform.SetParent(parent, false);
            RectTransform rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            Image image = imageObject.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            Vector2 size)
        {
            GameObject textObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.5f, .5f);
            rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            Texture2D texture = new Texture2D(
                CircleTextureSize,
                CircleTextureSize,
                TextureFormat.RGBA32,
                false);
            texture.name = "Runtime Circle UI Texture";
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(
                (CircleTextureSize - 1) * .5f,
                (CircleTextureSize - 1) * .5f);
            float radius = CircleTextureSize * .5f - 1f;
            for (int y = 0; y < CircleTextureSize; y++)
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float distance = Vector2.Distance(
                    new Vector2(x, y),
                    center);
                texture.SetPixel(
                    x,
                    y,
                    distance <= radius ? Color.white : Color.clear);
            }
            texture.Apply();
            circleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
                new Vector2(.5f, .5f),
                CircleTextureSize);
            circleSprite.name = "Runtime Circle UI Sprite";
            return circleSprite;
        }

        private static Sprite GetRingSprite()
        {
            if (ringSprite != null)
            {
                return ringSprite;
            }

            Texture2D texture = new Texture2D(
                CircleTextureSize,
                CircleTextureSize,
                TextureFormat.RGBA32,
                false);
            texture.name = "Runtime Ring UI Texture";
            texture.filterMode = FilterMode.Bilinear;
            Vector2 center = new Vector2(
                (CircleTextureSize - 1) * .5f,
                (CircleTextureSize - 1) * .5f);
            float outer = CircleTextureSize * .5f - 1f;
            float inner = outer - 7f;
            for (int y = 0; y < CircleTextureSize; y++)
            for (int x = 0; x < CircleTextureSize; x++)
            {
                float distance = Vector2.Distance(
                    new Vector2(x, y), center);
                texture.SetPixel(
                    x,
                    y,
                    distance <= outer && distance >= inner
                        ? Color.white
                        : Color.clear);
            }
            texture.Apply();
            ringSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
                new Vector2(.5f, .5f),
                CircleTextureSize);
            ringSprite.name = "Runtime Ring UI Sprite";
            return ringSprite;
        }

        private void OnDestroy()
        {
            if (rangeMaterial != null)
            {
                Destroy(rangeMaterial);
                rangeMaterial = null;
            }

            if (canvasObject != null)
            {
                Destroy(canvasObject);
                canvasObject = null;
            }

            if (eventSystemObject != null)
            {
                Destroy(eventSystemObject);
                eventSystemObject = null;
            }
        }
    }
}
