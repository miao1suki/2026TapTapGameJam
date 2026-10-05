using System.Collections;
using System.Collections.Generic;
using Project.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Subtitles
{
    [DisallowMultipleComponent]
    public sealed class SubtitleManager : MonoBehaviour,
        ISubtitleService
    {
        private sealed class SubtitleView
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public string FullText;
        }

        private static SubtitleManager instance;
        private readonly Dictionary<SubtitleLayer, SubtitleView> views =
            new Dictionary<SubtitleLayer, SubtitleView>();
        private readonly Dictionary<SubtitleLayer, Coroutine> routines =
            new Dictionary<SubtitleLayer, Coroutine>();
        private readonly Dictionary<SubtitleLayer, SubtitleStyle>
            activeStyles =
                new Dictionary<SubtitleLayer, SubtitleStyle>();

        [SerializeField] private SubtitleStyle centerDefault =
            new SubtitleStyle
            {
                fontSize = 56f,
                anchor = SubtitleAnchor.Center,
                normalizedX = .5f,
                normalizedY = .5f
            };
        [SerializeField] private SubtitleStyle bottomDefault =
            new SubtitleStyle
            {
                fontSize = 28f,
                anchor = SubtitleAnchor.Bottom,
                normalizedX = .5f,
                normalizedY = .12f,
                inAnimation = SubtitleAnimation.Fade,
                outAnimation = SubtitleAnimation.Fade
            };
        [SerializeField] private int canvasSortingOrder = 1000;
        [SerializeField] private bool useUnscaledTime = true;

        private Canvas canvas;

        public static SubtitleManager Instance
        {
            get
            {
                if (instance != null)
                {
                    return instance;
                }

                SubtitleManager existing =
                    ProjectDiscovery.FindFirst<SubtitleManager>();
                if (existing != null)
                {
                    return existing;
                }

                GameObject root = new GameObject("Subtitle Manager");
                return root.AddComponent<SubtitleManager>();
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            SubtitleServiceRegistry.Register(this);
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
        }

        private void OnDestroy()
        {
            SubtitleServiceRegistry.Unregister(this);
            if (instance == this)
            {
                instance = null;
            }
        }

        public void Show(SubtitleCue cue)
        {
            if (cue == null || string.IsNullOrWhiteSpace(cue.text))
            {
                return;
            }

            SubtitleStyle style = cue.useManagerDefaults ||
                                  cue.style == null
                ? GetDefault(cue.layer)
                : cue.style;
            Show(cue.layer, cue.text, style);
        }

        public void ShowLarge(
            string text,
            SubtitleStyle style = null)
        {
            Show(
                SubtitleLayer.Center,
                text,
                style ?? centerDefault);
        }

        public void ShowBottom(
            string text,
            SubtitleStyle style = null)
        {
            Show(
                SubtitleLayer.Bottom,
                text,
                style ?? bottomDefault);
        }

        public void Hide(
            SubtitleLayer layer,
            bool immediate = false)
        {
            if (!views.TryGetValue(layer, out SubtitleView view) ||
                view == null)
            {
                return;
            }

            StopRoutine(layer);
            if (immediate)
            {
                view.Text.text = string.Empty;
                view.Group.alpha = 0f;
                view.Text.gameObject.SetActive(false);
                return;
            }

            routines[layer] = StartCoroutine(
                HideRoutine(layer, view));
        }

        public void HideAll(bool immediate = false)
        {
            Hide(SubtitleLayer.Center, immediate);
            Hide(SubtitleLayer.Bottom, immediate);
        }

        public bool TryGetCameraViewport(
            out CameraViewportInfo info)
        {
            ICameraViewportSource source =
                ProjectDiscovery.FindFirstInterface<
                    ICameraViewportSource>();
            if (source == null)
            {
                info = default;
                return false;
            }

            return source.TryGetViewport(out info);
        }

        private void Show(
            SubtitleLayer layer,
            string text,
            SubtitleStyle style)
        {
            EnsureCanvas();
            SubtitleView view = EnsureView(layer);
            if (view == null)
            {
                return;
            }

            StopRoutine(layer);
            view.FullText = text;
            view.Text.text = text;
            view.Text.font = ResolveFont(style);
            view.Text.fontSize = style.fontSize;
            view.Text.color = style.color;
            view.Text.raycastTarget = false;
            view.Text.gameObject.SetActive(true);
            ApplyAnchor(view.Rect, style);
            activeStyles[layer] = style;

            routines[layer] = StartCoroutine(
                ShowRoutine(layer, view, style));
        }

        private IEnumerator ShowRoutine(
            SubtitleLayer layer,
            SubtitleView view,
            SubtitleStyle style)
        {
            Vector2 basePosition = view.Rect.anchoredPosition;
            float duration = Mathf.Max(0f, style.inDuration);
            float elapsed = 0f;
            ApplyAnimation(
                view,
                style,
                style.inAnimation,
                0f,
                0f,
                true,
                basePosition);
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float progress = duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / duration);
                ApplyAnimation(
                    view,
                    style,
                    style.inAnimation,
                    Evaluate(style.inCurve, progress),
                    elapsed,
                    true,
                    basePosition);
                yield return null;
            }

            ApplyAnimation(
                view,
                style,
                style.inAnimation,
                1f,
                duration,
                true,
                basePosition);
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(
                    Mathf.Max(0f, style.holdDuration));
            }
            else
            {
                yield return new WaitForSeconds(
                    Mathf.Max(0f, style.holdDuration));
            }

            duration = Mathf.Max(0f, style.outDuration);
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float progress = duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / duration);
                ApplyAnimation(
                    view,
                    style,
                    style.outAnimation,
                    Evaluate(style.outCurve, progress),
                    elapsed,
                    false,
                    basePosition);
                yield return null;
            }

            ApplyAnimation(
                view,
                style,
                style.outAnimation,
                1f,
                duration,
                false,
                basePosition);
            view.Text.text = string.Empty;
            view.Text.gameObject.SetActive(false);
            activeStyles.Remove(layer);
            routines.Remove(layer);
        }

        private IEnumerator HideRoutine(
            SubtitleLayer layer,
            SubtitleView view)
        {
            SubtitleStyle style =
                activeStyles.TryGetValue(
                    layer,
                    out SubtitleStyle activeStyle)
                    ? activeStyle
                    : GetDefault(layer);
            Vector2 basePosition = view.Rect.anchoredPosition;
            float duration = Mathf.Max(0f, style.outDuration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float progress = duration <= 0f
                    ? 1f
                    : Mathf.Clamp01(elapsed / duration);
                ApplyAnimation(
                    view,
                    style,
                    style.outAnimation,
                    Evaluate(style.outCurve, progress),
                    elapsed,
                    false,
                    basePosition);
                yield return null;
            }

            view.Text.text = string.Empty;
            view.Group.alpha = 0f;
            view.Text.gameObject.SetActive(false);
            activeStyles.Remove(layer);
            routines.Remove(layer);
        }

        private void ApplyAnimation(
            SubtitleView view,
            SubtitleStyle style,
            SubtitleAnimation animation,
            float progress,
            float elapsedSeconds,
            bool entering,
            Vector2 basePosition)
        {
            float alpha = 1f;
            float motion = style.motionDistance;
            Vector2 position = basePosition;
            Vector3 scale = Vector3.one;
            float rotation = 0f;
            switch (animation)
            {
                case SubtitleAnimation.None:
                    break;
                case SubtitleAnimation.Fade:
                    alpha = entering ? progress : 1f - progress;
                    break;
                case SubtitleAnimation.FadeSlideUp:
                    alpha = entering ? progress : 1f - progress;
                    position += Vector2.down * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.FadeSlideDown:
                    alpha = entering ? progress : 1f - progress;
                    position += Vector2.up * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.Scale:
                    alpha = entering ? progress : 1f - progress;
                    scale = Vector3.one * Mathf.Lerp(
                        style.scaleFrom,
                        1f,
                        entering ? progress : 1f - progress);
                    break;
                case SubtitleAnimation.SlideLeft:
                    position += Vector2.right * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.SlideRight:
                    position += Vector2.left * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.FadeSlideLeft:
                    alpha = entering ? progress : 1f - progress;
                    position += Vector2.right * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.FadeSlideRight:
                    alpha = entering ? progress : 1f - progress;
                    position += Vector2.left * motion *
                                (entering
                                    ? 1f - progress
                                    : progress);
                    break;
                case SubtitleAnimation.PopIn:
                    alpha = entering ? progress : 1f - progress;
                    float pop = Mathf.Sin(
                        Mathf.Clamp01(progress) * Mathf.PI) *
                                style.overshoot;
                    scale = Vector3.one * Mathf.Max(
                        .01f,
                        Mathf.Lerp(
                            style.scaleFrom,
                            1f,
                            entering ? progress : 1f - progress) + pop);
                    break;
                case SubtitleAnimation.BounceIn:
                    alpha = entering ? progress : 1f - progress;
                    float bounce = Mathf.Abs(
                        Mathf.Sin(
                            Mathf.Clamp01(progress) *
                            Mathf.PI *
                            2f)) *
                                  (1f - Mathf.Clamp01(progress)) *
                                  style.overshoot;
                    scale = Vector3.one * Mathf.Max(
                        .01f,
                        Mathf.Lerp(
                            style.scaleFrom,
                            1f,
                            entering ? progress : 1f - progress) + bounce);
                    position += Vector2.up * motion * .18f *
                                Mathf.Sin(
                                    Mathf.Clamp01(progress) *
                                    Mathf.PI);
                    break;
                case SubtitleAnimation.RotateIn:
                    alpha = entering ? progress : 1f - progress;
                    rotation = entering
                        ? Mathf.Lerp(
                            style.startRotation,
                            0f,
                            Mathf.Clamp01(progress))
                        : Mathf.Lerp(
                            0f,
                            style.startRotation,
                            Mathf.Clamp01(progress));
                    break;
                case SubtitleAnimation.Typewriter:
                    if (entering)
                    {
                        string fullText = view.FullText ?? string.Empty;
                        int characterCount = progress >= 1f
                            ? fullText.Length
                            : Mathf.Clamp(
                                Mathf.FloorToInt(
                                    elapsedSeconds *
                                    Mathf.Max(
                                        1f,
                                        style.charactersPerSecond)),
                                0,
                                fullText.Length);
                        view.Text.text = fullText.Substring(
                            0,
                            characterCount);
                    }
                    else
                    {
                        view.Text.text = view.FullText ?? string.Empty;
                        alpha = 1f - progress;
                    }
                    break;
                case SubtitleAnimation.Glitch:
                    alpha = entering
                        ? Mathf.Lerp(.35f, 1f, progress)
                        : Mathf.Lerp(1f, 0f, progress);
                    float glitchTime = useUnscaledTime
                        ? Time.unscaledTime
                        : Time.time;
                    float glitchSeed = glitchTime * 60f;
                    float glitchAmount =
                        motion * style.glitchStrength;
                    position += new Vector2(
                        (Mathf.PerlinNoise(glitchSeed, 0f) - .5f) *
                        glitchAmount,
                        (Mathf.PerlinNoise(0f, glitchSeed) - .5f) *
                        glitchAmount);
                    alpha *= Mathf.Lerp(
                        1f,
                        .45f,
                        style.glitchStrength);
                    alpha *= .8f +
                             .2f * Mathf.PerlinNoise(
                                glitchSeed * 2f,
                                1f);
                    break;
                case SubtitleAnimation.Float:
                    alpha = entering ? progress : 1f - progress;
                    float floatPhase = Mathf.Sin(
                        progress *
                        Mathf.PI *
                        2f *
                        Mathf.Max(.1f, style.floatSpeed));
                    float floatWeight =
                        1f - Mathf.Clamp01(progress);
                    position += Vector2.up *
                                floatPhase *
                                floatWeight *
                                style.floatAmplitude;
                    break;
                case SubtitleAnimation.Pulse:
                    alpha = entering ? progress : 1f - progress;
                    float pulse = Mathf.Sin(
                        progress *
                        Mathf.PI *
                        Mathf.Max(1f, style.floatSpeed)) *
                                  (1f - Mathf.Clamp01(progress)) *
                                  style.overshoot;
                    scale = Vector3.one * (1f + pulse);
                    break;
            }

            float clampedAlpha = Mathf.Clamp01(alpha);
            view.Group.alpha = clampedAlpha;
            if (view.Text != null)
            {
                view.Text.alpha = clampedAlpha;
            }
            view.Rect.anchoredPosition = position;
            view.Rect.localScale = scale;
            view.Rect.localEulerAngles =
                new Vector3(0f, 0f, rotation);
        }

        private void ApplyAnchor(
            RectTransform rect,
            SubtitleStyle style)
        {
            Vector2 anchor = style.anchor switch
            {
                SubtitleAnchor.Bottom => new Vector2(.5f, .12f),
                SubtitleAnchor.Top => new Vector2(.5f, .88f),
                SubtitleAnchor.TopLeft => new Vector2(.12f, .88f),
                SubtitleAnchor.TopRight => new Vector2(.88f, .88f),
                SubtitleAnchor.MiddleLeft => new Vector2(.12f, .5f),
                SubtitleAnchor.MiddleRight => new Vector2(.88f, .5f),
                SubtitleAnchor.BottomLeft => new Vector2(.12f, .12f),
                SubtitleAnchor.BottomRight => new Vector2(.88f, .12f),
                SubtitleAnchor.Custom => new Vector2(
                    style.normalizedX,
                    style.normalizedY),
                _ => new Vector2(.5f, .5f)
            };
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = style.offset;
        }

        private SubtitleView EnsureView(SubtitleLayer layer)
        {
            if (views.TryGetValue(layer, out SubtitleView existing) &&
                existing != null)
            {
                return existing;
            }

            GameObject textObject = new GameObject(
                layer == SubtitleLayer.Center
                    ? "Center Text"
                    : "Bottom Text",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(canvas.transform, false);
            RectTransform rect =
                textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1400f, 220f);
            TextMeshProUGUI text =
                textObject.GetComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            CanvasGroup group =
                textObject.GetComponent<CanvasGroup>();
            var view = new SubtitleView
            {
                Rect = rect,
                Group = group,
                Text = text
            };
            textObject.SetActive(false);
            views[layer] = view;
            return view;
        }

        private void EnsureCanvas()
        {
            if (canvas != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject(
                "Subtitle Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler =
                canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
        }

        private SubtitleStyle GetDefault(SubtitleLayer layer)
        {
            return layer == SubtitleLayer.Center
                ? centerDefault
                : bottomDefault;
        }

        private static TMP_FontAsset ResolveFont(
            SubtitleStyle style)
        {
            return style != null && style.font != null
                ? style.font
                : TMP_Settings.defaultFontAsset;
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;
        }

        private static float Evaluate(
            AnimationCurve curve,
            float progress)
        {
            return curve == null || curve.length < 2
                ? progress
                : curve.Evaluate(progress);
        }

        private void StopRoutine(SubtitleLayer layer)
        {
            if (routines.TryGetValue(layer, out Coroutine routine) &&
                routine != null)
            {
                StopCoroutine(routine);
            }
            routines.Remove(layer);
        }
    }
}
