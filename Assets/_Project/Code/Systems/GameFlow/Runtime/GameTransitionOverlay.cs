using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.GameFlow
{
    [DisallowMultipleComponent]
    public sealed class GameTransitionOverlay : MonoBehaviour
    {
        private Canvas canvas;
        private CanvasGroup group;
        private RawImage background;
        private RawImage black;
        private TextMeshProUGUI loadingText;
        private Material revealMaterial;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private GameTransitionSettings settings;
        private Texture backgroundTexture;
        private Color backgroundTint = Color.white;
        private Rect backgroundUv = new Rect(0f, 0f, 1f, 1f);
        private float currentRadius;
        private bool initialized;

        public static GameTransitionOverlay Ensure(
            Transform parent,
            GameTransitionSettings settings)
        {
            GameObject root = new GameObject(
                "Game Transition Overlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(GameTransitionOverlay));
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            GameTransitionOverlay overlay =
                root.GetComponent<GameTransitionOverlay>();
            overlay.Initialize(settings);
            return overlay;
        }

        public void Initialize(GameTransitionSettings value)
        {
            settings = value;
            if (!initialized)
            {
                Build();
                initialized = true;
            }
            SetLoadingTextVisible(false);
            SetBackgroundVisible(false);
            SetBlackVisible(false);
        }

        public void SetBlackVisible(bool visible)
        {
            if (black == null)
            {
                return;
            }

            black.gameObject.SetActive(visible);
            black.raycastTarget = visible;
            if (group != null)
            {
                group.blocksRaycasts = visible;
            }
            if (visible)
            {
                currentRadius = 0f;
                ApplyRevealRadius();
            }
        }

        public void ShowBlackImmediate()
        {
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }
            SetBlackVisible(true);
        }

        public void SetLoadingTextVisible(bool visible)
        {
            if (loadingText != null)
            {
                loadingText.gameObject.SetActive(visible);
            }
        }

        public void SetInputBlocking(bool value)
        {
            if (group != null)
            {
                group.blocksRaycasts = value;
            }
        }

        public void SetBackground(
            RawImage source)
        {
            if (source == null)
            {
                return;
            }

            backgroundTexture = source.texture;
            backgroundTint = source.color;
            backgroundUv = source.uvRect;
            ApplyBackground();
        }

        public void SetBackgroundVisible(bool visible)
        {
            if (background != null)
            {
                background.gameObject.SetActive(visible);
                background.raycastTarget = visible;
                if (visible)
                {
                    ApplyBackground();
                }
            }
        }

        public void ApplyBackgroundStateTo(RawImage target)
        {
            if (target == null)
            {
                return;
            }

            target.texture = backgroundTexture;
            target.color = backgroundTint;
            target.uvRect = backgroundUv;
        }

        public IEnumerator RevealFromAsync(
            Transform centerTransform,
            Vector2 viewportCenter,
            float speed,
            float softness)
        {
            if (black == null)
            {
                yield break;
            }

            Vector2 center = centerTransform != null
                ? WorldToViewport(centerTransform.position)
                : viewportCenter;
            center = new Vector2(
                Mathf.Clamp01(center.x),
                Mathf.Clamp01(center.y));
            if (revealMaterial != null)
            {
                revealMaterial.SetVector(
                    "_Center",
                    new Vector4(center.x, center.y, 0f, 0f));
                revealMaterial.SetFloat("_Softness", Mathf.Max(0f, softness));
            }

            float maxRadius = CalculateMaxRadius(center);
            float duration = maxRadius / Mathf.Max(.01f, speed);
            Tween tween = DOTween.To(
                    () => currentRadius,
                    value =>
                    {
                        currentRadius = value;
                        ApplyRevealRadius();
                    },
                    maxRadius,
                    duration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
        }

        public IEnumerator CoverFromCenterAsync(
            Vector2 viewportCenter,
            float speed,
            float softness)
        {
            return CoverFromCenterAsync(
                null,
                viewportCenter,
                speed,
                softness);
        }

        public IEnumerator CoverFromCenterAsync(
            Transform centerTransform,
            Vector2 viewportCenter,
            float speed,
            float softness)
        {
            if (black == null)
            {
                yield break;
            }

            Vector2 resolvedCenter = centerTransform != null
                ? WorldToViewport(centerTransform.position)
                : viewportCenter;
            Vector2 center = new Vector2(
                Mathf.Clamp01(resolvedCenter.x),
                Mathf.Clamp01(resolvedCenter.y));
            if (revealMaterial != null)
            {
                revealMaterial.SetVector(
                    "_Center",
                    new Vector4(center.x, center.y, 0f, 0f));
                revealMaterial.SetFloat("_Softness", Mathf.Max(0f, softness));
            }

            SetInputBlocking(true);
            SetBlackVisible(true);
            currentRadius = CalculateMaxRadius(center);
            ApplyRevealRadius();

            float duration = currentRadius / Mathf.Max(.01f, speed);
            Tween tween = DOTween.To(
                    () => currentRadius,
                    value =>
                    {
                        currentRadius = value;
                        ApplyRevealRadius();
                    },
                    0f,
                    duration)
                .SetEase(Ease.InCubic)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();

            // A negative radius closes the exact center pixel too.
            currentRadius = -.05f;
            ApplyRevealRadius();
        }

        public IEnumerator SlideBackgroundOutAsync(
            float duration,
            Ease ease,
            bool reverseEase = false)
        {
            if (background == null)
            {
                yield break;
            }

            RectTransform rect = background.rectTransform;
            Tween tween = DOTween.To(
                    () => rect.anchoredPosition.x,
                    value => rect.anchoredPosition = new Vector2(
                        value,
                        rect.anchoredPosition.y),
                    Screen.width,
                    Mathf.Max(.01f, duration))
                .SetUpdate(true).SetTarget(this);
            if (reverseEase)
                tween.SetEase((sampleTime, sampleDuration, overshoot, period) =>
                    1f - DOVirtual.EasedValue(0f, 1f, 1f - sampleTime / sampleDuration, ease));
            else tween.SetEase(ease);
            yield return tween.WaitForCompletion();
        }

        public IEnumerator SlideBackgroundInAsync(
            float duration,
            Ease ease)
        {
            if (background == null)
            {
                yield break;
            }

            RectTransform rect = background.rectTransform;
            rect.anchoredPosition = new Vector2(Screen.width, 0f);
            Tween tween = DOTween.To(
                    () => rect.anchoredPosition.x,
                    value => rect.anchoredPosition = new Vector2(
                        value,
                        rect.anchoredPosition.y),
                    0f,
                    Mathf.Max(.01f, duration))
                .SetEase(ease)
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
        }

        public IEnumerator FadeLoadingTextAsync(bool visible)
        {
            if (loadingText == null)
            {
                yield break;
            }
            if (font != null)
            {
                loadingText.font = font;
            }

            CanvasGroup textGroup =
                loadingText.GetComponent<CanvasGroup>();
            if (textGroup == null)
            {
                textGroup = loadingText.gameObject.AddComponent<CanvasGroup>();
            }

            textGroup.alpha = visible ? 0f : 1f;
            loadingText.gameObject.SetActive(true);
            float duration = settings != null
                ? settings.LoadingTextFadeDuration
                : .2f;
            Tween tween = DOTween.To(
                    () => textGroup.alpha,
                    value => textGroup.alpha = value,
                    visible ? 1f : 0f,
                    Mathf.Max(.01f, duration))
                .SetUpdate(true).SetTarget(this);
            yield return tween.WaitForCompletion();
            if (!visible)
            {
                loadingText.gameObject.SetActive(false);
            }
        }

        private void Build()
        {
            canvas = GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = gameObject.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }

            group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = gameObject.AddComponent<CanvasGroup>();
            }
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = false;

            black = CreateRawImage("Black Reveal", transform);
            background = CreateRawImage("Background Copy", transform);
            black.texture = Texture2D.whiteTexture;
            black.color = Color.black;
            revealMaterial = CreateRevealMaterial();
            if (revealMaterial != null)
            {
                black.material = revealMaterial;
            }

            loadingText = CreateLoadingText(transform);
        }

        private RawImage CreateRawImage(string name, Transform parent)
        {
            GameObject item = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(RawImage));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage image = item.GetComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateLoadingText(Transform parent)
        {
            GameObject item = new GameObject(
                "Loading Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            item.transform.SetParent(parent, false);
            RectTransform rect = item.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.text = settings != null
                ? settings.LoadingText
                : "加载中……";
            text.fontSize = settings != null
                ? settings.LoadingFontSize
                : 84f;
            text.color = settings != null
                ? settings.LoadingTextColor
                : Color.white;
            if (settings != null && settings.Font != null)
            {
                text.font = font != null ? font : settings.Font;
            }
            if (settings != null)
            {
                text.outlineColor = settings.LoadingOutlineColor;
                text.outlineWidth = settings.LoadingOutlineWidth;
            }
            return text;
        }

        private Material CreateRevealMaterial()
        {
            Shader shader = settings != null
                ? settings.RevealShader
                : null;
            if (shader == null)
            {
                shader = Shader.Find("UI/GameTransitionReveal");
            }
            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "Game Transition Reveal Material"
            };
            material.SetColor("_Color", Color.black);
            material.SetVector("_Center", new Vector4(.5f, .5f, 0f, 0f));
            material.SetFloat("_Radius", 0f);
            material.SetFloat(
                "_Softness",
                settings != null ? settings.GameplayRevealSoftness : .08f);
            return material;
        }

        private void ApplyBackground()
        {
            if (background == null)
            {
                return;
            }

            // A level opened directly has not captured the menu background yet.
            if (backgroundTexture == null)
            {
                backgroundTexture = Project.StartMenu.GameUiArtwork.Load()?.menuBackground;
            }
            background.texture = backgroundTexture;
            background.color = backgroundTint;
            background.uvRect = backgroundUv;
        }

        private void ApplyRevealRadius()
        {
            if (revealMaterial != null)
            {
                revealMaterial.SetFloat("_Radius", currentRadius);
            }
        }

        private static Vector2 WorldToViewport(Vector3 worldPosition)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return new Vector2(.5f, .5f);
            }

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            return new Vector2(
                screen.x / Mathf.Max(1f, Screen.width),
                screen.y / Mathf.Max(1f, Screen.height));
        }

        private static float CalculateMaxRadius(Vector2 center)
        {
            float aspect = Screen.height <= 0
                ? 1f
                : (float)Screen.width / Screen.height;
            Vector2 edge = new Vector2(
                Mathf.Max(center.x, 1f - center.x) * aspect,
                Mathf.Max(center.y, 1f - center.y));
            return edge.magnitude + .25f;
        }

        public void ResetPresentation()
        {
            DOTween.Kill(this);
            SetLoadingTextVisible(false);
            SetBackgroundVisible(false);
            SetBlackVisible(false);
            SetInputBlocking(false);
            if (background != null) background.rectTransform.anchoredPosition = Vector2.zero;
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            if (revealMaterial != null)
            {
                Destroy(revealMaterial);
            }
        }
    }
}
