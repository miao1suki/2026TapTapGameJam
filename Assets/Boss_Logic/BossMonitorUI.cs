using System.Collections;
using DG.Tweening;
using Project.BlockFeatures;
using UnityEngine;
using UnityEngine.UI;

namespace Project.BossLogic
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("2026TapTap/Boss/Boss 监控窗")]
    public sealed class BossMonitorUI : MonoBehaviour
    {
        [Header("窗口布局")]
        [SerializeField, Range(.05f, .5f)]
        private float screenWidthFraction = 7f / 30f;
        [SerializeField, Range(.05f, .8f)]
        private float screenHeightFraction = .35f;
        [SerializeField, Min(0f)]
        private float frameThickness = 3f;

        [Header("外观")]
        [SerializeField]
        private Color backgroundColor =
            new Color32(18, 22, 28, 230);
        [SerializeField]
        private Color frameColor =
            new Color32(82, 59, 66, 255);
        [SerializeField]
        private Color frameAccentColor =
            new Color32(246, 83, 50, 255);

        [Header("出现与退场")]
        [SerializeField, Min(0f)]
        private float showDuration = .35f;
        [SerializeField, Min(0f)]
        private float hideDuration = .35f;
        [SerializeField]
        private Ease showEase = Ease.OutCubic;
        [SerializeField]
        private Ease hideEase = Ease.InCubic;
        [SerializeField, Min(0f)]
        private float hideDelayAfterDeath = 5f;

        [Header("监控相机")]
        [SerializeField, Min(.1f)]
        private float monitorZoomMultiplier = 1f;
        [SerializeField, Min(.1f)]
        private float fallbackCameraDistance = 10f;
        [SerializeField, Range(.5f, 2f)]
        private float renderResolutionScale = 1.5f;
        [SerializeField, Min(64)]
        private int maximumRenderWidth = 2048;

        private BossController controller;
        private BossHealthBarUI healthBar;
        private Canvas canvas;
        private RectTransform windowRoot;
        private RectTransform revealMask;
        private RectTransform contentRoot;
        private RawImage monitorImage;
        private CanvasGroup canvasGroup;
        private Camera monitorCamera;
        private GameObject monitorCameraObject;
        private RenderTexture renderTexture;
        private Tween revealTween;
        private Coroutine hideRoutine;
        private float fullWidth;
        private float fullHeight;
        private float revealProgress;
        private bool visible;

        public float HideDuration => hideDuration;
        public bool IsVisible => visible;
        public RenderTexture MonitorTexture => renderTexture;

        private void Start()
        {
            canvas = GetComponent<Canvas>();
            controller = GetComponentInParent<BossController>();
            healthBar = GetComponent<BossHealthBarUI>();
            BuildWindow();
            BindController();
            if (controller != null &&
                controller.IsHealthSystemActive)
            {
                Show();
            }
        }

        private void Update()
        {
            UpdateLayout();
        }

        private void LateUpdate()
        {
            if (!visible ||
                monitorCamera == null ||
                CanvasRendererShouldPause())
            {
                return;
            }

            UpdateMonitorCamera();
        }

        private void OnDisable()
        {
            UnbindController();
            revealTween?.Kill();
            revealTween = null;
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            HideImmediate();
        }

        private void OnDestroy()
        {
            ReleaseRenderTexture();
            if (monitorCameraObject != null)
            {
                Destroy(monitorCameraObject);
                monitorCameraObject = null;
            }
        }

        public void Show()
        {
            if (windowRoot == null)
            {
                BuildWindow();
            }

            revealTween?.Kill();
            EnsureMonitorCamera();
            EnsureRenderTexture();
            visible = true;
            windowRoot.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;
            monitorCamera.enabled = true;
            revealProgress = 0f;
            ApplyReveal();
            revealTween = DOTween.To(
                    () => revealProgress,
                    value =>
                    {
                        revealProgress = value;
                        ApplyReveal();
                    },
                    1f,
                    showDuration)
                .SetEase(showEase);
            healthBar?.ShowHud();
        }

        public void Hide()
        {
            if (windowRoot == null || !visible)
            {
                return;
            }

            revealTween?.Kill();
            revealTween = DOTween.To(
                    () => revealProgress,
                    value =>
                    {
                        revealProgress = value;
                        ApplyReveal();
                    },
                    0f,
                    hideDuration)
                .SetEase(hideEase)
                .OnComplete(HideImmediate);
            healthBar?.HideHud(hideDuration);
        }

        public void HideImmediate()
        {
            visible = false;
            revealTween?.Kill();
            revealTween = null;
            revealProgress = 0f;
            ApplyReveal();
            if (windowRoot != null)
            {
                windowRoot.gameObject.SetActive(false);
            }
            if (monitorCamera != null)
            {
                monitorCamera.enabled = false;
            }
            ReleaseRenderTexture();
        }

        private void BindController()
        {
            if (controller == null)
            {
                return;
            }

            controller.HealthSystemActivated -=
                HandleHealthSystemActivated;
            controller.HealthSystemActivated +=
                HandleHealthSystemActivated;
            controller.Died -= HandleBossDied;
            controller.Died += HandleBossDied;
            controller.RespawnStateApplied -=
                HandleBossRespawnStateApplied;
            controller.RespawnStateApplied +=
                HandleBossRespawnStateApplied;
        }

        private void UnbindController()
        {
            if (controller == null)
            {
                return;
            }

            controller.HealthSystemActivated -=
                HandleHealthSystemActivated;
            controller.Died -= HandleBossDied;
            controller.RespawnStateApplied -=
                HandleBossRespawnStateApplied;
        }

        private void HandleHealthSystemActivated()
        {
            Show();
        }

        private void HandleBossDied()
        {
            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
            }
            hideRoutine = StartCoroutine(
                HideAfterDeathRoutine());
        }

        private void HandleBossRespawnStateApplied(
            float health,
            Vector3 position)
        {
            if (health <= 0f)
            {
                return;
            }

            if (hideRoutine != null)
            {
                StopCoroutine(hideRoutine);
                hideRoutine = null;
            }

            healthBar?.ShowHud();
            Show();
        }

        private IEnumerator HideAfterDeathRoutine()
        {
            if (hideDelayAfterDeath > 0f)
            {
                yield return new WaitForSeconds(
                    hideDelayAfterDeath);
            }

            Hide();
            hideRoutine = null;
        }

        private void BuildWindow()
        {
            if (windowRoot != null)
            {
                return;
            }

            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            GameObject root = new GameObject(
                "Boss Monitor Window",
                typeof(RectTransform),
                typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            windowRoot = (RectTransform)root.transform;
            windowRoot.anchorMin = new Vector2(0f, 1f);
            windowRoot.anchorMax = new Vector2(0f, 1f);
            windowRoot.pivot = new Vector2(0f, 1f);
            windowRoot.anchoredPosition = Vector2.zero;
            canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject maskObject = new GameObject(
                "Reveal Mask",
                typeof(RectTransform),
                typeof(RectMask2D));
            maskObject.transform.SetParent(windowRoot, false);
            revealMask = (RectTransform)maskObject.transform;
            revealMask.anchorMin = new Vector2(.5f, .5f);
            revealMask.anchorMax = new Vector2(.5f, .5f);
            revealMask.pivot = new Vector2(.5f, .5f);
            revealMask.anchoredPosition = Vector2.zero;

            GameObject content = new GameObject(
                "Window Content",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            content.transform.SetParent(revealMask, false);
            contentRoot = (RectTransform)content.transform;
            contentRoot.anchorMin = new Vector2(.5f, .5f);
            contentRoot.anchorMax = new Vector2(.5f, .5f);
            contentRoot.pivot = new Vector2(.5f, .5f);
            contentRoot.anchoredPosition = Vector2.zero;
            Image background = content.GetComponent<Image>();
            background.color = backgroundColor;
            background.raycastTarget = false;

            GameObject feed = new GameObject(
                "Monitor Feed",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            feed.transform.SetParent(contentRoot, false);
            RectTransform feedRect =
                (RectTransform)feed.transform;
            feedRect.anchorMin = Vector2.zero;
            feedRect.anchorMax = Vector2.one;
            feedRect.pivot = new Vector2(.5f, .5f);
            feedRect.offsetMin =
                new Vector2(frameThickness, frameThickness);
            feedRect.offsetMax =
                new Vector2(-frameThickness, -frameThickness);
            monitorImage = feed.GetComponent<RawImage>();
            monitorImage.color = Color.white;
            monitorImage.raycastTarget = false;

            BuildFrame();
            UpdateLayout(true);
            ApplyReveal();
            windowRoot.gameObject.SetActive(false);
        }

        private void BuildFrame()
        {
            float thickness = Mathf.Max(1f, frameThickness);
            CreateBorder(
                contentRoot,
                "Frame Top",
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(.5f, 1f),
                Vector2.zero,
                new Vector2(0f, thickness),
                Color.black);
            CreateBorder(
                contentRoot,
                "Frame Bottom",
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(.5f, 0f),
                Vector2.zero,
                new Vector2(0f, thickness),
                Color.black);
            CreateBorder(
                contentRoot,
                "Frame Left",
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(0f, .5f),
                Vector2.zero,
                new Vector2(thickness, 0f),
                frameColor);
            CreateBorder(
                contentRoot,
                "Frame Right",
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(1f, .5f),
                Vector2.zero,
                new Vector2(thickness, 0f),
                frameColor);

            RectTransform accentTop =
                CreateBorder(
                    contentRoot,
                    "Accent Top Left",
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    Vector2.zero,
                    new Vector2(28f, thickness),
                    frameAccentColor);
            accentTop.anchoredPosition =
                new Vector2(thickness, -thickness);
            RectTransform accentBottom =
                CreateBorder(
                    contentRoot,
                    "Accent Bottom Right",
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 0f),
                    Vector2.zero,
                    new Vector2(28f, thickness),
                    frameAccentColor);
            accentBottom.anchoredPosition =
                new Vector2(-thickness, thickness);
        }

        private static RectTransform CreateBorder(
            Transform parent,
            string objectName,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            GameObject item = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            item.transform.SetParent(parent, false);
            RectTransform rect =
                (RectTransform)item.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        private void UpdateLayout(bool force = false)
        {
            if (windowRoot == null || canvas == null)
            {
                return;
            }

            RectTransform canvasRect =
                (RectTransform)canvas.transform;
            float nextWidth =
                canvasRect.rect.width *
                screenWidthFraction;
            float nextHeight =
                canvasRect.rect.height *
                screenHeightFraction;
            if (!force &&
                Mathf.Abs(nextWidth - fullWidth) < .01f &&
                Mathf.Abs(nextHeight - fullHeight) < .01f)
            {
                return;
            }

            fullWidth = Mathf.Max(1f, nextWidth);
            fullHeight = Mathf.Max(1f, nextHeight);
            windowRoot.sizeDelta =
                new Vector2(fullWidth, fullHeight);
            contentRoot.sizeDelta =
                new Vector2(fullWidth, fullHeight);
            ApplyReveal();
            if (visible)
            {
                EnsureRenderTexture();
            }
        }

        private void ApplyReveal()
        {
            if (revealMask == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(revealProgress);
            revealMask.sizeDelta = new Vector2(
                fullWidth * progress,
                fullHeight * progress);
        }

        private void EnsureMonitorCamera()
        {
            if (monitorCameraObject == null)
            {
                monitorCameraObject = new GameObject(
                    "Boss Monitor Camera");
                monitorCamera = monitorCameraObject
                    .AddComponent<Camera>();
                monitorCamera.orthographic = true;
                monitorCamera.clearFlags =
                    CameraClearFlags.SolidColor;
                monitorCamera.backgroundColor =
                    backgroundColor;
                monitorCamera.allowHDR = false;
                monitorCamera.allowMSAA = false;
                monitorCamera.depth = -20f;
            }
        }

        private void EnsureRenderTexture()
        {
            if (monitorImage == null)
            {
                return;
            }

            float scaleFactor =
                canvas != null
                    ? canvas.scaleFactor
                    : 1f;
            int width = Mathf.Clamp(
                Mathf.RoundToInt(
                    fullWidth *
                    scaleFactor *
                    renderResolutionScale),
                64,
                Mathf.Max(64, maximumRenderWidth));
            int height = Mathf.Max(
                64,
                Mathf.RoundToInt(
                    width *
                    fullHeight /
                    Mathf.Max(1f, fullWidth)));
            if (renderTexture != null &&
                renderTexture.width == width &&
                renderTexture.height == height)
            {
                return;
            }

            ReleaseRenderTexture();
            renderTexture = new RenderTexture(
                width,
                height,
                16,
                RenderTextureFormat.ARGB32)
            {
                name = "Boss Monitor",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false
            };
            renderTexture.Create();
            monitorImage.texture = renderTexture;
            if (monitorCamera != null)
            {
                monitorCamera.targetTexture =
                    renderTexture;
            }
            int uiLayer =
                LayerMask.NameToLayer("UI");
            monitorCamera.cullingMask =
                uiLayer >= 0
                    ? ~(1 << uiLayer)
                    : ~0;
        }

        private void UpdateMonitorCamera()
        {
            if (controller == null)
            {
                controller =
                    ResolveBossController();
            }
            if (controller == null)
            {
                return;
            }

            Camera main = Camera.main;
            Vector3 bossPosition =
                controller.WorldPosition;
            float yaw = main != null
                ? main.transform.eulerAngles.y
                : 0f;
            Quaternion rotation =
                Quaternion.Euler(0f, yaw, 0f);
            float distance = main != null
                ? Mathf.Abs(
                    main.transform.position.z -
                    bossPosition.z)
                : fallbackCameraDistance;
            if (distance < .1f)
            {
                distance = fallbackCameraDistance;
            }

            Vector3 forward = rotation * Vector3.forward;
            monitorCamera.transform.SetPositionAndRotation(
                bossPosition - forward * distance,
                rotation);
            monitorCamera.orthographic = true;
            float mainSize =
                main != null && main.orthographic
                    ? main.orthographicSize
                    : 5f;
            monitorCamera.orthographicSize =
                Mathf.Max(
                    .01f,
                    mainSize * monitorZoomMultiplier);
        }

        private BossController ResolveBossController()
        {
            IBossRespawnStateReceiver receiver =
                BossRespawnStateRegistry.FindAvailable();
            return receiver as BossController;
        }

        private bool CanvasRendererShouldPause()
        {
            return canvas != null && !canvas.enabled;
        }

        private void ReleaseRenderTexture()
        {
            if (monitorCamera != null)
            {
                monitorCamera.targetTexture = null;
            }
            if (monitorImage != null)
            {
                monitorImage.texture = null;
            }
            if (renderTexture == null)
            {
                return;
            }

            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }
}
