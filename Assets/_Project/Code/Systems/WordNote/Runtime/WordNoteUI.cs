using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.WordNote
{
    public enum WordNotePosition
    {
        [InspectorName("居中")] Center = 0,
        [InspectorName("中间左侧")] MiddleLeft = 1,
        [InspectorName("中间右侧")] MiddleRight = 2,
        [InspectorName("下方")] Bottom = 3,
        [InspectorName("下方左侧")] BottomLeft = 4,
        [InspectorName("下方右侧")] BottomRight = 5,
        [InspectorName("上方")] Top = 6,
        [InspectorName("上方左侧")] TopLeft = 7,
        [InspectorName("上方右侧")] TopRight = 8
    }

    public enum WordNoteDisplayMode
    {
        [InspectorName("驻留")] Persistent = 0,
        [InspectorName("显示一段时间")] Timed = 1,
        [InspectorName("转瞬即逝")] Flash = 2
    }

    public enum WordNoteAnimation
    {
        [InspectorName("无")] None = 0,
        [InspectorName("原位显隐")] Fade = 1,
        [InspectorName("从左侧移动")] MoveLeft = 2,
        [InspectorName("从右侧移动")] MoveRight = 3,
        [InspectorName("从上方移动")] MoveUp = 4,
        [InspectorName("从下方移动")] MoveDown = 5,
        [InspectorName("左侧淡入淡出")] FadeMoveLeft = 6,
        [InspectorName("右侧淡入淡出")] FadeMoveRight = 7,
        [InspectorName("上方淡入淡出")] FadeMoveUp = 8,
        [InspectorName("下方淡入淡出")] FadeMoveDown = 9,
        [InspectorName("缩放")] Scale = 10,
        [InspectorName("缩放淡入淡出")] FadeScale = 11,
        [InspectorName("打字机")] Typewriter = 12
    }

    public enum WordNoteColor
    {
        [InspectorName("红")] Red = 0,
        [InspectorName("橙")] Orange = 1,
        [InspectorName("黄")] Yellow = 2,
        [InspectorName("绿")] Green = 3,
        [InspectorName("青")] Cyan = 4,
        [InspectorName("蓝")] Blue = 5,
        [InspectorName("紫")] Purple = 6,
        [InspectorName("白")] White = 7,
        [InspectorName("黑")] Black = 8
    }

    public enum WordNoteMessageState
    {
        [InspectorName("等待")] Queued = 0,
        [InspectorName("入场中")] Entering = 1,
        [InspectorName("显示中")] Showing = 2,
        [InspectorName("退场中")] Exiting = 3,
        [InspectorName("已结束")] Finished = 4
    }

    public enum WordNoteHorizontalDirection
    {
        [InspectorName("左")] Left = -1,
        [InspectorName("右")] Right = 1
    }

    public enum WordNoteVerticalDirection
    {
        [InspectorName("下")] Down = -1,
        [InspectorName("上")] Up = 1
    }

    [Serializable]
    public sealed class WordNoteMotionSettings
    {
        public bool useCombinedChannels;
        public bool useFade = true;
        public bool useHorizontalMove;
        public WordNoteHorizontalDirection horizontalDirection =
            WordNoteHorizontalDirection.Left;
        [Min(0f)] public float horizontalDistance = 140f;
        public bool useVerticalMove;
        public WordNoteVerticalDirection verticalDirection =
            WordNoteVerticalDirection.Down;
        [Min(0f)] public float verticalDistance = 80f;
        public bool useScale;
        public bool useTypewriter;
        [Min(1f)] public float typewriterCharactersPerSecond = 20f;
        [Min(0f)] public float duration = .25f;
        public Vector2 startPosition;
        public Vector2 endPosition;
        [Range(0f, 1f)] public float startAlpha = 1f;
        [Range(0f, 1f)] public float endAlpha = 1f;
        public Vector3 startScale = Vector3.one;
        public Vector3 endScale = Vector3.one;
        public float startSpeed;
        public float endSpeed;
        [Range(.01f, 10f)] public float easePower = 1f;

        public WordNoteMotionSettings Clone()
        {
            return new WordNoteMotionSettings
            {
                duration = duration,
                useCombinedChannels = useCombinedChannels,
                useFade = useFade,
                useHorizontalMove = useHorizontalMove,
                horizontalDirection = horizontalDirection,
                horizontalDistance = horizontalDistance,
                useVerticalMove = useVerticalMove,
                verticalDirection = verticalDirection,
                verticalDistance = verticalDistance,
                useScale = useScale,
                useTypewriter = useTypewriter,
                typewriterCharactersPerSecond =
                    typewriterCharactersPerSecond,
                startPosition = startPosition,
                endPosition = endPosition,
                startAlpha = startAlpha,
                endAlpha = endAlpha,
                startScale = startScale,
                endScale = endScale,
                startSpeed = startSpeed,
                endSpeed = endSpeed,
                easePower = easePower
            };
        }
    }

    [Serializable]
    public sealed class WordNoteStyle
    {
        public TMP_FontAsset font;
        [Min(1f)] public float fontSize = 42f;
        public bool richText = true;
        public WordNoteColor fontColor = WordNoteColor.Yellow;
        public bool useOutline;
        public WordNoteColor outlineColor = WordNoteColor.Blue;
        public WordNotePosition position = WordNotePosition.Center;
        public bool participateInStackLayout = true;
        public WordNoteDisplayMode displayMode =
            WordNoteDisplayMode.Timed;
        [Min(0f)] public float displayDuration = 2f;
        public bool useEnterAnimation = true;
        public WordNoteAnimation enterAnimation =
            WordNoteAnimation.FadeMoveDown;
        public WordNoteMotionSettings enterMotion =
            new WordNoteMotionSettings
            {
                duration = .22f,
                startPosition = Vector2.zero,
                endPosition = Vector2.zero,
                startAlpha = 0f,
                endAlpha = 1f,
                startScale = Vector3.one,
                endScale = Vector3.one
            };
        public bool useExitAnimation = true;
        public WordNoteAnimation exitAnimation =
            WordNoteAnimation.Fade;
        public WordNoteMotionSettings exitMotion =
            new WordNoteMotionSettings
            {
                duration = .18f,
                startPosition = Vector2.zero,
                endPosition = Vector2.zero,
                startAlpha = 1f,
                endAlpha = 0f,
                startScale = Vector3.one,
                endScale = new Vector3(.96f, .96f, 1f)
            };

        public WordNoteStyle Clone()
        {
            return new WordNoteStyle
            {
                font = font,
                fontSize = fontSize,
                richText = richText,
                fontColor = fontColor,
                useOutline = useOutline,
                outlineColor = outlineColor,
                position = position,
                participateInStackLayout = participateInStackLayout,
                displayMode = displayMode,
                displayDuration = displayDuration,
                useEnterAnimation = useEnterAnimation,
                enterAnimation = enterAnimation,
                enterMotion = enterMotion != null
                    ? enterMotion.Clone()
                    : new WordNoteMotionSettings(),
                useExitAnimation = useExitAnimation,
                exitAnimation = exitAnimation,
                exitMotion = exitMotion != null
                    ? exitMotion.Clone()
                    : new WordNoteMotionSettings()
            };
        }
    }

    internal sealed class WordNoteMessage
    {
        public int Id;
        public string Content;
        public WordNoteStyle Style;
        public WordNoteMessageState State;
        public bool ExitRequested;
        public bool PlayExitAnimation;
        public bool ImmediateExitRequested;
        public Vector2 StackOffset;
        public Tween CurrentTween;
        public Tween LayoutTween;
        public WordNoteView View;
        public WordNoteHandle Handle;
    }

    internal sealed class WordNoteView
    {
        public RectTransform Rect;
        public CanvasGroup Group;
        public TextMeshProUGUI Text;
        public Material RuntimeMaterial;
    }

    public sealed class WordNoteHandle
    {
        private readonly WordNoteUI owner;
        private readonly WordNoteMessage message;

        internal WordNoteHandle(
            WordNoteUI valueOwner,
            WordNoteMessage valueMessage)
        {
            owner = valueOwner;
            message = valueMessage;
        }

        public int Id => message.Id;
        public string Content => message.Content;
        public WordNotePosition Position => message.Style.position;
        public bool ParticipatesInStackLayout =>
            message.Style.participateInStackLayout;
        public WordNoteMessageState State => message.State;
        public bool IsFinished =>
            message.State == WordNoteMessageState.Finished;

        public bool ForceExit()
        {
            return owner.ExitById(Id, true);
        }

        public bool ForceExit(bool playExitAnimation)
        {
            return owner.ExitById(Id, playExitAnimation);
        }

        public bool ForceDisappear()
        {
            return owner.ExitById(Id, false);
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    [AddComponentMenu("2026TapTap/UI/Word Note UI")]
    public sealed class WordNoteUI : MonoBehaviour
    {
        private static readonly WordNotePosition[] AllPositions =
            (WordNotePosition[])Enum.GetValues(
                typeof(WordNotePosition));

        [Header("默认字体和显示")]
        [SerializeField] private TMP_FontAsset defaultFont;
        [SerializeField, Min(1f)] private float defaultFontSize = 42f;
        [SerializeField] private WordNoteColor defaultFontColor =
            WordNoteColor.Yellow;
        [SerializeField] private bool defaultUseOutline;
        [SerializeField] private WordNoteColor defaultOutlineColor =
            WordNoteColor.Blue;
        [SerializeField] private WordNotePosition defaultPosition =
            WordNotePosition.Center;
        [SerializeField] private bool defaultParticipateInStackLayout = true;
        [SerializeField] private WordNoteDisplayMode defaultDisplayMode =
            WordNoteDisplayMode.Timed;
        [SerializeField, Min(0f)] private float defaultDisplayDuration = 2f;
        [SerializeField] private bool defaultUseEnterAnimation = true;
        [SerializeField] private WordNoteAnimation defaultEnterAnimation =
            WordNoteAnimation.FadeMoveDown;
        [SerializeField] private bool defaultUseExitAnimation = true;
        [SerializeField] private WordNoteAnimation defaultExitAnimation =
            WordNoteAnimation.Fade;
        [SerializeField] private WordNoteMotionSettings defaultEnterMotion =
            new WordNoteMotionSettings
            {
                duration = .22f,
                startPosition = Vector2.zero,
                endPosition = Vector2.zero,
                startAlpha = 0f,
                endAlpha = 1f,
                startScale = Vector3.one,
                endScale = Vector3.one
            };
        [SerializeField] private WordNoteMotionSettings defaultExitMotion =
            new WordNoteMotionSettings
            {
                duration = .18f,
                startPosition = Vector2.zero,
                endPosition = Vector2.zero,
                startAlpha = 1f,
                endAlpha = 0f,
                startScale = Vector3.one,
                endScale = new Vector3(.96f, .96f, 1f)
            };
        [SerializeField, Min(0f)] private float defaultOutlineWidth = .15f;
        [SerializeField, Min(0f)] private float defaultOutlineSoftness;

        [Header("同位置堆叠")]
        [SerializeField, Min(0f)] private float stackSpacing = 64f;
        [SerializeField, Min(0f)] private float stackShiftDuration = .18f;
        [SerializeField] private Ease stackShiftEase = Ease.OutQuad;

        [Header("画布")]
        [SerializeField, Min(0)] private int sortingOrder = 1200;
        [SerializeField, Min(1f)] private float canvasReferenceWidth = 1920f;
        [SerializeField, Min(1f)] private float canvasReferenceHeight = 1080f;

        private readonly Dictionary<WordNotePosition, List<WordNoteMessage>>
            positionQueues =
                new Dictionary<WordNotePosition, List<WordNoteMessage>>();
        private readonly List<WordNoteMessage> totalQueue =
            new List<WordNoteMessage>();
        private Canvas canvas;
        private int nextId = 1;

        public static WordNoteUI Instance { get; private set; }
        public IReadOnlyList<WordNoteHandle> TotalQueue =>
            BuildHandleList(totalQueue);
        public int AliveCount => totalQueue.Count;
        public float StackSpacing => stackSpacing;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            canvas = GetComponent<Canvas>();
            EnsureCanvas();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            KillAllTweens();
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                DisappearAll(true);
            }
        }

        public WordNoteHandle Show(string content)
        {
            return Show(content, null);
        }

        public WordNoteHandle Show(
            string content,
            WordNoteStyle style)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            WordNoteStyle resolvedStyle =
                style != null
                    ? style.Clone()
                    : BuildDefaultStyle();
            resolvedStyle.position = ClampPosition(
                resolvedStyle.position);
            var message = new WordNoteMessage
            {
                Id = nextId++,
                Content = content.Trim(),
                Style = resolvedStyle,
                State = WordNoteMessageState.Queued,
                PlayExitAnimation =
                    resolvedStyle.useExitAnimation
            };
            message.Handle = new WordNoteHandle(this, message);
            message.View = CreateView(message);

            // Required order: total queue first, position queue second.
            totalQueue.Add(message);
            GetPositionQueueList(message.Style.position).Add(message);
            RelayoutPosition(message.Style.position, true);
            StartCoroutine(RunMessage(message));
            return message.Handle;
        }

        public WordNoteHandle Show(
            string content,
            WordNotePosition position,
            float duration = -1f,
            float fontSize = -1f,
            TMP_FontAsset font = null,
            WordNoteDisplayMode mode = WordNoteDisplayMode.Timed,
            bool useOutline = false,
            WordNoteColor fontColor = WordNoteColor.Yellow,
            WordNoteColor outlineColor = WordNoteColor.Yellow,
            bool participateInStackLayout = true)
        {
            WordNoteStyle style = BuildDefaultStyle();
            style.position = position;
            style.participateInStackLayout =
                participateInStackLayout;
            style.displayMode = mode;
            style.displayDuration = duration >= 0f
                ? duration
                : style.displayDuration;
            style.fontSize = fontSize > 0f
                ? fontSize
                : style.fontSize;
            style.fontColor = fontColor;
            style.useOutline = useOutline;
            style.outlineColor = outlineColor;
            if (font != null)
            {
                style.font = font;
            }

            return Show(content, style);
        }

        public WordNoteHandle ShowPersistent(string content)
        {
            return Show(
                content,
                defaultPosition,
                -1f,
                -1f,
                null,
                WordNoteDisplayMode.Persistent);
        }

        public WordNoteHandle ShowTimed(
            string content,
            float duration,
            WordNotePosition position = WordNotePosition.Center)
        {
            return Show(
                content,
                position,
                duration,
                -1f,
                null,
                WordNoteDisplayMode.Timed);
        }

        public WordNoteHandle ShowFlash(
            string content,
            WordNotePosition position = WordNotePosition.Center)
        {
            return Show(
                content,
                position,
                -1f,
                -1f,
                null,
                WordNoteDisplayMode.Flash);
        }

        public WordNoteHandle GetById(int id)
        {
            for (int index = 0; index < totalQueue.Count; index++)
            {
                if (totalQueue[index].Id == id)
                {
                    return totalQueue[index].Handle;
                }
            }

            return null;
        }

        public IReadOnlyList<WordNoteHandle> GetPositionQueue(
            WordNotePosition position)
        {
            return BuildHandleList(GetPositionQueueList(position));
        }

        public bool ExitById(
            int id,
            bool playExitAnimation = true)
        {
            WordNoteMessage message = FindMessage(id);
            if (message == null)
            {
                return false;
            }

            RequestExit(message, playExitAnimation);
            return true;
        }

        public bool ExitById(
            WordNoteHandle handle,
            bool playExitAnimation = true)
        {
            return handle != null &&
                   ExitById(handle.Id, playExitAnimation);
        }

        public void ExitAllAt(
            WordNotePosition position,
            bool playExitAnimation = true,
            bool includePersistent = false)
        {
            WordNoteMessage[] snapshot =
                GetPositionQueueList(position).ToArray();
            for (int index = 0; index < snapshot.Length; index++)
            {
                WordNoteMessage message = snapshot[index];
                if (!includePersistent &&
                    message.Style.displayMode ==
                    WordNoteDisplayMode.Persistent)
                {
                    continue;
                }

                RequestExit(message, playExitAnimation);
            }
        }

        public void ExitAll(
            bool playExitAnimation = true,
            bool includePersistent = false)
        {
            for (int index = 0; index < AllPositions.Length; index++)
            {
                ExitAllAt(
                    AllPositions[index],
                    playExitAnimation,
                    includePersistent);
            }
        }

        public void DisappearAllAt(
            WordNotePosition position,
            bool includePersistent = false)
        {
            WordNoteMessage[] snapshot =
                GetPositionQueueList(position).ToArray();
            for (int index = 0; index < snapshot.Length; index++)
            {
                WordNoteMessage message = snapshot[index];
                if (!includePersistent &&
                    message.Style.displayMode ==
                    WordNoteDisplayMode.Persistent)
                {
                    continue;
                }

                RemoveImmediately(message);
            }
        }

        public void DisappearAll(bool includePersistent = false)
        {
            for (int index = 0; index < AllPositions.Length; index++)
            {
                DisappearAllAt(
                    AllPositions[index],
                    includePersistent);
            }
        }

        public string GetAnimationDebugString()
        {
            if (totalQueue.Count == 0)
            {
                return "无消息";
            }

            var builder = new StringBuilder();
            for (int index = 0; index < totalQueue.Count; index++)
            {
                WordNoteMessage message = totalQueue[index];
                if (index > 0)
                {
                    builder.Append(" | ");
                }

                builder.Append('#');
                builder.Append(message.Id);
                builder.Append(' ');
                builder.Append(message.Style.position);
                builder.Append(' ');
                builder.Append(message.State);
                builder.Append(" y=");
                builder.Append(message.StackOffset.y.ToString("0"));
            }

            return builder.ToString();
        }

        private WordNoteStyle BuildDefaultStyle()
        {
            return new WordNoteStyle
            {
                font = defaultFont,
                fontSize = defaultFontSize,
                fontColor = defaultFontColor,
                useOutline = defaultUseOutline,
                outlineColor = defaultOutlineColor,
                position = defaultPosition,
                participateInStackLayout =
                    defaultParticipateInStackLayout,
                displayMode = defaultDisplayMode,
                displayDuration = defaultDisplayDuration,
                useEnterAnimation = defaultUseEnterAnimation,
                enterAnimation = defaultEnterAnimation,
                enterMotion = defaultEnterMotion != null
                    ? defaultEnterMotion.Clone()
                    : new WordNoteMotionSettings(),
                useExitAnimation = defaultUseExitAnimation,
                exitAnimation = defaultExitAnimation,
                exitMotion = defaultExitMotion != null
                    ? defaultExitMotion.Clone()
                    : new WordNoteMotionSettings()
            };
        }

        private WordNoteView CreateView(WordNoteMessage message)
        {
            WordNoteStyle style = message.Style;
            GameObject viewObject = new GameObject(
                $"WordNote_{message.Id}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(CanvasGroup),
                typeof(TextMeshProUGUI));
            viewObject.transform.SetParent(transform, false);
            RectTransform rect =
                viewObject.GetComponent<RectTransform>();
            rect.anchorMin = PositionToAnchor(style.position);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = new Vector2(
                canvasReferenceWidth * .68f,
                canvasReferenceHeight * .2f);
            rect.anchoredPosition = message.StackOffset;
            rect.localScale = Vector3.one;

            TextMeshProUGUI text =
                viewObject.GetComponent<TextMeshProUGUI>();
            text.font = style.font != null
                ? style.font
                : defaultFont != null
                    ? defaultFont
                    : TMP_Settings.defaultFontAsset;
            text.fontSize = style.fontSize;
            text.richText = style.richText;
            text.color = ResolveColor(style.fontColor);
            text.text = message.Content;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.gameObject.SetActive(true);

            var view = new WordNoteView
            {
                Rect = rect,
                Group = viewObject.GetComponent<CanvasGroup>(),
                Text = text
            };
            view.Group.alpha = 0f;
            ApplyOutline(view, style);
            return view;
        }

        private void ApplyOutline(
            WordNoteView view,
            WordNoteStyle style)
        {
            if (view == null || view.Text == null)
            {
                return;
            }

            TMP_FontAsset font = view.Text.font;
            if (font == null || font.material == null)
            {
                return;
            }

            if (view.RuntimeMaterial == null)
            {
                view.RuntimeMaterial = new Material(font.material);
                view.Text.fontMaterial = view.RuntimeMaterial;
            }

            Material material = view.RuntimeMaterial;
            material.SetFloat(
                ShaderUtilities.ID_OutlineWidth,
                style.useOutline ? defaultOutlineWidth : 0f);
            material.SetFloat(
                ShaderUtilities.ID_OutlineSoftness,
                style.useOutline ? defaultOutlineSoftness : 0f);
            material.SetColor(
                ShaderUtilities.ID_OutlineColor,
                ResolveColor(style.outlineColor));
            if (style.useOutline)
            {
                material.EnableKeyword("OUTLINE_ON");
            }
            else
            {
                material.DisableKeyword("OUTLINE_ON");
            }
        }

        private IEnumerator RunMessage(WordNoteMessage message)
        {
            ConfigureView(message.View, message);
            message.View.Rect.anchoredPosition = message.StackOffset;
            message.View.Rect.localScale = Vector3.one;
            message.View.Group.alpha = 1f;
            message.State = WordNoteMessageState.Entering;
            if (message.Style.useEnterAnimation)
            {
                yield return PlayTransition(
                    message,
                    message.Style.enterMotion,
                    message.Style.enterAnimation,
                    true);
            }

            if (!message.ImmediateExitRequested)
            {
                message.State = WordNoteMessageState.Showing;
                if (message.Style.displayMode ==
                    WordNoteDisplayMode.Timed)
                {
                    float remaining = Mathf.Max(
                        0f,
                        message.Style.displayDuration);
                    while (remaining > 0f &&
                           !message.ExitRequested)
                    {
                        remaining -= Time.unscaledDeltaTime;
                        yield return null;
                    }
                }
                else if (message.Style.displayMode ==
                         WordNoteDisplayMode.Flash)
                {
                    message.ExitRequested = true;
                }
                else
                {
                    while (!message.ExitRequested)
                    {
                        yield return null;
                    }
                }
            }

            if (!message.ImmediateExitRequested)
            {
                message.State = WordNoteMessageState.Exiting;
                if (message.PlayExitAnimation)
                {
                    yield return PlayTransition(
                        message,
                        message.Style.exitMotion,
                        message.Style.exitAnimation,
                        false);
                }
            }

            CompleteMessage(message);
        }

        private IEnumerator PlayTransition(
            WordNoteMessage message,
            WordNoteMotionSettings motion,
            WordNoteAnimation animation,
            bool entering)
        {
            WordNoteView view = message.View;
            if (view == null ||
                motion == null ||
                animation == WordNoteAnimation.None)
            {
                yield break;
            }

            ResolveMotion(
                view,
                motion,
                animation,
                entering,
                out Vector2 startPosition,
                out Vector2 endPosition,
                out float startAlpha,
                out float endAlpha,
                out Vector3 startScale,
                out Vector3 endScale);
            float duration = Mathf.Max(0f, motion.duration);
            bool typewriter =
                motion.useTypewriter ||
                animation == WordNoteAnimation.Typewriter;
            if (typewriter)
            {
                duration = Mathf.Max(
                    .01f,
                    (message.Content != null
                        ? message.Content.Length
                        : 0) /
                    Mathf.Max(
                        1f,
                        motion.typewriterCharactersPerSecond));
                ApplyTypewriter(
                    view,
                    message.Content,
                    0f,
                    entering);
            }

            if (duration <= 0f)
            {
                ApplyVisual(
                    view,
                    message.StackOffset + endPosition,
                    endAlpha,
                    endScale);
                yield break;
            }

            message.CurrentTween?.Kill(false);
            message.CurrentTween = DOTween.To(
                    () => 0f,
                    progress =>
                    {
                        float ease = EvaluateEase(
                            progress,
                            motion.easePower,
                            motion.startSpeed,
                            motion.endSpeed);
                        ApplyVisual(
                            view,
                            message.StackOffset +
                            Vector2.Lerp(
                                startPosition,
                                endPosition,
                                ease),
                            Mathf.Lerp(startAlpha, endAlpha, ease),
                            Vector3.Lerp(
                                startScale,
                                endScale,
                                ease));
                        if (typewriter)
                        {
                            ApplyTypewriter(
                                view,
                                message.Content,
                                progress,
                                entering);
                        }
                    },
                    1f,
                    duration)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    message.CurrentTween = null;
                });

            while (message.CurrentTween != null &&
                   message.CurrentTween.IsActive() &&
                   !message.ImmediateExitRequested)
            {
                yield return null;
            }

            if (message.ImmediateExitRequested)
            {
                yield break;
            }

            ApplyVisual(
                view,
                message.StackOffset + endPosition,
                endAlpha,
                endScale);
            if (typewriter)
            {
                ApplyTypewriter(
                    view,
                    message.Content,
                    1f,
                    entering);
            }
        }

        private static void ApplyTypewriter(
            WordNoteView view,
            string fullText,
            float progress,
            bool entering)
        {
            if (view == null || view.Text == null)
            {
                return;
            }

            string text = fullText ?? string.Empty;
            float clamped = Mathf.Clamp01(progress);
            int visibleCount = entering
                ? Mathf.CeilToInt(text.Length * clamped)
                : Mathf.FloorToInt(text.Length * (1f - clamped));
            visibleCount = Mathf.Clamp(
                visibleCount,
                0,
                text.Length);
            view.Text.text = visibleCount <= 0
                ? string.Empty
                : text.Substring(0, visibleCount);
        }

        private static void ResolveMotion(
            WordNoteView view,
            WordNoteMotionSettings motion,
            WordNoteAnimation animation,
            bool entering,
            out Vector2 startPosition,
            out Vector2 endPosition,
            out float startAlpha,
            out float endAlpha,
            out Vector3 startScale,
            out Vector3 endScale)
        {
            if (motion.useCombinedChannels)
            {
                Vector2 displacement = Vector2.zero;
                if (motion.useHorizontalMove)
                {
                    displacement.x =
                        (float)(int)motion.horizontalDirection *
                        Mathf.Max(0f, motion.horizontalDistance);
                }

                if (motion.useVerticalMove)
                {
                    displacement.y =
                        (float)(int)motion.verticalDirection *
                        Mathf.Max(0f, motion.verticalDistance);
                }

                startPosition = entering
                    ? displacement + motion.startPosition
                    : motion.startPosition;
                endPosition = entering
                    ? motion.endPosition
                    : displacement + motion.endPosition;
                startAlpha = motion.useFade
                    ? entering ? 0f : 1f
                    : 1f;
                endAlpha = motion.useFade
                    ? entering ? 1f : 0f
                    : 1f;
                startScale = motion.useScale
                    ? entering
                        ? motion.startScale
                        : motion.endScale
                    : Vector3.one;
                endScale = motion.useScale
                    ? entering
                        ? motion.endScale
                        : motion.startScale
                    : Vector3.one;
                return;
            }

            float width = view.Rect.rect.width > 0f
                ? view.Rect.rect.width
                : 1200f;
            float height = view.Rect.rect.height > 0f
                ? view.Rect.rect.height
                : 220f;
            Vector2 defaultOffset = Vector2.zero;
            if (animation == WordNoteAnimation.MoveLeft ||
                animation == WordNoteAnimation.FadeMoveLeft)
            {
                defaultOffset = new Vector2(-width, 0f);
            }
            else if (animation == WordNoteAnimation.MoveRight ||
                     animation == WordNoteAnimation.FadeMoveRight)
            {
                defaultOffset = new Vector2(width, 0f);
            }
            else if (animation == WordNoteAnimation.MoveUp ||
                     animation == WordNoteAnimation.FadeMoveUp)
            {
                defaultOffset = new Vector2(0f, height);
            }
            else if (animation == WordNoteAnimation.MoveDown ||
                     animation == WordNoteAnimation.FadeMoveDown)
            {
                defaultOffset = new Vector2(0f, -height);
            }

            bool fading =
                animation == WordNoteAnimation.Fade ||
                animation == WordNoteAnimation.FadeMoveLeft ||
                animation == WordNoteAnimation.FadeMoveRight ||
                animation == WordNoteAnimation.FadeMoveUp ||
                animation == WordNoteAnimation.FadeMoveDown ||
                animation == WordNoteAnimation.FadeScale;
            bool scaling =
                animation == WordNoteAnimation.Scale ||
                animation == WordNoteAnimation.FadeScale;

            startPosition = entering
                ? defaultOffset + motion.startPosition
                : motion.startPosition;
            endPosition = entering
                ? motion.endPosition
                : defaultOffset + motion.endPosition;
            startAlpha = fading
                ? entering ? 0f : motion.startAlpha
                : motion.startAlpha;
            endAlpha = fading
                ? entering ? 1f : 0f
                : motion.endAlpha;
            startScale = scaling
                ? entering
                    ? new Vector3(.82f, .82f, 1f)
                    : motion.startScale
                : motion.startScale;
            endScale = scaling
                ? entering
                    ? Vector3.one
                    : new Vector3(.82f, .82f, 1f)
                : motion.endScale;
        }

        private static float EvaluateEase(
            float progress,
            float easePower,
            float startSpeed,
            float endSpeed)
        {
            float t = Mathf.Clamp01(
                Mathf.Pow(
                    Mathf.Clamp01(progress),
                    Mathf.Max(.01f, easePower)));
            float t2 = t * t;
            float t3 = t2 * t;
            float h01 = -2f * t3 + 3f * t2;
            float h10 = t3 - 2f * t2 + t;
            float h11 = t3 - t2;
            return Mathf.Clamp01(
                h01 + h10 * startSpeed + h11 * endSpeed);
        }

        private static void ApplyVisual(
            WordNoteView view,
            Vector2 position,
            float alpha,
            Vector3 scale)
        {
            view.Rect.anchoredPosition = position;
            view.Rect.localScale = scale;
            view.Group.alpha = Mathf.Clamp01(alpha);
        }

        private void ConfigureView(
            WordNoteView view,
            WordNoteMessage message)
        {
            TMP_FontAsset font = message.Style.font != null
                ? message.Style.font
                : defaultFont;
            view.Text.font = font != null
                ? font
                : TMP_Settings.defaultFontAsset;
            view.Text.fontSize = message.Style.fontSize;
            view.Text.richText = message.Style.richText;
            view.Text.color = ResolveColor(message.Style.fontColor);
            view.Text.text = message.Content;
            ApplyOutline(view, message.Style);
        }

        private void RequestExit(
            WordNoteMessage message,
            bool playExitAnimation)
        {
            if (message == null ||
                message.State == WordNoteMessageState.Finished)
            {
                return;
            }

            message.ExitRequested = true;
            message.PlayExitAnimation = playExitAnimation;
            if (!playExitAnimation)
            {
                message.ImmediateExitRequested = true;
                message.CurrentTween?.Kill(false);
                message.CurrentTween = null;
            }
        }

        private void RemoveImmediately(WordNoteMessage message)
        {
            if (message == null ||
                message.State == WordNoteMessageState.Finished)
            {
                return;
            }

            message.ImmediateExitRequested = true;
            message.ExitRequested = true;
            message.CurrentTween?.Kill(false);
            message.CurrentTween = null;
            message.LayoutTween?.Kill(false);
            message.LayoutTween = null;
            CompleteMessage(message);
        }

        private void CompleteMessage(WordNoteMessage message)
        {
            if (message == null ||
                message.State == WordNoteMessageState.Finished)
            {
                return;
            }

            WordNotePosition position = message.Style.position;
            GetPositionQueueList(position).Remove(message);
            totalQueue.Remove(message);
            message.State = WordNoteMessageState.Finished;
            message.CurrentTween?.Kill(false);
            message.CurrentTween = null;
            message.LayoutTween?.Kill(false);
            message.LayoutTween = null;

            if (message.View != null)
            {
                if (message.View.RuntimeMaterial != null)
                {
                    Destroy(message.View.RuntimeMaterial);
                    message.View.RuntimeMaterial = null;
                }

                Destroy(message.View.Rect.gameObject);
                message.View = null;
            }

            RelayoutPosition(position, true);
        }

        private void RelayoutPosition(
            WordNotePosition position,
            bool animated)
        {
            List<WordNoteMessage> queue = GetPositionQueueList(position);
            int stackIndex = 0;
            for (int index = 0; index < queue.Count; index++)
            {
                WordNoteMessage message = queue[index];
                if (message == null ||
                    message.State == WordNoteMessageState.Finished)
                {
                    continue;
                }

                if (!message.Style.participateInStackLayout)
                {
                    message.StackOffset = Vector2.zero;
                    if (message.View != null)
                    {
                        message.LayoutTween?.Kill(false);
                        message.LayoutTween = null;
                        message.View.Rect.anchoredPosition =
                            Vector2.zero;
                    }

                    continue;
                }

                Vector2 target = new Vector2(
                    0f,
                    -stackSpacing * stackIndex);
                message.StackOffset = target;
                if (message.View == null)
                {
                    continue;
                }

                message.LayoutTween?.Kill(false);
                message.LayoutTween = null;
                if (!animated ||
                    message.State != WordNoteMessageState.Showing)
                {
                    message.View.Rect.anchoredPosition = target;
                }
                else
                {
                    message.LayoutTween = message.View.Rect
                        .DOAnchorPos(target, stackShiftDuration)
                        .SetEase(stackShiftEase)
                        .SetUpdate(true)
                        .OnComplete(() =>
                        {
                            message.LayoutTween = null;
                        });
                }

                stackIndex++;
            }
        }

        private WordNoteMessage FindMessage(int id)
        {
            for (int index = 0; index < totalQueue.Count; index++)
            {
                if (totalQueue[index].Id == id)
                {
                    return totalQueue[index];
                }
            }

            return null;
        }

        private List<WordNoteMessage> GetPositionQueueList(
            WordNotePosition position)
        {
            if (!positionQueues.TryGetValue(
                    position,
                    out List<WordNoteMessage> queue))
            {
                queue = new List<WordNoteMessage>();
                positionQueues[position] = queue;
            }

            return queue;
        }

        private static IReadOnlyList<WordNoteHandle> BuildHandleList(
            List<WordNoteMessage> source)
        {
            var result = new List<WordNoteHandle>(
                source != null ? source.Count : 0);
            if (source == null)
            {
                return result;
            }

            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] != null &&
                    source[index].Handle != null)
                {
                    result.Add(source[index].Handle);
                }
            }

            return result;
        }

        private void EnsureCanvas()
        {
            if (canvas == null)
            {
                canvas = GetComponent<Canvas>();
            }

            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = sortingOrder;
            }

            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = gameObject.AddComponent<CanvasScaler>();
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(
                canvasReferenceWidth,
                canvasReferenceHeight);
            scaler.matchWidthOrHeight = .5f;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private static Vector2 PositionToAnchor(
            WordNotePosition position)
        {
            switch (position)
            {
                case WordNotePosition.MiddleLeft:
                    return new Vector2(.12f, .5f);
                case WordNotePosition.MiddleRight:
                    return new Vector2(.88f, .5f);
                case WordNotePosition.Bottom:
                    return new Vector2(.5f, .12f);
                case WordNotePosition.BottomLeft:
                    return new Vector2(.12f, .12f);
                case WordNotePosition.BottomRight:
                    return new Vector2(.88f, .12f);
                case WordNotePosition.Top:
                    return new Vector2(.5f, .88f);
                case WordNotePosition.TopLeft:
                    return new Vector2(.12f, .88f);
                case WordNotePosition.TopRight:
                    return new Vector2(.88f, .88f);
                default:
                    return new Vector2(.5f, .5f);
            }
        }

        private static WordNotePosition ClampPosition(
            WordNotePosition position)
        {
            int value = Mathf.Clamp(
                (int)position,
                0,
                AllPositions.Length - 1);
            return (WordNotePosition)value;
        }

        private static Color ResolveColor(WordNoteColor color)
        {
            switch (color)
            {
                case WordNoteColor.Red:
                    return new Color(1f, 0f, 0f, 1f);
                case WordNoteColor.Orange:
                    return new Color(1f, .5f, 0f, 1f);
                case WordNoteColor.Yellow:
                    return new Color(1f, .92f, 0f, 1f);
                case WordNoteColor.Green:
                    return new Color(0f, 1f, .25f, 1f);
                case WordNoteColor.Cyan:
                    return new Color(0f, 1f, 1f, 1f);
                case WordNoteColor.Blue:
                    return new Color(.15f, .45f, 1f, 1f);
                case WordNoteColor.Purple:
                    return new Color(.65f, .2f, 1f, 1f);
                case WordNoteColor.White:
                    return Color.white;
                case WordNoteColor.Black:
                    return Color.black;
                default:
                    return Color.white;
            }
        }

        private void KillAllTweens()
        {
            for (int index = 0; index < totalQueue.Count; index++)
            {
                WordNoteMessage message = totalQueue[index];
                message.CurrentTween?.Kill(false);
                message.LayoutTween?.Kill(false);
                if (message.View != null &&
                    message.View.RuntimeMaterial != null)
                {
                    Destroy(message.View.RuntimeMaterial);
                }
            }
        }
    }
}
